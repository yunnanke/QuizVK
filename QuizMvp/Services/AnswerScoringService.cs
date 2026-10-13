using System;
using System.Collections.Generic;
using System.Linq;

namespace QuizMvp.Services;

public static class AnswerScoringService
{
    public static AnswerScoreResult Score(
        IEnumerable<int> correctOptionIds,
        IEnumerable<int>? selectedOptionIds,
        int questionPoints)
    {
        ArgumentNullException.ThrowIfNull(correctOptionIds);

        if (questionPoints < 0)
            throw new ArgumentOutOfRangeException(nameof(questionPoints));

        var correct = correctOptionIds.ToArray();
        var selected = (selectedOptionIds ?? Array.Empty<int>()).ToArray();

        var isCorrect = correct.Length > 0
            && correct.Length == correct.Distinct().Count()
            && selected.Length == selected.Distinct().Count()
            && correct.Length == selected.Length
            && correct.OrderBy(id => id).SequenceEqual(selected.OrderBy(id => id));

        return new AnswerScoreResult(isCorrect, isCorrect ? questionPoints : 0);
    }
}

public sealed record AnswerScoreResult(bool IsCorrect, int PointsEarned);
