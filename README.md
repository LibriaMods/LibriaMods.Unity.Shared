# LibriaMods.Shared

Shared library for [LibriaMods](https://github.com/LibriaMods) mods.

## Fx – post-processing presets for any Unity game

Switch the game's look (Noir, Sepia, Old Film or your own presets) through the game's **own**
post-processing, so the UI and subtitles are left alone.

- Supports **HDRP**, **URP** and **Post Processing Stack v2**, detected at runtime via reflection
- No reference to MelonLoader or any render pipeline: one DLL for any Unity **Mono** game and any mod loader
  (IL2CPP games are not supported)
- Built-in presets plus your own `.ini` files, smooth fade in/out

```csharp
using LibriaMods.Shared;

Fx.Log = msg => LoggerInstance.Msg(msg);   // optional
Fx.Apply("Noir");                           // built-in or custom preset by name
Fx.Toggle("Sepia");
Fx.Off();
// call Fx.Update() every frame (drives the fade)
```

### Built-in presets

| Preset | Look |
|---|---|
| `Noir` | Black & white, extra contrast, light grain and vignette |
| `Sepia` | Warm old photograph |
| `Old Film` | Worn 1930s newsreel: heavy grain, strong vignette, slight colour fringing |

### Custom presets

`.ini` files in `<game>\UserData\LibriaMods\Presets` (folder configurable via `Fx.CustomPresetsFolder`).
A custom preset with the same `name` as a built-in one replaces it.

```ini
name = My Look

[ColorAdjustments]      ; HDRP / URP – section = effect class name
saturation = -100       ; key = parameter field name
contrast   = 35

[FilmGrain]
type      = Medium3     ; enums by name
intensity = 0.45

[SplitToning]
shadows = #5A3C1E       ; colours: #RRGGBB(AA) or r,g,b[,a]

[ColorGrading]          ; Post Processing Stack v2
saturation = -100
```

Effects/parameters the game's pipeline doesn't have are skipped, so one preset covers every pipeline.

Common effects – HDRP/URP: `ColorAdjustments`, `FilmGrain`, `Vignette`, `SplitToning`, `ChannelMixer`,
`WhiteBalance`, `Bloom`, `ChromaticAberration`, `Tonemapping`, `LiftGammaGain`.
PPv2: `ColorGrading`, `Grain`, `Vignette`, `Bloom`, `ChromaticAberration`.

## Writing mods: `ILibriaMod` + the loader

`LibriaMods.Loader` is the only MelonLoader mod. It loads every DLL in `UserData\LibriaMods\Mods`,
creates the public classes that implement `ILibriaMod` and calls them every frame. Mods only reference
`LibriaMods.Shared` and Unity – no MelonLoader – so the same mod could run under another loader later.

```csharp
using LibriaMods.Shared;
using UnityEngine;

[LibriaMod("My Mod", "1.0.0")]
[LibriaGame("Microids", "SyberiaTWB")]          // optional: only load in this game
public class MyMod : LibriaMod
{
    IConfigEntry<KeyCode> key;

    public override void OnLoad(IModContext ctx)
    {
        base.OnLoad(ctx);
        key = ctx.Config("Key", KeyCode.F9, "What the key does");   // UserData\LibriaMods\<Id>.cfg
    }

    public override void OnUpdate()
    {
        if (Input.GetKeyDown(key.Value)) Fx.Toggle("Noir");
    }

    public override void OnUnload() => Fx.Off();  // undo everything: a reload creates a new instance
}
```

**Hot reload:** DLLs are loaded from memory, so you can rebuild while the game runs. With
`AutoReload = true` in `UserData\LibriaMods.Loader.cfg` a changed DLL is reloaded within a second;
**F5** reloads everything. Changes to `LibriaMods.Shared` itself (the contract) need a game restart.

Player install for any LibriaMods mod:

```
Mods\LibriaMods.Loader.dll
UserLibs\LibriaMods.Shared.dll
UserData\LibriaMods\Mods\<the mod>.dll
```

## Install (for players)

Each mod's zip ships the loader and this library (see above).

## Building

No game needed: Unity is referenced from the `UnityEngine.Modules` NuGet package. Open
`LibriaMods.Shared.slnx` and build.
