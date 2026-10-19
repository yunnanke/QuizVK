using System.Reflection;

namespace QuizMvp.Infrastructure;

public static class BuildInfo
{
    public static string Version =>
        typeof(BuildInfo).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion
        ?? "unknown";
}
