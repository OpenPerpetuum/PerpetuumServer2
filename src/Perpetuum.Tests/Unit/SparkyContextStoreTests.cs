using Perpetuum.Services.Sparky;
using Xunit;

namespace Perpetuum.Tests.Unit;

public sealed class SparkyContextStoreTests
{
    [Fact]
    public async Task Caller_lane_preserves_order_and_keeps_other_callers_concurrent()
    {
        var scheduler = new SparkyCallerLaneScheduler();
        var firstIdentity = new SparkyRequestIdentity(SparkySource.Game, 1);
        var otherIdentity = new SparkyRequestIdentity(SparkySource.Game, 2);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var order = new List<int>();
        var orderLock = new object();

        Task first = scheduler.Schedule(firstIdentity, async () =>
        {
            lock (orderLock) order.Add(1);
            firstStarted.TrySetResult();
            await releaseFirst.Task;
        });
        await firstStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Task second = scheduler.Schedule(firstIdentity, () =>
        {
            lock (orderLock) order.Add(2);
            return Task.CompletedTask;
        });
        Task other = scheduler.Schedule(otherIdentity, () =>
        {
            lock (orderLock) order.Add(3);
            return Task.CompletedTask;
        });

        await other.WaitAsync(TimeSpan.FromSeconds(2));
        lock (orderLock) Assert.Equal([1, 3], order);
        releaseFirst.TrySetResult();
        await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(2));
        lock (orderLock) Assert.Equal([1, 3, 2], order);
    }

    [Fact]
    public void Conversation_context_is_isolated_by_source_and_caller()
    {
        var store = new SparkyContextStore(10, TimeSpan.FromMinutes(15));
        var now = DateTimeOffset.UtcNow;
        var gameIdentity = new SparkyRequestIdentity(SparkySource.Game, 42);
        var discordIdentity = new SparkyRequestIdentity(SparkySource.Discord, 42);

        store.AppendExchange(gameIdentity, Guid.NewGuid(), "What is Nia?", "I need more context.", null, 0, now);

        var game = store.GetAndTouch(gameIdentity, now.AddSeconds(1));
        var discord = store.GetAndTouch(discordIdentity, now.AddSeconds(1));

        Assert.NotNull(game);
        Assert.Collection(game.Turns,
            turn => Assert.Equal(new SparkyConversationTurn("user", "What is Nia?"), turn),
            turn => Assert.Equal(new SparkyConversationTurn("assistant", "I need more context."), turn));
        Assert.Null(discord);
    }

    [Fact]
    public void Append_exchange_is_idempotent_and_preserves_pending_clarification()
    {
        var store = new SparkyContextStore(10, TimeSpan.FromMinutes(15));
        var identity = new SparkyRequestIdentity(SparkySource.Game, 42);
        var requestId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var pending = new SparkyPendingClarification(
            "What are T3 sensor amplifier stats?",
            "Stats",
            ["Da Gama-PLS10", "Desenspure"]);
        var update = new SparkyConversationUpdate("Caller is resolving a T3 sensor amplifier.", null, pending);

        store.AppendExchange(identity, requestId, "T3 sensor amplifier stats", "Which variant do you mean?", update, 0, now);
        store.AppendExchange(identity, requestId, "T3 sensor amplifier stats", "Which variant do you mean?", update, 0, now.AddSeconds(1));

        var context = store.GetAndTouch(identity, now.AddSeconds(2));

        Assert.NotNull(context);
        Assert.Equal(2, context.Turns.Count);
        Assert.Equal("Caller is resolving a T3 sensor amplifier.", context.Summary);
        Assert.Equal(pending.OriginalQuestion, context.PendingClarification?.OriginalQuestion);
        Assert.Equal(pending.CandidateNames, context.PendingClarification?.CandidateNames);
    }

    [Fact]
    public void New_answer_without_a_validated_clarification_clears_old_choices()
    {
        var store = new SparkyContextStore(10, TimeSpan.FromMinutes(15));
        var identity = new SparkyRequestIdentity(SparkySource.Game, 43);
        var now = DateTimeOffset.UtcNow;
        var pending = new SparkyPendingClarification("What are the stats?", "Stats", ["Da Gama", "Desenspure"]);

        store.AppendExchange(identity, Guid.NewGuid(), "T3 sensor amplifier stats", "Which variant: Da Gama or Desenspure?",
            new SparkyConversationUpdate(null, null, pending), 0, now);
        store.AppendExchange(identity, Guid.NewGuid(), "Da Gama", "Its stats are ...",
            null, 0, now.AddSeconds(1));

        Assert.Null(store.GetAndTouch(identity, now.AddSeconds(2))?.PendingClarification);
    }

    [Fact]
    public void Context_pruning_keeps_latest_turns_and_separate_summary_and_pending_choice()
    {
        var store = new SparkyContextStore(10, TimeSpan.FromMinutes(15), maxTurns: 2, maxCharacters: 4096);
        var identity = new SparkyRequestIdentity(SparkySource.Discord, 99);
        var now = DateTimeOffset.UtcNow;
        var pending = new SparkyPendingClarification("Which robot?", "Stats", ["Arbalest", "Weasel"]);

        store.AppendExchange(identity, Guid.NewGuid(), "old question", "old answer", null, 0, now);
        store.AppendExchange(
            identity,
            Guid.NewGuid(),
            "new question",
            "new answer",
            new SparkyConversationUpdate("Earlier topic was about an unrelated item.", "Weasel", pending),
            0,
            now.AddSeconds(1));

        var context = store.GetAndTouch(identity, now.AddSeconds(2));

        Assert.NotNull(context);
        Assert.Collection(context.Turns,
            turn => Assert.Equal(new SparkyConversationTurn("user", "new question"), turn),
            turn => Assert.Equal(new SparkyConversationTurn("assistant", "new answer"), turn));
        Assert.Equal("Earlier topic was about an unrelated item.", context.Summary);
        Assert.Equal("Weasel", context.ItemDisplayName);
        Assert.Equal(pending.OriginalQuestion, context.PendingClarification?.OriginalQuestion);
        Assert.Equal(pending.CandidateNames, context.PendingClarification?.CandidateNames);
    }

    [Fact]
    public void Context_prunes_oldest_complete_exchange_to_character_budget()
    {
        var store = new SparkyContextStore(10, TimeSpan.FromMinutes(15), maxTurns: 8, maxCharacters: 4096);
        var identity = new SparkyRequestIdentity(SparkySource.Game, 88);
        var now = DateTimeOffset.UtcNow;
        string oldQuestion = new('o', 2000);
        string oldAnswer = new('a', 2000);
        string latestQuestion = new('n', 2000);
        string latestAnswer = new('z', 2000);

        store.AppendExchange(identity, Guid.NewGuid(), oldQuestion, oldAnswer, null, 0, now);
        store.AppendExchange(identity, Guid.NewGuid(), latestQuestion, latestAnswer, null, 0, now.AddSeconds(1));

        var context = store.GetAndTouch(identity, now.AddSeconds(2));

        Assert.NotNull(context);
        Assert.Equal(4000, context.Turns.Sum(turn => turn.Content.Length));
        Assert.Collection(context.Turns,
            turn => Assert.Equal(latestQuestion, turn.Content),
            turn => Assert.Equal(latestAnswer, turn.Content));
    }

    [Fact]
    public void Context_capacity_evicts_least_recently_active_caller()
    {
        var store = new SparkyContextStore(1, TimeSpan.FromMinutes(15));
        var first = new SparkyRequestIdentity(SparkySource.Game, 1);
        var second = new SparkyRequestIdentity(SparkySource.Discord, 2);
        var now = DateTimeOffset.UtcNow;
        store.AppendExchange(first, Guid.NewGuid(), "first", "answer", null, 0, now);
        store.AppendExchange(second, Guid.NewGuid(), "second", "answer", null, 0, now.AddSeconds(1));

        Assert.Null(store.GetAndTouch(first, now.AddSeconds(2)));
        Assert.NotNull(store.GetAndTouch(second, now.AddSeconds(2)));
    }

    [Fact]
    public void Context_expires_after_sliding_lifetime()
    {
        var store = new SparkyContextStore(10, TimeSpan.FromMinutes(15));
        var identity = new SparkyRequestIdentity(SparkySource.Game, 7);
        var now = DateTimeOffset.UtcNow;
        store.AppendExchange(identity, Guid.NewGuid(), "question", "answer", null, 0, now);

        Assert.NotNull(store.GetAndTouch(identity, now.AddMinutes(14)));
        Assert.NotNull(store.GetAndTouch(identity, now.AddMinutes(28)));
        Assert.Null(store.GetAndTouch(identity, now.AddMinutes(44)));
    }
}
