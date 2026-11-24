namespace KoklukBedrockModloader;

public sealed partial class MainForm
{
    void OfferUpdateCleanup(bool automatic = false)
    {
        string title = T("Old update files", "Eski güncelleme dosyaları");
        if (updatesBusy)
        {
            if (!automatic) ShowThemedPopup(title, T("Wait for the current update to finish.", "Devam eden güncellemenin bitmesini bekle."));
            return;
        }

        try
        {
            string root = Path.Combine(appDataRoot, "Updates");
            string[] files = CachedUpdateFiles(root).ToArray();
            if (settings.UpdateCleanupPending)
            {
                settings.UpdateCleanupPending = false;
                SaveSettings();
            }
            if (files.Length == 0)
            {
                if (!automatic) ShowThemedPopup(title, T("No old update files found.", "Eski güncelleme dosyası bulunamadı."));
                return;
            }

            string message = T($"Delete {files.Length} old update file(s) from this folder?\n\n{root}",
                $"Bu klasördeki {files.Length} eski güncelleme dosyası silinsin mi?\n\n{root}");
            if (ShowThemedPopup(title, message, true, T("Delete files", "Dosyaları sil"), destructive: true) != DialogResult.Yes || updatesBusy)
                return;

            int deleted = 0;
            var approved = new HashSet<string>(files, StringComparer.OrdinalIgnoreCase);
            foreach (string file in CachedUpdateFiles(root).Where(approved.Contains).ToArray())
            {
                try { File.Delete(file); deleted++; }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                { LogAction($"Could not delete update file: {ex.Message}", $"Güncelleme dosyası silinemedi: {ex.Message}"); }
            }
            UpdateStatus(T($"Removed {deleted} of {files.Length} old update files.", $"{files.Length} eski güncelleme dosyasından {deleted} tanesi silindi."));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { ShowError(ex, T("Update cleanup failed", "Güncelleme temizliği başarısız")); }
    }

    static IEnumerable<string> CachedUpdateFiles(string root)
    {
        if (!Directory.Exists(root) || File.GetAttributes(root).HasFlag(FileAttributes.ReparsePoint)) yield break;
        foreach (string folder in Directory.EnumerateDirectories(root))
        {
            if (!Guid.TryParseExact(Path.GetFileName(folder), "N", out _) ||
                File.GetAttributes(folder).HasFlag(FileAttributes.ReparsePoint)) continue;
            foreach (string name in new[] { "BedrockLoader-Setup.exe", "setup.download" })
            {
                string file = Path.Combine(folder, name);
                if (File.Exists(file) && !File.GetAttributes(file).HasFlag(FileAttributes.ReparsePoint)) yield return file;
            }
        }
    }
}
