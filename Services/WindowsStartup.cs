using Microsoft.Win32;

namespace KoklukBedrockModloader;

internal static class WindowsStartup
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string EntryName = "BedrockLoader";

    internal static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(EntryName) is string command && !string.IsNullOrWhiteSpace(command);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            return false;
        }
    }

    internal static void SetEnabled(bool enable)
    {
        using var key = enable
            ? Registry.CurrentUser.CreateSubKey(RunKey)
            : Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        if (enable) key!.SetValue(EntryName, $"\"{Application.ExecutablePath}\" --start-in-tray", RegistryValueKind.String);
        else key?.DeleteValue(EntryName, throwOnMissingValue: false);
    }
}
