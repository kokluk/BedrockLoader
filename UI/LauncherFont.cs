namespace KoklukBedrockModloader;

internal static class LauncherFont
{
    internal static readonly string Family = ResolveFamily();

    static string ResolveFamily()
    {
        if (!LauncherAppearance.IsFluent) return "Segoe UI";
        using var font = new Font("Segoe UI Variable Text", 9);
        return font.Name == "Segoe UI Variable Text" ? font.Name : "Segoe UI";
    }

    public static Font Create(float size, FontStyle style = FontStyle.Regular) => new(Family, size, style);
}
