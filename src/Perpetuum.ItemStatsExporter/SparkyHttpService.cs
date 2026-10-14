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
using Perpetuum.Services.Sparky;

namespace Perpetuum.ItemStatsExporter;

internal static class SparkyHttpService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public static async Task<int> RunAsync(
        SparkyKnowledgeBundle bundle,
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
        var interpreter = new SparkyQuestionInterpreter(options);
        var answerRouter = new SparkyAnswerRouter(options);

        app.MapGet("/health", () => Results.Json(new
        {
            status = "ok",
            bundleVersion = bundle.Manifest.BundleVersion[..Math.Min(bundle.Manifest.BundleVersion.Length, 16)]
        }));
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

                if (!inferenceSlots.Wait(0))
                {
                    int busyLanguageId = bundle.Languages.Any(language => language.LanguageId == request.ContextLanguageId)
                        ? request.ContextLanguageId!.Value
                        : bundle.Languages.FirstOrDefault(language => language.LanguageId == 0)?.LanguageId
                          ?? bundle.Languages[0].LanguageId;
                    outcome = "busy";
                    statusCode = StatusCodes.Status200OK;
                    return Results.Json(new SparkyApiResponse(
                        "busy",
                        Answer: SparkyResponseTemplates.Get(busyLanguageId, SparkyResponseTemplateKey.Busy),
                        LanguageId: busyLanguageId), JsonOptions);
                }

                try
                {
                    var interpretation = await interpreter.InterpretAsync(
                        question,
                        bundle.Languages,
                        request.ContextLanguageId,
                        context.RequestAborted).ConfigureAwait(false);
                    var answer = await answerRouter.AnswerAsync(
                        question,
                        interpretation,
                        bundle,
                        request.ContextItem,
                        request.ContextLanguageId,
                        context.RequestAborted).ConfigureAwait(false);
                    logger.LogInformation(
                        "Sparky request {RequestId} interpreted intent={Intent}/{SmallTalkKind}; query={EnglishSearchQuery}; entities={MentionedEntities}; selected source IDs={SelectedSourceIds}",
                        requestId,
                        interpretation.Intent,
                        interpretation.SmallTalkKind,
                        interpretation.EnglishSearchQuery ??
                            (interpretation.MentionedEntities is { Count: > 0 } queryEntities
                                ? string.Join(",", queryEntities)
                                : "(none)"),
                        interpretation.MentionedEntities is { Count: > 0 } mentionedEntities
                            ? string.Join(",", mentionedEntities)
                            : "(none)",
                        answer.SelectedSourceIds is { Count: > 0 } selectedSourceIds
                            ? string.Join(",", selectedSourceIds)
                            : "(none)");

                    outcome = answer.Status;
                    statusCode = StatusCodes.Status200OK;
                    return Results.Json(
                        new SparkyApiResponse(answer.Status, answer.Item, answer.Answer, answer.Candidates, answer.LanguageId),
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
