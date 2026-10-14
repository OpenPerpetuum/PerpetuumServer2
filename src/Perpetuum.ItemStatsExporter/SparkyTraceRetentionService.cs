using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Perpetuum.ItemStatsExporter;

internal sealed class SparkyTraceRetentionService(
    SparkyTraceStore store,
    SparkyProfileManager profiles,
    ILogger<SparkyTraceRetentionService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ApplyRetentionPassAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch
            {
                logger.LogWarning("Sparky trace retention could not complete; the next scheduled pass will retry.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromHours(24), stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
        }
    }

    internal async Task ApplyRetentionPassAsync(CancellationToken cancellationToken)
    {
        foreach (SparkyApiProfile profile in Enum.GetValues<SparkyApiProfile>())
        {
            SparkyTraceRetentionPolicy policy = await profiles
                .GetTraceRetentionPolicyAsync(profile, cancellationToken)
                .ConfigureAwait(false);
            var result = await store.ApplyRetentionAsync(
                TimeSpan.FromDays(policy.ContentRetentionDays),
                TimeSpan.FromDays(policy.MetadataRetentionDays),
                cancellationToken: cancellationToken,
                environment: profile).ConfigureAwait(false);
            if (result.ContentRowsCleared > 0 || result.ReplayContentRowsCleared > 0 ||
                result.EvaluationQuestionsCleared > 0 || result.FeedbackNotesCleared > 0 || result.MetadataRowsDeleted > 0)
                logger.LogInformation("Sparky {Profile} retention cleared {TraceContentRows} trace-content rows, {EvaluationQuestions} reviewed-example questions, {FeedbackNotes} feedback notes, and {ReplayContentRows} replay-content rows; deleted {MetadataRows} metadata rows.",
                    profile, result.ContentRowsCleared, result.EvaluationQuestionsCleared, result.FeedbackNotesCleared,
                    result.ReplayContentRowsCleared, result.MetadataRowsDeleted);
        }
    }
}
