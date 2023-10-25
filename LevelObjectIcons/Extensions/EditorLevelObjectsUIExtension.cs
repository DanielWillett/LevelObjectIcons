using DanielWillett.LevelObjectIcons.Models;
using DanielWillett.ReflectionTools;
using DanielWillett.UITools;
using DanielWillett.UITools.API.Extensions;
using DanielWillett.UITools.API.Extensions.Members;
using HarmonyLib;
using SDG.Framework.Utilities;
using SDG.Unturned;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Random = UnityEngine.Random;

namespace DanielWillett.LevelObjectIcons.Extensions;

[UIExtension(typeof(EditorLevelObjectsUI))]
internal class EditorLevelObjectsUIExtension : UIExtension, IDisposable
{
    private static List<EditorSelection>? _selections;
    private static List<EditorSelection> EditorObjectSelection => _selections ??=
        (List<EditorSelection>?)typeof(EditorObjects).GetField("selection", BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null)
        ?? throw new MemberAccessException("Unable to find field: EditorObjects.selection.");

    private static readonly InstanceGetter<Asset, AssetOrigin>? GetAssetOrigin = Accessor.GenerateInstanceGetter<Asset, AssetOrigin>("origin");
    private static readonly StaticSetter<float>? SetPitch = Accessor.GenerateStaticSetter<EditorLook, float>("_pitch");
    private static readonly StaticSetter<float>? SetYaw = Accessor.GenerateStaticSetter<EditorLook, float>("_yaw");

    private const int Size = 158;
    private static bool _patched;
#nullable disable
    [ExistingMember("container")]
    private readonly SleekFullscreenBox _container;

    [ExistingMember("assetsScrollBox")]
    private readonly SleekList<Asset> _assetsScrollBox;

    [ExistingMember("selectedBox")]
    private ISleekBox SelectedBox { get; }

    private readonly ISleekBox _displayTitle;
    private readonly ISleekImage _preview;
    private bool _isGeneratingIcon;
    private bool _editorActive;
    private bool _subbed;
    private int _materialIndex;
    private int _materialTtl;
    private float _nextIcon;
    private float _lastUpdate;
    
    private readonly ISleekToggle _isEditingToggle;
    private readonly ISleekButton _saveEditButton;
    private readonly ISleekButton _saveNewEditButton;
    private readonly ISleekButton _gotoOffsetButton;
    private readonly ISleekField _offsetField;
    private readonly ISleekLabel _editHint;
    private readonly ISleekLabel _materialIndexLbl;
    private readonly ISleekLabel _noteText;

    /// <summary>
    /// If the live editor is enabled.
    /// </summary>
    public bool EditorActive
    {
        get => _editorActive;
        private set
        {
            _editorActive = value;
            if (!value)
            {
                OnToggled(_isEditingToggle, false);
                _isEditingToggle.Value = false;
            }

            _isEditingToggle.IsVisible = value;
        }
    }

