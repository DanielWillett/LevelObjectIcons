# Level Object Icons Module

*This was originally a part of [DevkitServer](https://github.com/DanielWillett/DevkitServer) and has been ported to a standalone module.*

### Object Preview
![Features](https://i.imgur.com/uxSm4qF.png)

# Installation and Usage Guide
**See video: [YouTube](https://youtu.be/bgBJCmkwR7I)**

# Installation
* Download and extract the latest module from [Releases](https://github.com/DanielWillett/LevelObjectIcons/releases/) into your `Unturned/Modules` folder.
  * I recommend not using the standalone module in case any other modules use the UITools library, but both will work.
* Edit the configuration file if desired (see below).
* Launch without *BattlEye* so the module loads.

# Features
* View objects, NPCs, and decals without placing them.
* Cycle through all of the material options in the default material palette (if set).
* Edit object icons in-game for any objects.
* Objects without custom icons are automatically lined up for any object.
  * The front is assumed to be the -Z face of the object when at a Euler rotation of `(-90, 0, 0)` (this is the default for most objects in-game, which is why it was chosen).<br>
* All vanilla objects have icon camera transform overrides (when necessary).
* Any mod can easily add their own offsets directly to their Bundles folder.
* Preview the text in notes without placing them.
* Default icons for notes, flags, billboards, decals, and NPCs that will update automatically if a new one is added later on.
* Modules can add their own default icon provider by implementing the `IDefaultIconProvider` interface.

# Configuration
There are two config files.
## `level_object_icons_config.json`
Main settings file for the module with the following options:
* `edit_keybind`: Key used to toggle the Live Editor checkbox. Default: `F8`
* `log_mising_keybind`: Key used to print all objects that don't have an offset to Client.log. Default: `Keypad5`
* `cycle_material_palette`: Enables cycling between materials in the material palette. This may cause some lag on lower-end machines. Default: `true`
* `debug_logging`: Enables extra logging to see what files are being used, etc. Default: `false`
* `disable_default_provider_search`: Disables searching other modules for default icon providers. Set this to `true` if errors arise from `ApplyDefaultProviders`. Default: `false`

Open in Visual Studio Code or another IDE that supports JSON schemas to get auto-complete.

## Localization
Change translation values for UIs and add a language here. `English.dat` has the default values but you can add your own language if desired, similar to how vanilla localization files work.

# Implementing custom overrides for your mod

## JSON Icon Provider Method (recommended)
You can create offsets using the in-editor object extension menu. You must have this enabled in the config, which will be enabled by default.

Spawn in the object you want to edit (E).

Press 'F8' while in the Object Editor. You should see a toggle box appear called **Live Editor**, ensure it's checked.
It is normal to have a minor FPS drop while editing.

You must have the asset selected and the object you just spawned in selected, then you'll see the preview moving as you move your camera.

You can have multiple objects selected, as long as you only have one selected of the type you're editing. This can be useful for making multiple icons have the exact same offset.

Position your camera so the preview looks how you want the icon, then either hit **Save** or **Save New** (see below).

**Save New** saves to (or updates if theres already an icon preset there) the custom icon file: `<Module Folder>\configured_icons.json`, where they can be copied into your mod or plugin.

**Save** looks for a non-readonly file with an icon preset for that asset and saves there, otherwise it saves to the same place as **Save New**.

### In-Game Editor
![editor](https://i.imgur.com/CaTgpiA.png)


### Read Locations

Except for the *Custom icons file*, the file name must start with one of the following (case insensitive) and be a `.json` file:

* Object Icons
* Object Icon Presets
* Object Presets
* object_icons
* object_icon_presets
* object_presets

|Location|Readonly|Recursive File Discovery|
|---|---|---|
|Custom icons file: `<Module Folder>\configured_icons.json`|No|N/A|
|Loaded workshop content: `steamapps\workshop\304930\*`|Yes|Yes|
|Sandbox folder: `Unturned\Sandbox`|No|Yes|
|Vanilla bundles folder: `Unturned\Bundles`|Yes|No|
|Vanilla object bundles folder: `Unturned\Bundles\Objects`|Yes|Yes|
|Loaded and Enabled Modules: `Unturned\Modules\*`|Yes|Yes|

To include custom icon offsets in your mod, delete the custom icon file at `<Module Folder>\configured_icons.json` and exit and rejoin the editor if needed.

Then go through all your objects, place them, configure the icon like described above, and hit **Save New**.

The priority will be set as high as it needs to be to be picked over any other presets.

Once you're done, cut the custom icon file to somewhere in your mod folder (it must be under `~\Bundles` for maps).

### File Format 
*For manual entry*
```jsonc
[
  {
    // Asset GUID or UInt16 ID - Objects do not require UInt16 IDs so they will likely become discontinued.
    "object": "GUID or ID",

    // Position offset (m)
    "position": [x: decimal, y: decimal, z: decimal],

    // Euler rotation offset (you can add a fourth argument (w: decimal) to make it read as a Quaternion)
    "rotation": [x: decimal, y: decimal, z: decimal],

    // Optional - Default is 0. Higher priorities are used first.
    "priority": integer
  },
  {
    "object": "205a8cc33c9849c9bd65790403d0753d",
    "position": [-3, -10, 9],
    "rotation": [-107, -80, 260],
    "priority": 1
  }
]
```

### Copy Offsets
You can copy offsets of other objects by selecting the object asset, then right clicking the **Goto** button to fill it's GUID into the text box next to it. You could also just type the GUID in manually.

Next select the asset you want to align and a spawned level object object of the same type (not more than one) and left click **Goto**, it will teleport your camera to the same offset as the other object had relative to your selected object, and you can save it from there.


## "Icon" Method
I recommend using the JSON method described above for compatibility and performance reasons.

Adding an empty GameObject as a child to the base prefab will provide a position and rotation for a camera.

Name it `Icon` (`Icon2` is also supported).

To preview it in Unity, add a Camera Component in perspective mode with 60 FOV. Then just position the camera where you want.

Make sure you remove the Camera and I'd recommend disabling your `Icon` GameObject for performance reasons.
