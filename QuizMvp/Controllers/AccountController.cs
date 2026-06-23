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

        public AccountController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(string username, string password)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Username == username);

            if (user != null && VerifyPassword(password, user.PasswordHash))
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
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Register(string username, string email, string password, string role)
        {
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
    }
}