    internal EditorLevelObjectsUIExtension()
    {
        ISleekBox displayBox = Glazier.Get().CreateBox();
        displayBox.PositionScale_X = 1f;
        displayBox.PositionScale_Y = 1f;
        displayBox.PositionOffset_X = _assetsScrollBox.PositionOffset_X - (Size + 30);
        displayBox.PositionOffset_Y = -Size - 20;
        displayBox.SizeOffset_X = Size + 20;
        displayBox.SizeOffset_Y = Size + 20;
        _container.AddChild(displayBox);

        _displayTitle = Glazier.Get().CreateBox();
        _displayTitle.PositionScale_X = 1f;
        _displayTitle.PositionScale_Y = 1f;
        _displayTitle.PositionOffset_X = _assetsScrollBox.PositionOffset_X - (Size + 30);
        _displayTitle.PositionOffset_Y = -Size - 60;
        _displayTitle.SizeOffset_X = Size + 20;
        _displayTitle.SizeOffset_Y = 30;
        _displayTitle.Text = LevelObjectIconsNexus.Localization.format("NoAssetSelected");

        _container.AddChild(_displayTitle);

        _preview = Glazier.Get().CreateImage();
        _preview.SizeScale_X = 1f;
        _preview.SizeScale_Y = 1f;
        _preview.SizeOffset_X = -20;
        _preview.SizeOffset_Y = -20;
        _preview.PositionOffset_X = 10;
        _preview.PositionOffset_Y = 10;
        _preview.ShouldDestroyTexture = true;
        displayBox.AddChild(_preview);

        _editHint = Glazier.Get().CreateLabel();
        _editHint.TextContrastContext = ETextContrastContext.ColorfulBackdrop;
        _editHint.Text = LevelObjectIconsNexus.Localization.format("ObjectIconEditorToggleHint", MenuConfigurationControlsUI.getKeyCodeText(LevelObjectIconsNexus.Config.EditKeybind));
        _editHint.PositionScale_X = 1f;
        _editHint.PositionScale_Y = 1f;
        _editHint.PositionOffset_X = _assetsScrollBox.PositionOffset_X - (Size + 30);
        _editHint.PositionOffset_Y = -20;
        _editHint.TextAlignment = TextAnchor.MiddleCenter;
        _editHint.TextColor = new SleekColor(ESleekTint.FOREGROUND);
        _editHint.SizeOffset_X = Size + 20;
        _editHint.SizeOffset_Y = 20;

        _container.AddChild(_editHint);

        _materialIndexLbl = Glazier.Get().CreateLabel();
        _materialIndexLbl.TextContrastContext = ETextContrastContext.ColorfulBackdrop;
        _materialIndexLbl.PositionScale_X = 1f;
        _materialIndexLbl.PositionScale_Y = 1f;
        _materialIndexLbl.PositionOffset_X = _assetsScrollBox.PositionOffset_X - (Size + 20);
        _materialIndexLbl.PositionOffset_Y = -Size - 10;
        _materialIndexLbl.TextAlignment = TextAnchor.MiddleLeft;
        _materialIndexLbl.TextColor = new SleekColor(ESleekTint.FOREGROUND);
        _materialIndexLbl.SizeOffset_X = Size + 20;
        _materialIndexLbl.SizeOffset_Y = 20;
        _materialIndexLbl.IsVisible = true;
        _materialIndexLbl.Text = string.Empty;

        _container.AddChild(_materialIndexLbl);

        _editorActive = false;

        _isEditingToggle = Glazier.Get().CreateToggle();
        _isEditingToggle.PositionScale_X = 1f;
        _isEditingToggle.PositionScale_Y = 1f;
        _isEditingToggle.PositionOffset_X = _displayTitle.PositionOffset_X - 30;
        _isEditingToggle.PositionOffset_Y = -Size - 55;
        _isEditingToggle.SizeOffset_X = 20;
        _isEditingToggle.SizeOffset_Y = 20;
        _isEditingToggle.AddLabel(LevelObjectIconsNexus.Localization.format("ObjectIconEditorToggle"), new SleekColor(ESleekTint.FOREGROUND).Get(), ESleekSide.LEFT);
        _isEditingToggle.SideLabel.TextContrastContext = ETextContrastContext.ColorfulBackdrop;
        _isEditingToggle.IsVisible = false;
        _isEditingToggle.OnValueChanged += OnToggled;
        _container.AddChild(_isEditingToggle);

        _saveEditButton = Glazier.Get().CreateButton();
        _saveEditButton.PositionScale_X = 1f;
        _saveEditButton.PositionScale_Y = 1f;
        _saveEditButton.PositionOffset_X = _isEditingToggle.PositionOffset_X - Size + 10;
        _saveEditButton.PositionOffset_Y = -Size - 25;
        _saveEditButton.SizeOffset_X = Size / 2;
        _saveEditButton.SizeOffset_Y = 30;
        _saveEditButton.Text = LevelObjectIconsNexus.Localization.format("ObjectIconEditorSave");
        _saveEditButton.OnClicked += OnSaveEdit;
        _saveEditButton.IsVisible = false;
        _container.AddChild(_saveEditButton);

        _saveNewEditButton = Glazier.Get().CreateButton();
        _saveNewEditButton.PositionScale_X = 1f;
        _saveNewEditButton.PositionScale_Y = 1f;
        _saveNewEditButton.PositionOffset_X = _isEditingToggle.PositionOffset_X + (Size + 30) / 2 - Size;
        _saveNewEditButton.PositionOffset_Y = -Size - 25;
        _saveNewEditButton.SizeOffset_X = Size / 2;
        _saveNewEditButton.SizeOffset_Y = 30;
        _saveNewEditButton.Text = LevelObjectIconsNexus.Localization.format("ObjectIconEditorSaveNew");
        _saveNewEditButton.OnClicked += OnSaveNewEdit;
        _saveNewEditButton.IsVisible = false;
        _container.AddChild(_saveNewEditButton);

        _offsetField = Glazier.Get().CreateStringField();
        _offsetField.PositionScale_X = 1f;
        _offsetField.PositionScale_Y = 1f;
        _offsetField.PositionOffset_X = _saveEditButton.PositionOffset_X;
        _offsetField.PositionOffset_Y = _saveEditButton.PositionOffset_Y + 40;
        _offsetField.SizeOffset_X = 3 * Size / 4 - 5;
        _offsetField.SizeOffset_Y = 30;
        _offsetField.TooltipText = LevelObjectIconsNexus.Localization.format("ObjectIconEditorOffsetAssetHint");
        _offsetField.IsVisible = false;
        _container.AddChild(_offsetField);

        _gotoOffsetButton = Glazier.Get().CreateButton();
        _gotoOffsetButton.PositionScale_X = 1f;
        _gotoOffsetButton.PositionScale_Y = 1f;
        _gotoOffsetButton.PositionOffset_X = _offsetField.PositionOffset_X + _offsetField.SizeOffset_X + 10;
        _gotoOffsetButton.PositionOffset_Y = _offsetField.PositionOffset_Y;
        _gotoOffsetButton.SizeOffset_X = Size / 4;
        _gotoOffsetButton.SizeOffset_Y = 30;
        _gotoOffsetButton.Text = LevelObjectIconsNexus.Localization.format("ObjectIconEditorOffsetAssetButton");
        _gotoOffsetButton.IsVisible = false;
        _gotoOffsetButton.OnClicked += OnClickedGotoAsset;
        _gotoOffsetButton.OnRightClicked += OnRightClickedGotoAsset;
        _container.AddChild(_gotoOffsetButton);

        _noteText = Glazier.Get().CreateLabel();
        _noteText.AllowRichText = true;
        _noteText.TextColor = ESleekTint.FOREGROUND;
        _noteText.FontSize = ESleekFontSize.Small;
        _noteText.TextAlignment = TextAnchor.LowerCenter;
        _noteText.TextContrastContext = ETextContrastContext.ColorfulBackdrop;
        _noteText.Text = string.Empty;
        _noteText.IsVisible = false;
        _noteText.PositionScale_X = 1f;
        _noteText.PositionScale_Y = 0f;
        _noteText.SizeScale_Y = 1f;
        _noteText.SizeOffset_X = Size * 3f + 90f;
        _noteText.SizeOffset_Y = -(Size + 160);
        _noteText.PositionOffset_X = displayBox.PositionOffset_X - _noteText.SizeOffset_X + displayBox.SizeOffset_X;
        _noteText.PositionOffset_Y = 90f;
        _container.AddChild(_noteText);

        UpdateSelectedObject(true);
        if (!_patched)
            Patch();
    }
#nullable restore

