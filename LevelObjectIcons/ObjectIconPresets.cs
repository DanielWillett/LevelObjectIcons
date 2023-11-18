using DanielWillett.LevelObjectIcons.API;
using DanielWillett.LevelObjectIcons.Configuration;
using DanielWillett.LevelObjectIcons.Models;
using DanielWillett.ReflectionTools;
using Newtonsoft.Json;
using SDG.Framework.Modules;
using SDG.Unturned;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Module = SDG.Framework.Modules.Module;

namespace DanielWillett.LevelObjectIcons;

/// <summary>
/// Storage provider for object icon presets.
/// </summary>
public static class ObjectIconPresets
{
    private static readonly List<JsonConfigurationFile<List<AssetIconPreset>>> _presetProviders = new List<JsonConfigurationFile<List<AssetIconPreset>>>(1);
    private static JsonConfigurationFile<List<AssetIconPreset>>? _customPresets;
    private static readonly string _customPresetsPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "configured_icons.json");
    private static readonly Dictionary<Guid, AssetIconPreset> _presetsIntl = new Dictionary<Guid, AssetIconPreset>(128);

    private static readonly List<AssetIconPreset> DefaultPresets = new List<AssetIconPreset>(128);

    /// <summary>
    /// The default rotation an object is placed at.
    /// </summary>
    public static readonly Quaternion DefaultObjectRotation = Quaternion.Euler(-90f, 0.0f, 0.0f);

    /// <summary>
    /// The current asset icon being edited.
    /// </summary>
    public static AssetIconPreset? ActivelyEditing { get; private set; }

    /// <summary>
    /// Static switch for debug logging. Can be overwritten by config.
    /// </summary>
    public static bool DebugLogging { get; set; }

    /// <summary>
    /// A map of object GUIDs to the highest priority <see cref="AssetIconPreset"/>s.
    /// </summary>
    public static IReadOnlyDictionary<Guid, AssetIconPreset> ActivePresets { get; } = new ReadOnlyDictionary<Guid, AssetIconPreset>(_presetsIntl);

    private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
    {
        Formatting = Formatting.Indented,
        Culture = CultureInfo.InvariantCulture
    };

    internal static void Init()
    {
        Level.onPostLevelLoaded += OnLevelLoaded;
    }
    internal static void Deinit()
    {
        Level.onPostLevelLoaded += OnLevelLoaded;
    }
    internal static void UpdateEditCache(LevelObject levelObject, ObjectAsset asset)
    {
        Transform? ctrl = MainCamera.instance.transform;
        if (ctrl == null)
            return;

        Transform? transform = levelObject.transform ?? levelObject.skybox ?? levelObject.placeholderTransform;
        if (transform == null)
            return;

        if (ActivelyEditing == null || ActivelyEditing.Object != asset.GUID)
        {
            ActivelyEditing = new AssetIconPreset
            {
                Object = asset.GUID,
                File = null
            };
        }

        ActivelyEditing.IconPosition = transform.InverseTransformPoint(ctrl.transform.position);
        ActivelyEditing.IconRotation = transform.InverseTransformRotation(ctrl.transform.rotation);

        IconGenerator.ClearCache(asset.GUID);
    }
    internal static void SaveEditCache(bool asNew)
    {
        AssetIconPreset? preset = ActivelyEditing;
        if (preset == null)
            return;
        Guid guid = preset.Object;

        asNewRedo:
        if (asNew)
        {
            AssetIconPreset? existing = null;
            bool contains = false;
            if (_customPresets != null)
            {
                existing = _customPresets.Configuration.Where(x => x.Object == guid).OrderByDescending(x => x.Priority).FirstOrDefault();
                contains = existing != null;
            }
            else _presetProviders.Add(_customPresets = new JsonConfigurationFile<List<AssetIconPreset>>(_customPresetsPath)
            {
                SerializerOptions = JsonSettings
            });
            
            existing ??= new AssetIconPreset
            {
                Object = guid,
                File = _customPresets.File
            };

            int priority = -1;
            foreach (AssetIconPreset p in _presetProviders.SelectMany(x => x.Configuration).Where(x => x.Object == guid && x != existing))
            {
                if (priority < p.Priority)
                    priority = p.Priority;
            }

            ++priority;

            existing.Priority = priority;
            existing.IconPosition = preset.IconPosition;
            existing.IconRotation = preset.IconRotation;
            if (!contains)
                _customPresets.Configuration.Add(existing);
            _customPresets.SaveConfig();
            _presetsIntl[guid] = existing;

            CommandWindow.Log($"Updated asset icon preset: {preset.Object}, saved to {_customPresets.File}.");
        }
        else
        {
            KeyValuePair<JsonConfigurationFile<List<AssetIconPreset>>, AssetIconPreset?> kvp = _presetProviders
                .Where(x => !x.ReadOnlyReloading)
                .SelectMany(x => x.Configuration.Select(y => new KeyValuePair<JsonConfigurationFile<List<AssetIconPreset>>, AssetIconPreset>(x, y)))
                .Where(x => x.Value.Object == guid)
                .OrderByDescending(x => x.Value.Priority)
                .FirstOrDefault()!;

            if (kvp.Value == null)
            {
                asNew = true;
                goto asNewRedo;
            }

            int priority = -1;
            foreach (AssetIconPreset p in _presetProviders.SelectMany(x => x.Configuration).Where(x => x.Object == guid && x != kvp.Value))
            {
                if (priority < p.Priority)
                    priority = p.Priority;
            }

            ++priority;
            kvp.Value.Priority = priority;
            kvp.Value.IconPosition = preset.IconPosition;
            kvp.Value.IconRotation = preset.IconRotation;
            kvp.Key.SaveConfig();
            _presetsIntl[guid] = kvp.Value;
            CommandWindow.Log($"Updated asset icon preset: {preset.Object}, saved to {kvp.Key.File}.");
        }

        ClearEditCache();
    }
    internal static void ClearEditCache()
    {
        if (ActivelyEditing == null)
            return;
        
        IconGenerator.ClearCache(ActivelyEditing.Object);
        ActivelyEditing = null;
    }
    private static void OnLevelLoaded(int level)
    {
        if (level != Level.BUILD_INDEX_GAME)
            return;

        ReloadPresetProviders();
    }

    internal static void ReloadPresetProviders()
    {
        ThreadUtil.assertIsGameThread();

        foreach (JsonConfigurationFile<List<AssetIconPreset>> config in _presetProviders)
            config.OnRead -= OnConfigReloaded;
        _presetProviders.Clear();
        if (DebugLogging)
            CommandWindow.Log($"[{IconGenerator.Source}] Searching for object icon preset provider JSON files.");
        string dir;
        if (Provider.provider?.workshopService?.ugc != null)
        {
            foreach (SteamContent content in Provider.provider.workshopService.ugc)
            {
                dir = content.type == ESteamUGCType.MAP ? Path.Combine(content.path, "Bundles") : content.path;
                if (!Directory.Exists(dir))
                    continue;
                DiscoverAssetIconPresetProvidersIn(dir, true);
                foreach (string directory in Directory.EnumerateDirectories(dir, "*", SearchOption.AllDirectories))
                    DiscoverAssetIconPresetProvidersIn(directory, true);
            }
        }

        int workshop = _presetProviders.Count;

        if (Level.info != null && Level.info.path != null)
        {
            dir = Path.Combine(Level.info.path, "Bundles");
            if (Directory.Exists(dir))
            {
                DiscoverAssetIconPresetProvidersIn(dir, true);
                foreach (string directory in Directory.EnumerateDirectories(dir, "*", SearchOption.AllDirectories))
                    DiscoverAssetIconPresetProvidersIn(directory, true);
            }
        }

        dir = Path.Combine(ReadWrite.PATH, "Sandbox");
        if (Directory.Exists(dir))
        {
            DiscoverAssetIconPresetProvidersIn(dir, false);

            foreach (string directory in Directory.EnumerateDirectories(dir, "*", SearchOption.AllDirectories))
                DiscoverAssetIconPresetProvidersIn(directory, false);
        }

        DiscoverAssetIconPresetProvidersIn(Path.Combine(ReadWrite.PATH, "Bundles"), true);

        dir = Path.Combine(ReadWrite.PATH, "Bundles", "Objects");
        if (Directory.Exists(dir))
        {
            DiscoverAssetIconPresetProvidersIn(dir, true);
            foreach (string directory in Directory.EnumerateDirectories(dir, "*", SearchOption.AllDirectories))
                DiscoverAssetIconPresetProvidersIn(directory, true);
        }

        if (ModuleHook.modules != null)
        {
            foreach (Module module in ModuleHook.modules)
            {
                if (!module.isEnabled)
                    continue;

                DiscoverAssetIconPresetProvidersIn(module.config.DirectoryPath, true);
                foreach (string directory in Directory.EnumerateDirectories(module.config.DirectoryPath, "*", SearchOption.AllDirectories))
                    DiscoverAssetIconPresetProvidersIn(directory, true);
            }

        }
        if (!_presetProviders.Exists(x => x.File.Equals(_customPresetsPath, StringComparison.Ordinal)))
        {
            string path = Path.GetFullPath(_customPresetsPath);
            _presetProviders.Add(_customPresets = new JsonConfigurationFile<List<AssetIconPreset>>(path)
            {
                SerializerOptions = JsonSettings
            });

            if (DebugLogging)
                CommandWindow.Log($"[{IconGenerator.Source}] + Registered working icon provider {path}.");
        }

        _presetsIntl.Clear();

        ApplyDefaultProviders();

        int ct = DefaultPresets.Count;

        foreach (AssetIconPreset preset in DefaultPresets)
        {
            preset.File = null;
            _presetsIntl[preset.Object] = preset;
        }

        for (int i = _presetProviders.Count - 1; i >= 0; i--)
        {
            JsonConfigurationFile<List<AssetIconPreset>> configFile = _presetProviders[i];
            configFile.ReloadConfig();
            if (configFile.Configuration is not { Count: > 0 })
            {
                _presetProviders.RemoveAt(i);
                continue;
            }
            configFile.Configuration.Sort((a, b) => b.Priority.CompareTo(a.Priority));
            configFile.OnRead += OnConfigReloaded;
            ct += configFile.Configuration.Count;
            foreach (AssetIconPreset preset in configFile.Configuration)
            {
                if (Assets.find(preset.Object) == null)
                {
                    if (i >= workshop)
                        CommandWindow.LogWarning($"[{IconGenerator.Source}] {(DebugLogging ? "+ " : string.Empty)}Object not found for icon preset: {preset.Object} in {configFile.File}.");
                    else if (DebugLogging)
                        CommandWindow.Log($"[{IconGenerator.Source}] + Object not found for workshop icon preset: {preset.Object} in {configFile.File}.");
                    continue;
                }
                preset.File = configFile.File;
                if (!_presetsIntl.TryGetValue(preset.Object, out AssetIconPreset existing) || existing.Priority < preset.Priority)
                    _presetsIntl[preset.Object] = preset;
            }
        }

        CommandWindow.Log($"[{IconGenerator.Source}] {(DebugLogging ? "+ " : string.Empty)}Registered {_presetsIntl.Count} unique icon presets from {ct} presets.");

        GC.Collect();
    }
    private static void OnConfigReloaded()
    {
        IconGenerator.ClearCache();
    }
    private static void DiscoverAssetIconPresetProvidersIn(string path, bool isReadonly)
    {
        if (!Directory.Exists(path))
            return;

        foreach (string file in Directory.EnumerateFiles(path, "*.json", SearchOption.TopDirectoryOnly))
        {
            string name = Path.GetFileNameWithoutExtension(file);
            if ((name.StartsWith("object icons", StringComparison.InvariantCultureIgnoreCase)
                || name.StartsWith("object_icons", StringComparison.InvariantCultureIgnoreCase)
                || name.StartsWith("object icon presets", StringComparison.InvariantCultureIgnoreCase)
                || name.StartsWith("object_icon_presets", StringComparison.InvariantCultureIgnoreCase)
                || name.StartsWith("object presets", StringComparison.InvariantCultureIgnoreCase)
                || name.StartsWith("object_presets", StringComparison.InvariantCultureIgnoreCase)
                ) && !_presetProviders.Exists(x => x.File.Equals(file, StringComparison.Ordinal)))
            {
                if (DebugLogging)
                    CommandWindow.Log($"[{IconGenerator.Source}] + Registered icon provider {file}.");
                _presetProviders.Add(new JsonConfigurationFile<List<AssetIconPreset>>(Path.GetFullPath(file))
                {
                    ReadOnlyReloading = isReadonly,
                    SerializerOptions = JsonSettings
                });
            }
        }
    }
    private static void ApplyDefaultProviders()
    {
        IEnumerable<Type> types;

        if (LevelObjectIconsNexus.Config.DisableDefaultProviderSearch)
            types = Accessor.GetTypesSafe(true);
        else
            types = Accessor.GetTypesSafe(ModuleHook.modules.Where(x => x.assemblies != null).SelectMany(x => x.assemblies));
        
        List<IDefaultIconProvider> providers = new List<IDefaultIconProvider>(2);
        foreach (Type type in types.Where(x => x is { IsInterface: false, IsAbstract: false }))
        {
            try
            {
                if (!typeof(IDefaultIconProvider).IsAssignableFrom(type))
                    continue;
            }
            catch (Exception ex)
            {
                if (DebugLogging)
                    CommandWindow.Log($"[{IconGenerator.Source}] - Failed to check type: {type.FullName} ({ex.GetType().Name} - {ex.Message}).");
                continue;
            }

            try
            {
                IDefaultIconProvider provider = (IDefaultIconProvider)Activator.CreateInstance(type);
                providers.Add(provider);
            }
            catch (Exception ex)
            {
                CommandWindow.LogError($"[{IconGenerator.Source}] + Unable to apply icon provider: {type.FullName}.");
                CommandWindow.LogError(ex);
            }
        }

        providers.Sort((a, b) => b.Priority.CompareTo(a.Priority));

        if (DebugLogging)
        {
            foreach (IDefaultIconProvider provider in providers)
            {
                CommandWindow.Log($"[{IconGenerator.Source}] + Registered default icon provider: {provider.GetType().Name} (Priority: {provider.Priority}).");
            }
        }

        List<ObjectAsset> objects = new List<ObjectAsset>(4096);
        Assets.find(objects);

        foreach (ObjectAsset obj in objects)
        {
            if (DefaultPresets.Exists(x => x.Object == obj.GUID))
                continue;

            IDefaultIconProvider? provider = providers.Find(x => x.AppliesTo(obj));

            if (provider == null)
                continue;

            provider.GetMetrics(obj, out Vector3 position, out Quaternion rotation);

            DefaultPresets.Add(new AssetIconPreset
            {
                Object = obj.GUID,
                Priority = int.MinValue,
                IconPosition = position,
                IconRotation = rotation
            });
        }
    }
}