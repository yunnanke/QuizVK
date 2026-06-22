using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;

namespace QuizMvp.Services;

public sealed class AppStore
{
    private readonly string _dbPath;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public AppStore(IWebHostEnvironment env)
    {
        var dataDir = Path.Combine(env.ContentRootPath, "Data");
        Directory.CreateDirectory(dataDir);
        _dbPath = Path.Combine(dataDir, "quizmvp.db");

        Initialize();
    }

    public async Task<AppUser?> GetUserAsync(string id)
    {
        await using var conn = OpenConnection();
        return LoadUser(conn, id);
    }

    public async Task<IEnumerable<Quiz>> GetQuizzesForAsync(AppUser user)
    {
        await using var conn = OpenConnection();
        var command = conn.CreateCommand();
        command.CommandText = user.Role == UserRole.Organizer
            ? "SELECT * FROM Quizzes WHERE OrganizerId = $organizerId ORDER BY datetime(CreatedAt) DESC;"
            : "SELECT * FROM Quizzes ORDER BY datetime(CreatedAt) DESC;";

        if (user.Role == UserRole.Organizer)
        {
            command.Parameters.AddWithValue("$organizerId", user.Id);
        }

        var quizzes = new List<Quiz>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            quizzes.Add(MapQuiz(reader));
        }

