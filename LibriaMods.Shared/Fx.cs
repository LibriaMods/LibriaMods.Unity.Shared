using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace LibriaMods.Shared
{
    /// <summary>
    /// Post-processing presets for any Unity (Mono) game: HDRP, URP or Post Processing Stack v2,
    /// detected at runtime through reflection. No dependency on MelonLoader or a render pipeline.
    ///
    /// Usage from a mod:
    ///   Fx.Log = msg => LoggerInstance.Msg(msg);   // optional
    ///   Fx.BuiltIn.AddRange(myPresets);             // the mod's own presets (set again on every OnLoad)
    ///   Fx.Apply("Noir");                           // a built-in or a custom .ini by name
    ///   ... and call Fx.Update() every frame (drives the fade).
    /// </summary>
    public static class Fx
    {
        public static Action<string> Log = _ => { };
        public static Action<string> Verbose = _ => { };

        /// <summary>Seconds to fade a preset in/out (0 = instant).</summary>
        public static float FadeSeconds = 0.5f;

        /// <summary>Folder with user .ini presets. Default: &lt;game&gt;\UserData\LibriaMods\Presets.</summary>
        public static string CustomPresetsFolder =
            Path.Combine(Path.Combine(Path.GetDirectoryName(Application.dataPath), "UserData"), Path.Combine("LibriaMods", "Presets"));

        /// <summary>Presets supplied by the mod (they live in the mod so hot reload picks up changes).
        /// Clear and refill it in OnLoad; custom .ini files with the same name replace them.</summary>
        public static readonly List<Preset> BuiltIn = new List<Preset>();

        public static Preset Current { get; private set; }
        public static bool IsOn => Current != null;
        public static string PipelineName => backend?.Name ?? "none";

        static Backend backend;
        static bool detectFailed;
        static FilmDamage damage;
        static ScreenOverlay screen;
        static float weight, targetWeight, fadeSpeed = float.PositiveInfinity;

        // ------------------------------------------------------------------ presets

        /// <summary>Built-in presets plus every .ini in <see cref="CustomPresetsFolder"/> (re-read each call;
        /// a custom file with the same name overrides a built-in one).</summary>
        public static List<Preset> AllPresets()
        {
            var list = new List<Preset>(BuiltIn);
            if (Directory.Exists(CustomPresetsFolder))
            {
                foreach (var f in Directory.GetFiles(CustomPresetsFolder, "*.ini").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
                {
                    try
                    {
                        var p = Preset.Load(f);
                        // A custom preset with a built-in's name replaces it in place (keeps the cycling order).
                        int i = list.FindIndex(x => x.Name.Equals(p.Name, StringComparison.OrdinalIgnoreCase));
                        if (i >= 0) list[i] = p; else list.Add(p);
                    }
                    catch (Exception e) { Log($"Bad preset {Path.GetFileName(f)}: {e.Message}"); }
                }
            }
            return list;
        }

        public static Preset Find(string name) => AllPresets().FirstOrDefault(p =>
            p.Name.Equals(name, StringComparison.OrdinalIgnoreCase) ||
            (p.FilePath != null && Path.GetFileNameWithoutExtension(p.FilePath).Equals(name, StringComparison.OrdinalIgnoreCase)));

        // ------------------------------------------------------------------ control

        public static bool Apply(string name)
        {
            var p = Find(name);
            if (p == null) { Log($"Preset '{name}' not found (built-in: {string.Join(", ", BuiltIn.Select(x => x.Name).ToArray())})"); return false; }
            Apply(p);
            return true;
        }

        public static void Apply(Preset preset)
        {
            Current = preset;

            // Overlay effects handled by LibriaMods itself (any pipeline): [FilmDamage] and [Screen]
            damage?.Destroy();
            damage = null;
            var dmg = preset.Effects.FirstOrDefault(e => e.Type.Equals("FilmDamage", StringComparison.OrdinalIgnoreCase));
            if (dmg != null)
            {
                damage = new FilmDamage();
                damage.Configure(dmg.Values, Verbose);
            }

            // [Screen]: scanlines, RGB mask, refresh bar, snow, CRT bezel
            screen?.Destroy();
            screen = null;
            var scr = preset.Effects.FirstOrDefault(e => e.Type.Equals("Screen", StringComparison.OrdinalIgnoreCase));
            if (scr != null)
            {
                screen = new ScreenOverlay();
                screen.Configure(scr.Values, Verbose);
            }

            // Post-processing effects through the game's pipeline
            int n = 0;
            if (EnsureBackend())
            {
                var pipelineOnly = new Preset { Name = preset.Name, FilePath = preset.FilePath };
                pipelineOnly.Effects.AddRange(preset.Effects.Where(e => e != dmg && e != scr));
                n = backend.Apply(pipelineOnly, Verbose);
            }

            weight = FadeSeconds > 0 ? 0f : 1f;
            FadeTo(1f);
            Log($"{preset.Name} on ({n} settings, {PipelineName}{(damage != null ? " + film damage" : "")}{(screen != null ? " + screen" : "")})");
            Toast.Show(preset.Name);
        }

        public static void Off()
        {
            if (Current == null) return;
            Log($"{Current.Name} off");
            Toast.Show($"{Current.Name} off");
            Current = null;
            FadeTo(0f);
        }

        /// <summary>Turn the named preset on, or off if it is the current one.</summary>
        public static void Toggle(string name)
        {
            if (Current != null && Current.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) Off();
            else Apply(name);
        }

        /// <summary>Next/previous preset in <see cref="AllPresets"/>; "off" sits between the last and the first.</summary>
        public static void Cycle(int dir = 1)
        {
            var all = AllPresets();
            int i = Current == null ? -1 : all.FindIndex(p => p.Name == Current.Name);
            int next = i + dir;
            if (next < -1) next = all.Count - 1;
            if (next == -1 || next >= all.Count) Off(); else Apply(all[next]);
        }

        /// <summary>Re-read the current preset (its .ini, or the built-in list) and apply it again.</summary>
        public static void Reload()
        {
            if (Current == null) return;
            Apply(Current.FilePath != null && File.Exists(Current.FilePath) ? Preset.Load(Current.FilePath) : Find(Current.Name) ?? Current);
        }

        /// <summary>Call every frame (the LibriaMods loader does this for you).</summary>
        public static void Update()
        {
            Toast.Tick();

            if (!Mathf.Approximately(weight, targetWeight))
                weight = Mathf.MoveTowards(weight, targetWeight, fadeSpeed * Time.unscaledDeltaTime);

            if (backend != null) backend.Weight = weight;

            if (damage != null)
            {
                damage.Opacity = weight;
                if (Current == null && weight <= 0f) { damage.Destroy(); damage = null; }
                else damage.Tick(Time.unscaledDeltaTime, Log);
            }

            if (screen != null)
            {
                screen.Opacity = weight;
                if (Current == null && weight <= 0f) { screen.Destroy(); screen = null; }
                else screen.Tick(Time.unscaledDeltaTime, Log);
            }
        }

        public static void Shutdown()
        {
            Toast.Destroy();
            damage?.Destroy();
            damage = null;
            screen?.Destroy();
            screen = null;
            backend?.Destroy();
            backend = null;
        }

        static void FadeTo(float w)
        {
            targetWeight = w;
            fadeSpeed = FadeSeconds > 0 ? 1f / FadeSeconds : float.PositiveInfinity;
        }

        static bool EnsureBackend()
        {
            if (backend != null) return true;
            if (detectFailed) return false;
            backend = Backend.Detect(Log);
            detectFailed = backend == null;
            if (backend != null) Verbose($"Render pipeline: {backend.Name}");
            return backend != null;
        }
    }
}