    protected override void Opened()
    {
        if (!_subbed)
        {
            TimeUtility.updated += OnUpdate;
            _subbed = true;
        }
    }

    protected override void Closed()
    {
        if (_subbed)
        {
            TimeUtility.updated -= OnUpdate;
            _subbed = false;
        }
    }

    void IDisposable.Dispose()
    {
        Closed();
    }

    /// <summary>
    /// Call this after manually updating the selected object or buildable type.
    /// </summary>
    public static void UpdateSelection(ObjectAsset? levelObject, ItemAsset? buildable)
    {
        if (levelObject == null && buildable == null)
            ObjectIconPresets.ClearEditCache();
        EditorLevelObjectsUIExtension? inst = UnturnedUIToolsNexus.UIExtensionManager.GetInstance<EditorLevelObjectsUIExtension>();
        if (inst == null)
            return;
        try
        {
            ISleekBox? box = inst.SelectedBox;
            if (box == null)
                return;

            if (levelObject == null && buildable is not ItemBarricadeAsset and not ItemStructureAsset)
                box.Text = string.Empty;
            else if (levelObject != null)
                box.Text = levelObject.FriendlyName;
            else
                box.Text = buildable!.FriendlyName;
            
            inst._materialIndex = -1;
            inst.UpdateSelectedObject(true);
        }
        catch (Exception ex)
        {
            CommandWindow.LogError($"[{IconGenerator.Source}] Error updating selection.");
            CommandWindow.LogError(ex);
        }
    }

