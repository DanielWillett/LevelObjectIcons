using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using UnityEngine;

namespace DanielWillett.LevelObjectIcons.Configuration;
public class LevelObjectIconsConfig
{
    [JsonProperty("$schema", DefaultValueHandling = DefaultValueHandling.Ignore)]
    public string SchemaURI => "https://raw.githubusercontent.com/DanielWillett/LevelObjectIcons/master/Schemas/level_object_icons_config_schema.json";

    [JsonProperty("edit_keybind")]
    [JsonConverter(typeof(StringEnumConverter))]
    public KeyCode EditKeybind { get; set; } = KeyCode.F8;

    [JsonProperty("log_mising_keybind")]
    [JsonConverter(typeof(StringEnumConverter))]
    public KeyCode LogMissingKeybind { get; set; } = KeyCode.Keypad5;

    [JsonProperty("cycle_material_palette")]
    public bool ShouldCycleMaterialPalette { get; set; } = true;

    [JsonProperty("debug_logging")]
    public bool EnableDebugLogging { get; set; }
}