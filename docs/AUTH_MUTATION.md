# Auth password-check mutation

The integration tests in `tests/QuizMvp.Tests/AuthControllerTests.cs` include
wrong-password cases for the MVC login route. They are intended to detect the
same behavior-changing mutation described in the practice report: bypassing
the password comparison must make the wrong-password cases fail.

For a local mutation experiment, temporarily replace the
`VerifyPassword(password, user.PasswordHash)` part of the successful-login
condition in `QuizMvp/Controllers/AccountController.cs` with `true`. Keep the
null/empty-password guard. Run the focused test project; the wrong-password
cases must fail because login redirects instead of returning the Login view.
Restore the original condition immediately after the experiment. Do not commit
the mutated production code.

Run the tests with:

```powershell
dotnet test tests/QuizMvp.Tests/QuizMvp.Tests.csproj
```

The current application stores SHA-256/Base64 password hashes and uses MVC
sessions. JWT, BCrypt, API status-code contracts, and role-protected API routes
from the report are not implemented by this project, so those report scenarios
need an agreed application contract before they can be tested here.
