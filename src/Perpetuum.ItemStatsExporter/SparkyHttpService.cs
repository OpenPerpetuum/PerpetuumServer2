using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http.Headers;
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
        SparkyTraceStore? traceStore = null;
        try
        {
            traceStore = new SparkyTraceStore(options.TraceDatabasePath ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "Sparky",
                "traces",
                "sparky-traces.db"));
        }
        catch
        {
            // Tracing is intentionally optional for answering; the listener still starts.
        }

        string commonApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        if (string.IsNullOrWhiteSpace(commonApplicationData))
            commonApplicationData = Path.Combine(Path.GetPathRoot(Environment.CurrentDirectory) ?? string.Empty, "ProgramData");
        var bundleCatalog = new SparkyBundleCatalog(
            Path.Combine(commonApplicationData, "Sparky", "bundles"),
            Program.ReadKnowledgeBundle);
        var profileManager = new SparkyProfileManager(traceStore, bundleCatalog, options, bundle);
        bool profileStorageFallback = false;
        try
        {
            await profileManager.InitializeAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            profileStorageFallback = true;
            profileManager = new SparkyProfileManager(null, bundleCatalog, options, bundle);
            await profileManager.InitializeAsync(cancellationToken).ConfigureAwait(false);
        }

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
        var tunnelStatuses = new SparkyTunnelStatusRegistry();
        tunnelStatuses.Register("game-api", options.Tunnel is not null);
        tunnelStatuses.Register("admin-panel", options.AdminTunnel is not null);
        builder.Services.AddSingleton(tunnelStatuses);
        builder.Services.AddSingleton(profileManager);
        if (options.Tunnel is not null)
        {
            builder.Services.AddSingleton<IHostedService>(provider => new SparkyTunnelSupervisor(
                options.Tunnel,
                provider.GetRequiredService<SparkyTunnelStatusRegistry>(),
                provider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<SparkyTunnelSupervisor>>()));
        }
        if (options.AdminTunnel is not null)
        {
            builder.Services.AddSingleton<IHostedService>(provider => new SparkyTunnelSupervisor(
                options.AdminTunnel,
                provider.GetRequiredService<SparkyTunnelStatusRegistry>(),
                provider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<SparkyTunnelSupervisor>>()));
        }

        if (traceStore is not null)
        {
            builder.Services.AddSingleton(traceStore);
            builder.Services.AddSingleton<IHostedService>(provider => new SparkyTraceRetentionService(
                provider.GetRequiredService<SparkyTraceStore>(),
                provider.GetRequiredService<SparkyProfileManager>(),
                provider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<SparkyTraceRetentionService>>()));
        }

        builder.WebHost.ConfigureKestrel(serverOptions =>
        {
            serverOptions.Limits.MaxRequestBodySize = Math.Max(
                options.MaxRequestBodyBytes,
                SparkyTraceEventValidator.MaxBodyBytes);
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
        if (traceStore is null)
        {
            logger.LogError("Sparky trace storage could not be initialized; answering remains enabled, but trace uploads and request history are unavailable.");
        }
        if (profileStorageFallback)
            logger.LogError("Sparky profile storage could not be initialized; the service is using in-memory legacy Dev settings and profile edits will not persist.");

        var credentialResolver = options.CreateApiCredentialResolver();
        var concurrencyLimiter = new SparkyConcurrencyLimiter();

        app.MapGet("/health", () => Results.Json(new
        {
            status = "ok",
            bundleVersion = profileManager.TryGetSnapshot(SparkyApiProfile.Dev, out var activeDev)
                ? activeDev.BundleVersion[..Math.Min(activeDev.BundleVersion.Length, 16)]
                : bundle.Manifest.BundleVersion[..Math.Min(bundle.Manifest.BundleVersion.Length, 16)],
            profiles = profileManager.GetSummaries()
        }));
        app.MapPost("/v1/answer", async (HttpContext context) =>
        {
            var timer = Stopwatch.StartNew();
            var requestId = Guid.NewGuid();
            var profile = SparkyApiProfile.Dev;
            SparkyProfileSnapshot? activeSnapshot = null;
            SparkyServiceOptions? requestOptions = null;
            var outcome = "invalid_request";
            var statusCode = StatusCodes.Status400BadRequest;

            try
            {
                if (!TryResolveBearerProfile(context, credentialResolver, out profile))
                {
                    outcome = "unauthorized";
                    statusCode = StatusCodes.Status401Unauthorized;
                    return Results.Json(new SparkyApiError("unauthorized"), statusCode: statusCode);
                }

                if (!profileManager.TryGetSnapshot(profile, out var resolvedSnapshot))
                {
                    outcome = "profile_unavailable";
                    statusCode = StatusCodes.Status503ServiceUnavailable;
                    return Results.Json(new SparkyApiError("profile_unavailable"), statusCode: statusCode);
                }
                activeSnapshot = resolvedSnapshot;
                requestOptions = activeSnapshot.ApplyTo(options);
                var requestBundle = activeSnapshot.Bundle;

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

                byte[]? requestBytes;
                try
                {
                    requestBytes = await ReadBoundedBodyAsync(
                        context.Request.Body,
                        options.MaxRequestBodyBytes,
                        context.RequestAborted);
                }
                catch (Microsoft.AspNetCore.Http.BadHttpRequestException exception)
                    when (exception.StatusCode == StatusCodes.Status413PayloadTooLarge)
                {
                    outcome = "request_too_large";
                    statusCode = StatusCodes.Status413PayloadTooLarge;
                    return Results.Json(new SparkyApiError("request_too_large"), statusCode: statusCode);
                }

                if (requestBytes is null)
                {
                    outcome = "request_too_large";
                    statusCode = StatusCodes.Status413PayloadTooLarge;
                    return Results.Json(new SparkyApiError("request_too_large"), statusCode: statusCode);
                }

                SparkyApiRequest? request;
                try
                {
                    request = JsonSerializer.Deserialize<SparkyApiRequest>(requestBytes, JsonOptions);
                }
                catch (JsonException)
                {
                    outcome = "invalid_request";
                    statusCode = StatusCodes.Status400BadRequest;
                    return Results.Json(new SparkyApiError("invalid_request"), statusCode: statusCode);
                }
                if (request is null || !request.TryResolveRequestId(profile, out requestId) ||
                    string.IsNullOrWhiteSpace(request.Question))
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

                if (!concurrencyLimiter.TryAcquire(profile, requestOptions.MaxConcurrentRequests, options.MaxConcurrentRequests))
                {
                    int busyLanguageId = requestBundle.Languages.Any(language => language.LanguageId == request.ContextLanguageId)
                        ? request.ContextLanguageId!.Value
                        : requestBundle.Languages.FirstOrDefault(language => language.LanguageId == 0)?.LanguageId
                          ?? requestBundle.Languages[0].LanguageId;
                    outcome = "busy";
                    statusCode = StatusCodes.Status200OK;
                    await TryAppendTraceAsync(
                        traceStore,
                        CreateAiEvent(requestId, "request", "busy", question: question, bundleVersion: requestBundle.Manifest.BundleVersion, configRevision: activeSnapshot.RevisionId),
                        profile,
                        options,
                        logger,
                        context.RequestAborted,
                        activeSnapshot.Configuration.CaptureContent).ConfigureAwait(false);
                    return Results.Json(new SparkyApiResponse(
                        "busy",
                        Answer: SparkyResponseTemplates.Get(busyLanguageId, SparkyResponseTemplateKey.Busy),
                        LanguageId: busyLanguageId), JsonOptions);
                }

                try
                {
                    await TryAppendTraceAsync(
                        traceStore,
                        CreateAiEvent(requestId, "request", "accepted", question: question, bundleVersion: requestBundle.Manifest.BundleVersion, configRevision: activeSnapshot.RevisionId),
                        profile,
                        options,
                        logger,
                        context.RequestAborted,
                        activeSnapshot.Configuration.CaptureContent).ConfigureAwait(false);

                    var interpreter = new SparkyQuestionInterpreter(requestOptions);
                    var answerRouter = new SparkyAnswerRouter(
                        requestOptions,
                        activeSnapshot.Configuration.Retrieval,
                        activeSnapshot.Configuration.PersonaStyle);
                    var interpretation = await interpreter.InterpretAsync(
                        question,
                        requestBundle.Languages,
                        request.ContextLanguageId,
                        context.RequestAborted,
                        activeSnapshot.Configuration.RouteAliases).ConfigureAwait(false);
                    await TryAppendTraceAsync(
                        traceStore,
                        CreateAiEvent(
                            requestId,
                            "interpretation",
                            "completed",
                            intent: interpretation.Intent.ToString(),
                            focus: interpretation.RobotFocus?.ToString() ?? interpretation.CatalogFocus?.ToString() ?? interpretation.SmallTalkKind?.ToString(),
                            searchQuery: interpretation.EnglishSearchQuery,
                            entityCandidates: interpretation.MentionedEntities,
                            bundleVersion: requestBundle.Manifest.BundleVersion,
                            configRevision: activeSnapshot.RevisionId),
                        profile,
                        options,
                        logger,
                        context.RequestAborted,
                        activeSnapshot.Configuration.CaptureContent).ConfigureAwait(false);

                    var answer = await answerRouter.AnswerAsync(
                        question,
                        interpretation,
                        requestBundle,
                        request.ContextItem,
                        request.ContextLanguageId,
                        context.RequestAborted).ConfigureAwait(false);
                    await TryAppendTraceAsync(
                        traceStore,
                        CreateAiEvent(
                            requestId,
                            "retrieval",
                            "completed",
                            intent: interpretation.Intent.ToString(),
                            focus: interpretation.RobotFocus?.ToString() ?? interpretation.CatalogFocus?.ToString() ?? interpretation.SmallTalkKind?.ToString(),
                            searchQuery: answer.EnglishSearchQuery ?? interpretation.EnglishSearchQuery,
                            entityCandidates: interpretation.MentionedEntities,
                            selectedSourceIds: answer.SelectedSourceIds,
                            bundleVersion: requestBundle.Manifest.BundleVersion,
                            configRevision: activeSnapshot.RevisionId),
                        profile,
                        options,
                        logger,
                        context.RequestAborted,
                        activeSnapshot.Configuration.CaptureContent).ConfigureAwait(false);
                    await TryAppendTraceAsync(
                        traceStore,
                        CreateAiEvent(
                            requestId,
                            "answer",
                            answer.Status,
                            elapsedMilliseconds: timer.ElapsedMilliseconds,
                            intent: interpretation.Intent.ToString(),
                            focus: interpretation.RobotFocus?.ToString() ?? interpretation.CatalogFocus?.ToString() ?? interpretation.SmallTalkKind?.ToString(),
                            searchQuery: answer.EnglishSearchQuery ?? interpretation.EnglishSearchQuery,
                            entityCandidates: interpretation.MentionedEntities,
                            selectedSourceIds: answer.SelectedSourceIds,
                            modelId: requestOptions.ModelId,
                            bundleVersion: requestBundle.Manifest.BundleVersion,
                            configRevision: activeSnapshot.RevisionId,
                            answer: answer.Answer,
                            finishStatus: answer.FinishStatus),
                        profile,
                        options,
                        logger,
                        context.RequestAborted,
                        activeSnapshot.Configuration.CaptureContent).ConfigureAwait(false);
                    if (activeSnapshot.Configuration.CaptureContent)
                    {
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
                    }
                    else
                    {
                        logger.LogInformation(
                            "Sparky request {RequestId} interpreted intent={Intent}/{SmallTalkKind}; content omitted by profile; selected source IDs={SelectedSourceIds}",
                            requestId,
                            interpretation.Intent,
                            interpretation.SmallTalkKind,
                            answer.SelectedSourceIds is { Count: > 0 } selectedSourceIds
                                ? string.Join(",", selectedSourceIds)
                                : "(none)");
                    }

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
                    await TryAppendTraceAsync(
                        traceStore,
                        CreateAiEvent(requestId, "answer", "timeout", elapsedMilliseconds: timer.ElapsedMilliseconds, modelId: requestOptions?.ModelId, bundleVersion: activeSnapshot?.BundleVersion, configRevision: activeSnapshot?.RevisionId),
                        profile,
                        options,
                        logger,
                        CancellationToken.None,
                        activeSnapshot?.Configuration.CaptureContent).ConfigureAwait(false);
                    return Results.Json(new SparkyApiError("upstream_timeout"), statusCode: statusCode);
                }
                catch (Exception exception) when (exception is HttpRequestException or InvalidDataException or JsonException)
                {
                    outcome = "upstream_error";
                    statusCode = StatusCodes.Status502BadGateway;
                    await TryAppendTraceAsync(
                        traceStore,
                        CreateAiEvent(requestId, "answer", "unavailable", elapsedMilliseconds: timer.ElapsedMilliseconds, modelId: requestOptions?.ModelId, bundleVersion: activeSnapshot?.BundleVersion, configRevision: activeSnapshot?.RevisionId),
                        profile,
                        options,
                        logger,
                        CancellationToken.None,
                        activeSnapshot?.Configuration.CaptureContent).ConfigureAwait(false);
                    return Results.Json(new SparkyApiError("upstream_unavailable"), statusCode: statusCode);
                }
                finally
                {
                    concurrencyLimiter.Release(profile);
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

        app.MapPost("/v1/trace-events", async (HttpContext context) =>
        {
            SparkyTraceIngestionResult result;
            try
            {
                result = await SparkyTraceIngestion.HandleAsync(
                    context.Request.Headers.Authorization,
                    context.Request.ContentType,
                    context.Request.ContentLength,
                    context.Request.Body,
                    credentialResolver,
                    traceStore,
                    context.RequestAborted).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
            {
                return Results.Empty;
            }

            if (result.StatusCode == StatusCodes.Status503ServiceUnavailable)
            {
                logger.LogWarning("Sparky trace batch could not be committed; the sender should retry it.");
            }

            if (result.StatusCode == StatusCodes.Status200OK)
            {
                return Results.Json(new { acknowledgedEventIds = result.AcknowledgedEventIds });
            }
            return Results.Json(new
            {
                error = result.Error,
                acknowledgedEventIds = result.AcknowledgedEventIds,
                conflictingEventIds = result.ConflictingEventIds
            }, statusCode: result.StatusCode);
        });

        var adminBuilder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
        {
            Args = [],
            EnvironmentName = Environments.Production
        });
        adminBuilder.Services.ConfigureHttpJsonOptions(jsonOptions =>
            jsonOptions.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        adminBuilder.Logging.ClearProviders();
        adminBuilder.Logging.AddConsole();
        adminBuilder.WebHost.ConfigureKestrel(serverOptions =>
        {
            serverOptions.Limits.MaxRequestBodySize = SparkyBundleCatalog.DefaultMaximumBytes;
            serverOptions.Listen(IPAddress.Loopback, options.AdminServiceUri!.Port, listenOptions =>
            {
                listenOptions.UseHttps(httpsOptions =>
                {
                    httpsOptions.ServerCertificate = options.ServiceCertificate;
                });
            });
        });

        await using var adminApp = adminBuilder.Build();
        using var adminAuthentication = new SparkyAdminAuthentication(
            options.AdminApiToken ?? throw new InvalidDataException("SPARKY_ADMIN_API_TOKEN is required."),
            options.ApiToken,
            options.LiveApiToken);
        SparkyAdminEndpoints.Map(
            adminApp,
            adminAuthentication,
            traceStore,
            options,
            bundle,
            tunnelStatuses,
            profileManager,
            new SparkyReplayService(traceStore, profileManager, concurrencyLimiter, options));

        logger.LogInformation("Sparky local HTTPS service listening on {ServiceUrl}", options.ServiceUri);
        logger.LogInformation("Sparky local HTTPS admin panel listening on {AdminServiceUrl}", options.AdminServiceUri);
        await app.StartAsync(cancellationToken);
        await adminApp.StartAsync(cancellationToken);
        var gameShutdown = app.WaitForShutdownAsync(cancellationToken);
        var adminShutdown = adminApp.WaitForShutdownAsync(cancellationToken);
        await Task.WhenAny(gameShutdown, adminShutdown).ConfigureAwait(false);
        await adminApp.StopAsync(CancellationToken.None).ConfigureAwait(false);
        await app.StopAsync(CancellationToken.None).ConfigureAwait(false);
        await Task.WhenAll(gameShutdown, adminShutdown).ConfigureAwait(false);
        return 0;
    }

    private static bool TryResolveBearerProfile(
        HttpContext context,
        SparkyApiCredentialResolver credentialResolver,
        out SparkyApiProfile profile)
    {
        profile = default;
        if (!AuthenticationHeaderValue.TryParse(context.Request.Headers.Authorization, out var authorization) ||
            !string.Equals(authorization.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(authorization.Parameter))
        {
            return false;
        }

        return credentialResolver.TryResolve(authorization.Parameter, out profile);
    }

    private static bool IsJsonRequest(string? contentType)
    {
        return MediaTypeHeaderValue.TryParse(contentType, out var parsed) &&
               string.Equals(parsed.MediaType, "application/json", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<byte[]?> ReadBoundedBodyAsync(
        Stream body,
        int maximumBytes,
        CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream(Math.Min(maximumBytes, 8192));
        var chunk = new byte[8192];
        while (true)
        {
            int read = await body.ReadAsync(chunk, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return buffer.ToArray();
            }

            if (buffer.Length + read > maximumBytes)
            {
                return null;
            }

            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }
    }

    private static SparkyTraceEvent CreateAiEvent(
        Guid requestId,
        string stage,
        string outcome,
        long? elapsedMilliseconds = null,
        string? intent = null,
        string? focus = null,
        string? searchQuery = null,
        IReadOnlyList<string>? entityCandidates = null,
        IReadOnlyList<string>? selectedSourceIds = null,
        string? modelId = null,
        string? bundleVersion = null,
        string? configRevision = null,
        string? question = null,
        string? answer = null,
        string? finishStatus = null) => new(
            Guid.NewGuid(),
            requestId,
            "AI",
            stage,
            outcome,
            DateTimeOffset.UtcNow,
            elapsedMilliseconds,
            Intent: intent,
            Focus: focus,
            SearchQuery: searchQuery,
            EntityCandidates: entityCandidates,
            SelectedSourceIds: selectedSourceIds,
            ModelId: modelId,
            ConfigRevision: configRevision,
            BundleVersion: bundleVersion,
            Question: question,
            Answer: answer,
            FinishStatus: finishStatus);

    private static async Task TryAppendTraceAsync(
        SparkyTraceStore? store,
        SparkyTraceEvent traceEvent,
        SparkyApiProfile profile,
        SparkyServiceOptions options,
        ILogger logger,
        CancellationToken cancellationToken,
        bool? captureContent = null)
    {
        if (store is null)
        {
            return;
        }

        try
        {
            var boundedEvent = traceEvent with
            {
                Question = traceEvent.Question?.Length <= 2000 ? traceEvent.Question : null,
                Answer = traceEvent.Answer?.Length <= 2000 ? traceEvent.Answer : null
            };
            await store.AppendAsync(
                boundedEvent,
                profile,
                captureContent ?? options.ShouldCaptureTraceContent(profile),
                cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            logger.LogWarning("Sparky trace storage failed for a request; answering continues without that trace stage.");
        }
    }
}
