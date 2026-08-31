using Microsoft.Win32;

namespace Ignyos.LanPortal.Api.Services;

public interface IWindowsStartupRegistration
{
    WindowsStartupRegistrationState GetState();

    void Apply(bool enabled);
}

public sealed class WindowsStartupRegistration(
    IAppSettingsStore settingsStore,
    ILogger<WindowsStartupRegistration> logger) : IWindowsStartupRegistration, IHostedService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Ignyos LAN Portal";

    public Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            Apply(settingsStore.GetRunAtWindowsStartup());
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Unable to apply Windows startup registration.");
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public WindowsStartupRegistrationState GetState()
    {
        if (!OperatingSystem.IsWindows())
        {
            return new WindowsStartupRegistrationState(false, false, null, "Windows startup registration is only available on Windows.");
        }

        var command = BuildStartupCommand();
        if (command is null)
        {
            return new WindowsStartupRegistrationState(false, false, null, "Startup registration is available only in an installed LAN Portal package.");
        }

        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        var storedValue = key?.GetValue(ValueName) as string;
        var isEnabled = !string.IsNullOrWhiteSpace(storedValue);
        var message = isEnabled && !string.Equals(storedValue, command, StringComparison.Ordinal)
            ? "LAN Portal is registered for startup, but the command will be refreshed the next time settings are saved."
            : "LAN Portal can be started automatically when you sign in to Windows.";

        return new WindowsStartupRegistrationState(true, isEnabled, command, message);
    }

    public void Apply(bool enabled)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var command = BuildStartupCommand();
        if (command is null)
        {
            return;
        }

        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
            ?? Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);

        if (enabled)
        {
            key.SetValue(ValueName, command, RegistryValueKind.String);
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }

    private static string? BuildStartupCommand()
    {
        var baseDirectory = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var appDirectory = Directory.GetParent(baseDirectory)?.FullName;
        if (string.IsNullOrWhiteSpace(appDirectory))
        {
            return null;
        }

        var launcherPath = Path.Combine(appDirectory, "Launch-LanPortal.ps1");
        if (!File.Exists(launcherPath))
        {
            return null;
        }

        var powershellPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            "WindowsPowerShell",
            "v1.0",
            "powershell.exe");

        return $"\"{powershellPath}\" -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{launcherPath}\"";
    }
}

public sealed record WindowsStartupRegistrationState(
    bool IsSupported,
    bool IsEnabled,
    string? Command,
    string Message);
