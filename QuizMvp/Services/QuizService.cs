using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using QuizMvp.Data;
using QuizMvp.Models;

namespace QuizMvp.Services;

public sealed class QuizService : IQuizService
{
    private const string RoomCodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private readonly ApplicationDbContext _dbContext;
    private readonly IAnswerScoringService _answerScoringService;

    public QuizService(ApplicationDbContext dbContext, IAnswerScoringService answerScoringService)
    {
        _dbContext = dbContext;
        _answerScoringService = answerScoringService;
    }

    public async Task<int> CreateQuizAsync(
        Quiz quiz,
        int creatorId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(quiz);

        if (string.IsNullOrWhiteSpace(quiz.Title))
            throw new ArgumentException("A quiz title is required.", nameof(quiz));

        var creatorExists = await _dbContext.Users
            .AnyAsync(user => user.Id == creatorId, cancellationToken);

        if (!creatorExists)
            throw new InvalidOperationException("The quiz creator does not exist.");

        quiz.CreatorId = creatorId;
        quiz.CreatedAt = DateTime.UtcNow;
        _dbContext.Quizzes.Add(quiz);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return quiz.Id;
    }

    public async Task<Session?> StartSessionAsync(
        int quizId,
        int creatorId,
        SessionStatus status = SessionStatus.Waiting,
        CancellationToken cancellationToken = default)
    {
        var quizExists = await _dbContext.Quizzes
            .AnyAsync(quiz => quiz.Id == quizId && quiz.CreatorId == creatorId, cancellationToken);

        if (!quizExists)
            return null;

        string joinCode;
        do
        {
            joinCode = CreateRoomCode();
        }
        while (await _dbContext.Sessions.AnyAsync(session => session.JoinCode == joinCode, cancellationToken));

        var session = new Session
        {
            QuizId = quizId,
            JoinCode = joinCode,
            StartedAt = DateTime.UtcNow,
            Status = status
        };

        _dbContext.Sessions.Add(session);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return session;
    }

    public async Task<AnswerScoreResult?> SubmitAnswerAsync(
        int sessionId,
        int questionId,
        int userId,
        IEnumerable<int>? selectedOptionIds,
        CancellationToken cancellationToken = default)
    {
        var session = await _dbContext.Sessions
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == sessionId, cancellationToken);
        if (session is null || session.Status == SessionStatus.Finished)
            return null;

        var question = await _dbContext.Questions
            .Include(item => item.Options)
            .FirstOrDefaultAsync(item => item.Id == questionId && item.QuizId == session.QuizId, cancellationToken);
        if (question is null)
            return null;

        var selectedIds = (selectedOptionIds ?? Array.Empty<int>()).Distinct().ToArray();
        if (selectedIds.Any(id => question.Options.All(option => option.Id != id)))
            return null;

        var correctIds = question.Options
            .Where(option => option.IsCorrect)
            .Select(option => option.Id);
        var score = _answerScoringService.Score(correctIds, selectedIds, question.Points);

        _dbContext.ParticipantAnswers.Add(new ParticipantAnswer
        {
            SessionId = sessionId,
            QuestionId = questionId,
            UserId = userId,
            SelectedOptionIds = System.Text.Json.JsonSerializer.Serialize(selectedIds),
            IsCorrect = score.IsCorrect,
            PointsEarned = score.PointsEarned,
            SubmittedAt = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
        return score;
    }

    private static string CreateRoomCode()
    {
        Span<char> code = stackalloc char[6];
        for (var index = 0; index < code.Length; index++)
            code[index] = RoomCodeAlphabet[RandomNumberGenerator.GetInt32(RoomCodeAlphabet.Length)];

        return new string(code);
    }
}
