using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuizMvp.Data;
using QuizMvp.Models;
using QuizMvp.Services;

namespace QuizMvp.Controllers
{
    public class QuizController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IQuizService _quizService;

        public QuizController(ApplicationDbContext context, IQuizService quizService)
        {
            _context = context;
            _quizService = quizService;
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

            var quizId = await _quizService.CreateQuizAsync(quiz, userId.Value);

            return Ok(new { id = quizId });
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

            if (quiz.CreatorId != userId.Value)
                return Forbid();

            var session = await _quizService.StartSessionAsync(quizId, userId.Value, SessionStatus.Active);
            if (session == null)
                return NotFound();

            ViewBag.SessionId = session.Id;
            ViewBag.RoomCode = session.JoinCode;
            ViewBag.TotalQuestions = quiz.Questions.Count;

            return View(quiz);
        }

        [HttpPost]
        public async Task<IActionResult> StartQuizSession(int quizId)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
                return Unauthorized();

            var session = await _quizService.StartSessionAsync(quizId, userId.Value);
            if (session == null)
                return NotFound();

            var totalQuestions = await _context.Questions.CountAsync(q => q.QuizId == quizId);

            return Ok(new
            {
                sessionId = session.Id,
                roomCode = session.JoinCode,
                totalQuestions
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
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
                return Unauthorized(new { error = "Пользователь не авторизован" });

            if (submission == null)
                return BadRequest(new { error = "Данные не переданы" });

            var score = await _quizService.SubmitAnswerAsync(
                submission.SessionId,
                submission.QuestionId,
                userId.Value,
                submission.SelectedOptionIds);

            if (score == null)
                return BadRequest(new { error = "Ответ не относится к активной сессии или содержит неверные варианты" });

            return Ok(new
            {
                isCorrect = score.IsCorrect,
                points = score.PointsEarned,
                message = score.IsCorrect ? "Правильно!" : "Неправильно"
            });
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
