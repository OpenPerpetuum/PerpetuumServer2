using Perpetuum.Services.Sparky;
using Xunit;

namespace Perpetuum.Tests.Unit;

public sealed class SparkyLiveKnowledgeRetryPolicyTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(400, false)]
    [InlineData(408, true)]
    [InlineData(429, true)]
    [InlineData(500, true)]
    public void Only_transient_http_failures_are_retried(int statusCode, bool expected)
    {
        Assert.Equal(expected, SparkyRequestCoordinator.ShouldRetryLiveKnowledgePublish(statusCode));
    }
}
