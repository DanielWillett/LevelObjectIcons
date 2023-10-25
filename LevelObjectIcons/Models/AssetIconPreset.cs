using DanielWillett.LevelObjectIcons.Converters;
using Newtonsoft.Json;
using SDG.Unturned;
using System;
using UnityEngine;

namespace DanielWillett.LevelObjectIcons.Models;
public class AssetIconPreset
{
    [JsonProperty("name", NullValueHandling = NullValueHandling.Ignore)]
    public string? Name => Assets.find(Object)?.name;

    [JsonProperty("object")]
    public Guid Object { get; set; }

    [JsonProperty("position")]
    [JsonConverter(typeof(Vector3Converter))]
    public Vector3 IconPosition { get; set; }

    [JsonProperty("rotation")]
    [JsonConverter(typeof(QuaternionEulerConverter))]
    public Quaternion IconRotation { get; set; }

    [JsonProperty("priority", DefaultValueHandling = DefaultValueHandling.Ignore)]
    public int Priority { get; set; }

    [JsonIgnore]
    public string? File { get; set; }
}