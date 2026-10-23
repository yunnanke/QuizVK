using Microsoft.AspNetCore.Mvc;
using QuizMvp.Authentication;

namespace QuizMvp.Controllers
{
    public class AccountController : Controller
    {
        private readonly IAuthenticationModule _authenticationModule;

        public AccountController(IAuthenticationModule authenticationModule)
        {
            _authenticationModule = authenticationModule;
        }

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(string? username, string? password)
        {
            var result = await _authenticationModule.AuthenticateAsync(
                new LoginRequest(username, password),
                HttpContext.RequestAborted);

            if (result.Status == AuthenticationStatus.Authenticated && result.Account is { } account)
            {
                SetSession(account);
                return RedirectToAction("Index", "Home");
            }

            ViewBag.Error = "Неверное имя пользователя или пароль";
            return View();
        }

        [HttpGet]
        public IActionResult Register()
        {
            if (!_authenticationModule.RegistrationEnabled)
            {
                return RegistrationUnavailable();
            }

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Register(string username, string email, string password, string role)
        {
            var result = await _authenticationModule.RegisterAsync(
                new RegistrationRequest(username, email, password, role),
                HttpContext.RequestAborted);

            if (result.Status == RegistrationStatus.Disabled)
            {
                return RegistrationUnavailable();
            }

            if (result.Status == RegistrationStatus.UsernameAlreadyExists)
            {
                ViewBag.Error = "Пользователь с таким именем уже существует";
                return View();
            }

            if (result.Status == RegistrationStatus.InvalidPassword)
            {
                ViewBag.Error = $"Регистрация отклонена. Пароль не соответствует требованиям: {RegistrationPasswordPolicy.RequirementsMessage}";
                ViewBag.Username = username;
                ViewBag.Email = email;
                ViewBag.Role = role;
                return View();
            }

            if (result.Status != RegistrationStatus.Registered || result.Account is not { } account)
            {
                throw new InvalidOperationException("Модуль аутентификации вернул неизвестный результат регистрации.");
            }

            SetSession(account);

            return RedirectToAction("Index", "Home");
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }

        private void SetSession(AuthenticatedAccount account)
        {
            HttpContext.Session.SetInt32("UserId", account.Id);
            HttpContext.Session.SetString("Username", account.Username);
            HttpContext.Session.SetString("Role", account.Role);
        }

        private IActionResult RegistrationUnavailable()
        {
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            ViewBag.Error = "Регистрация временно отключена настройками приложения.";
            return View("Register");
        }
    }
}
