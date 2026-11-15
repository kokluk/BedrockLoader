using System.IO.Compression;

namespace KoklukBedrockModloader;

public sealed partial class MainForm
{
    async void ExportMods(string root, string[] selected)
    {
        if (selected.Length == 0) return;
        await ExportSelectionAsync("mods", "zip", archive =>
        {
            foreach (string name in selected)
            {
                string file = BedrockSkinSource.SafeChild(root, name.Split('/', '\\'));
                archive.CreateEntryFromFile(file, name.Replace('\\', '/'));
            }
        }, [root]);
    }

    async void ExportSelectedPacks()
    {
        var selected = resourcePackList.CheckedItems.OfType<InstalledPack>()
            .Concat(behaviorPackList.CheckedItems.OfType<InstalledPack>()).ToArray();
        if (selected.Length == 0) return;
        string root = comMojangRoot;
        await ExportSelectionAsync(selected.Length == 1 ? selected[0].DirectoryName : "packs",
            selected.Length == 1 ? "mcpack" : "mcaddon", archive =>
        {
            foreach (var (pack, index) in selected.Select((pack, index) => (pack, index)))
            {
                string path = BedrockSkinSource.SafeChild(root, pack.Category, pack.DirectoryName);
                if (!File.Exists(BedrockSkinSource.SafeChild(path, "manifest.json"))) throw new InvalidDataException("manifest.json");
                if (selected.Length == 1) WriteExportFolder(archive, path);
                else
                {
                    using var stream = archive.CreateEntry($"{index + 1}-{SanitizeName(pack.DirectoryName)}.mcpack",
                        CompressionLevel.NoCompression).Open();
                    using var nested = new ZipArchive(stream, ZipArchiveMode.Create);
                    WriteExportFolder(nested, path);
                }
            }
        }, [root]);
    }

    static void WriteExportFolder(ZipArchive archive, string folder)
    {
        if (IsReparsePoint(folder)) throw new IOException("Linked export folder.");
        var options = new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint };
        foreach (string file in Directory.EnumerateFiles(folder, "*", options))
            archive.CreateEntryFromFile(file, Path.GetRelativePath(folder, file).Replace('\\', '/'));
    }

    async Task ExportSelectionAsync(string name, string extension, Action<ZipArchive> write, string[] sourceFolders)
    {
        using var picker = new SaveFileDialog
        {
            FileName = SanitizeName(name) + "." + extension, DefaultExt = extension,
            Filter = $"{extension.ToUpperInvariant()} (*.{extension})|*.{extension}"
        };
        if (picker.ShowDialog(this) != DialogResult.OK) return;
        string target = picker.FileName;
        if (sourceFolders.Any(folder => Path.GetFullPath(target).StartsWith(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder)) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
        {
            ShowThemedPopup(T("Choose another location", "Başka bir konum seç"),
                T("Save the archive outside the source folder.", "Arşivi kaynak klasörün dışında bir yere kaydet."));
            return;
        }
        string temporary = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await Task.Run(() =>
            {
                using (var archive = ZipFile.Open(temporary, ZipArchiveMode.Create)) write(archive);
                File.Move(temporary, target, overwrite: true);
            });
            if (!IsDisposed) UpdateStatus(T("Export complete.", "Dışa aktarma tamamlandı."));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException)
        {
            if (!IsDisposed) ShowError(ex, T("Export failed", "Dışa aktarılamadı"));
        }
        finally
        {
            try { File.Delete(temporary); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }
}
