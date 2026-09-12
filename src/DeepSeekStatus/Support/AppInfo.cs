using System.Reflection;

namespace DeepSeekStatus.Support;

public static class AppInfo
{
    public const string ProjectUrl = "https://github.com/owenzhao/DeepSeekStatus";
    public const string Name = "DeepSeek Status";

    public static string Version { get; } = LoadVersion();

    public static string DisplayName => $"{Name} {Version}";

    private static string LoadVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrEmpty(informational))
        {
            var plus = informational.IndexOf('+');
            return plus >= 0 ? informational[..plus] : informational;
        }

        return assembly.GetName().Version?.ToString(3) ?? "1.0.0";
    }
}
