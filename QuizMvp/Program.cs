using Microsoft.EntityFrameworkCore;
using QuizMvp.Data;
using QuizMvp.Hubs;
using QuizMvp.Infrastructure;
using QuizMvp.Services;

var builder = WebApplication.CreateBuilder(args);
var runtimeCheckRequested = args.Contains("--check-runtime-config", StringComparer.OrdinalIgnoreCase);
var connectionString = builder.Configuration["QUIZVK_CONNECTION_STRING"]
    ?? builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrWhiteSpace(connectionString))
{
    const string message = "Configuration error: set QUIZVK_CONNECTION_STRING or ConnectionStrings:DefaultConnection.";

    if (runtimeCheckRequested)
    {
        Console.Error.WriteLine(message);
        Environment.Exit(2);
    }

    throw new InvalidOperationException(message);
}

if (runtimeCheckRequested)
{
    Console.WriteLine($"Runtime configuration valid. Version={BuildInfo.Version}; Environment={builder.Environment.EnvironmentName}");
    return;
}

// Добавление сервисов
builder.Services.AddControllersWithViews();
builder.Services.AddSingleton<IAnswerScoringService, ExactMatchAnswerScoringService>();
builder.Services.AddScoped<IQuizService, QuizService>();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromDays(1);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// PostgreSQL connection is supplied through the environment or .NET user-secrets.
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

// Добавление SignalR
builder.Services.AddSignalR();

var app = builder.Build();

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
app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    version = BuildInfo.Version,
    environment = app.Environment.EnvironmentName
}));
app.MapGet("/health/ready", async (ApplicationDbContext dbContext, CancellationToken cancellationToken) =>
    await dbContext.Database.CanConnectAsync(cancellationToken)
        ? Results.Ok(new { status = "ready" })
        : Results.StatusCode(StatusCodes.Status503ServiceUnavailable));

app.Logger.LogInformation(
    "QuizVK started. Version {Version}; Environment {Environment}",
    BuildInfo.Version,
    app.Environment.EnvironmentName);

app.Run();