        return quizzes;
    }

    public async Task<Quiz?> GetQuizAsync(string id)
    {
        await using var conn = OpenConnection();
        return LoadQuiz(conn, id);
    }

    public async Task<OperationResult> RegisterAsync(RegisterRequest request)
    {
        await _gate.WaitAsync();
        try
        {
            var email = request.Email.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(request.DisplayName) || string.IsNullOrWhiteSpace(email) || request.Password.Length < 4)
            {
                return new(false, "Заполните имя, почту и пароль минимум из 4 символов.");
            }

            await using var conn = OpenConnection();
            var existing = await GetUserByEmailAsync(conn, email);
            if (existing is not null)
            {
                return new(false, "Пользователь с такой почтой уже существует.");
            }

            var user = new AppUser
            {
                DisplayName = request.DisplayName.Trim(),
                Email = email,
                PasswordHash = Hash(request.Password),
                Role = request.Role
            };

            var command = conn.CreateCommand();
            command.CommandText = """
                INSERT INTO Users (Id, DisplayName, Email, PasswordHash, Role, CreatedAt)
                VALUES ($id, $displayName, $email, $passwordHash, $role, $createdAt);
                """;
            command.Parameters.AddWithValue("$id", user.Id);
            command.Parameters.AddWithValue("$displayName", user.DisplayName);
            command.Parameters.AddWithValue("$email", user.Email);
            command.Parameters.AddWithValue("$passwordHash", user.PasswordHash);
            command.Parameters.AddWithValue("$role", (int)user.Role);
            command.Parameters.AddWithValue("$createdAt", user.CreatedAt.ToString("O"));
            await command.ExecuteNonQueryAsync();

            return new(true, "", user);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<AppUser?> ValidateUserAsync(string email, string password)
    {
        await using var conn = OpenConnection();
        var normalized = email.Trim().ToLowerInvariant();
        var hash = Hash(password);
        var command = conn.CreateCommand();
        command.CommandText = "SELECT * FROM Users WHERE Email = $email AND PasswordHash = $passwordHash LIMIT 1;";
        command.Parameters.AddWithValue("$email", normalized);
        command.Parameters.AddWithValue("$passwordHash", hash);
        await using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? MapUser(reader) : null;
    }

    public async Task<Quiz> SaveQuizAsync(string organizerId, QuizDraft draft)
    {
        await _gate.WaitAsync();
        try
        {
            var quiz = string.IsNullOrWhiteSpace(draft.Id)
                ? new Quiz { OrganizerId = organizerId }
                : await GetQuizOwnedByAsync(organizerId, draft.Id!) ?? new Quiz { OrganizerId = organizerId };

            quiz.Title = string.IsNullOrWhiteSpace(draft.Title) ? "Новый квиз" : draft.Title.Trim();
            quiz.Category = string.IsNullOrWhiteSpace(draft.Category) ? "Общее" : draft.Category.Trim();
            quiz.SecondsPerQuestion = Math.Clamp(draft.SecondsPerQuestion, 10, 180);
            quiz.Rules = draft.Rules?.Trim() ?? "";
            quiz.Questions = NormalizeQuestions(draft.Questions);

            await using var conn = OpenConnection();
            var command = conn.CreateCommand();
            command.CommandText = """
                INSERT INTO Quizzes (Id, OrganizerId, Title, Category, SecondsPerQuestion, Rules, QuestionsJson, CreatedAt)
                VALUES ($id, $organizerId, $title, $category, $seconds, $rules, $questionsJson, $createdAt)
                ON CONFLICT(Id) DO UPDATE SET
                    OrganizerId = excluded.OrganizerId,
                    Title = excluded.Title,
                    Category = excluded.Category,
                    SecondsPerQuestion = excluded.SecondsPerQuestion,
                    Rules = excluded.Rules,
                    QuestionsJson = excluded.QuestionsJson;
                """;
            command.Parameters.AddWithValue("$id", quiz.Id);
            command.Parameters.AddWithValue("$organizerId", organizerId);
            command.Parameters.AddWithValue("$title", quiz.Title);
            command.Parameters.AddWithValue("$category", quiz.Category);
            command.Parameters.AddWithValue("$seconds", quiz.SecondsPerQuestion);
            command.Parameters.AddWithValue("$rules", quiz.Rules);
            command.Parameters.AddWithValue("$questionsJson", JsonSerializer.Serialize(quiz.Questions, _json));
            command.Parameters.AddWithValue("$createdAt", quiz.CreatedAt.ToString("O"));
            await command.ExecuteNonQueryAsync();

            return quiz;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<QuizSession> CreateSessionAsync(string organizerId, string quizId)
    {
        await _gate.WaitAsync();
        try
        {
            var quiz = await GetQuizOwnedByAsync(organizerId, quizId) ?? throw new InvalidOperationException("Quiz not found.");
            var session = new QuizSession
            {
                QuizId = quiz.Id,
                OrganizerId = organizerId,
                Code = GenerateCode()
            };

            await using var conn = OpenConnection();
            var command = conn.CreateCommand();
            command.CommandText = """
                INSERT INTO Sessions (Id, QuizId, OrganizerId, Code, Status, CurrentQuestionIndex, CreatedAt, FinishedAt)
                VALUES ($id, $quizId, $organizerId, $code, $status, $currentQuestionIndex, $createdAt, $finishedAt);
                """;
            command.Parameters.AddWithValue("$id", session.Id);
            command.Parameters.AddWithValue("$quizId", session.QuizId);
            command.Parameters.AddWithValue("$organizerId", organizerId);
            command.Parameters.AddWithValue("$code", session.Code);
            command.Parameters.AddWithValue("$status", (int)session.Status);
            command.Parameters.AddWithValue("$currentQuestionIndex", session.CurrentQuestionIndex);
            command.Parameters.AddWithValue("$createdAt", session.CreatedAt.ToString("O"));
            command.Parameters.AddWithValue("$finishedAt", DBNull.Value);
            await command.ExecuteNonQueryAsync();

            return session;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<OperationResult> JoinSessionAsync(string code, string userId)
    {
        await _gate.WaitAsync();
        try
        {
            await using var conn = OpenConnection();
            var session = await GetSessionByCodeAsync(conn, code.Trim());
            var user = await GetUserAsync(userId);
            if (session is null || user is null || session.Status == SessionStatus.Finished)
            {
                return new(false, "Комната не найдена или уже завершена.");
            }

            var existing = await GetSessionPlayerAsync(conn, session.Id, userId);
            if (existing is null)
            {
                var command = conn.CreateCommand();
                command.CommandText = """
                    INSERT INTO SessionPlayers (SessionId, UserId, DisplayName, Score, JoinedAt)
                    VALUES ($sessionId, $userId, $displayName, 0, $joinedAt);
                    """;
                command.Parameters.AddWithValue("$sessionId", session.Id);
                command.Parameters.AddWithValue("$userId", userId);
                command.Parameters.AddWithValue("$displayName", user.DisplayName);
                command.Parameters.AddWithValue("$joinedAt", DateTime.UtcNow.ToString("O"));
                await command.ExecuteNonQueryAsync();
            }

            session.Players = await LoadPlayersAsync(conn, session.Id);
            session.Answers = await LoadAnswersAsync(conn, session.Id);
            return new(true, "", Session: session);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<QuizSession?> GetSessionByCodeAsync(string code)
    {
        await using var conn = OpenConnection();
        return await GetSessionByCodeAsync(conn, code);
    }

    public async Task<QuizSession?> StartSessionAsync(string code, string organizerId)
    {
        await _gate.WaitAsync();
        try
        {
            await using var conn = OpenConnection();
            var session = await GetSessionByCodeAsync(conn, code);
            if (session is null || session.OrganizerId != organizerId)
            {
                return null;
            }

            session.Status = SessionStatus.Running;
            session.CurrentQuestionIndex = 0;
            await SaveSessionAsync(conn, session);
            return session;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<QuizSession?> MoveNextAsync(string code, string organizerId)
    {
        await _gate.WaitAsync();
        try
        {
            await using var conn = OpenConnection();
            var session = await GetSessionByCodeAsync(conn, code);
            var quiz = session is null ? null : await LoadQuizForSessionAsync(conn, session.QuizId);
            if (session is null || quiz is null || session.OrganizerId != organizerId)
            {
                return null;
            }

            if (session.CurrentQuestionIndex < quiz.Questions.Count - 1)
            {
                session.CurrentQuestionIndex++;
            }
            else
            {
                session.Status = SessionStatus.Finished;
                session.FinishedAt = DateTime.UtcNow;
            }

            await SaveSessionAsync(conn, session);
            return session;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<QuizSession?> EndSessionAsync(string code, string organizerId)
    {
        await _gate.WaitAsync();
        try
        {
            await using var conn = OpenConnection();
            var session = await GetSessionByCodeAsync(conn, code);
            if (session is null || session.OrganizerId != organizerId)
            {
                return null;
            }

            session.Status = SessionStatus.Finished;
            session.FinishedAt = DateTime.UtcNow;
            await SaveSessionAsync(conn, session);
            return session;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<QuizSession?> SubmitAnswerAsync(string code, string userId, string questionId, List<string> optionIds)
    {
        await _gate.WaitAsync();
        try
        {
            await using var conn = OpenConnection();
            var session = await GetSessionByCodeAsync(conn, code);
            var quiz = session is null ? null : await LoadQuizForSessionAsync(conn, session.QuizId);
            var question = quiz is null || session is null || session.CurrentQuestionIndex < 0 || session.CurrentQuestionIndex >= quiz.Questions.Count
                ? null
                : quiz.Questions[session.CurrentQuestionIndex];

            if (session is null || quiz is null || question is null || question.Id != questionId || session.Status != SessionStatus.Running)
            {
                return null;
            }

            var alreadyAnswered = await HasSubmittedAnswerAsync(conn, session.Id, userId, questionId);
            if (alreadyAnswered)
            {
                session.Players = await LoadPlayersAsync(conn, session.Id);
                session.Answers = await LoadAnswersAsync(conn, session.Id);
                return session;
            }

            var correctIds = question.Options.Where(x => x.IsCorrect).Select(x => x.Id).Order().ToArray();
            var submittedIds = optionIds.Distinct().Order().ToArray();
            var correct = correctIds.SequenceEqual(submittedIds);

            var command = conn.CreateCommand();
            command.CommandText = """
                INSERT INTO SubmittedAnswers (SessionId, UserId, QuestionId, OptionIdsJson, Correct, SubmittedAt)
                VALUES ($sessionId, $userId, $questionId, $optionIdsJson, $correct, $submittedAt);
                """;
            command.Parameters.AddWithValue("$sessionId", session.Id);
            command.Parameters.AddWithValue("$userId", userId);
            command.Parameters.AddWithValue("$questionId", questionId);
            command.Parameters.AddWithValue("$optionIdsJson", JsonSerializer.Serialize(submittedIds, _json));
            command.Parameters.AddWithValue("$correct", correct ? 1 : 0);
            command.Parameters.AddWithValue("$submittedAt", DateTime.UtcNow.ToString("O"));
            await command.ExecuteNonQueryAsync();

            if (correct)
            {
                var playerCommand = conn.CreateCommand();
                playerCommand.CommandText = """
                    UPDATE SessionPlayers
                    SET Score = Score + 100
                    WHERE SessionId = $sessionId AND UserId = $userId;
                    """;
                playerCommand.Parameters.AddWithValue("$sessionId", session.Id);
                playerCommand.Parameters.AddWithValue("$userId", userId);
                await playerCommand.ExecuteNonQueryAsync();
            }

            session.Players = await LoadPlayersAsync(conn, session.Id);
            session.Answers = await LoadAnswersAsync(conn, session.Id);
            return session;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<object> GetHistoryAsync(AppUser user)
    {
        await using var conn = OpenConnection();
        if (user.Role == UserRole.Organizer)
        {
            var command = conn.CreateCommand();
            command.CommandText = """
                SELECT s.Code, s.Status, s.CreatedAt, s.FinishedAt, q.Title AS QuizTitle, COUNT(sp.UserId) AS Players
                FROM Sessions s
                INNER JOIN Quizzes q ON q.Id = s.QuizId
                LEFT JOIN SessionPlayers sp ON sp.SessionId = s.Id
                WHERE s.OrganizerId = $userId
                GROUP BY s.Id
                ORDER BY datetime(s.CreatedAt) DESC;
                """;
            command.Parameters.AddWithValue("$userId", user.Id);

            var items = new List<object>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                items.Add(new
                {
                    code = reader.GetString(0),
                    status = ParseSessionStatus(reader.GetInt32(1)).ToString(),
                    createdAt = reader.GetString(2),
                    finishedAt = reader.IsDBNull(3) ? null : reader.GetString(3),
                    quiz = reader.GetString(4),
                    players = reader.GetInt32(5)
                });
            }

            return items;
        }

        var userAnswers = await LoadUserAnswersWithQuizAsync(conn, user.Id);
        return userAnswers;
    }

    public async Task<object> GetDashboardStatsAsync(AppUser user)
    {
        await using var conn = OpenConnection();
        var stats = new
        {
            quizzes = await CountAsync(conn, user.Role == UserRole.Organizer
                ? "SELECT COUNT(*) FROM Quizzes WHERE OrganizerId = $userId;"
                : "SELECT COUNT(*) FROM Quizzes;",
                ("$userId", user.Id)),
            sessions = await CountAsync(conn, user.Role == UserRole.Organizer
                ? "SELECT COUNT(*) FROM Sessions WHERE OrganizerId = $userId;"
                : "SELECT COUNT(*) FROM Sessions INNER JOIN SessionPlayers sp ON sp.SessionId = Sessions.Id WHERE sp.UserId = $userId;",
                ("$userId", user.Id)),
            activeSessions = await CountAsync(conn, "SELECT COUNT(*) FROM Sessions WHERE Status = $status;", ("$status", (int)SessionStatus.Running)),
            finishedSessions = await CountAsync(conn, "SELECT COUNT(*) FROM Sessions WHERE Status = $status;", ("$status", (int)SessionStatus.Finished)),
            questions = await CountAsync(conn, user.Role == UserRole.Organizer
                ? "SELECT COALESCE(SUM(json_array_length(QuestionsJson)), 0) FROM Quizzes WHERE OrganizerId = $userId;"
                : "SELECT COALESCE(SUM(json_array_length(QuestionsJson)), 0) FROM Quizzes;",
                ("$userId", user.Id)),
            players = await CountAsync(conn, "SELECT COUNT(*) FROM SessionPlayers;")
        };

        return stats;
    }

    public async Task<object> BuildRoomStateAsync(string code)
    {
        await using var conn = OpenConnection();
        var session = await GetSessionByCodeAsync(conn, code);
        var quiz = session is null ? null : await LoadQuizForSessionAsync(conn, session.QuizId);
        var question = quiz is null || session is null || session.CurrentQuestionIndex < 0 || session.CurrentQuestionIndex >= quiz.Questions.Count
            ? null
            : quiz.Questions[session.CurrentQuestionIndex];

        if (session is null)
        {
            return new { type = "state", session = (object?)null, quiz = (object?)null, question = (object?)null, leaderboard = Array.Empty<object>() };
        }

        session.Players = await LoadPlayersAsync(conn, session.Id);
        session.Answers = await LoadAnswersAsync(conn, session.Id);

        return new
        {
            type = "state",
            session,
            quiz = quiz is null ? null : new { quiz.Id, quiz.Title, quiz.Category, quiz.SecondsPerQuestion, quiz.Rules, TotalQuestions = quiz.Questions.Count },
            question = question is null ? null : HideCorrectAnswers(question),
            leaderboard = session.Players.OrderByDescending(x => x.Score).ThenBy(x => x.DisplayName).ToList(),
            answeredCount = session.Answers.Count(x => x.QuestionId == question?.Id)
        };
    }

    private void Initialize()
    {
        using var conn = OpenConnection();
        CreateSchema(conn);

        if (HasAnyUsers(conn))
        {
            return;
        }

        if (TryImportLegacySeed(conn))
        {
            return;
        }

        SeedDemoData(conn);
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = _dbPath,
            Cache = SqliteCacheMode.Shared,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString());
        connection.Open();
        using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = ON;";
        pragma.ExecuteNonQuery();
        return connection;
    }

    private static void CreateSchema(SqliteConnection conn)
    {
        var command = conn.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS Users (
                Id TEXT PRIMARY KEY,
                DisplayName TEXT NOT NULL,
                Email TEXT NOT NULL UNIQUE,
                PasswordHash TEXT NOT NULL,
                Role INTEGER NOT NULL,
                CreatedAt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS Quizzes (
                Id TEXT PRIMARY KEY,
                OrganizerId TEXT NOT NULL,
                Title TEXT NOT NULL,
                Category TEXT NOT NULL,
                SecondsPerQuestion INTEGER NOT NULL,
                Rules TEXT NOT NULL,
                QuestionsJson TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                FOREIGN KEY (OrganizerId) REFERENCES Users(Id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS Sessions (
                Id TEXT PRIMARY KEY,
                QuizId TEXT NOT NULL,
                OrganizerId TEXT NOT NULL,
                Code TEXT NOT NULL UNIQUE,
                Status INTEGER NOT NULL,
                CurrentQuestionIndex INTEGER NOT NULL,
                CreatedAt TEXT NOT NULL,
                FinishedAt TEXT NULL,
                FOREIGN KEY (QuizId) REFERENCES Quizzes(Id) ON DELETE CASCADE,
                FOREIGN KEY (OrganizerId) REFERENCES Users(Id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS SessionPlayers (
                SessionId TEXT NOT NULL,
                UserId TEXT NOT NULL,
                DisplayName TEXT NOT NULL,
                Score INTEGER NOT NULL,
                JoinedAt TEXT NOT NULL,
                PRIMARY KEY (SessionId, UserId),
                FOREIGN KEY (SessionId) REFERENCES Sessions(Id) ON DELETE CASCADE,
                FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS SubmittedAnswers (
                SessionId TEXT NOT NULL,
                UserId TEXT NOT NULL,
                QuestionId TEXT NOT NULL,
                OptionIdsJson TEXT NOT NULL,
                Correct INTEGER NOT NULL,
                SubmittedAt TEXT NOT NULL,
                PRIMARY KEY (SessionId, UserId, QuestionId),
                FOREIGN KEY (SessionId) REFERENCES Sessions(Id) ON DELETE CASCADE,
                FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
            );

            CREATE INDEX IF NOT EXISTS IX_Quizzes_OrganizerId ON Quizzes(OrganizerId);
            CREATE INDEX IF NOT EXISTS IX_Sessions_OrganizerId ON Sessions(OrganizerId);
            CREATE INDEX IF NOT EXISTS IX_Sessions_Code ON Sessions(Code);
            CREATE INDEX IF NOT EXISTS IX_SessionPlayers_UserId ON SessionPlayers(UserId);
            CREATE INDEX IF NOT EXISTS IX_SubmittedAnswers_UserId ON SubmittedAnswers(UserId);
            """;
        command.ExecuteNonQuery();
    }

    private bool HasAnyUsers(SqliteConnection conn)
    {
        using var command = conn.CreateCommand();
        command.CommandText = "SELECT EXISTS(SELECT 1 FROM Users LIMIT 1);";
        return Convert.ToInt32(command.ExecuteScalar()) == 1;
    }

    private bool TryImportLegacySeed(SqliteConnection conn)
    {
        var legacyPath = Path.Combine(Path.GetDirectoryName(_dbPath)!, "store.json");
        if (!File.Exists(legacyPath))
        {
            return false;
        }

        try
        {
            var json = File.ReadAllText(legacyPath);
            var legacy = JsonSerializer.Deserialize<StoreData>(json, _json);
            if (legacy is null)
            {
                return false;
            }

            using var transaction = conn.BeginTransaction();
            InsertLegacyData(conn, transaction, legacy);
            transaction.Commit();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void SeedDemoData(SqliteConnection conn)
    {
        var organizer = new AppUser
        {
            DisplayName = "Организатор",
            Email = "organizer@example.com",
            PasswordHash = Hash("1234"),
            Role = UserRole.Organizer
        };
        var participant = new AppUser
        {
            DisplayName = "Участник",
            Email = "participant@example.com",
            PasswordHash = Hash("1234"),
            Role = UserRole.Participant
        };

        using var transaction = conn.BeginTransaction();
        InsertUser(conn, transaction, organizer);
        InsertUser(conn, transaction, participant);
        InsertQuiz(conn, transaction, BuildDemoQuiz(organizer.Id));
        transaction.Commit();
    }

    private void InsertLegacyData(SqliteConnection conn, SqliteTransaction transaction, StoreData legacy)
    {
        foreach (var user in legacy.Users)
        {
            InsertUser(conn, transaction, user);
        }

        foreach (var quiz in legacy.Quizzes)
        {
            InsertQuiz(conn, transaction, quiz);
        }

        foreach (var session in legacy.Sessions)
        {
            InsertSession(conn, transaction, session);
            foreach (var player in session.Players)
            {
                InsertPlayer(conn, transaction, session.Id, player);
            }

            foreach (var answer in session.Answers)
            {
                InsertAnswer(conn, transaction, session.Id, answer);
            }
        }
    }

    private void InsertUser(SqliteConnection conn, SqliteTransaction transaction, AppUser user)
    {
        using var command = conn.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT OR REPLACE INTO Users (Id, DisplayName, Email, PasswordHash, Role, CreatedAt)
            VALUES ($id, $displayName, $email, $passwordHash, $role, $createdAt);
            """;
        command.Parameters.AddWithValue("$id", user.Id);
        command.Parameters.AddWithValue("$displayName", user.DisplayName);
        command.Parameters.AddWithValue("$email", user.Email);
        command.Parameters.AddWithValue("$passwordHash", user.PasswordHash);
        command.Parameters.AddWithValue("$role", (int)user.Role);
        command.Parameters.AddWithValue("$createdAt", user.CreatedAt.ToString("O"));
        command.ExecuteNonQuery();
    }

    private void InsertQuiz(SqliteConnection conn, SqliteTransaction transaction, Quiz quiz)
    {
        using var command = conn.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT OR REPLACE INTO Quizzes (Id, OrganizerId, Title, Category, SecondsPerQuestion, Rules, QuestionsJson, CreatedAt)
            VALUES ($id, $organizerId, $title, $category, $seconds, $rules, $questionsJson, $createdAt);
            """;
        command.Parameters.AddWithValue("$id", quiz.Id);
        command.Parameters.AddWithValue("$organizerId", quiz.OrganizerId);
        command.Parameters.AddWithValue("$title", quiz.Title);
        command.Parameters.AddWithValue("$category", quiz.Category);
        command.Parameters.AddWithValue("$seconds", quiz.SecondsPerQuestion);
        command.Parameters.AddWithValue("$rules", quiz.Rules);
        command.Parameters.AddWithValue("$questionsJson", JsonSerializer.Serialize(quiz.Questions, _json));
        command.Parameters.AddWithValue("$createdAt", quiz.CreatedAt.ToString("O"));
        command.ExecuteNonQuery();
    }

    private void InsertSession(SqliteConnection conn, SqliteTransaction transaction, QuizSession session)
    {
        using var command = conn.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT OR REPLACE INTO Sessions (Id, QuizId, OrganizerId, Code, Status, CurrentQuestionIndex, CreatedAt, FinishedAt)
            VALUES ($id, $quizId, $organizerId, $code, $status, $currentQuestionIndex, $createdAt, $finishedAt);
            """;
        command.Parameters.AddWithValue("$id", session.Id);
        command.Parameters.AddWithValue("$quizId", session.QuizId);
        command.Parameters.AddWithValue("$organizerId", session.OrganizerId);
        command.Parameters.AddWithValue("$code", session.Code);
        command.Parameters.AddWithValue("$status", (int)session.Status);
        command.Parameters.AddWithValue("$currentQuestionIndex", session.CurrentQuestionIndex);
        command.Parameters.AddWithValue("$createdAt", session.CreatedAt.ToString("O"));
        command.Parameters.AddWithValue("$finishedAt", session.FinishedAt is null ? DBNull.Value : session.FinishedAt.Value.ToString("O"));
        command.ExecuteNonQuery();
    }

    private void InsertPlayer(SqliteConnection conn, SqliteTransaction transaction, string sessionId, SessionPlayer player)
    {
        using var command = conn.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT OR REPLACE INTO SessionPlayers (SessionId, UserId, DisplayName, Score, JoinedAt)
            VALUES ($sessionId, $userId, $displayName, $score, $joinedAt);
            """;
        command.Parameters.AddWithValue("$sessionId", sessionId);
        command.Parameters.AddWithValue("$userId", player.UserId);
        command.Parameters.AddWithValue("$displayName", player.DisplayName);
        command.Parameters.AddWithValue("$score", player.Score);
        command.Parameters.AddWithValue("$joinedAt", player.JoinedAt.ToString("O"));
        command.ExecuteNonQuery();
    }

    private void InsertAnswer(SqliteConnection conn, SqliteTransaction transaction, string sessionId, SubmittedAnswer answer)
    {
        using var command = conn.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT OR REPLACE INTO SubmittedAnswers (SessionId, UserId, QuestionId, OptionIdsJson, Correct, SubmittedAt)
            VALUES ($sessionId, $userId, $questionId, $optionIdsJson, $correct, $submittedAt);
            """;
        command.Parameters.AddWithValue("$sessionId", sessionId);
        command.Parameters.AddWithValue("$userId", answer.UserId);
        command.Parameters.AddWithValue("$questionId", answer.QuestionId);
        command.Parameters.AddWithValue("$optionIdsJson", JsonSerializer.Serialize(answer.OptionIds, _json));
        command.Parameters.AddWithValue("$correct", answer.Correct ? 1 : 0);
        command.Parameters.AddWithValue("$submittedAt", answer.SubmittedAt.ToString("O"));
        command.ExecuteNonQuery();
    }

    private async Task<AppUser?> GetUserByEmailAsync(SqliteConnection conn, string email)
    {
        var command = conn.CreateCommand();
        command.CommandText = "SELECT * FROM Users WHERE Email = $email LIMIT 1;";
        command.Parameters.AddWithValue("$email", email);
        await using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? MapUser(reader) : null;
    }

    private AppUser? LoadUser(SqliteConnection conn, string id)
    {
        using var command = conn.CreateCommand();
        command.CommandText = "SELECT * FROM Users WHERE Id = $id LIMIT 1;";
        command.Parameters.AddWithValue("$id", id);
        using var reader = command.ExecuteReader();
        return reader.Read() ? MapUser(reader) : null;
    }

    private async Task<Quiz?> GetQuizOwnedByAsync(string organizerId, string quizId)
    {
        await using var conn = OpenConnection();
        var quiz = LoadQuiz(conn, quizId);
        return quiz is not null && quiz.OrganizerId == organizerId ? quiz : null;
    }

    private Quiz? LoadQuiz(SqliteConnection conn, string id)
    {
        using var command = conn.CreateCommand();
        command.CommandText = "SELECT * FROM Quizzes WHERE Id = $id LIMIT 1;";
        command.Parameters.AddWithValue("$id", id);
        using var reader = command.ExecuteReader();
        return reader.Read() ? MapQuiz(reader) : null;
    }

    private async Task<Quiz?> LoadQuizForSessionAsync(SqliteConnection conn, string quizId)
    {
        var quiz = LoadQuiz(conn, quizId);
        await Task.CompletedTask;
        return quiz;
    }

    private async Task<QuizSession?> GetSessionByCodeAsync(SqliteConnection conn, string code)
    {
        var command = conn.CreateCommand();
        command.CommandText = "SELECT * FROM Sessions WHERE Code = $code LIMIT 1;";
        command.Parameters.AddWithValue("$code", code.Trim().ToUpperInvariant());
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        var session = MapSession(reader);
        session.Players = await LoadPlayersAsync(conn, session.Id);
        session.Answers = await LoadAnswersAsync(conn, session.Id);
        return session;
    }

    private async Task<SessionPlayer?> GetSessionPlayerAsync(SqliteConnection conn, string sessionId, string userId)
    {
        var command = conn.CreateCommand();
        command.CommandText = """
            SELECT * FROM SessionPlayers
            WHERE SessionId = $sessionId AND UserId = $userId
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$sessionId", sessionId);
        command.Parameters.AddWithValue("$userId", userId);
        await using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? MapPlayer(reader) : null;
    }

    private async Task<List<SessionPlayer>> LoadPlayersAsync(SqliteConnection conn, string sessionId)
    {
        var players = new List<SessionPlayer>();
        var command = conn.CreateCommand();
        command.CommandText = """
            SELECT * FROM SessionPlayers
            WHERE SessionId = $sessionId
            ORDER BY Score DESC, DisplayName ASC;
            """;
        command.Parameters.AddWithValue("$sessionId", sessionId);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            players.Add(MapPlayer(reader));
        }

        return players;
    }

    private async Task<List<SubmittedAnswer>> LoadAnswersAsync(SqliteConnection conn, string sessionId)
    {
        var answers = new List<SubmittedAnswer>();
        var command = conn.CreateCommand();
        command.CommandText = """
            SELECT * FROM SubmittedAnswers
            WHERE SessionId = $sessionId
            ORDER BY datetime(SubmittedAt) ASC;
            """;
        command.Parameters.AddWithValue("$sessionId", sessionId);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            answers.Add(MapAnswer(reader));
        }

        return answers;
    }

    private async Task<bool> HasSubmittedAnswerAsync(SqliteConnection conn, string sessionId, string userId, string questionId)
    {
        var command = conn.CreateCommand();
        command.CommandText = """
            SELECT EXISTS(
                SELECT 1 FROM SubmittedAnswers
                WHERE SessionId = $sessionId AND UserId = $userId AND QuestionId = $questionId
            );
            """;
        command.Parameters.AddWithValue("$sessionId", sessionId);
        command.Parameters.AddWithValue("$userId", userId);
        command.Parameters.AddWithValue("$questionId", questionId);
        var value = await command.ExecuteScalarAsync();
        return Convert.ToInt32(value) == 1;
    }

    private async Task SaveSessionAsync(SqliteConnection conn, QuizSession session)
    {
        var command = conn.CreateCommand();
        command.CommandText = """
            UPDATE Sessions
            SET Status = $status,
                CurrentQuestionIndex = $currentQuestionIndex,
                FinishedAt = $finishedAt
            WHERE Id = $id;
            """;
        command.Parameters.AddWithValue("$status", (int)session.Status);
        command.Parameters.AddWithValue("$currentQuestionIndex", session.CurrentQuestionIndex);
        command.Parameters.AddWithValue("$finishedAt", session.FinishedAt is null ? DBNull.Value : session.FinishedAt.Value.ToString("O"));
        command.Parameters.AddWithValue("$id", session.Id);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<object> LoadUserAnswersWithQuizAsync(SqliteConnection conn, string userId)
    {
        var command = conn.CreateCommand();
        command.CommandText = """
            SELECT s.Code, s.Status, s.CreatedAt, s.FinishedAt, q.Title AS QuizTitle,
                   COALESCE(sp.Score, 0) AS Score
            FROM SubmittedAnswers sa
            INNER JOIN Sessions s ON s.Id = sa.SessionId
            INNER JOIN Quizzes q ON q.Id = s.QuizId
            LEFT JOIN SessionPlayers sp ON sp.SessionId = s.Id AND sp.UserId = sa.UserId
            WHERE sa.UserId = $userId
            GROUP BY s.Id
            ORDER BY datetime(s.CreatedAt) DESC;
            """;
        command.Parameters.AddWithValue("$userId", userId);

        var items = new List<object>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new
            {
                code = reader.GetString(0),
                status = ParseSessionStatus(reader.GetInt32(1)).ToString(),
                createdAt = reader.GetString(2),
                finishedAt = reader.IsDBNull(3) ? null : reader.GetString(3),
                quiz = reader.GetString(4),
                score = reader.GetInt32(5)
            });
        }

        return items;
    }

    private async Task<int> CountAsync(SqliteConnection conn, string sql, params (string name, object value)[] parameters)
    {
        var command = conn.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt32(result ?? 0);
    }

    private static Quiz MapQuiz(SqliteDataReader reader) => new()
    {
        Id = reader.GetString(reader.GetOrdinal("Id")),
        OrganizerId = reader.GetString(reader.GetOrdinal("OrganizerId")),
        Title = reader.GetString(reader.GetOrdinal("Title")),
        Category = reader.GetString(reader.GetOrdinal("Category")),
        SecondsPerQuestion = reader.GetInt32(reader.GetOrdinal("SecondsPerQuestion")),
        Rules = reader.GetString(reader.GetOrdinal("Rules")),
        Questions = JsonSerializer.Deserialize<List<Question>>(reader.GetString(reader.GetOrdinal("QuestionsJson"))) ?? [],
        CreatedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("CreatedAt")))
    };

    private static AppUser MapUser(SqliteDataReader reader) => new()
    {
        Id = reader.GetString(reader.GetOrdinal("Id")),
        DisplayName = reader.GetString(reader.GetOrdinal("DisplayName")),
        Email = reader.GetString(reader.GetOrdinal("Email")),
        PasswordHash = reader.GetString(reader.GetOrdinal("PasswordHash")),
        Role = (UserRole)reader.GetInt32(reader.GetOrdinal("Role")),
        CreatedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("CreatedAt")))
    };

    private static QuizSession MapSession(SqliteDataReader reader) => new()
    {
        Id = reader.GetString(reader.GetOrdinal("Id")),
        QuizId = reader.GetString(reader.GetOrdinal("QuizId")),
        OrganizerId = reader.GetString(reader.GetOrdinal("OrganizerId")),
        Code = reader.GetString(reader.GetOrdinal("Code")),
        Status = ParseSessionStatus(reader.GetInt32(reader.GetOrdinal("Status"))),
        CurrentQuestionIndex = reader.GetInt32(reader.GetOrdinal("CurrentQuestionIndex")),
        CreatedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("CreatedAt"))),
        FinishedAt = reader.IsDBNull(reader.GetOrdinal("FinishedAt")) ? null : DateTime.Parse(reader.GetString(reader.GetOrdinal("FinishedAt")))
    };

    private static SessionPlayer MapPlayer(SqliteDataReader reader) => new()
    {
        UserId = reader.GetString(reader.GetOrdinal("UserId")),
        DisplayName = reader.GetString(reader.GetOrdinal("DisplayName")),
        Score = reader.GetInt32(reader.GetOrdinal("Score")),
        JoinedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("JoinedAt")))
    };

    private static SubmittedAnswer MapAnswer(SqliteDataReader reader) => new()
    {
        UserId = reader.GetString(reader.GetOrdinal("UserId")),
        QuestionId = reader.GetString(reader.GetOrdinal("QuestionId")),
        OptionIds = JsonSerializer.Deserialize<List<string>>(reader.GetString(reader.GetOrdinal("OptionIdsJson"))) ?? [],
        Correct = reader.GetInt32(reader.GetOrdinal("Correct")) == 1,
        SubmittedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("SubmittedAt")))
    };

    private static Question HideCorrectAnswers(Question question) => new()
    {
        Id = question.Id,
        Text = question.Text,
        Kind = question.Kind,
        ImageUrl = question.ImageUrl,
        ChoiceMode = question.ChoiceMode,
        Options = question.Options.Select(x => new AnswerOption { Id = x.Id, Text = x.Text }).ToList()
    };

    private static List<Question> NormalizeQuestions(IEnumerable<Question> questions) =>
        questions
            .Where(x => !string.IsNullOrWhiteSpace(x.Text) && x.Options.Count >= 2)
            .Select(x =>
            {
                x.Id = string.IsNullOrWhiteSpace(x.Id) ? Guid.NewGuid().ToString("N") : x.Id;
                foreach (var option in x.Options)
                {
                    option.Id = string.IsNullOrWhiteSpace(option.Id) ? Guid.NewGuid().ToString("N") : option.Id;
                }

                if (x.Options.All(o => !o.IsCorrect))
                {
                    x.Options[0].IsCorrect = true;
                }

                return x;
            })
            .ToList();

    private static string GenerateCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        return new string(Enumerable.Range(0, 6).Select(_ => chars[RandomNumberGenerator.GetInt32(chars.Length)]).ToArray());
    }

    private static string Hash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes("quiz-mvp:" + value));
        return Convert.ToHexString(bytes);
    }

    private static SessionStatus ParseSessionStatus(int value) => Enum.IsDefined(typeof(SessionStatus), value) ? (SessionStatus)value : SessionStatus.Lobby;

    private static Quiz BuildDemoQuiz(string organizerId) => new()
    {
        OrganizerId = organizerId,
        Title = "Демо-квиз по веб-разработке",
        Category = "IT",
        SecondsPerQuestion = 30,
        Rules = "За правильный ответ начисляется 100 баллов. Ответ доступен только на текущем вопросе.",
        Questions =
        [
            new()
            {
                Text = "Какой протокол используется для обмена событиями в реальном времени в этом MVP?",
                ChoiceMode = ChoiceMode.Single,
                Options =
                [
                    new() { Text = "WebSocket", IsCorrect = true },
                    new() { Text = "FTP" },
                    new() { Text = "SMTP" }
                ]
            },
            new()
            {
                Text = "Какие роли есть в системе?",
                ChoiceMode = ChoiceMode.Multiple,
                Options =
                [
                    new() { Text = "Организатор", IsCorrect = true },
                    new() { Text = "Участник", IsCorrect = true },
                    new() { Text = "Гость базы данных" }
                ]
            }
        ]
    };
}
