using System.Net.Http;
using System.Security.Cryptography;

namespace KoklukBedrockModloader;

public sealed partial class MainForm
{
    bool installingModloader;
    Button? installModloaderButton;

    async void InstallModloader()
    {
        if (installingModloader) return;
        const string name = "vcruntime140_1.dll";
        const string url = "https://github.com/kokluk/BedrockFree/releases/download/McBedrock/" + name;
        const string sha256 = "15B2223804AC8A3A10A93233618768413F280C08055CF6E2917DFBC9938246D6";
        installingModloader = true;
        if (installModloaderButton is { IsDisposed: false } button) button.Enabled = false;
        string? staged = null;
        try
        {
            string folder = settings.ContentPath.Trim().Trim('"');
            if (!Directory.Exists(folder))
            {
                using var picker = new FolderBrowserDialog
                {
                    Description = T("Select Minecraft's Content folder", "Minecraft'ın Content klasörünü seç"),
                    UseDescriptionForTitle = true,
                    ShowNewFolderButton = false
                };
                if (picker.ShowDialog(this) != DialogResult.OK) return;
                folder = picker.SelectedPath;
            }
            folder = Path.GetFullPath(folder);
            string target = Path.Combine(folder, name);
            bool replace = File.Exists(target);
            string message = T(
                $"Download {name} and install it in the Minecraft Content folder?\n\nSource:\n{url}\n\nDestination:\n{target}\n\nDownload and installation start after your approval.",
                $"{name} dosyası aşağıdaki bağlantıdan indirilip Minecraft Content klasörüne kurulsun mu?\n\nKaynak:\n{url}\n\nHedef:\n{target}\n\nİndirme ve kurulum yalnızca kuruluma izin verirsen başlayacak.");
            if (ShowThemedPopup(T("Install modloader", "Modloader kurulumu"), message, true) != DialogResult.Yes) return;
            if (replace && ShowThemedPopup(T("Replace existing file?", "Var olan dosya değiştirilsin mi?"),
                T($"A file already exists here:\n\n{target}\n\nReplace it with the downloaded modloader? Close Minecraft before continuing.",
                  $"Bu konumda zaten bir dosya var:\n\n{target}\n\nİndirilen modloader ile değiştirilsin mi? Devam etmeden önce Minecraft'ı kapat."),
                true) != DialogResult.Yes) return;
            UpdateStatus(T("Downloading modloader…", "Modloader indiriliyor…"));
            using var http = new HttpClient { MaxResponseContentBufferSize = 50176, Timeout = TimeSpan.FromSeconds(45) };
            var bytes = await http.GetByteArrayAsync(url);
            if (bytes.Length != 50176 || Convert.ToHexString(SHA256.HashData(bytes)) != sha256)
                throw new InvalidDataException(T("Modloader verification failed.", "Modloader doğrulaması başarısız."));
            staged = Path.Combine(Path.GetDirectoryName(target)!, $".{Guid.NewGuid():N}.tmp");
            await File.WriteAllBytesAsync(staged, bytes);
            File.Move(staged, target, replace);
            UpdateStatus(T("Modloader installed.", "Modloader kuruldu."));
            ShowThemedPopup(T("Installation complete", "Kurulum tamamlandı"),
                T($"Modloader installed successfully.\n\n{target}", $"Modloader başarıyla kuruldu.\n\n{target}"));
        }
        catch (Exception ex) { ShowError(ex, T("Modloader installation failed", "Modloader kurulamadı")); }
        finally
        {
            installingModloader = false;
            if (installModloaderButton is { IsDisposed: false } homeButton) homeButton.Enabled = true;
            try { if (staged != null) File.Delete(staged); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            { LogAction($"Could not remove staged modloader: {ex.Message}", $"Geçici modloader dosyası silinemedi: {ex.Message}"); }
        }
    }
}
