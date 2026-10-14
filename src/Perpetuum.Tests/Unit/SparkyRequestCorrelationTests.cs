using System.Text.Json;
using Perpetuum.Services.EventServices.EventMessages;
using Perpetuum.Services.Sparky;
using Xunit;
using AiApiRequest = Perpetuum.ItemStatsExporter.SparkyApiRequest;
using SparkyApiProfile = Perpetuum.ItemStatsExporter.SparkyApiProfile;
using GameApiRequest = Perpetuum.Services.Sparky.SparkyApiRequest;

namespace Perpetuum.Tests.Unit;

public sealed class SparkyRequestCorrelationTests
{
    [Fact]
    public void Game_api_request_serialization_preserves_trace_guid()
    {
        var traceId = Guid.NewGuid();
        var request = new GameApiRequest(traceId, "Who is Weasel?", "Weasel", 0);

        var json = JsonSerializer.Serialize(request, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var roundTrip = JsonSerializer.Deserialize<GameApiRequest>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.NotNull(roundTrip);
        Assert.Equal(traceId, roundTrip.RequestId);
    }

    [Fact]
    public void Concurrent_requests_get_different_trace_guids()
    {
        var traceIds = Enumerable.Range(0, 64)
            .Select(_ => Task.Run(SparkyRequestCoordinator.CreateTraceId))
            .Select(task => task.GetAwaiter().GetResult())
            .ToArray();

        Assert.Equal(traceIds.Length, traceIds.Distinct().Count());
    }

    [Fact]
    public void Response_message_keeps_the_request_trace_guid()
    {
        var traceId = Guid.NewGuid();
        var message = new SparkyResponseMessage(
            7,
            traceId,
            SparkySource.Game,
            null,
            "General chat",
            0,
            0,
            SparkyApiResult.Unavailable);

        Assert.Equal(traceId, message.TraceId);
    }

    [Fact]
    public void AI_generates_missing_trace_id_only_for_legacy_dev_requests()
    {
        var devRequest = new AiApiRequest(null, "question");
        var liveRequest = new AiApiRequest(null, "question");

        Assert.True(devRequest.TryResolveRequestId(SparkyApiProfile.Dev, out var generatedId));
        Assert.NotEqual(Guid.Empty, generatedId);
        Assert.False(liveRequest.TryResolveRequestId(SparkyApiProfile.Live, out var rejectedId));
        Assert.Equal(Guid.Empty, rejectedId);
    }

    [Fact]
    public void AI_rejects_empty_trace_id_and_body_profile_selection()
    {
        var emptyIdRequest = new AiApiRequest(Guid.Empty, "question");
        Assert.False(emptyIdRequest.TryResolveRequestId(SparkyApiProfile.Dev, out _));

        string payload = $$"""{"requestId":"{{Guid.NewGuid()}}","question":"question","profile":"live"}""";
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<AiApiRequest>(payload));
    }

    [Theory]
    [InlineData(1, null, "dev", true)]
    [InlineData(2, "dev", "dev", false)]
    [InlineData(2, "live", "live", false)]
    public void Protected_client_settings_map_to_a_known_profile(
        int version,
        string? configuredProfile,
        string expectedProfile,
        bool expectedLegacy)
    {
        Assert.True(SparkyClientOptions.TryResolveProtectedProfile(
            version,
            configuredProfile,
            out var actualProfile,
            out var isLegacy));

        Assert.Equal(expectedProfile, actualProfile);
        Assert.Equal(expectedLegacy, isLegacy);
    }

    [Theory]
    [InlineData(2, null)]
    [InlineData(2, "production")]
    [InlineData(3, "dev")]
    public void Protected_client_settings_reject_unknown_profiles(
        int version,
        string? configuredProfile)
    {
        Assert.False(SparkyClientOptions.TryResolveProtectedProfile(
            version,
            configuredProfile,
            out _,
            out _));
    }
}
