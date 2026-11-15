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
    ///   Fx.Apply("Noir");                           // built-in (see <see cref="Presets"/>) or custom .ini
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

        public static Preset Current { get; private set; }
        public static bool IsOn => Current != null;
        public static string PipelineName => backend?.Name ?? "none";

        static Backend backend;
        static bool detectFailed;
        static Preset pending;
        static float targetWeight, fadeSpeed = float.PositiveInfinity;

        // ------------------------------------------------------------------ presets

        /// <summary>Built-in presets plus every .ini in <see cref="CustomPresetsFolder"/> (re-read each call;
        /// a custom file with the same name overrides a built-in one).</summary>
        public static List<Preset> AllPresets()
        {
            var list = new List<Preset>(Presets.BuiltIn);
            if (Directory.Exists(CustomPresetsFolder))
            {
                foreach (var f in Directory.GetFiles(CustomPresetsFolder, "*.ini").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
                {
                    try
                    {
                        var p = Preset.Load(f);
                        list.RemoveAll(x => x.Name.Equals(p.Name, StringComparison.OrdinalIgnoreCase));
                        list.Add(p);
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
            if (p == null) { Log($"Preset '{name}' not found (built-in: {string.Join(", ", Presets.BuiltIn.Select(x => x.Name).ToArray())})"); return false; }
            Apply(p);
            return true;
        }

        public static void Apply(Preset preset)
        {
            Current = preset;
            if (!EnsureBackend()) { pending = preset; return; }
            pending = null;

            int n = backend.Apply(preset, Verbose);
            backend.Weight = FadeSeconds > 0 ? 0f : 1f;
            FadeTo(1f);
            Log($"{preset.Name} on ({n} settings, {backend.Name})");
        }

        public static void Off()
        {
            if (Current == null) return;
            Log($"{Current.Name} off");
            Current = null;
            pending = null;
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

        /// <summary>Re-read the current preset (if it came from a file) and apply it again.</summary>
        public static void Reload()
        {
            if (Current == null) return;
            Apply(Current.FilePath != null && File.Exists(Current.FilePath) ? Preset.Load(Current.FilePath) : Current);
        }

        /// <summary>Call every frame.</summary>
        public static void Update()
        {
            if (pending != null && EnsureBackend()) Apply(pending);
            if (backend == null) return;

            float w = backend.Weight;
            if (Mathf.Approximately(w, targetWeight)) return;
            backend.Weight = Mathf.MoveTowards(w, targetWeight, fadeSpeed * Time.unscaledDeltaTime);
        }

        public static void Shutdown()
        {
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
