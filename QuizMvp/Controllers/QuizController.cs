using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuizMvp.Data;
using QuizMvp.Models;
using System.Text.Json;

namespace QuizMvp.Controllers
{
    public class QuizController : Controller
    {
        private readonly ApplicationDbContext _context;

        public QuizController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult Create()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
                return RedirectToAction("Login", "Account");

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> CreateQuiz([FromBody] QuizDto quizDto)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
                return Unauthorized();

            var quiz = new Quiz
            {
                Title = quizDto.Title,
                Description = quizDto.Description,
                Category = quizDto.Category,
                TimeLimitPerQuestion = quizDto.TimeLimit,
                Rules = quizDto.Rules,
                CreatorId = userId.Value,
                Questions = quizDto.Questions.Select(q => new Question
                {
                    Text = q.Text,
                    Type = q.Type == "image" ? QuestionType.Image : QuestionType.Text,
                    ImageUrl = q.ImageUrl,
                    IsMultipleChoice = q.IsMultipleChoice,
                    Points = q.Points,
                    Options = q.Options.Select(o => new AnswerOption
                    {
                        Text = o.Text,
                        ImageUrl = o.ImageUrl,
                        IsCorrect = o.IsCorrect
                    }).ToList()
                }).ToList()
            };

            _context.Quizzes.Add(quiz);
            await _context.SaveChangesAsync();

            return Ok(new { id = quiz.Id });
        }

        [HttpGet]
        public async Task<IActionResult> StartQuiz(int quizId)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
                return RedirectToAction("Login", "Account");

            var quiz = await _context.Quizzes
                .Include(q => q.Questions)
                .ThenInclude(q => q.Options)
                .FirstOrDefaultAsync(q => q.Id == quizId);

            if (quiz == null)
                return NotFound();

            if (quiz.CreatorId != userId)
                return Forbid();

