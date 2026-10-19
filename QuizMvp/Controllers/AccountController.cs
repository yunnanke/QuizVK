using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuizMvp.Data;
using QuizMvp.Models;
using System.Security.Cryptography;
using System.Text;

namespace QuizMvp.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly AuthenticationModuleSettings _authenticationSettings;

        public AccountController(
            ApplicationDbContext context,
            AuthenticationModuleSettings authenticationSettings)
        {
            _context = context;
            _authenticationSettings = authenticationSettings;
        }

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(string? username, string? password)
        {
            var user = username is null
                ? null
                : await _context.Users.FirstOrDefaultAsync(u => u.Username == username);

            if (user != null && !string.IsNullOrEmpty(password) && VerifyPassword(password, user.PasswordHash))
            {
                HttpContext.Session.SetInt32("UserId", user.Id);
                HttpContext.Session.SetString("Username", user.Username);
                HttpContext.Session.SetString("Role", user.Role);
                return RedirectToAction("Index", "Home");
            }

            ViewBag.Error = "Неверное имя пользователя или пароль";
            return View();
        }

        [HttpGet]
        public IActionResult Register()
        {
            if (!_authenticationSettings.RegistrationEnabled)
            {
                return RegistrationUnavailable();
            }

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Register(string username, string email, string password, string role)
        {
            if (!_authenticationSettings.RegistrationEnabled)
            {
                return RegistrationUnavailable();
            }

            if (await _context.Users.AnyAsync(u => u.Username == username))
            {
                ViewBag.Error = "Пользователь с таким именем уже существует";
                return View();
            }

            var user = new User
            {
                Username = username,
                Email = email,
                PasswordHash = HashPassword(password),
                Role = role ?? "Participant"
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            HttpContext.Session.SetInt32("UserId", user.Id);
            HttpContext.Session.SetString("Username", user.Username);
            HttpContext.Session.SetString("Role", user.Role);

            return RedirectToAction("Index", "Home");
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }

        private string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(bytes);
        }

        private bool VerifyPassword(string password, string hash)
        {
            return HashPassword(password) == hash;
        }

        private IActionResult RegistrationUnavailable()
        {
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            ViewBag.Error = "Регистрация временно отключена настройками приложения.";
            return View("Register");
        }
    }
}
