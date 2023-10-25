using System;
using DanielWillett.LevelObjectIcons.API;
using SDG.Unturned;
using UnityEngine;

namespace DanielWillett.LevelObjectIcons.DefaultProviders;
internal class FlagDefaultProvider : IDefaultIconProvider
{
    private static readonly Vector3 DefaultPosition = new Vector3(5.01f, 0.911f, 10.48f);
    private static readonly Quaternion DefaultRotation = Quaternion.Euler(351.16f, 270f, 281.73f);

    /// <inheritdoc/>
    public int Priority => int.MinValue;

    /// <inheritdoc/>
    public void GetMetrics(ObjectAsset @object, out Vector3 position, out Quaternion rotation)
    {
        position = DefaultPosition;
        rotation = DefaultRotation;
    }

    /// <inheritdoc/>
    public bool AppliesTo(ObjectAsset @object) => @object is
    {
        isSnowshoe: true,
        interactability: EObjectInteractability.RUBBLE,
        type: EObjectType.MEDIUM or EObjectType.LARGE or EObjectType.SMALL
    } && @object.name.StartsWith("Flag_", StringComparison.Ordinal);
}
