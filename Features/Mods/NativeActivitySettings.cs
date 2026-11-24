namespace KoklukBedrockModloader;

public sealed partial class MainForm
{
    void ConfigureNativeActivity(Form owner)
    {
        const string name = "BedrockLoaderPresence.dll";
        var target = Path.Combine(modsFolder, name);
        var source = Path.Combine(AppContext.BaseDirectory, "Assets", name);
        string title = T("Discord activity mod", "Discord etkinlik modu");
        try
        {
            bool installed = File.Exists(target) && File.ReadAllBytes(target).AsSpan().SequenceEqual(File.ReadAllBytes(source));
            string verb = installed ? T("Remove", "Kaldır") : File.Exists(target) ? T("Replace", "Değiştir") : T("Install", "Kur");
            string info = installed ? T("Remove the activity DLL from your mods folder?", "Etkinlik DLL dosyası mod klasöründen kaldırılsın mı?") :
                T("Install the local-world activity DLL? Close Minecraft before installation. A compatible native modloader is required.",
                  "Yerel dünya etkinliğini bildiren DLL kurulsun mu? Kurulumdan önce Minecraft’ı kapat. Uyumlu bir native modloader gerekir.");
            if (ShowThemedPopup(title, info + "\n\n" + target, true, verb, owner: owner) != DialogResult.Yes) return;
            if (installed) File.Delete(target);
            else
            {
                Directory.CreateDirectory(modsFolder);
                var staged = Path.Combine(modsFolder, $".{Guid.NewGuid():N}.tmp");
                try { File.Copy(source, staged); File.Move(staged, target, true); }
                finally { if (File.Exists(staged)) File.Delete(staged); }
            }
            RefreshMods();
            ShowThemedPopup(title, installed ? T("Mod removed.", "Mod kaldırıldı.") :
                T("Mod installed. Restart Minecraft to load it.", "Mod kuruldu. Yüklemek için Minecraft’ı yeniden başlat."), owner: owner);
        }
        catch (Exception ex) { ShowThemedPopup(title, ex.Message, owner: owner); }
    }
}
