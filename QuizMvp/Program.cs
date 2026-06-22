using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using QuizMvp.Services;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/";
        options.AccessDeniedPath = "/";
        options.Cookie.Name = "QuizMvp.Auth";
    });

builder.Services.AddAuthorization();
builder.Services.AddSingleton<AppStore>();
builder.Services.AddSingleton<QuizSocketHub>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseWebSockets();
app.UseAuthentication();
app.UseAuthorization();

app.MapPost("/api/register", async (RegisterRequest request, AppStore store, HttpContext http) =>
{
    var result = await store.RegisterAsync(request);
    if (!result.Success)
    {
        return Results.BadRequest(new { result.Message });
    }

    await SignInAsync(http, result.User!.Id, result.User.DisplayName, result.User.Role.ToString());
    return Results.Ok(result.User.ToPublic());
});

app.MapPost("/api/login", async (LoginRequest request, AppStore store, HttpContext http) =>
{
    var user = await store.ValidateUserAsync(request.Email, request.Password);
    if (user is null)
    {
        return Results.BadRequest(new { Message = "Неверная почта или пароль." });
    }

    await SignInAsync(http, user.Id, user.DisplayName, user.Role.ToString());
    return Results.Ok(user.ToPublic());
});

app.MapPost("/api/logout", async (HttpContext http) =>
{
    await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Ok();
});

app.MapGet("/api/me", async (HttpContext http, AppStore store) =>
{
    var id = http.User.FindFirstValue(ClaimTypes.NameIdentifier);
    var user = id is null ? null : await store.GetUserAsync(id);
    return Results.Ok(user?.ToPublic());
});

app.MapGet("/api/dashboard", async (HttpContext http, AppStore store) =>
{
    var user = await RequireUserAsync(http, store);
    return user is null
        ? Results.Unauthorized()
        : Results.Ok(await store.GetDashboardStatsAsync(user));
});

app.MapGet("/api/quizzes", async (HttpContext http, AppStore store) =>
{
    var user = await RequireUserAsync(http, store);
    return user is null
        ? Results.Unauthorized()
        : Results.Ok(await store.GetQuizzesForAsync(user));
});

app.MapGet("/api/quizzes/{id}", async (string id, HttpContext http, AppStore store) =>
{
    var user = await RequireUserAsync(http, store);
    var quiz = user is null ? null : await store.GetQuizAsync(id);
    return quiz is null ? Results.NotFound() : Results.Ok(quiz);
});

app.MapPost("/api/quizzes", async (QuizDraft draft, HttpContext http, AppStore store) =>
{
    var user = await RequireUserAsync(http, store);
    if (user is null || user.Role != UserRole.Organizer)
    {
        return Results.Forbid();
    }

    var quiz = await store.SaveQuizAsync(user.Id, draft);
    return Results.Ok(quiz);
});

app.MapPost("/api/sessions", async (StartSessionRequest request, HttpContext http, AppStore store, QuizSocketHub hub) =>
{
    var user = await RequireUserAsync(http, store);
    if (user is null || user.Role != UserRole.Organizer)
    {
        return Results.Forbid();
    }

    var session = await store.CreateSessionAsync(user.Id, request.QuizId);
    await hub.BroadcastRoomAsync(session.Code, new { type = "session-created", session });
    return Results.Ok(session);
});

app.MapPost("/api/join", async (JoinRoomRequest request, HttpContext http, AppStore store) =>
{
    var user = await RequireUserAsync(http, store);
    if (user is null)
    {
        return Results.Unauthorized();
    }

    var result = await store.JoinSessionAsync(request.Code, user.Id);
    return result.Success ? Results.Ok(result.Session) : Results.BadRequest(new { result.Message });
});

app.MapGet("/api/history", async (HttpContext http, AppStore store) =>
{
    var user = await RequireUserAsync(http, store);
    return user is null ? Results.Unauthorized() : Results.Ok(await store.GetHistoryAsync(user));
});

app.Map("/ws/quiz", async (HttpContext context, QuizSocketHub hub, AppStore store) =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }

    var user = await RequireUserAsync(context, store);
    if (user is null)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return;
    }

    using var socket = await context.WebSockets.AcceptWebSocketAsync();
    await hub.HandleAsync(socket, user);
});

app.MapFallbackToFile("index.html");

app.Run();

static async Task<AppUser?> RequireUserAsync(HttpContext http, AppStore store)
{
    var id = http.User.FindFirstValue(ClaimTypes.NameIdentifier);
    return id is null ? null : await store.GetUserAsync(id);
}

static async Task SignInAsync(HttpContext http, string id, string name, string role)
{
    var claims = new List<Claim>
    {
        new(ClaimTypes.NameIdentifier, id),
        new(ClaimTypes.Name, name),
        new(ClaimTypes.Role, role)
    };
    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
    await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
}
