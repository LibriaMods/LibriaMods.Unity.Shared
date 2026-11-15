using System.Collections.Generic;

namespace LibriaMods.Shared
{
    /// <summary>Built-in presets. Each one targets HDRP, URP and PPv2 at once (unknown sections are skipped).</summary>
    public static class Presets
    {
        public static Preset Noir => Preset.Parse(@"; LibriaMods preset - black & white film noir
; Sections are effect class names; unknown ones are skipped, so this works on HDRP, URP and PPv2.
name = Noir

; ---- HDRP / URP ----
[ColorAdjustments]
saturation   = -100
contrast     = 35
postExposure = -0.15

[FilmGrain]
type      = Medium3
intensity = 0.45
response  = 0.8

[Vignette]
intensity  = 0.4
smoothness = 0.45

; ---- Post Processing Stack v2 (built-in pipeline) ----
[ColorGrading]
saturation   = -100
contrast     = 35
postExposure = -0.15

[Grain]
intensity  = 0.45
size       = 1.2
lumContrib = 0.8
", "Noir");

        public static Preset Sepia => Preset.Parse(@"; LibriaMods preset - warm sepia photograph
name = Sepia

; ---- HDRP / URP ----
[ColorAdjustments]
saturation = -100
contrast   = 10

[SplitToning]
shadows    = #5A3C1E
highlights = #F0D2A0
balance    = 10

[Vignette]
intensity  = 0.3
smoothness = 0.5

; ---- Post Processing Stack v2 ----
[ColorGrading]
saturation  = -100
contrast    = 10
colorFilter = #F2D6A8
", "Sepia");

        public static Preset OldFilm => Preset.Parse(@"; LibriaMods preset - worn 1930s newsreel (fits Dana's timeline)
name = Old Film

; ---- HDRP / URP ----
[ColorAdjustments]
saturation   = -100
contrast     = 45
postExposure = -0.25

[SplitToning]
shadows    = #2E2A24
highlights = #E6DCC8
balance    = 0

[FilmGrain]
type      = Large02
intensity = 0.8
response  = 0.6

[Vignette]
intensity  = 0.55
smoothness = 0.35
roundness  = 0.8

[ChromaticAberration]
intensity = 0.15

; ---- Post Processing Stack v2 ----
[ColorGrading]
saturation   = -100
contrast     = 45
postExposure = -0.25
colorFilter  = #E6DCC8

[Grain]
intensity  = 0.8
size       = 1.8
lumContrib = 0.6
", "OldFilm");

        public static IEnumerable<Preset> BuiltIn
        {
            get { yield return Noir; yield return Sepia; yield return OldFilm; }
        }
    }
}
