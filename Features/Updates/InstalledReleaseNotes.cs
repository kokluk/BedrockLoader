namespace KoklukBedrockModloader;

public sealed partial class MainForm
{
    static bool IsVersionUpgrade(string? previous, Version current)
    {
        if (string.IsNullOrWhiteSpace(previous)) return false;
        try { return ReleaseUpdates.ParseVersion(previous) < current; }
        catch (InvalidDataException) { return false; }
    }

    void ShowInstalledReleaseNotes()
    {
        var current = ReleaseUpdates.CurrentVersion;
        string marker = Path.Combine(appDataRoot, "PreviousInstalledVersion.txt");
        string? previousInstalled = null;
        try { if (File.Exists(marker)) previousInstalled = File.ReadAllText(marker); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }

        bool upgraded = IsVersionUpgrade(settings.LastRunVersion, current) ||
            (settings.LastRunVersion != current.ToString() && IsVersionUpgrade(previousInstalled, current));
        if (settings.LastRunVersion != current.ToString())
        {
            settings.LastRunVersion = current.ToString();
            SaveSettings();
        }
        try { if (previousInstalled != null) File.Delete(marker); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }

        if (!upgraded) return;
        ShowThemedPopup(T($"What's new in {DisplayVersion}", $"{DisplayVersion} sürümündeki yenilikler"),
            T("""
              Setup wizard
              • On first launch, setup opens before the launcher. Six clickable steps cover welcome, profile, appearance, customization, Discord and review.
              • The wizard matches the launcher window and has feature cards, step icons, live theme previews and a Skip button.
              • Welcome cards use centered text, larger icons and optional pointer glow. A transparent wolf logo appears in the wizard header.
              • Profile previews the taskbar icon. Choose a shape and a custom color or let it follow the active theme.

              Appearance and dialogs
              • AMOLED stays dark and offers red, blue, yellow, green, rose and lavender. Yellow replaces Orange.
              • Language changes preserve the launcher frame. The theme selector animates when closed from its header.
              • Dialog actions are at the bottom. Customization and Discord use icon rows with right-aligned switches.
              • Launcher Settings under Customization edits the taskbar and tray icon. About links to the source code, bug reports and suggestions on GitHub.

              Discord and exports
              • Idle presence shows Main Menu across the launcher. Turning off RPC disables the version and elapsed-time options.
              • Export checked mods to ZIP, one pack to MCPACK, multiple packs to MCADDON, or a selected Minecraft world to MCWORLD.
              """,
              """
              Kurulum sihirbazı
              • İlk açılışta sihirbaz launcher’dan önce açılıyor. Hoş geldin, Profil, Görünüm, Özelleştirme, Discord ve Son Kontrol adımları tıklanabiliyor.
              • Sihirbaz launcher ile aynı boyutta açılıyor. Karşılama kartları, başlık simgeleri, canlı tema önizlemesi ve Atla düğmesi eklendi.
              • Karşılama kartlarında ortalanmış metinler, daha büyük simgeler ve isteğe bağlı fare parıltısı var. Sihirbazın başlığında şeffaf kurt logosu görünüyor.
              • Profil adımında görev çubuğu simgesi önizlenebiliyor. Simgenin şekli ve özel rengi seçilebiliyor veya tema rengini izlemesi sağlanabiliyor.

              Görünüm ve pencereler
              • AMOLED karanlık kalıyor ve kırmızı, mavi, sarı, yeşil, gül ve lavanta sunuyor. Turuncu temanın yerini Sarı aldı.
              • Dil değişirken launcher çerçevesi yerinde kalıyor. Tema menüsünün başlığına basınca kapanış animasyonu oynuyor.
              • Pencere düğmeleri altta. Özelleştirme ve Discord ayarlarında simgeli satırlar ve sağa hizalı anahtarlar var.
              • Özelleştirme’deki Launcher Ayarları’ndan görev çubuğu ve sistem tepsisi simgesi düzenlenebiliyor. Hakkında sayfasında kaynak kodu, hata bildirimi ve öneri için GitHub bağlantıları var.

              Discord ve dışa aktarma
              • Oyun kapalıyken launcher’ın tüm sayfalarında Ana Menü gösteriliyor. RPC kapalıyken sürüm ve süre seçenekleri devre dışı kalıyor.
              • Seçili modlar ZIP, tek paket MCPACK, birden fazla paket MCADDON ve seçili Minecraft dünyası MCWORLD olarak dışa aktarılabiliyor.
              """));
    }
}
