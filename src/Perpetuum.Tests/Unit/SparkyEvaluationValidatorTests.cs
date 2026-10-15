using Perpetuum.ItemStatsExporter;
using Xunit;

namespace Perpetuum.Tests.Unit;

public sealed class SparkyEvaluationValidatorTests
{
    [Fact]
    public void Accepts_a_bounded_reviewed_follow_up_sequence_with_expectations()
    {
        var request = new SparkyEvaluationExampleRequest(
            Guid.NewGuid(),
            "StructuredItem",
            [],
            [
                new SparkyEvaluationTurn("What are T3 sensor amplifier stats?", "Game", "StructuredItem", "T3 sensor amplifier"),
                new SparkyEvaluationTurn("Da Gama", "Game", "StructuredItem", "Da Gama", RequiredFacts: ["Optimal range"]),
                new SparkyEvaluationTurn("And its full recipe?", "Game", "StructuredItem", "Da Gama", ExpectedSourceIds: ["item-da-gama"], ForbiddenClaims: ["invented material"])
            ],
            "Clarification followed by a precise item answer.");

        Assert.True(SparkyEvaluationValidator.TryValidate(request, out string error), error);
    }

    [Fact]
    public void Rejects_sequences_with_invalid_scope_size_or_expectation_counts()
    {
        SparkyEvaluationExampleRequest wrongScope = Request([new SparkyEvaluationTurn("hi", "RealWorld")]);
        SparkyEvaluationExampleRequest overlong = Request([new SparkyEvaluationTurn(new string('q', 2001))]);
        SparkyEvaluationExampleRequest excessive = Request(Enumerable.Range(0, 13)
            .Select(_ => new SparkyEvaluationTurn("question")).ToArray());
        SparkyEvaluationExampleRequest tooManyExpectations = Request([new SparkyEvaluationTurn(
            "question", "Game", "MechanicsLore", "Theseus",
            ExpectedSourceIds: Enumerable.Range(0, 16).Select(index => $"source-{index}").ToArray(),
            RequiredFacts: Enumerable.Range(0, 16).Select(index => $"fact-{index}").ToArray(),
            ForbiddenClaims: Enumerable.Range(0, 16).Select(index => $"claim-{index}").ToArray(),
            ExpectedProperties: Enumerable.Range(0, 16).Select(index => $"key:{index}").ToArray())]);

        Assert.False(SparkyEvaluationValidator.TryValidate(wrongScope, out _));
        Assert.False(SparkyEvaluationValidator.TryValidate(overlong, out _));
        Assert.False(SparkyEvaluationValidator.TryValidate(excessive, out _));
        Assert.False(SparkyEvaluationValidator.TryValidate(tooManyExpectations, out _));
    }

    private static SparkyEvaluationExampleRequest Request(IReadOnlyList<SparkyEvaluationTurn> turns) =>
        new(Guid.NewGuid(), null, [], turns);
}
