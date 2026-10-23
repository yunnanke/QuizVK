using QuizMvp.Models;

namespace QuizMvp.Services;

public interface IQuizService
{
    Task<int> CreateQuizAsync(Quiz quiz, int creatorId, CancellationToken cancellationToken = default);

    Task<Session?> StartSessionAsync(
        int quizId,
        int creatorId,
        SessionStatus status = SessionStatus.Waiting,
        CancellationToken cancellationToken = default);

    Task<AnswerScoreResult?> SubmitAnswerAsync(
        int sessionId,
        int questionId,
        int userId,
        IEnumerable<int>? selectedOptionIds,
        CancellationToken cancellationToken = default);
}
