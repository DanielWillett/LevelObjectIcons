using DanielWillett.LevelObjectIcons.Converters;
using Newtonsoft.Json;
using SDG.Unturned;
using System;
using UnityEngine;

namespace DanielWillett.LevelObjectIcons.Models;

/// <summary>
/// Represents an icon's transform, priority, and sometimes the file it was saved in.
/// </summary>
public class AssetIconPreset
{
    /// <summary>
    /// Read-only name to improve readability of the config file.
    /// </summary>
    [JsonProperty("name", NullValueHandling = NullValueHandling.Ignore)]
    public string? Name => Assets.find(Object)?.name;

    /// <summary>
    /// <see cref="Guid"/> of the object this icon is representing.
    /// </summary>
    [JsonProperty("object")]
    public Guid Object { get; set; }

    /// <summary>
    /// Position offset of the camera framing the icon.
    /// </summary>
    [JsonProperty("position")]
    [JsonConverter(typeof(Vector3Converter))]
    public Vector3 IconPosition { get; set; }

    /// <summary>
    /// Rotation offset of the camera framing the icon.
    /// </summary>
    [JsonProperty("rotation")]
    [JsonConverter(typeof(QuaternionEulerConverter))]
    public Quaternion IconRotation { get; set; }

    /// <summary>
    /// Priority of this icon over other icons.
    /// </summary>
    [JsonProperty("priority", DefaultValueHandling = DefaultValueHandling.Ignore)]
    public int Priority { get; set; }

    /// <summary>
    /// File source of this icon if available.
    /// </summary>
    [JsonIgnore]
    public string? File { get; set; }
}