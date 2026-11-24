namespace KoklukBedrockModloader;

internal static class LauncherAppearance
{
#if FLUENT_VARIANT
    internal static readonly bool IsFluent = true;
#else
    internal static readonly bool IsFluent = false;
#endif

    internal static int Radius(int original, int fluent) => IsFluent ? fluent : original;
    internal static bool SquareButtons { get; set; } = true;
}

public sealed partial class MainForm
{
    void ApplyFluentSurfaces()
    {
        if (!LauncherAppearance.IsFluent || settings.Theme != "Material You") return;
        const double tint = .13;
        if (lightMode)
        {
            Bg = MixColors(Color.FromArgb(240, 241, 244), Accent, tint);
            Surface = MixColors(Color.FromArgb(252, 252, 253), Accent, tint / 2);
            SurfaceAlt = MixColors(Color.FromArgb(232, 234, 238), Accent, tint);
            TextColor = Color.FromArgb(30, 30, 32);
            Muted = Color.FromArgb(83, 84, 90);
        }
        else
        {
            Bg = MixColors(Color.FromArgb(31, 31, 34), Accent, tint);
            Surface = MixColors(Color.FromArgb(41, 41, 44), Accent, tint);
            SurfaceAlt = MixColors(Color.FromArgb(54, 54, 58), Accent, tint);
            TextColor = Color.FromArgb(246, 246, 249);
            Muted = Color.FromArgb(199, 199, 207);
        }
    }
}