    private void OnRightClickedGotoAsset(ISleekElement button)
    {
        _offsetField.Text = LevelObjectIconsNexus.SelectedAsset is not ObjectAsset asset ? string.Empty : asset.GUID.ToString("N");
    }
    private void OnClickedGotoAsset(ISleekElement button)
    {
        if (!Guid.TryParse(_offsetField.Text, out Guid guid) || Assets.find(guid) is not ObjectAsset asset)
        {
            _offsetField.Text = string.Empty;
            return;
        }
        
        if (LevelObjectIconsNexus.SelectedAsset is not ObjectAsset selectedAsset)
            return;
        
        List<EditorSelection> selections = EditorObjectSelection;
        LevelObject? target = null;
        foreach (EditorSelection selection in selections)
        {
            LevelObject? levelObject = null;
            for (int x = 0; x < Regions.WORLD_SIZE; ++x)
            {
                for (int y = 0; y < Regions.WORLD_SIZE; ++y)
                {
                    List<LevelObject> objRegion = LevelObjects.objects[x, y];
                    int ct = objRegion.Count;
                    for (int i = 0; i < ct; ++i)
                    {
                        if (ReferenceEquals(objRegion[i].transform, selection.transform))
                        {
                            levelObject = objRegion[i];
                            goto found;
                        }
                    }
                }
            }

            found:
            if (levelObject == null || levelObject.GUID != selectedAsset.GUID)
                continue;

            if (target == null)
                target = levelObject;
            else
            {
                target = null;
                break;
            }
        }

        if (target == null)
        {
            CommandWindow.LogWarning($"Tried to goto asset {selectedAsset.FriendlyName} but not selected.");
            return;
        }

        IconGenerator.ObjectIconMetrics metrics = IconGenerator.GetObjectIconMetrics(asset);
        IconGenerator.GetCameraPositionAndRotation(in metrics, target.transform, out Vector3 position, out Quaternion rotation);

        Vector3 euler = rotation.eulerAngles;
        Editor.editor.gameObject.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, euler.y, 0f));
        MainCamera.instance.transform.localRotation = Quaternion.Euler(euler.x, 0f, 0f);

        SetYaw?.Invoke(euler.y);
        SetPitch?.Invoke(Mathf.Clamp(euler.x, -90f, 90f));
    }

    private void OnToggled(ISleekToggle toggle, bool state)
    {
        if (!_editorActive)
            state = false;

        _saveNewEditButton.IsVisible = state;
        _saveEditButton.IsVisible = state;
        _offsetField.IsVisible = state;
        _gotoOffsetButton.IsVisible = state;
        _editHint.IsVisible = !state;
    }
    private void OnSaveEdit(ISleekElement button)
    {
        Asset? asset = LevelObjectIconsNexus.SelectedAsset;
        if (asset == null)
            return;
        ObjectIconPresets.SaveEditCache(false);
        UpdateSelectedObject(false);
    }
    private void OnSaveNewEdit(ISleekElement button)
    {
        Asset? asset = LevelObjectIconsNexus.SelectedAsset;
        if (asset == null)
            return;
        ObjectIconPresets.SaveEditCache(true);
        UpdateSelectedObject(false);
    }
    private static void OnUpdate()
    {
        EditorLevelObjectsUIExtension? inst = UnturnedUIToolsNexus.UIExtensionManager.GetInstance<EditorLevelObjectsUIExtension>();
        if (inst == null)
            return;
        
        if (InputEx.GetKeyDown(LevelObjectIconsNexus.Config.EditKeybind))
            inst.EditorActive = !inst.EditorActive;

        if (InputEx.GetKeyDown(LevelObjectIconsNexus.Config.LogMissingKeybind))
            LogMissingOffsets();

        if (inst._isGeneratingIcon)
            return;
        
        if (!inst._isEditingToggle.Value || LevelObjectIconsNexus.SelectedAsset is not ObjectAsset { type: EObjectType.SMALL or EObjectType.MEDIUM or EObjectType.LARGE } asset)
            goto clear;
        
        LevelObject? selectedObject = null;
        foreach (EditorSelection selection in EditorObjectSelection)
        {
            LevelObject? levelObject = null;
            for (int x = 0; x < Regions.WORLD_SIZE; ++x)
            {
                for (int y = 0; y < Regions.WORLD_SIZE; ++y)
                {
                    List<LevelObject> objRegion = LevelObjects.objects[x, y];
                    int ct = objRegion.Count;
                    for (int i = 0; i < ct; ++i)
                    {
                        if (ReferenceEquals(objRegion[i].transform, selection.transform))
                        {
                            levelObject = objRegion[i];
                            goto found;
                        }
                    }
                }
            }

            found:
            if (levelObject == null || levelObject.asset.GUID != asset.GUID)
                continue;
            
            if (selectedObject != null)
                goto clear;

            selectedObject = levelObject;
        }

        if (selectedObject == null)
            goto clear;

        ObjectIconPresets.UpdateEditCache(selectedObject, asset);
        inst.UpdateSelectedObject(false);
        return;

        clear:
        if (ObjectIconPresets.ActivelyEditing != null)
        {
            ObjectIconPresets.ClearEditCache();
            inst.UpdateSelectedObject(true);
        }

        if (LevelObjectIconsNexus.Config.ShouldCycleMaterialPalette && inst._materialIndex >= 0 && inst._nextIcon > 0f && inst._nextIcon < Time.realtimeSinceStartup)
            inst.UpdateSelectedObject(true);
    }
    internal void UpdateSelectedObject(bool updateMat)
    {
        _preview.Texture = null;
        Asset? asset = LevelObjectIconsNexus.SelectedAsset;
        if (asset != null)
        {
            _isGeneratingIcon = true;
            _nextIcon = -1f;
            string text = asset.FriendlyName;
            if (asset is ObjectAsset obj)
                text += " (" + obj.type switch
                {
                    EObjectType.LARGE => "Large",
                    EObjectType.MEDIUM => "Medium",
                    EObjectType.SMALL => "Small",
                    EObjectType.DECAL => "Decal",
                    EObjectType.NPC => "NPC",
                    _ => "Object"
                } + ")";
            else if (asset is ItemStructureAsset)
                text += " (Structure)";
            else if (asset is ItemBarricadeAsset)
                text += " (Barricade)";
            _displayTitle.Text = text;
            Color rarityColor = asset is ItemAsset item ? ItemTool.getRarityColorUI(item.rarity) : Color.white;
            _displayTitle.BackgroundColor = SleekColor.BackgroundIfLight(rarityColor);
            _displayTitle.TextColor = rarityColor;

            if (asset is ObjectAsset obj2)
            {
                if (obj2.interactability != EObjectInteractability.NOTE)
                {
                    if (_noteText.IsVisible)
                    {
                        _noteText.IsVisible = false;
                        _noteText.Text = string.Empty;
                    }
                }
                else
                {
                    _noteText.IsVisible = true;
                    _noteText.Text = obj2.interactabilityText;
                }

                if (updateMat && obj2.materialPalette.isValid && obj2.materialPalette.Find() is { materials.Count: > 0 } palette)
                {
                    _materialTtl = palette.materials.Count;
                    _materialIndex = _materialTtl == 1 || !LevelObjectIconsNexus.Config.ShouldCycleMaterialPalette ? -1 : (_materialIndex == -1 ? Random.Range(0, _materialTtl) : (_materialIndex + 1) % _materialTtl);
                }
                else if (updateMat) _materialTtl = 0;
                else _materialIndex = -1;
            }

            ObjectRenderOptions? options;
            if (Time.realtimeSinceStartup - _lastUpdate > 1f && (_materialIndex == -1 || _materialTtl == 0))
                options = null;
            else
                options = new ObjectRenderOptions
                {
                    MaterialIndexOverride = !updateMat && _materialIndex == -1 ? 0 : _materialIndex
                };

            IconGenerator.GetIcon(asset, Size, Size, options, OnIconReady);
        }
        else
        {
            if (_noteText.IsVisible)
            {
                _noteText.IsVisible = false;
                _noteText.Text = string.Empty;
            }

            _displayTitle.TextColor = ESleekTint.FOREGROUND;
            _displayTitle.Text = LevelObjectIconsNexus.Localization.format("NoAssetSelected");
            _materialIndexLbl.Text = string.Empty;
        }

        _lastUpdate = Time.realtimeSinceStartup;
    }

    private void OnIconReady(Asset asset, Texture? texture, bool destroy, ObjectRenderOptions? options)
    {
        _isGeneratingIcon = false;
        if (EditorObjects.selectedItemAsset != asset && EditorObjects.selectedObjectAsset != asset)
            return;
        _preview.Texture = texture;
        _preview.ShouldDestroyTexture = destroy;
        
        if (texture != null)
        {
            float aspect = (float)texture.width / texture.height;
            if (Mathf.Approximately(aspect, 1f))
            {
                _preview.SizeOffset_X = -20;
                _preview.SizeOffset_Y = -20;
                _preview.PositionOffset_X = 10;
                _preview.PositionOffset_Y = 10;
            }
            else if (aspect > 1f)
            {
                _preview.SizeOffset_X = -20f;
                _preview.SizeOffset_Y = -(1f - 1f / aspect) * Size - 20f;
                _preview.PositionOffset_X = 10f;
                _preview.PositionOffset_Y = (1f - 1f / aspect) * Size / 2f + 10f;
            }
            else
            {
                _preview.PositionOffset_X = (1f - aspect) * Size / 2f + 10f;
                _preview.PositionOffset_Y = 10f;
                _preview.SizeOffset_X = -(1f - aspect) * Size - 20f;
                _preview.SizeOffset_Y = -20f;
            }

            _materialIndexLbl.Text = _materialIndex == -1 || !LevelObjectIconsNexus.Config.ShouldCycleMaterialPalette ? string.Empty : $"{_materialIndex} / {_materialTtl - 1}";
        }
        else
        {
            _preview.SizeOffset_X = -20;
            _preview.SizeOffset_Y = -20;
            _preview.PositionOffset_X = 10;
            _preview.PositionOffset_Y = 10;
            _materialIndexLbl.Text = string.Empty;
        }
        
        _nextIcon = Time.realtimeSinceStartup + 1f;
    }
    private static void Patch()
    {
        MethodInfo? target = typeof(EditorLevelObjectsUI).GetMethod("onClickedAssetButton", BindingFlags.Static | BindingFlags.NonPublic);
        if (target == null)
        {
            CommandWindow.LogError($"[{IconGenerator.Source}] Failed to find method: EditorLevelObjectsUI.onClickedAssetButton");
            return;
        }
        
        LevelObjectIconsNexus.Patcher.Patch(target, finalizer: new HarmonyMethod(Accessor.GetMethod(OnUpdatedElement)!));
        _patched = true;
    }
    private static void OnUpdatedElement(bool __runOriginal)
    {
        if (!__runOriginal)
            return;

        EditorLevelObjectsUIExtension? inst = UnturnedUIToolsNexus.UIExtensionManager.GetInstance<EditorLevelObjectsUIExtension>();

        if (inst == null)
            return;

        inst._materialIndex = -1;
        inst.UpdateSelectedObject(true);
    }

    /// <summary>
    /// Logs any objects missing offsets to console.
    /// </summary>
    public static void LogMissingOffsets()
    {
        List<ObjectAsset> objects = new List<ObjectAsset>(4096);
        Assets.find(objects);

        ulong lastMod = ulong.MaxValue;
        foreach (ObjectAsset obj in objects
                     .Where(x => x.type is not EObjectType.DECAL and not EObjectType.NPC)
                     .OrderByDescending(x => GetAssetOrigin?.Invoke(x)?.workshopFileId ?? 0ul)
                     .ThenBy(x => x.getFilePath()))
        {
            if (ObjectIconPresets.Presets.ContainsKey(obj.GUID))
                continue;

            AssetOrigin? assetOrigin = GetAssetOrigin?.Invoke(obj);
            ulong modId = assetOrigin?.workshopFileId ?? 0ul;

            string path = obj.getFilePath();

            if (modId == 0ul && path.IndexOf("/Sandbox/", StringComparison.Ordinal) != -1)
                modId = 1ul;

            if (modId != lastMod)
            {
                lastMod = modId;
                CommandWindow.Log(string.Empty);
                string? modName;
                if (modId == 0ul)
                {
                    modName = "Vanilla Content";
                    CommandWindow.Log($"=== {modName} ===");
                }
                else if (modId == 1ul)
                {
                    modName = "Sandbox Content";
                    CommandWindow.Log($"=== {modName} ===");
                }
                else
                {
                    modName = null;
                    if (assetOrigin != null)
                    {
                        int index = assetOrigin.name.IndexOf('"');
                        if (index != -1 && index < assetOrigin.name.Length - 1)
                        {
                            int index2 = assetOrigin.name.IndexOf('"', index + 1);
                            modName = assetOrigin.name.Substring(index + 1, index2 - index - 1);
                        }
                    }

                    CommandWindow.Log(modName == null ? $"=== Mod: {modId} ===" : $"=== Mod: {modName} ({modId}) ===");
                }
            }

            if (modId is 0ul or 1ul)
                path = path.Replace(UnturnedPaths.RootDirectory.FullName, string.Empty);
            else
            {
                SteamContent? ugc = Provider.provider?.workshopService?.ugc?.Find(x => x.publishedFileID.m_PublishedFileId == modId);
                if (ugc != null)
                    path = path.Replace(Path.GetFullPath(ugc.path), string.Empty);
            }

            CommandWindow.Log($"Missing Object: {obj.FriendlyName,-30} @ {path}");
        }
    }
}