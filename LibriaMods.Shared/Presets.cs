using System.Collections.Generic;

namespace LibriaMods.Shared
{
    /// <summary>
    /// Built-in presets (tuned on Syberia: The World Before, a dark HDRP game). Each targets HDRP, URP and PPv2
    /// at once (sections a pipeline doesn't have are skipped); [FilmDamage] is LibriaMods' own overlay.
    /// A custom .ini with the same name in <see cref="Fx.CustomPresetsFolder"/> replaces a built-in preset.
    /// Black &amp; white with a tint uses the Channel Mixer: HDRP applies saturation after tinting,
    /// so "saturation = -100" would wipe the tint out.
    /// </summary>
    public static class Presets
    {
        /// <summary>Black & white, punchy contrast, light grain and vignette.</summary>
        public static Preset Noir => Preset.Parse(@"
name = Noir
[ColorAdjustments]
saturation   = -100
contrast     = 28
postExposure = 0.98
[LiftGammaGain]
gamma = 1, 1, 1, 0.03
[FilmGrain]
type      = Medium3
intensity = 0.3
response  = 0.8
[Vignette]
intensity  = 0.3
smoothness = 0.45
[ColorGrading]
saturation   = -100
contrast     = 28
postExposure = 0.98
[Grain]
intensity  = 0.3
size       = 1.2
lumContrib = 0.8
", "Noir");

        /// <summary>Black & white with the slightly blue tone of old prints and CRT TVs (Channel Mixer, so the tint survives).</summary>
        public static Preset NoirCold => Preset.Parse(@"
name = Noir Cold
[ChannelMixer]
redOutRedIn     = 28
redOutGreenIn   = 54
redOutBlueIn    = 10
greenOutRedIn   = 29
greenOutGreenIn = 58
greenOutBlueIn  = 11
blueOutRedIn    = 33
blueOutGreenIn  = 65
blueOutBlueIn   = 12
[ColorAdjustments]
contrast     = 28
postExposure = 0.98
[FilmGrain]
type      = Medium3
intensity = 0.3
response  = 0.8
[Vignette]
intensity  = 0.3
smoothness = 0.45
[ColorGrading]
mixerRedOutRedIn     = 28
mixerRedOutGreenIn   = 54
mixerRedOutBlueIn    = 10
mixerGreenOutRedIn   = 29
mixerGreenOutGreenIn = 58
mixerGreenOutBlueIn  = 11
mixerBlueOutRedIn    = 33
mixerBlueOutGreenIn  = 65
mixerBlueOutBlueIn   = 12
contrast     = 28
postExposure = 0.98
[Grain]
intensity  = 0.3
size       = 1.2
lumContrib = 0.8
", "NoirCold");

        /// <summary>Warm old photograph via the classic sepia colour matrix (Channel Mixer).</summary>
        public static Preset Sepia => Preset.Parse(@"
name = Sepia
[ChannelMixer]
redOutRedIn     = 39
redOutGreenIn   = 77
redOutBlueIn    = 19
greenOutRedIn   = 35
greenOutGreenIn = 69
greenOutBlueIn  = 17
blueOutRedIn    = 27
blueOutGreenIn  = 53
blueOutBlueIn   = 13
[ColorAdjustments]
contrast     = 8
postExposure = 1.0
[Vignette]
intensity  = 0.2
smoothness = 0.5
[ColorGrading]
mixerRedOutRedIn     = 39
mixerRedOutGreenIn   = 77
mixerRedOutBlueIn    = 19
mixerGreenOutRedIn   = 35
mixerGreenOutGreenIn = 69
mixerGreenOutBlueIn  = 17
mixerBlueOutRedIn    = 27
mixerBlueOutGreenIn  = 53
mixerBlueOutBlueIn   = 13
contrast     = 8
postExposure = 1.0
", "Sepia");

        /// <summary>Noir colours plus a worn print: grain, vignette, light scratches, dust and flicker.</summary>
        public static Preset OldFilm => Preset.Parse(@"
name = Old Film
[ColorAdjustments]
saturation   = -100
contrast     = 28
postExposure = 0.98
[LiftGammaGain]
gamma = 1, 1, 1, 0.03
[FilmGrain]
type      = Large02
intensity = 0.45
response  = 0.7
[Vignette]
intensity  = 0.35
smoothness = 0.4
roundness  = 0.8
[FilmDamage]
amount    = 0.45
bands     = 0
rolls     = 0
scratches = 0.8
dust      = 0.8
hairs     = 0.6
burns     = 0.2
flicker   = 0.6
pops      = 0.3
[ColorGrading]
saturation   = -100
contrast     = 28
postExposure = 0.98
[Grain]
intensity  = 0.45
size       = 1.6
lumContrib = 0.7
", "OldFilm");

        /// <summary>Noir colours plus a badly damaged print: bands, rolls, burns, scratches, dust.</summary>
        public static Preset DamagedFilm => Preset.Parse(@"
name = Damaged Film
[ColorAdjustments]
saturation   = -100
contrast     = 28
postExposure = 0.98
[LiftGammaGain]
gamma = 1, 1, 1, 0.03
[FilmGrain]
type      = Large02
intensity = 0.55
response  = 0.6
[Vignette]
intensity  = 0.4
smoothness = 0.4
[ChromaticAberration]
intensity = 0.1
[FilmDamage]
amount    = 0.6
bands     = 0.5
rolls     = 0.4
scratches = 1
dust      = 1
hairs     = 0.8
burns     = 0.5
flicker   = 0.8
pops      = 0.5
[ColorGrading]
saturation   = -100
contrast     = 28
postExposure = 0.98
[Grain]
intensity  = 0.55
size       = 1.8
lumContrib = 0.6
", "DamagedFilm");

        public static IEnumerable<Preset> BuiltIn
        {
            get { yield return Noir; yield return NoirCold; yield return Sepia; yield return OldFilm; yield return DamagedFilm; }
        }
    }
}
