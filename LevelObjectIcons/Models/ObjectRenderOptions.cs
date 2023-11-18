using SDG.Unturned;

namespace DanielWillett.LevelObjectIcons.Models;

/// <summary>
/// Options for rendering an icon.
/// </summary>
public class ObjectRenderOptions
{
    /// <summary>
    /// Material index to use to generate the icon.
    /// </summary>
    public int MaterialIndexOverride { get; set; } = -1;

    /// <summary>
    /// Material palette to use to generate the icon.
    /// </summary>
    public AssetReference<MaterialPaletteAsset> MaterialPaletteOverride { get; set; } = AssetReference<MaterialPaletteAsset>.invalid;
}
