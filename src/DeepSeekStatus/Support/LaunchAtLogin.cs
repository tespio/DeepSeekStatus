using Microsoft.Win32;

namespace DeepSeekStatus.Support;

public static class LaunchAtLogin
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "DeepSeekStatus";

    public static string ExecutablePath => Environment.ProcessPath ?? string.Empty;

    public static bool IsEnabled
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
                return key?.GetValue(ValueName) is string value && value.Length > 0;
            }
            catch
            {
                return false;
            }
        }
    }

    public static string? Set(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, true);
            if (key is null)
            {
                return "run key unavailable";
            }

            if (enabled)
            {
                key.SetValue(ValueName, $"\"{ExecutablePath}\"", RegistryValueKind.String);
            }
            else
            {
                key.DeleteValue(ValueName, false);
            }

            return null;
        }
        catch (Exception exception)
        {
            return exception.Message;
        }
    }
}
