# LibriaMods.Shared

Shared library for [LibriaMods](https://github.com/LibriaMods) mods.

## Fx – post-processing presets for any Unity game

Switch the game's look (black & white, sepia, old film, VHS, CRT or your own presets) through the game's **own**
post-processing, so the UI and subtitles are left alone.

- Supports **HDRP**, **URP** and **Post Processing Stack v2**, detected at runtime via reflection
- No reference to MelonLoader or any render pipeline: one DLL for any Unity **Mono** game and any mod loader
  (IL2CPP games are not supported)
- Presets supplied by the mod plus your own `.ini` files, smooth fade in/out

```csharp
using LibriaMods.Shared;

Fx.Log = msg => LoggerInstance.Msg(msg);   // optional
Fx.Apply("Noir");                           // built-in or custom preset by name
Fx.Toggle("Sepia");
Fx.Off();
// call Fx.Update() every frame (drives the fade)
```

### Presets come from the mod

The library has no presets of its own: each mod adds its looks to `Fx.BuiltIn` in `OnLoad`
(see [Syberia Noir](https://github.com/LibriaMods/LibriaMods.STWB.Noir) for a full set tuned on an HDRP game).
Keeping them in the mod means the loader's hot reload picks up changes, and every game gets presets tuned for it.

```csharp
public override void OnLoad(IModContext context)
{
    Fx.BuiltIn.Clear();
    Fx.BuiltIn.Add(Preset.Parse(@"
name = Noir
[ColorAdjustments]
saturation = -100
contrast   = 28"));
}
public override void OnUnload() { Fx.Off(); Fx.BuiltIn.Clear(); }
```

**Tinted black & white:** HDRP applies saturation *after* tinting, so `saturation = -100` wipes out
split toning or colour filters. Make the image monochrome with the `ChannelMixer` instead (see `Noir Cold`
and `Sepia` in Syberia Noir): every output channel is a mix of the input channels, so the tint is part of the conversion.

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

### Film damage overlay – `[FilmDamage]`

Old-film damage that post-processing can't do, drawn by LibriaMods itself as a full-screen overlay
between the 3D scene and the game's UI – so it works in **any** Unity game, whatever the pipeline:

- moving **tracking bands** and occasional **rolls** sweeping the frame
- wandering vertical **scratches**, **dust** specks and the odd **hair**
- cigarette **burns** that bloom white-hot with a dark scorched rim, then fade
- light **flicker** and occasional exposure **pops**

It's procedural (no textures) and generated at a film-like frame rate, so it never repeats.

```ini
[FilmDamage]
amount    = 1     ; master strength (all values: 0 = off, 1 = default, >1 = stronger)
bands     = 1
rolls     = 1
scratches = 1
dust      = 1
hairs     = 1
burns     = 1
flicker   = 1
pops      = 1
fps       = 14    ; damage frame rate
width     = 640   ; damage layer resolution (lower = softer and cheaper)
sortingOrder = -100  ; overlay canvas order; raise it if it draws under something it shouldn't
seed      = 0     ; 0 = random
```

Common effects – HDRP/URP: `ColorAdjustments`, `FilmGrain`, `Vignette`, `SplitToning`, `ChannelMixer`,
`WhiteBalance`, `Bloom`, `ChromaticAberration`, `Tonemapping`, `LiftGammaGain`.
PPv2: `ColorGrading`, `Grain`, `Vignette`, `Bloom`, `ChromaticAberration`.

### Old screen overlay – `[Screen]`

Old TV / video look, drawn like `[FilmDamage]` as an overlay above the scene and below the UI:

```ini
[Screen]
scanlines = 0.3   ; darkness of the scanlines (0..1)
lines     = 360   ; lines over the screen height (rounded to whole pixels, so no moiré)
mask      = 0     ; RGB aperture-grille stripes (0..1)
maskSize  = 3     ; screen pixels per RGB triad
roll      = 0     ; slow bright refresh bar (0..1)
rollSpeed = 0.12  ; screen heights per second
snow      = 0     ; video noise (0..1)
bezel     = 0.25  ; rounded black corners of a curved tube (0..1)
```

## Toast – on-screen messages

`Toast.Show("Speed: 1.6x")` shows a small message at the top of the screen that fades out after ~2 s
(Unity UI through reflection, works in any game). Fx shows the preset name automatically when it changes;
set `Toast.Enabled = false` to turn that off.

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
