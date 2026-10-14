using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace Perpetuum.ItemStatsExporter;

internal sealed record SparkyTraceIngestionResult(
    int StatusCode,
    string? Error = null,
    IReadOnlyList<Guid>? AcknowledgedEventIds = null,
    IReadOnlyList<Guid>? ConflictingEventIds = null);

internal static class SparkyTraceIngestion
{
    public static async Task<SparkyTraceIngestionResult> HandleAsync(
        string? authorizationHeader,
        string? contentType,
        long? contentLength,
        Stream requestBody,
        SparkyApiCredentialResolver credentialResolver,
        SparkyTraceStore? store,
        CancellationToken cancellationToken)
    {
        if (!AuthenticationHeaderValue.TryParse(authorizationHeader, out var authorization) ||
            !string.Equals(authorization.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase) ||
            !credentialResolver.TryResolve(authorization.Parameter, out SparkyApiProfile profile))
        {
            return new SparkyTraceIngestionResult(StatusCodes.Status401Unauthorized, "unauthorized");
        }

        if (!MediaTypeHeaderValue.TryParse(contentType, out var mediaType) ||
            !string.Equals(mediaType.MediaType, "application/json", StringComparison.OrdinalIgnoreCase))
        {
            return new SparkyTraceIngestionResult(StatusCodes.Status415UnsupportedMediaType, "unsupported_content_type");
        }

        if (contentLength > SparkyTraceEventValidator.MaxBodyBytes)
        {
            return new SparkyTraceIngestionResult(StatusCodes.Status413PayloadTooLarge, "request_too_large");
        }

        byte[]? body;
        try
        {
            body = await ReadBoundedBodyAsync(
                requestBody,
                SparkyTraceEventValidator.MaxBodyBytes,
                cancellationToken).ConfigureAwait(false);
        }
        catch (BadHttpRequestException exception) when (exception.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            return new SparkyTraceIngestionResult(StatusCodes.Status413PayloadTooLarge, "request_too_large");
        }

        if (body is null)
        {
            return new SparkyTraceIngestionResult(StatusCodes.Status413PayloadTooLarge, "request_too_large");
        }

        if (!SparkyTraceEventValidator.TryDeserializeBatch(body, out var requestEvents) || requestEvents is null)
        {
            return new SparkyTraceIngestionResult(StatusCodes.Status400BadRequest, "invalid_trace_batch");
        }

        if (store is null)
        {
            return new SparkyTraceIngestionResult(StatusCodes.Status503ServiceUnavailable, "trace_storage_unavailable");
        }

        var events = requestEvents.Select(requestEvent => new SparkyTraceEvent(
            requestEvent.EventId,
            requestEvent.RequestId,
            requestEvent.Source,
            requestEvent.Stage,
            requestEvent.Outcome,
            requestEvent.RecordedAtUtc,
            requestEvent.ElapsedMilliseconds,
            requestEvent.DetailCode)).ToArray();
        try
        {
            var append = await store.AppendBatchAsync(
                events,
                profile,
                captureContent: false,
                cancellationToken).ConfigureAwait(false);
            return append.ConflictingEventIds.Count > 0
                ? new SparkyTraceIngestionResult(
                    StatusCodes.Status409Conflict,
                    "event_id_conflict",
                    append.AcknowledgedEventIds,
                    append.ConflictingEventIds)
                : new SparkyTraceIngestionResult(
                    StatusCodes.Status200OK,
                    AcknowledgedEventIds: append.AcknowledgedEventIds);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return new SparkyTraceIngestionResult(StatusCodes.Status503ServiceUnavailable, "trace_storage_unavailable");
        }
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
}