            return View(quiz);
        }

        [HttpGet]
        public async Task<IActionResult> Play(int quizId)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
                return RedirectToAction("Login", "Account");

            var quiz = await _context.Quizzes
                .Include(q => q.Questions)
                .ThenInclude(q => q.Options)
                .FirstOrDefaultAsync(q => q.Id == quizId);

            if (quiz == null)
                return NotFound();

            var roomCode = GenerateRoomCode();
            var session = new Session
            {
                QuizId = quizId,
                JoinCode = roomCode,
                StartedAt = DateTime.UtcNow,
                Status = SessionStatus.Active,
                CurrentQuestionIndex = 0
            };

            _context.Sessions.Add(session);
            await _context.SaveChangesAsync();

            ViewBag.SessionId = session.Id;
            ViewBag.RoomCode = roomCode;
            ViewBag.TotalQuestions = quiz.Questions.Count;

            return View(quiz);
        }

        [HttpPost]
        public async Task<IActionResult> StartQuizSession(int quizId)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
                return Unauthorized();

            var quiz = await _context.Quizzes
                .Include(q => q.Questions)
                .FirstOrDefaultAsync(q => q.Id == quizId);

            if (quiz == null)
                return NotFound();

            var roomCode = GenerateRoomCode();
            while (await _context.Sessions.AnyAsync(s => s.JoinCode == roomCode))
            {
                roomCode = GenerateRoomCode();
            }

            var session = new Session
            {
                QuizId = quizId,
                JoinCode = roomCode,
                StartedAt = DateTime.UtcNow,
                Status = SessionStatus.Waiting
            };

            _context.Sessions.Add(session);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                sessionId = session.Id,
                roomCode = roomCode,
                totalQuestions = quiz.Questions.Count
            });
        }

        [HttpGet]
        public async Task<IActionResult> Statistics()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
                return RedirectToAction("Login", "Account");

            var quizzes = await _context.Quizzes
                .Where(q => q.CreatorId == userId)
                .Include(q => q.Sessions)
                .ThenInclude(s => s.ParticipantAnswers)
                .OrderByDescending(q => q.CreatedAt)
                .ToListAsync();

            var stats = quizzes.Select(q => new
            {
                QuizTitle = q.Title,
                QuizId = q.Id,
                TotalSessions = q.Sessions.Count,
                TotalParticipants = q.Sessions.SelectMany(s => s.ParticipantAnswers)
                    .Select(a => a.UserId)
                    .Distinct()
                    .Count(),
                AverageScore = q.Sessions
                    .SelectMany(s => s.ParticipantAnswers)
                    .GroupBy(a => a.UserId)
                    .Select(g => g.Sum(a => a.PointsEarned))
                    .DefaultIfEmpty(0)
                    .Average()
            }).ToList();

            ViewBag.Statistics = stats;
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
                return RedirectToAction("Login", "Account");

            var quiz = await _context.Quizzes
                .Include(q => q.Questions)
                .Include(q => q.Sessions)
                .ThenInclude(s => s.ParticipantAnswers)
                .ThenInclude(pa => pa.User)
                .FirstOrDefaultAsync(q => q.Id == id && q.CreatorId == userId);

            if (quiz == null)
                return NotFound();

            return View(quiz);
        }

        [HttpGet]
        public async Task<IActionResult> Join(string roomCode)
        {
            var session = await _context.Sessions
                .Include(s => s.Quiz)
                .ThenInclude(q => q.Questions)
                .ThenInclude(q => q.Options)
                .FirstOrDefaultAsync(s => s.JoinCode == roomCode && s.Status != SessionStatus.Finished);

            if (session == null)
            {
                ViewBag.Error = "Квиз не найден или уже завершен";
                return View("JoinError");
            }

            return View(session);
        }

        [HttpGet]
        public async Task<IActionResult> GetQuizData(int sessionId)
        {
            var session = await _context.Sessions
                .Include(s => s.Quiz)
                .ThenInclude(q => q.Questions)
                .ThenInclude(q => q.Options)
                .FirstOrDefaultAsync(s => s.Id == sessionId);

            if (session == null)
                return NotFound();

            var questions = session.Quiz.Questions.Select(q => new
            {
                q.Id,
                q.Text,
                q.Type,
                q.ImageUrl,
                q.IsMultipleChoice,
                Options = q.Options.Select(o => new { o.Id, o.Text, o.ImageUrl })
            });

            return Ok(new
            {
                quizTitle = session.Quiz.Title,
                totalQuestions = session.Quiz.Questions.Count,
                questions = questions
            });
        }

        [HttpPost]
        public async Task<IActionResult> SubmitAnswer([FromBody] AnswerSubmission submission)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                    return Unauthorized(new { error = "Пользователь не авторизован" });

                if (submission == null)
                    return BadRequest(new { error = "Данные не переданы" });

                var question = await _context.Questions
                    .Include(q => q.Options)
                    .FirstOrDefaultAsync(q => q.Id == submission.QuestionId);

                if (question == null)
                    return NotFound(new { error = "Вопрос не найден" });

                var selectedIds = submission.SelectedOptionIds ?? new List<int>();
                var correctIds = question.Options.Where(o => o.IsCorrect).Select(o => o.Id).ToList();

                bool isCorrect = correctIds.Count == selectedIds.Count &&
                                 !correctIds.Except(selectedIds).Any();

                var answer = new ParticipantAnswer
                {
                    SessionId = submission.SessionId,
                    QuestionId = submission.QuestionId,
                    UserId = userId.Value,
                    SelectedOptionIds = JsonSerializer.Serialize(selectedIds),
                    IsCorrect = isCorrect,
                    PointsEarned = isCorrect ? question.Points : 0,
                    SubmittedAt = DateTime.UtcNow
                };

                _context.ParticipantAnswers.Add(answer);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    isCorrect,
                    points = answer.PointsEarned,
                    message = isCorrect ? "Правильно!" : "Неправильно"
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error submitting answer: {ex.Message}");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetLeaderboard(int sessionId)
        {
            var answers = await _context.ParticipantAnswers
                .Include(pa => pa.User)
                .Where(pa => pa.SessionId == sessionId)
                .GroupBy(pa => new { pa.UserId, pa.User.Username })
                .Select(g => new
                {
                    username = g.Key.Username,
                    totalScore = g.Sum(pa => pa.PointsEarned)
                })
                .OrderByDescending(r => r.totalScore)
                .ToListAsync();

            return Ok(answers);
        }

        private string GenerateRoomCode()
        {
            var chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, 6)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        [HttpGet]
        public async Task<IActionResult> GetQuestion(int sessionId, int questionIndex)
        {
            try
            {
                var session = await _context.Sessions
                    .Include(s => s.Quiz)
                    .ThenInclude(q => q.Questions)
                    .ThenInclude(q => q.Options)
                    .FirstOrDefaultAsync(s => s.Id == sessionId);

                if (session == null)
                    return NotFound(new { error = "Сессия не найдена" });

                var questions = session.Quiz.Questions.OrderBy(q => q.Id).ToList();

                if (questionIndex >= questions.Count)
                    return NotFound(new { error = "Вопрос не найден" });

                var question = questions[questionIndex];

                return Ok(new
                {
                    id = question.Id,
                    text = question.Text,
                    imageUrl = question.ImageUrl,
                    isMultiple = question.IsMultipleChoice,
                    points = question.Points,
                    index = questionIndex,
                    total = questions.Count,
                    options = question.Options.Select(o => new {
                        o.Id,
                        o.Text,
                        o.ImageUrl
                    })
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting question: {ex.Message}");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetHistory()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
                return Unauthorized();

            var history = await _context.ParticipantAnswers
                .Include(pa => pa.Session)
                .ThenInclude(s => s.Quiz)
                .Where(pa => pa.UserId == userId)
                .GroupBy(pa => pa.SessionId)
                .Select(g => new
                {
                    quizTitle = g.First().Session.Quiz.Title,
                    score = g.Sum(pa => pa.PointsEarned),
                    date = g.First().SubmittedAt
                })
                .OrderByDescending(h => h.date)
                .ToListAsync();

            return Ok(history);
        }

        [HttpGet]
        public async Task<IActionResult> MyQuizzes()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
                return RedirectToAction("Login", "Account");

            var quizzes = await _context.Quizzes
                .Where(q => q.CreatorId == userId)
                .Include(q => q.Questions)
                .OrderByDescending(q => q.CreatedAt)
                .ToListAsync();

            return View(quizzes);
        }
    }

    public class QuizDto
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
        public int TimeLimit { get; set; } = 30;
        public string Rules { get; set; }
        public List<QuestionDto> Questions { get; set; }
    }

    public class QuestionDto
    {
        public string Text { get; set; }
        public string Type { get; set; } = "text";
        public string ImageUrl { get; set; }
        public bool IsMultipleChoice { get; set; }
        public int Points { get; set; } = 10;
        public List<AnswerOptionDto> Options { get; set; }
    }

    public class AnswerOptionDto
    {
        public string Text { get; set; }
        public string ImageUrl { get; set; }
        public bool IsCorrect { get; set; }
    }

    public class AnswerSubmission
    {
        public int SessionId { get; set; }
        public int QuestionId { get; set; }
        public List<int> SelectedOptionIds { get; set; }
    }
}