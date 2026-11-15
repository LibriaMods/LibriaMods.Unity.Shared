using System.Collections.Generic;

namespace LibriaMods.Shared
{
    /// <summary>
    /// Built-in presets. Each targets HDRP, URP and PPv2 at once (sections a pipeline doesn't have are skipped);
    /// [FilmDamage] is LibriaMods' own overlay and works everywhere.
    /// A custom .ini with the same name in <see cref="Fx.CustomPresetsFolder"/> replaces a built-in preset.
    /// </summary>
    public static class Presets
    {
        public static Preset Noir => Preset.Parse(@"
name = Noir
[ColorAdjustments]
saturation   = -100
contrast     = 12
postExposure = 0.35
[LiftGammaGain]
lift  = 1, 1, 1, 0.04
gamma = 1, 1, 1, 0.06
[FilmGrain]
type      = Medium3
intensity = 0.35
response  = 0.8
[Vignette]
intensity  = 0.25
smoothness = 0.5
[ColorGrading]
saturation   = -100
contrast     = 12
postExposure = 0.35
[Grain]
intensity  = 0.35
size       = 1.2
lumContrib = 0.8
", "Noir");

        public static Preset Sepia => Preset.Parse(@"
name = Sepia
[ColorAdjustments]
saturation   = -100
contrast     = 8
postExposure = 0.2
[SplitToning]
shadows    = #5A3C1E
highlights = #F0D2A0
balance    = 10
[Vignette]
intensity  = 0.25
smoothness = 0.5
[ColorGrading]
saturation   = -100
contrast     = 8
postExposure = 0.2
colorFilter  = #F2D6A8
", "Sepia");

        public static Preset OldFilm => Preset.Parse(@"
name = Old Film
[ColorAdjustments]
saturation   = -100
contrast     = 20
postExposure = 0.25
[LiftGammaGain]
lift = 1, 1, 1, 0.05
[SplitToning]
shadows    = #2E2A24
highlights = #E6DCC8
balance    = 0
[FilmGrain]
type      = Large02
intensity = 0.7
response  = 0.6
[Vignette]
intensity  = 0.4
smoothness = 0.4
roundness  = 0.8
[ColorGrading]
saturation   = -100
contrast     = 20
postExposure = 0.25
colorFilter  = #E6DCC8
[Grain]
intensity  = 0.7
size       = 1.8
lumContrib = 0.6
[FilmDamage]
amount    = 0.6
bands     = 0
rolls     = 0
scratches = 1
dust      = 1
hairs     = 1
burns     = 0.4
flicker   = 1
pops      = 0.5
", "Old Film");

        public static Preset DamagedFilm => Preset.Parse(@"
name = Damaged Film
[ColorAdjustments]
saturation   = -100
contrast     = 25
postExposure = 0.2
[LiftGammaGain]
lift = 1, 1, 1, 0.05
[FilmGrain]
type      = Large02
intensity = 0.9
response  = 0.5
[Vignette]
intensity  = 0.5
smoothness = 0.35
[ChromaticAberration]
intensity = 0.2
[ColorGrading]
saturation   = -100
contrast     = 25
postExposure = 0.2
[Grain]
intensity  = 0.9
size       = 2
lumContrib = 0.5
[FilmDamage]
amount    = 1
bands     = 1
rolls     = 1
scratches = 1.3
dust      = 1.3
hairs     = 1
burns     = 1
flicker   = 1.5
pops      = 1
", "Damaged Film");

        public static IEnumerable<Preset> BuiltIn
        {
            get { yield return Noir; yield return Sepia; yield return OldFilm; yield return DamagedFilm; }
        }
    }
}
