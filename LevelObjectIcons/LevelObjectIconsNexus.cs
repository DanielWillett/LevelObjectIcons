﻿using DanielWillett.LevelObjectIcons.Configuration;
using DanielWillett.UITools;
using HarmonyLib;
using Newtonsoft.Json;
using SDG.Framework.Modules;
using SDG.Unturned;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace DanielWillett.LevelObjectIcons;

/// <summary>
/// Module nexus for the Level Object Icons module.
/// </summary>
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

    /// <summary>
    /// Localization data for the current language.
    /// </summary>
    public static Local Localization = new Local(DefaultLocal);

    /// <summary>
    /// Configuration instance for <see cref="LevelObjectIconsNexus"/>.
    /// </summary>
    public static LevelObjectIconsConfig Config => _configObj?.Configuration ?? new LevelObjectIconsConfig();

    /// <summary>
    /// Patcher instance for LevelObjectIcons.
    /// </summary>
    public static Harmony Patcher { get; } = new Harmony("DanielWillett.LevelObjectIcons");

    /// <summary>
    /// Host game object which has the <see cref="IconGenerator"/> component attached.
    /// </summary>
    public static GameObject? GameObjectHost { get; private set; }

    /// <summary>
    /// Save <see cref="Config"/> to file.
    /// </summary>
    public static void SaveConfig()
    {
        ObjectIconPresets.DebugLogging = Config.EnableDebugLogging;

        CheckConfig();

        _configObj!.SaveConfig();
    }

    /// <summary>
    /// Read <see cref="Config"/> from file.
    /// </summary>
    public static void ReloadConfig()
    {
        CheckConfig();

        _configObj!.ReloadConfig();

        ObjectIconPresets.DebugLogging = Config.EnableDebugLogging;
    }

    /// <summary>
    /// Read <see cref="Localization"/> from file.
    /// </summary>
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
        UnityEngine.Object.DontDestroyOnLoad(GameObjectHost);

        ReloadConfig();
        ReloadTranslations();

        ObjectIconPresets.Init();

        GameObject objectItemGeneratorHost = new GameObject("ObjectIconGenerator", typeof(Light), typeof(IconGenerator), typeof(Camera));
        objectItemGeneratorHost.transform.SetParent(GameObjectHost.transform, true);
        objectItemGeneratorHost.hideFlags = HideFlags.DontSave;

        CommandWindow.Log("Done loading LevelObjectIcons.");

        ModuleHook.onModulesInitialized += OnModulesInit;
    }

    private void OnModulesInit()
    {
        ModuleHook.onModulesInitialized -= OnModulesInit;

        UnturnedUIToolsNexus.InitializeIfNotStandalone();
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