using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using UnityEngine;

namespace DanielWillett.LevelObjectIcons.Configuration;

/// <summary>
/// Configuration for <see cref="LevelObjectIconsNexus"/>.
/// </summary>
public class LevelObjectIconsConfig
{
    /// <summary>
    /// URL to the config schema.
    /// </summary>
    [JsonProperty("$schema", DefaultValueHandling = DefaultValueHandling.Ignore)]
    public string SchemaURI => "https://raw.githubusercontent.com/DanielWillett/LevelObjectIcons/master/Schemas/level_object_icons_config_schema.json";

    /// <summary>
    /// Key used to toggle the Live Editor checkbox.
    /// </summary>
    [JsonProperty("edit_keybind")]
    [JsonConverter(typeof(StringEnumConverter))]
    public KeyCode EditKeybind { get; set; } = KeyCode.F8;

    /// <summary>
    /// Key used to print all objects that don't have an offset to Client.log.
    /// </summary>
    [JsonProperty("log_mising_keybind")]
    [JsonConverter(typeof(StringEnumConverter))]
    public KeyCode LogMissingKeybind { get; set; } = KeyCode.Keypad5;

    /// <summary>
    /// Enables cycling between materials in the material palette. May cause some lag on lower end machines.
    /// </summary>
    [JsonProperty("cycle_material_palette")]
    public bool ShouldCycleMaterialPalette { get; set; } = true;

    /// <summary>
    /// Enables extra logging to see what files are being used, etc.
    /// </summary>
    [JsonProperty("debug_logging")]
    public bool EnableDebugLogging { get; set; }

    /// <summary>
    /// Disables searching other modules for default icon providers. Set this to true if errors arise from ApplyDefaultProviders.
    /// </summary>
    [JsonProperty("disable_default_provider_search")]
    public bool DisableDefaultProviderSearch { get; set; }
}