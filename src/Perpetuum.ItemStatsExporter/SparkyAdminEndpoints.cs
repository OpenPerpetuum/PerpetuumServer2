using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.FileProviders;

namespace Perpetuum.ItemStatsExporter;

internal static class SparkyAdminEndpoints
{
    private const string AdminApiPath = "/admin/api";

    public static void Map(
        WebApplication app,
        SparkyAdminAuthentication authentication,
        SparkyTraceStore? traceStore,
        SparkyServiceOptions options,
        SparkyKnowledgeBundle bundle,
        SparkyTunnelStatusRegistry tunnelStatuses,
        SparkyProfileManager profileManager,
        SparkyReplayService replayService)
    {
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments(AdminApiPath))
            {
                context.Response.Headers.CacheControl = "no-store";
                long maximumBodyBytes = context.Request.Path.Equals("/admin/api/bundles", StringComparison.OrdinalIgnoreCase)
                    ? SparkyBundleCatalog.DefaultMaximumBytes
                    : 64 * 1024;
                var bodySizeFeature = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
                if (bodySizeFeature is { IsReadOnly: false })
                    bodySizeFeature.MaxRequestBodySize = maximumBodyBytes;
                if (context.Request.ContentLength > maximumBodyBytes)
                {
                    context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
                    await context.Response.WriteAsJsonAsync(new SparkyApiError("request_too_large"), context.RequestAborted)
                        .ConfigureAwait(false);
                    return;
                }
                if (!authentication.IsAuthorized(context.Request.Headers.Authorization))
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    await context.Response.WriteAsJsonAsync(new SparkyApiError("unauthorized"), context.RequestAborted)
                        .ConfigureAwait(false);
                    return;
                }
            }

            await next(context).ConfigureAwait(false);
        });

        app.MapGet("/", () => Results.Redirect("/sparky-admin/"));
        app.MapGet("/admin/api/health", () => Results.Json(new
        {
            status = "ok",
            bundleVersion = profileManager.TryGetSnapshot(SparkyApiProfile.Dev, out var activeDev)
                ? activeDev.BundleVersion[..Math.Min(activeDev.BundleVersion.Length, 16)]
                : bundle.Manifest.BundleVersion[..Math.Min(bundle.Manifest.BundleVersion.Length, 16)],
            modelId = profileManager.TryGetSnapshot(SparkyApiProfile.Dev, out var devModel)
                ? devModel.Configuration.ModelId
                : options.ModelId,
            traceStoreAvailable = traceStore is not null,
            traceContentCapture = new
            {
                dev = profileManager.TryGetSnapshot(SparkyApiProfile.Dev, out var devSnapshot)
                    ? devSnapshot.Configuration.CaptureContent
                    : false,
                live = profileManager.TryGetSnapshot(SparkyApiProfile.Live, out var liveSnapshot)
                    ? liveSnapshot.Configuration.CaptureContent
                    : false
            },
            tunnels = tunnelStatuses.GetSnapshot(),
            profiles = profileManager.GetSummaries()
        }));

        app.MapGet("/admin/api/profiles", () => Results.Json(profileManager.GetSummaries()));

        app.MapGet("/admin/api/profiles/{profile}", async (string profile, HttpContext context) =>
        {
            if (!TryParseProfile(profile, out var selectedProfile))
                return Results.Json(new SparkyApiError("invalid_profile"), statusCode: StatusCodes.Status400BadRequest);
            try
            {
                return Results.Json(await profileManager.GetDetailsAsync(selectedProfile, context.RequestAborted).ConfigureAwait(false));
            }
            catch
            {
                return Results.Json(new SparkyApiError("profile_storage_unavailable"), statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        });

        app.MapPut("/admin/api/profiles/{profile}/draft", async (
            string profile,
            SparkyProfileDraftRequest request,
            HttpContext context) =>
        {
            if (traceStore is null)
                return Results.Json(new SparkyApiError("trace_storage_unavailable"), statusCode: StatusCodes.Status503ServiceUnavailable);
            if (!TryParseProfile(profile, out var selectedProfile) || request.Configuration is null)
                return Results.Json(new SparkyApiError("invalid_profile"), statusCode: StatusCodes.Status400BadRequest);
            var result = await profileManager.SaveDraftAsync(
                selectedProfile,
                request.Configuration,
                request.Reason,
                context.RequestAborted).ConfigureAwait(false);
            return ToOperationResult(result);
        });

        app.MapPost("/admin/api/profiles/{profile}/validate", (
            string profile,
            SparkyProfileDraft configuration) =>
        {
            if (!TryParseProfile(profile, out var selectedProfile))
                return Results.Json(new SparkyApiError("invalid_profile"), statusCode: StatusCodes.Status400BadRequest);
            var errors = profileManager.Validate(selectedProfile, configuration);
            return Results.Json(new { valid = errors.Count == 0, errors });
        });

        app.MapPost("/admin/api/profiles/{profile}/activate", async (
            string profile,
            SparkyProfileActivationRequest request,
            HttpContext context) =>
        {
            if (traceStore is null)
                return Results.Json(new SparkyApiError("trace_storage_unavailable"), statusCode: StatusCodes.Status503ServiceUnavailable);
            if (!TryParseProfile(profile, out var selectedProfile) || string.IsNullOrWhiteSpace(request.RevisionId))
                return Results.Json(new SparkyApiError("invalid_profile"), statusCode: StatusCodes.Status400BadRequest);
            var result = await profileManager.ActivateAsync(
                selectedProfile,
                request.RevisionId,
                request.Reason,
                context.RequestAborted).ConfigureAwait(false);
            return ToOperationResult(result);
        });

        app.MapPost("/admin/api/profiles/live/promote", async (
            SparkyProfilePromotionRequest request,
            HttpContext context) =>
        {
            if (traceStore is null)
                return Results.Json(new SparkyApiError("trace_storage_unavailable"), statusCode: StatusCodes.Status503ServiceUnavailable);
            if (string.IsNullOrWhiteSpace(request.SourceRevisionId))
                return Results.Json(new SparkyApiError("invalid_profile"), statusCode: StatusCodes.Status400BadRequest);
            var result = await profileManager.PromoteDevToLiveAsync(
                request.SourceRevisionId,
                request.Reason,
                context.RequestAborted).ConfigureAwait(false);
            return ToOperationResult(result);
        });

        app.MapPost("/admin/api/profiles/{profile}/rollback", async (
            string profile,
            SparkyProfileRollbackRequest request,
            HttpContext context) =>
        {
            if (traceStore is null)
                return Results.Json(new SparkyApiError("trace_storage_unavailable"), statusCode: StatusCodes.Status503ServiceUnavailable);
            if (!TryParseProfile(profile, out var selectedProfile))
                return Results.Json(new SparkyApiError("invalid_profile"), statusCode: StatusCodes.Status400BadRequest);
            return ToOperationResult(await profileManager.RollbackAsync(
                selectedProfile, request.Reason, context.RequestAborted).ConfigureAwait(false));
        });

        app.MapGet("/admin/api/bundles", () => Results.Json(profileManager.GetBundles()));
        app.MapPost("/admin/api/bundles", async (HttpContext context) =>
        {
            if (!IsJsonRequest(context.Request.ContentType) || context.Request.ContentLength > SparkyBundleCatalog.DefaultMaximumBytes)
                return Results.Json(new SparkyApiError("invalid_bundle_upload"), statusCode: StatusCodes.Status400BadRequest);
            try
            {
                SparkyBundleInfo bundleInfo = await profileManager.StageBundleAsync(context.Request.Body, context.RequestAborted).ConfigureAwait(false);
                return Results.Json(bundleInfo, statusCode: StatusCodes.Status201Created);
            }
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
            {
                return Results.Empty;
            }
            catch (InvalidDataException)
            {
                return Results.Json(new SparkyApiError("invalid_bundle_upload"), statusCode: StatusCodes.Status400BadRequest);
            }
            catch
            {
                return Results.Json(new SparkyApiError("bundle_storage_unavailable"), statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        });

        app.MapGet("/admin/api/traces", async (HttpContext context) =>
        {
            if (traceStore is null)
            {
                return Results.Json(new SparkyApiError("trace_storage_unavailable"), statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            if (!TryReadTraceQuery(context.Request.Query, out var query))
            {
                return Results.Json(new SparkyApiError("invalid_trace_query"), statusCode: StatusCodes.Status400BadRequest);
            }

            try
            {
                var traces = await traceStore.QueryTracesAsync(query, context.RequestAborted).ConfigureAwait(false);
                return Results.Json(traces);
            }
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
            {
                return Results.Empty;
            }
            catch (ArgumentException)
            {
                return Results.Json(new SparkyApiError("invalid_trace_query"), statusCode: StatusCodes.Status400BadRequest);
            }
            catch
            {
                return Results.Json(new SparkyApiError("trace_storage_unavailable"), statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        });

        app.MapGet("/admin/api/traces/{requestId:guid}", async (Guid requestId, HttpContext context) =>
        {
            if (traceStore is null)
            {
                return Results.Json(new SparkyApiError("trace_storage_unavailable"), statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            if (!TryReadEnvironment(context.Request.Query, out var environment))
            {
                return Results.Json(new SparkyApiError("invalid_trace_query"), statusCode: StatusCodes.Status400BadRequest);
            }

            try
            {
                var trace = await traceStore.GetTraceAsync(requestId, environment, context.RequestAborted).ConfigureAwait(false);
                if (trace is null)
                {
                    return Results.NotFound(new SparkyApiError("trace_not_found"));
                }

                var labels = await traceStore.GetFeedbackLabelsAsync(requestId, environment, context.RequestAborted).ConfigureAwait(false);
                return Results.Json(new { trace, labels });
            }
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
            {
                return Results.Empty;
            }
            catch
            {
                return Results.Json(new SparkyApiError("trace_storage_unavailable"), statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        });

        app.MapPost("/admin/api/traces/{requestId:guid}/feedback", async (
            Guid requestId,
            SparkyTraceFeedbackRequest request,
            HttpContext context) =>
        {
            if (traceStore is null)
                return Results.Json(new SparkyApiError("trace_storage_unavailable"), statusCode: StatusCodes.Status503ServiceUnavailable);
            if (requestId == Guid.Empty || !TryReadEnvironment(context.Request.Query, out var environment) ||
                !SparkyTraceEventValidator.IsAllowedFeedbackLabel(request.Label))
                return Results.Json(new SparkyApiError("invalid_feedback"), statusCode: StatusCodes.Status400BadRequest);
            var feedback = new SparkyTraceFeedback(Guid.NewGuid(), requestId, request.Label, request.Note, DateTimeOffset.UtcNow);
            if (!SparkyTraceEventValidator.IsValidFeedback(feedback))
                return Results.Json(new SparkyApiError("invalid_feedback"), statusCode: StatusCodes.Status400BadRequest);
            try
            {
                bool saved = await traceStore.AddFeedbackAsync(feedback, environment, context.RequestAborted).ConfigureAwait(false);
                return saved
                    ? Results.Json(new { saved = true, feedbackId = feedback.FeedbackId })
                    : Results.NotFound(new SparkyApiError("trace_not_found"));
            }
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
            {
                return Results.Empty;
            }
            catch
            {
                return Results.Json(new SparkyApiError("trace_storage_unavailable"), statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        });

        app.MapPost("/admin/api/evaluations", async (SparkyEvaluationExampleRequest request, HttpContext context) =>
        {
            if (traceStore is null)
                return Results.Json(new SparkyApiError("trace_storage_unavailable"), statusCode: StatusCodes.Status503ServiceUnavailable);
            if (!TryReadEnvironment(context.Request.Query, out var environment) || request.RequestId == Guid.Empty ||
                !SparkyTraceEventValidator.IsValidEvaluationExpectation(request.ExpectedIntent, request.ExpectedProperties))
                return Results.Json(new SparkyApiError("invalid_evaluation"), statusCode: StatusCodes.Status400BadRequest);
            try
            {
                var example = await traceStore.CreateEvaluationExampleAsync(
                    request.RequestId, environment, request.ExpectedIntent, request.ExpectedProperties, context.RequestAborted)
                    .ConfigureAwait(false);
                return example is null
                    ? Results.Json(new SparkyApiError("captured_question_required"), statusCode: StatusCodes.Status409Conflict)
                    : Results.Json(example, statusCode: StatusCodes.Status201Created);
            }
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
            {
                return Results.Empty;
            }
            catch (InvalidDataException)
            {
                return Results.Json(new SparkyApiError("invalid_evaluation"), statusCode: StatusCodes.Status400BadRequest);
            }
            catch
            {
                return Results.Json(new SparkyApiError("trace_storage_unavailable"), statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        });

        app.MapGet("/admin/api/evaluations", async (HttpContext context) =>
        {
            if (traceStore is null)
                return Results.Json(new SparkyApiError("trace_storage_unavailable"), statusCode: StatusCodes.Status503ServiceUnavailable);
            if (!TryReadEnvironment(context.Request.Query, out var environment))
                return Results.Json(new SparkyApiError("invalid_trace_query"), statusCode: StatusCodes.Status400BadRequest);
            int limit = 100;
            if (context.Request.Query.TryGetValue("limit", out var limitText) &&
                (!int.TryParse(limitText, NumberStyles.None, CultureInfo.InvariantCulture, out limit) || limit is < 1 or > 100))
                return Results.Json(new SparkyApiError("invalid_trace_query"), statusCode: StatusCodes.Status400BadRequest);
            try
            {
                return Results.Json(await traceStore.GetEvaluationExamplesAsync(environment, limit, context.RequestAborted).ConfigureAwait(false));
            }
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
            {
                return Results.Empty;
            }
            catch
            {
                return Results.Json(new SparkyApiError("trace_storage_unavailable"), statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        });

        app.MapPost("/admin/api/replays", async (SparkyReplayRequest request, HttpContext context) =>
        {
            try
            {
                var replay = await replayService.RunAsync(request, context.RequestAborted).ConfigureAwait(false);
                return Results.Json(replay, statusCode: StatusCodes.Status201Created);
            }
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
            {
                return Results.Empty;
            }
            catch (InvalidDataException exception)
            {
                string code = exception.Message == "draft_not_found" ? "draft_not_found" : "invalid_replay";
                return Results.Json(new SparkyApiError(code), statusCode: code == "draft_not_found" ? StatusCodes.Status404NotFound : StatusCodes.Status400BadRequest);
            }
            catch (InvalidOperationException exception)
            {
                return exception.Message switch
                {
                    "profile_disabled" => Results.Json(new SparkyApiError("profile_disabled"), statusCode: StatusCodes.Status409Conflict),
                    "replay_busy" => Results.Json(new SparkyApiError("replay_busy"), statusCode: StatusCodes.Status429TooManyRequests),
                    "trace_storage_unavailable" => Results.Json(new SparkyApiError("trace_storage_unavailable"), statusCode: StatusCodes.Status503ServiceUnavailable),
                    _ => Results.Json(new SparkyApiError("replay_failed"), statusCode: StatusCodes.Status503ServiceUnavailable)
                };
            }
            catch
            {
                return Results.Json(new SparkyApiError("replay_failed"), statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        });

        app.MapGet("/admin/api/replays/{replayId:guid}", async (Guid replayId, HttpContext context) =>
        {
            if (replayId == Guid.Empty || !TryReadEnvironment(context.Request.Query, out var environment))
                return Results.Json(new SparkyApiError("invalid_replay"), statusCode: StatusCodes.Status400BadRequest);
            var replay = await replayService.GetAsync(replayId, environment, context.RequestAborted).ConfigureAwait(false);
            return replay is null ? Results.NotFound(new SparkyApiError("replay_not_found")) : Results.Json(replay);
        });

        app.MapGet("/admin/api/traces/{requestId:guid}/export", async (Guid requestId, HttpContext context) =>
        {
            if (traceStore is null)
                return Results.Json(new SparkyApiError("trace_storage_unavailable"), statusCode: StatusCodes.Status503ServiceUnavailable);
            if (!TryReadEnvironment(context.Request.Query, out var environment) ||
                !bool.TryParse(context.Request.Query["includeContent"].FirstOrDefault() ?? "false", out bool includeContent))
                return Results.Json(new SparkyApiError("invalid_trace_query"), statusCode: StatusCodes.Status400BadRequest);
            try
            {
                var timeline = await traceStore.GetTraceAsync(requestId, environment, context.RequestAborted).ConfigureAwait(false);
                if (timeline is null)
                    return Results.NotFound(new SparkyApiError("trace_not_found"));
                var labels = await traceStore.GetFeedbackLabelsAsync(requestId, environment, context.RequestAborted).ConfigureAwait(false);
                string json = SparkyDiagnosticExporter.Serialize(timeline, labels, includeContent);
                return Results.Content(json, "application/json; charset=utf-8");
            }
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
            {
                return Results.Empty;
            }
            catch (InvalidDataException)
            {
                return Results.Json(new SparkyApiError("export_too_large"), statusCode: StatusCodes.Status413PayloadTooLarge);
            }
            catch
            {
                return Results.Json(new SparkyApiError("trace_storage_unavailable"), statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        });

        app.MapGet("/admin/api/tunnels", () => Results.Json(tunnelStatuses.GetSnapshot()));

        string webRoot = Path.Combine(AppContext.BaseDirectory, "wwwroot", "sparky-admin");
        if (Directory.Exists(webRoot))
        {
            var provider = new PhysicalFileProvider(webRoot);
            app.Lifetime.ApplicationStopped.Register(provider.Dispose);
            app.UseDefaultFiles(new DefaultFilesOptions
            {
                FileProvider = provider,
                RequestPath = "/sparky-admin"
            });
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = provider,
                RequestPath = "/sparky-admin"
            });
        }
    }

    private static bool TryReadTraceQuery(
        IQueryCollection values,
        out SparkyTraceQuery query)
    {
        query = new SparkyTraceQuery(SparkyApiProfile.Dev);
        if (!TryReadEnvironment(values, out var environment) ||
            !TryReadUtc(values, "fromUtc", out var fromUtc) ||
            !TryReadUtc(values, "toUtc", out var toUtc) ||
            !TryReadOptional(values, "source", source => SparkyTraceEventValidator.IsAllowedSource(source, allowAiSource: true), out var source) ||
            !TryReadOptional(values, "status", SparkyTraceEventValidator.IsAllowedOutcome, out var outcome) ||
            !TryReadOptional(values, "label", SparkyTraceEventValidator.IsAllowedFeedbackLabel, out var label))
        {
            return false;
        }

        if (fromUtc > toUtc)
        {
            return false;
        }

        int limit = 50;
        if (values.TryGetValue("limit", out var limitValue) &&
            (!int.TryParse(limitValue, NumberStyles.None, CultureInfo.InvariantCulture, out limit) || limit is < 1 or > 100))
        {
            return false;
        }

        query = new SparkyTraceQuery(environment, fromUtc, toUtc, source, outcome, limit, label);
        return true;
    }

    private static bool TryParseProfile(string value, out SparkyApiProfile profile) =>
        Enum.TryParse(value, ignoreCase: true, out profile) && Enum.IsDefined(profile);

    private static IResult ToOperationResult(SparkyProfileOperationResult result)
    {
        if (result.Success)
            return Results.Json(new { success = true, revisionId = result.RevisionId });
        if (result.Errors is { Count: > 0 })
            return Results.Json(new { success = false, revisionId = result.RevisionId, errors = result.Errors }, statusCode: StatusCodes.Status400BadRequest);
        return result.ErrorCode switch
        {
            "profile_disabled" or "live_requires_promotion" =>
                Results.Json(new SparkyApiError(result.ErrorCode), statusCode: StatusCodes.Status409Conflict),
            "draft_not_found" or "source_revision_not_found" or "no_rollback_revision" =>
                Results.Json(new SparkyApiError(result.ErrorCode), statusCode: StatusCodes.Status404NotFound),
            _ => Results.Json(new SparkyApiError(result.ErrorCode ?? "profile_operation_failed"), statusCode: StatusCodes.Status503ServiceUnavailable)
        };
    }

    private static bool IsJsonRequest(string? contentType) =>
        Microsoft.Net.Http.Headers.MediaTypeHeaderValue.TryParse(contentType, out var parsed) &&
        string.Equals(parsed.MediaType.Value, "application/json", StringComparison.OrdinalIgnoreCase);

    private static bool TryReadEnvironment(IQueryCollection values, out SparkyApiProfile environment)
    {
        environment = SparkyApiProfile.Dev;
        if (!values.TryGetValue("environment", out var rawValue))
        {
            return true;
        }

        return Enum.TryParse(rawValue.ToString(), ignoreCase: true, out environment) &&
               Enum.IsDefined(environment);
    }

    private static bool TryReadUtc(IQueryCollection values, string key, out DateTimeOffset? result)
    {
        result = null;
        if (!values.TryGetValue(key, out var rawValue))
        {
            return true;
        }

        if (!DateTimeOffset.TryParse(rawValue.ToString(), CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out var parsed) || parsed.Offset != TimeSpan.Zero)
        {
            return false;
        }

        result = parsed;
        return true;
    }

    private static bool TryReadOptional(
        IQueryCollection values,
        string key,
        Func<string, bool> isAllowed,
        out string? value)
    {
        value = null;
        if (!values.TryGetValue(key, out var rawValue))
        {
            return true;
        }

        string candidate = rawValue.ToString();
        if (candidate.Length is < 1 or > 64 || !isAllowed(candidate))
        {
            return false;
        }

        value = candidate;
        return true;
    }
}
