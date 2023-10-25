using DanielWillett.LevelObjectIcons.Configuration;
using HarmonyLib;
using SDG.Framework.Modules;
using SDG.Unturned;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using UnityEngine;

namespace DanielWillett.LevelObjectIcons;

public sealed class LevelObjectIconsNexus : IModuleNexus
{
    private static JsonConfigurationFile<LevelObjectIconsConfig>? _configObj;

    /// <summary>
    /// Selected object or item asset (the one that will get placed when you press [E]).
    /// </summary>
    /// <remarks>Will be an <see cref="ItemBarricadeAsset"/>, <see cref="ItemStructureAsset"/>, <see cref="ObjectAsset"/>, or <see langword="null"/>.</remarks>
    public static Asset? SelectedAsset => EditorObjects.isBuilding ? (Asset?)EditorObjects.selectedObjectAsset ?? EditorObjects.selectedItemAsset : null;

    private static readonly DatDictionary DefaultLocal = new DatDictionary
    {
        { "NoAssetSelected",                   new DatValue("No Asset Selected") },
        { "ObjectIconEditorToggleHint",        new DatValue("[{0}] to edit"    ) },
        { "ObjectIconEditorToggle",            new DatValue("Live Editor"      ) },
        { "ObjectIconEditorSave",              new DatValue("Save"             ) },
        { "ObjectIconEditorSaveNew",           new DatValue("Save New"         ) },
        { "ObjectIconEditorOffsetAssetHint",   new DatValue("Goto offset"      ) },
        { "ObjectIconEditorOffsetAssetButton", new DatValue("Go"               ) }
    };

    public static Local Localization = new Local(DefaultLocal);
    public static LevelObjectIconsConfig Config => _configObj?.Configuration ?? new LevelObjectIconsConfig();
    public static Harmony Patcher { get; } = new Harmony("DanielWillett.LevelObjectIcons");
    public static GameObject? GameObjectHost { get; private set; }

    public static void SaveConfig()
    {
        CheckConfig();

        _configObj!.SaveConfig();
    }
    public static void ReloadConfig()
    {
        CheckConfig();

        _configObj!.ReloadConfig();
    }

    public static void ReloadTranslations()
    {
        try
        {
            string local = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "Localization");

            if (File.Exists(Path.Combine(local, "English.dat")) || !string.Equals(Provider.language, "English", StringComparison.Ordinal) &&
                                                                    File.Exists(Path.Combine(local, Provider.language + ".dat")))
            {
                Localization = SDG.Unturned.Localization.tryRead(local, false) ?? new Local(DefaultLocal);
            }
            else
            {
                Directory.CreateDirectory(local);
                using TextWriter writer = new StreamWriter(Path.Combine(local, "English.dat"), false);

                foreach (KeyValuePair<string, IDatNode> value in DefaultLocal)
                {
                    if (value.Value is not DatValue str)
                        continue;

                    writer.WriteLine(value.Key + " " + str.value);
                }

                writer.Flush();
            }
        }
        catch (Exception ex)
        {
            CommandWindow.LogError("Error reloading translations for LevelObjectIcons.");
            CommandWindow.LogError(ex);
        }
    }
    private static void CheckConfig()
    {
        if (_configObj == null)
        {
            string config = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "level_object_icons_config.json");

            _configObj = new JsonConfigurationFile<LevelObjectIconsConfig>(config)
            {
                SerializerOptions = new JsonSerializerSettings
                {
                    Formatting = Formatting.Indented,
                    Culture = CultureInfo.InvariantCulture
                }
            };
        }
    }
    void IModuleNexus.initialize()
    {
        CommandWindow.Log("Loading LevelObjectIcons module by DanielWillett - https://github.com/DanielWillett/LevelObjectIcons");

        GameObjectHost = new GameObject("LevelObjectIcons")
        {
            hideFlags = HideFlags.DontSave
        };

        ReloadConfig();
        ReloadTranslations();

#if DEBUG
        ObjectIconPresets.DebugLogging = true;
#endif

        ObjectIconPresets.Init();

        GameObject objectItemGeneratorHost = new GameObject("ObjectIconGenerator", typeof(Light), typeof(IconGenerator), typeof(Camera));
        objectItemGeneratorHost.transform.SetParent(GameObjectHost.transform, true);
        objectItemGeneratorHost.hideFlags = HideFlags.DontSave;

        CommandWindow.Log("Done loading LevelObjectIcons.");
    }
    void IModuleNexus.shutdown()
    {
        CommandWindow.Log("Unloading LevelObjectIcons module...");

        if (GameObjectHost != null)
        {
            UnityEngine.Object.Destroy(GameObjectHost);
            GameObjectHost = null;
        }

        ObjectIconPresets.Deinit();
    }
}