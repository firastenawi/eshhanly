using MudBlazor;

namespace Eshhanly.Components.Layout;

internal static class EshhanlyTheme
{
    public static readonly MudTheme Default = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#0E9F6E",   // EV green
            Secondary = "#1C64F2",
            AppbarBackground = "#0E9F6E",
            Background = "#F6F8F7",
        },
        PaletteDark = new PaletteDark
        {
            Primary = "#31C48D",
            Secondary = "#76A9FA",
        },
        Typography = new Typography
        {
            Default = new DefaultTypography { FontFamily = ["Cairo", "Segoe UI", "Tahoma", "sans-serif"] },
            H1 = new H1Typography { FontFamily = ["Cairo", "sans-serif"] },
            H4 = new H4Typography { FontFamily = ["Cairo", "sans-serif"], FontWeight = "700" },
            H5 = new H5Typography { FontFamily = ["Cairo", "sans-serif"], FontWeight = "700" },
            H6 = new H6Typography { FontFamily = ["Cairo", "sans-serif"], FontWeight = "700" },
            Button = new ButtonTypography { FontFamily = ["Cairo", "sans-serif"], TextTransform = "none" },
        },
        LayoutProperties = new LayoutProperties { DefaultBorderRadius = "10px" },
    };
}
