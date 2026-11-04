using KitPlatform.Infrastructure.Learning;
using Xunit;

namespace KitPlatform.Tests;

public sealed class LearningQuizShuffleTests
{
    private static Guid G(int n) => new($"00000000-0000-0000-0000-{n:D12}");

    [Fact]
    public void Same_inputs_give_the_same_permutation()
    {
        var a = LearningQuizShuffle.OptionOrder(G(1), G(2), 3, 4);
        var b = LearningQuizShuffle.OptionOrder(G(1), G(2), 3, 4);

        Assert.Equal(a, b);
        Assert.Equal([0, 1, 2, 3], a.OrderBy(x => x).ToArray());
    }

    [Fact]
    public void Permutation_changes_with_user_question_and_attempt()
    {
        var baseline = Enumerable.Range(1, 50)
            .Select(q => string.Join(",", LearningQuizShuffle.OptionOrder(G(1), G(1000 + q), 1, 4)))
            .ToList();
        var otherUser = Enumerable.Range(1, 50)
            .Select(q => string.Join(",", LearningQuizShuffle.OptionOrder(G(2), G(1000 + q), 1, 4)))
            .ToList();
        var nextAttempt = Enumerable.Range(1, 50)
            .Select(q => string.Join(",", LearningQuizShuffle.OptionOrder(G(1), G(1000 + q), 2, 4)))
            .ToList();

        Assert.NotEqual(baseline, otherUser);
        Assert.NotEqual(baseline, nextAttempt);
        Assert.True(baseline.Distinct().Count() > 5);
    }

    [Fact]
    public void Display_answer_maps_back_to_canonical_index()
    {
        var order = LearningQuizShuffle.OptionOrder(G(7), G(8), 1, 3);
        var displayOfCorrect = Array.IndexOf(order, 1);

        Assert.Equal(1, LearningQuizShuffle.ToCanonical(order, displayOfCorrect));
        Assert.Equal(-1, LearningQuizShuffle.ToCanonical(order, 3));
        Assert.Equal(-1, LearningQuizShuffle.ToCanonical(order, -1));
    }

    /// <summary>
    /// Seed L1: đáp án đúng luôn ở index 1. Chọn cùng một ô cho mọi câu không còn đạt 80%.
    /// </summary>
    [Fact]
    public void Picking_the_same_option_for_every_question_no_longer_passes_reliably()
    {
        const int questions = 6;
        const int options = 3;
        const int canonicalCorrect = 1;
        const int passPct = 80;
        var questionIds = Enumerable.Range(0, questions).Select(i => G(500 + i)).ToArray();

        var passes = 0;
        var trials = 0;
        for (var user = 1; user <= 300; user++)
        {
            for (var display = 0; display < options; display++)
            {
                trials++;
                var correct = questionIds.Count(q =>
                    LearningQuizShuffle.ToCanonical(
                        LearningQuizShuffle.OptionOrder(G(user), q, 1, options), display) == canonicalCorrect);
                if (100 * correct / questions >= passPct)
                    passes++;
            }
        }

        Assert.True(passes * 10 < trials, $"same-option strategy passed {passes}/{trials}");
    }

    [Fact]
    public void Question_order_is_a_permutation_and_varies_by_user()
    {
        var a = LearningQuizShuffle.QuestionOrder(G(1), G(99), 1, 6);
        var differs = Enumerable.Range(2, 20)
            .Any(u => !LearningQuizShuffle.QuestionOrder(G(u), G(99), 1, 6).SequenceEqual(a));

        Assert.Equal(Enumerable.Range(0, 6), a.OrderBy(x => x));
        Assert.True(differs);
    }
}
