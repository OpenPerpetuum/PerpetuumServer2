using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Perpetuum.ItemStatsExporter;

internal static class SparkyHttpService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public static async Task<int> RunAsync(
        ExportDocument export,
        SparkyServiceOptions options,
        CancellationToken cancellationToken)
    {
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
        {
            Args = [],
            EnvironmentName = Environments.Production
        });

        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();
        builder.Services.AddWindowsService(serviceOptions =>
        {
            serviceOptions.ServiceName = "Perpetuum Sparky Service";
        });
        if (options.Tunnel is not null)
        {
            builder.Services.AddSingleton(options.Tunnel);
            builder.Services.AddHostedService<SparkyTunnelSupervisor>();
        }

        builder.WebHost.ConfigureKestrel(serverOptions =>
        {
            serverOptions.Limits.MaxRequestBodySize = options.MaxRequestBodyBytes;
            serverOptions.Listen(IPAddress.Loopback, options.ServiceUri.Port, listenOptions =>
            {
                listenOptions.UseHttps(httpsOptions =>
                {
                    httpsOptions.ServerCertificate = options.ServiceCertificate;
                });
            });
        });

        await using var app = builder.Build();
        var logger = app.Logger;
        using var inferenceSlots = new SemaphoreSlim(options.MaxConcurrentRequests);

        app.MapGet("/health", () => Results.Json(new { status = "ok" }));
        app.MapPost("/v1/answer", async (HttpContext context) =>
        {
            var timer = Stopwatch.StartNew();
            var requestId = context.TraceIdentifier;
            var outcome = "invalid_request";
            var statusCode = StatusCodes.Status400BadRequest;

            try
            {
                if (!HasValidBearerToken(context, options.ApiToken))
                {
                    outcome = "unauthorized";
                    statusCode = StatusCodes.Status401Unauthorized;
                    return Results.Json(new SparkyApiError("unauthorized"), statusCode: statusCode);
                }

                if (!IsJsonRequest(context.Request.ContentType))
                {
                    outcome = "unsupported_content_type";
                    statusCode = StatusCodes.Status415UnsupportedMediaType;
                    return Results.Json(new SparkyApiError("unsupported_content_type"), statusCode: statusCode);
                }

                if (context.Request.ContentLength > options.MaxRequestBodyBytes)
                {
                    outcome = "request_too_large";
                    statusCode = StatusCodes.Status413PayloadTooLarge;
                    return Results.Json(new SparkyApiError("request_too_large"), statusCode: statusCode);
                }

                SparkyApiRequest? request;
                try
                {
                    request = await JsonSerializer.DeserializeAsync<SparkyApiRequest>(
                        context.Request.Body,
                        JsonOptions,
                        context.RequestAborted);
                }
                catch (Microsoft.AspNetCore.Http.BadHttpRequestException exception)
                    when (exception.StatusCode == StatusCodes.Status413PayloadTooLarge)
                {
                    outcome = "request_too_large";
                    statusCode = StatusCodes.Status413PayloadTooLarge;
                    return Results.Json(new SparkyApiError("request_too_large"), statusCode: statusCode);
                }
                catch (JsonException)
                {
                    outcome = "invalid_request";
                    statusCode = StatusCodes.Status400BadRequest;
                    return Results.Json(new SparkyApiError("invalid_request"), statusCode: statusCode);
                }

                if (string.IsNullOrWhiteSpace(request?.Question))
                {
                    outcome = "invalid_request";
                    statusCode = StatusCodes.Status400BadRequest;
                    return Results.Json(new SparkyApiError("invalid_request"), statusCode: statusCode);
                }

                var question = request.Question.Trim();
                if (question.Length > options.MaxQuestionCharacters)
                {
                    outcome = "question_too_long";
                    statusCode = StatusCodes.Status413PayloadTooLarge;
                    return Results.Json(new SparkyApiError("question_too_long"), statusCode: statusCode);
                }

                if (request.ContextItem?.Length > 256)
                {
                    outcome = "invalid_request";
                    statusCode = StatusCodes.Status400BadRequest;
                    return Results.Json(new SparkyApiError("invalid_request"), statusCode: statusCode);
                }

                var lookup = ItemStatsLookup.Resolve(question, export.Items, export.ProductionRecipes, request.ContextItem);
                if (lookup.Status == "not_found")
                {
                    outcome = "not_found";
                    statusCode = StatusCodes.Status200OK;
                    return Results.Json(new SparkyApiResponse("not_found"), JsonOptions);
                }

                if (lookup.Status == "ambiguous")
                {
                    outcome = "ambiguous";
                    statusCode = StatusCodes.Status200OK;
                    var candidates = lookup.Candidates
                        .Where(candidate => !string.Equals(
                            candidate.DisplayName,
                            candidate.DefinitionName,
                            StringComparison.OrdinalIgnoreCase))
                        .Select(candidate => candidate.DisplayName)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray();
                    return Results.Json(new SparkyApiResponse("ambiguous", Candidates: candidates), JsonOptions);
                }

                if (lookup.Item is null || !inferenceSlots.Wait(0))
                {
                    outcome = lookup.Item is null ? "lookup_error" : "busy";
                    statusCode = lookup.Item is null
                        ? StatusCodes.Status502BadGateway
                        : StatusCodes.Status429TooManyRequests;
                    if (statusCode == StatusCodes.Status429TooManyRequests)
                    {
                        context.Response.Headers.RetryAfter = "1";
                    }

                    return Results.Json(
                        new SparkyApiError(lookup.Item is null ? "service_error" : "busy"),
                        statusCode: statusCode);
                }

                try
                {
                    var answer = await LocalModelAnswerer.AnswerAsync(
                        options.ModelBaseUrl,
                        options.ModelId,
                        question,
                        lookup.Item,
                        options.MaxAnswerCharacters,
                        options.MaxCompletionTokens,
                        options.InferenceTimeout,
                        apiToken: options.ModelApiToken,
                        cancellationToken: context.RequestAborted);
                    outcome = "answered";
                    statusCode = StatusCodes.Status200OK;
                    return Results.Json(
                        new SparkyApiResponse("answered", lookup.Item.ContextDisplayName, answer),
                        JsonOptions);
                }
                catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
                {
                    outcome = "cancelled";
                    statusCode = 499;
                    return Results.Empty;
                }
                catch (OperationCanceledException)
                {
                    outcome = "upstream_timeout";
                    statusCode = StatusCodes.Status504GatewayTimeout;
                    return Results.Json(new SparkyApiError("upstream_timeout"), statusCode: statusCode);
                }
                catch (Exception exception) when (exception is HttpRequestException or InvalidDataException or JsonException)
                {
                    outcome = "upstream_error";
                    statusCode = StatusCodes.Status502BadGateway;
                    return Results.Json(new SparkyApiError("upstream_unavailable"), statusCode: statusCode);
                }
                finally
                {
                    inferenceSlots.Release();
                }
            }
            finally
            {
                timer.Stop();
                logger.LogInformation(
                    "Sparky request {RequestId} completed with {Outcome} and HTTP {StatusCode} in {ElapsedMilliseconds} ms",
                    requestId,
                    outcome,
                    statusCode,
                    timer.ElapsedMilliseconds);
            }
        });

        logger.LogInformation("Sparky local HTTPS service listening on {ServiceUrl}", options.ServiceUri);
        await app.StartAsync(cancellationToken);
        await app.WaitForShutdownAsync(cancellationToken);
        return 0;
    }

    private static bool HasValidBearerToken(HttpContext context, string expectedToken)
    {
        if (!AuthenticationHeaderValue.TryParse(context.Request.Headers.Authorization, out var authorization) ||
            !string.Equals(authorization.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(authorization.Parameter))
        {
            return false;
        }

        var expectedBytes = Encoding.UTF8.GetBytes(expectedToken);
        var actualBytes = Encoding.UTF8.GetBytes(authorization.Parameter);
        return expectedBytes.Length == actualBytes.Length &&
               CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }

    private static bool IsJsonRequest(string? contentType)
    {
        return MediaTypeHeaderValue.TryParse(contentType, out var parsed) &&
               string.Equals(parsed.MediaType, "application/json", StringComparison.OrdinalIgnoreCase);
    }
}
