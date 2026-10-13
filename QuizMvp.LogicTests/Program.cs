using QuizMvp.Services;

var mutationCheck = args.Contains("--mutation-check", StringComparer.OrdinalIgnoreCase);
var cases = new[]
{
    new TestCase("TC-SCORE-01", "FR-08", new[] { 11 }, new[] { 11 }, 10, true, 10, "Класс эквивалентности: точное совпадение"),
    new TestCase("TC-SCORE-02", "FR-08", new[] { 11, 12 }, new[] { 12, 11 }, 15, true, 15, "Перестановка: порядок выбора не влияет"),
    new TestCase("TC-SCORE-03", "FR-08", new[] { 11, 12 }, new[] { 11 }, 15, false, 0, "Класс ошибки: выбран не весь набор"),
    new TestCase("TC-SCORE-04", "FR-08", new[] { 11, 12 }, new[] { 11, 12, 13 }, 15, false, 0, "Граница и комбинация: один лишний вариант"),
    new TestCase("TC-SCORE-05", "FR-08", new[] { 11 }, Array.Empty<int>(), 10, false, 0, "Граница: нет выбранных вариантов"),
    new TestCase("TC-SCORE-06", "FR-08", new[] { 11, 12 }, new[] { 11, 11 }, 15, false, 0, "Некорректный ввод: дублирующийся идентификатор"),
    new TestCase("TC-SCORE-07", "FR-08", Array.Empty<int>(), Array.Empty<int>(), 10, false, 0, "Некорректная конфигурация: нет правильного варианта"),
    new TestCase("TC-SCORE-08", "FR-08", new[] { 11 }, null, 10, false, 0, "Граница контракта: null трактуется как отсутствие выбора"),
    new TestCase("TC-SCORE-09", "FR-08", new[] { 11 }, new[] { 11 }, -1, false, 0, "Граница конфигурации: отрицательные баллы отклоняются", typeof(ArgumentOutOfRangeException))
};

if (mutationCheck)
{
    var killedBy = cases.FirstOrDefault(testCase => !Matches(MutatedScore(testCase.CorrectIds, testCase.SelectedIds, testCase.Points), testCase));
    if (killedBy is null)
    {
        Console.Error.WriteLine("MUTATION SURVIVED: набор не обнаружил дефект.");
        return 1;
    }

    Console.WriteLine($"MUTATION KILLED by {killedBy.Id}: {killedBy.DesignTechnique}.");
    return 0;
}

var failures = 0;
foreach (var testCase in cases)
{
    try
    {
        var actual = AnswerScoringService.Score(testCase.CorrectIds, testCase.SelectedIds, testCase.Points);
        if (testCase.ExpectedException is null && Matches(actual, testCase))
        {
            Console.WriteLine($"PASS {testCase.Id}: {testCase.DesignTechnique}");
            continue;
        }

        failures++;
        Console.Error.WriteLine($"FAIL {testCase.Id}: expected assertion was not met.");
    }
    catch (Exception exception) when (testCase.ExpectedException?.IsInstanceOfType(exception) == true)
    {
        Console.WriteLine($"PASS {testCase.Id}: expected {exception.GetType().Name}.");
    }
    catch (Exception exception)
    {
        failures++;
        Console.Error.WriteLine($"FAIL {testCase.Id}: unexpected {exception.GetType().Name}.");
    }
}

return failures == 0 ? 0 : 1;

static bool Matches(AnswerScoreResult actual, TestCase expected) =>
    actual.IsCorrect == expected.ExpectedIsCorrect && actual.PointsEarned == expected.ExpectedPoints;

static AnswerScoreResult MutatedScore(IEnumerable<int> correctIds, IEnumerable<int>? selectedIds, int points)
{
    // Intentional mutation for PZ-02: an extra selected option is ignored.
    var correct = correctIds.ToArray();
    var selected = (selectedIds ?? Array.Empty<int>()).ToArray();
    var isCorrect = correct.Length > 0 && !correct.Except(selected).Any();
    return new AnswerScoreResult(isCorrect, isCorrect ? points : 0);
}

sealed record TestCase(
    string Id,
    string RequirementId,
    int[] CorrectIds,
    int[]? SelectedIds,
    int Points,
    bool ExpectedIsCorrect,
    int ExpectedPoints,
    string DesignTechnique,
    Type? ExpectedException = null);
