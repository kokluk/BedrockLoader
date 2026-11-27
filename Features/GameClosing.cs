using System.ComponentModel;
using System.Diagnostics;

namespace KoklukBedrockModloader;

public sealed partial class MainForm
{
    Action WatchHomeGame(Control page, Control closeActions)
    {
        var timer = new System.Windows.Forms.Timer { Interval = 1200 };
        bool busy = false;
        async Task RefreshAsync()
        {
            if (busy || page.IsDisposed || !page.Visible) return;
            busy = true;
            bool cinnabar = cinnabarMode;
            bool nightly = settings.CinnabarNightly;
            string path = settings.ContentPath;
            try
            {
                var game = await Task.Run(() => DetectHomeGame(cinnabar, nightly, path));
                if (!page.IsDisposed && page.Visible && cinnabar == cinnabarMode &&
                    (cinnabar ? nightly == settings.CinnabarNightly : path == settings.ContentPath))
                    closeActions.Visible = game != null;
            }
            catch (Exception ex) when (ex is Win32Exception or IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
            { if (!page.IsDisposed) closeActions.Hide(); }
            finally { busy = false; }
        }
        timer.Tick += (_, _) => _ = RefreshAsync();
        page.VisibleChanged += (_, _) =>
        {
            if (page.Visible && !page.Disposing) { timer.Start(); _ = RefreshAsync(); }
            else { timer.Stop(); if (!closeActions.IsDisposed) closeActions.Hide(); }
        };
        page.Disposed += (_, _) => timer.Dispose();
        return () =>
        {
            closeActions.Hide();
            if (page.Visible) { timer.Start(); _ = RefreshAsync(); }
        };
    }

    MinecraftInstallation.RunningGame? DetectHomeGame(bool cinnabar, bool nightly, string contentPath) =>
        cinnabar ? FindRunningCinnabar(FindCinnabar(nightly)) : MinecraftInstallation.FindRunningGame(contentPath);

    void CloseSelectedGame(bool force)
    {
        string name = cinnabarMode ? "Cinnabar" : "Minecraft";
        try
        {
            var game = DetectHomeGame(cinnabarMode, settings.CinnabarNightly, settings.ContentPath);
            if (game == null)
            {
                UpdateStatus(T($"{name} is not running.", $"{name} çalışmıyor."));
                return;
            }
            if (force && ShowThemedPopup(T($"Force close {name}?", $"{name} zorla kapatılsın mı?"),
                T("Unsaved progress may be lost.", "Kaydedilmemiş ilerleme kaybolabilir."),
                true, T("Force close", "Zorla kapat"), destructive: true) != DialogResult.Yes) return;

            using var process = Process.GetProcessById(game.Pid);
            if (process.StartTime.ToUniversalTime() != game.StartedUtc || process.HasExited)
            {
                UpdateStatus(T($"{name} is no longer running.", $"{name} artık çalışmıyor."));
                return;
            }
            if (force)
            {
                process.Kill();
                UpdateStatus(T($"Force close requested for {name}.", $"{name} için zorla kapatma istendi."));
            }
            else if (process.CloseMainWindow())
                UpdateStatus(T($"Close requested for {name}.", $"{name} için normal kapatma istendi."));
            else
                UpdateStatus(T($"{name} did not accept a normal close request.", $"{name} normal kapatma isteğini kabul etmedi."));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        { UpdateStatus(T($"{name} is no longer running.", $"{name} artık çalışmıyor.")); }
        catch (Exception ex) when (ex is Win32Exception or IOException or UnauthorizedAccessException)
        { ShowError(ex, T($"Could not close {name}", $"{name} kapatılamadı")); }
    }

    static MinecraftInstallation.RunningGame? FindRunningCinnabar(string? executable)
    {
        if (string.IsNullOrEmpty(executable)) return null;
        MinecraftInstallation.RunningGame? newest = null;
        foreach (var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(executable)))
        {
            using (process)
            {
                try
                {
                    if (!string.Equals(process.MainModule?.FileName, executable, StringComparison.OrdinalIgnoreCase) ||
                        process.HasExited) continue;
                    DateTime started = process.StartTime.ToUniversalTime();
                    if (newest == null || newest.StartedUtc < started) newest = new(process.Id, started, null);
                }
                catch (Exception ex) when (ex is Win32Exception or InvalidOperationException) { }
            }
        }
        return newest;
    }
}
