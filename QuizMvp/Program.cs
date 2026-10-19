using Microsoft.EntityFrameworkCore;
using System.Reflection;
using System.Text.Json;
using QuizMvp.Data;
using QuizMvp.Hubs;
using QuizMvp.Models;

var builder = WebApplication.CreateBuilder(args);

var authenticationSettings = AuthenticationModuleSettings.Parse(
    builder.Configuration["Authentication:RegistrationEnabled"]);
builder.Services.AddSingleton(authenticationSettings);

// Добавление сервисов
builder.Services.AddControllersWithViews();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromDays(1);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Настройка SQLite
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Добавление SignalR
builder.Services.AddSignalR();

var app = builder.Build();

var assembly = typeof(Program).Assembly;
var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
    ?? assembly.GetName().Version?.ToString()
    ?? "unknown";
var revision = version.Split('+', 2).ElementAtOrDefault(1) ?? "unknown";
var buildConfiguration = assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration
    ?? "unknown";

app.Logger.LogInformation(
    "Запуск модуля аутентификации; регистрация включена: {RegistrationEnabled}; версия {Version}; конфигурация сборки {BuildConfiguration}.",
    authenticationSettings.RegistrationEnabled,
    version,
    buildConfiguration);

app.MapGet("/_diagnostics/authentication", (AuthenticationModuleSettings settings) =>
    Results.Json(new
    {
        status = "ready",
        module = "Authentication",
        registrationEnabled = settings.RegistrationEnabled,
        version,
        revision,
        buildConfiguration
    }, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));

// Настройка пайплайна
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

// ⭐ ВАЖНО: Правильный порядок маршрутов
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapHub<QuizHub>("/quizHub");

// Создание базы данных при запуске
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    dbContext.Database.EnsureCreated();
}

app.Run();
public partial class Program { }
