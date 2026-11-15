using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace LibriaMods.Shared
{
    /// <summary>
    /// Talks to the game's post-processing system purely through reflection, so LibriaMods.Shared has no
    /// compile-time dependency on any render pipeline and loads in any Unity (Mono) game.
    /// </summary>
    internal abstract class Backend
    {
        public abstract string Name { get; }
        protected GameObject go;
        protected Component volume;
        protected object profile;

        public static Backend Detect(Action<string> log)
        {
            if (Srp.TryCreate(out var srp)) return srp;
            if (PpV2.TryCreate(out var pp)) return pp;
            log("No supported post-processing found (needs HDRP, URP or Post Processing Stack v2).");
            return null;
        }

        /// <summary>Replace the volume's effects with the preset. Returns the number of parameters set.</summary>
        public abstract int Apply(Preset preset, Action<string> verbose);

        public abstract float Weight { get; set; }

        public virtual void Destroy()
        {
            if (go != null) UnityEngine.Object.Destroy(go);
            go = null; volume = null; profile = null;
        }

        protected void EnsureObject(string name)
        {
            if (go != null) return;
            go = new GameObject(name);
            UnityEngine.Object.DontDestroyOnLoad(go);
        }

        // ------------------------------------------------------------------ helpers

        protected static Type FindType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t;
                try { t = asm.GetType(fullName, false); } catch { continue; }
                if (t != null) return t;
            }
            return null;
        }

        protected static Type FindEffectType(string name, Type baseType, params string[] namespaces)
        {
            foreach (var ns in namespaces)
            {
                var t = FindType(ns + "." + name);
                if (t != null && baseType.IsAssignableFrom(t)) return t;
            }
            return null;
        }

        /// <summary>Set a VolumeParameter&lt;T&gt; / ParameterOverride&lt;T&gt; field on an effect from a string.</summary>
        protected static bool SetParam(object effect, string field, string raw, Action<string> verbose)
        {
            var f = effect.GetType().GetField(field, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (f == null) { verbose($"    {effect.GetType().Name}.{field}: no such parameter"); return false; }
            var param = f.GetValue(effect);
            if (param == null) return false;

            // Walk up to the generic VolumeParameter<T> / ParameterOverride<T> to learn T.
            Type valueType = null;
            for (var t = param.GetType(); t != null; t = t.BaseType)
                if (t.IsGenericType && t.GetGenericArguments().Length == 1) { valueType = t.GetGenericArguments()[0]; break; }
            if (valueType == null) { verbose($"    {field}: unsupported parameter type {param.GetType().Name}"); return false; }

            object value;
            try { value = Convert(raw, valueType); }
            catch (Exception e) { verbose($"    {field} = {raw}: can't convert to {valueType.Name} ({e.Message})"); return false; }

            var ov = param.GetType().GetMethod("Override", new[] { valueType });
            if (ov != null) ov.Invoke(param, new[] { value });
            else
            {
                param.GetType().GetProperty("value")?.SetValue(param, value, null);
                param.GetType().GetProperty("overrideState")?.SetValue(param, true, null);
                param.GetType().GetField("overrideState")?.SetValue(param, true);
            }
            verbose($"    {effect.GetType().Name}.{field} = {raw}");
            return true;
        }

        static object Convert(string s, Type t)
        {
            s = s.Trim();
            if (t == typeof(float)) return Preset.F(s);
            if (t == typeof(int)) return int.Parse(s, CultureInfo.InvariantCulture);
            if (t == typeof(bool)) return s == "1" || s.Equals("true", StringComparison.OrdinalIgnoreCase) || s.Equals("on", StringComparison.OrdinalIgnoreCase);
            if (t.IsEnum) return Enum.Parse(t, s, true);
            if (t == typeof(Color)) return ParseColor(s);
            var n = s.Split(',').Select(Preset.F).ToArray();
            if (t == typeof(Vector2)) return new Vector2(n[0], n[1]);
            if (t == typeof(Vector3)) return new Vector3(n[0], n[1], n[2]);
            if (t == typeof(Vector4)) return new Vector4(n[0], n[1], n[2], n.Length > 3 ? n[3] : 0f);
            throw new NotSupportedException(t.Name);
        }

        static Color ParseColor(string s)
        {
            if (s.StartsWith("#"))
            {
                var h = s.Substring(1);
                float B(int i) => int.Parse(h.Substring(i, 2), NumberStyles.HexNumber) / 255f;
                return new Color(B(0), B(2), B(4), h.Length >= 8 ? B(6) : 1f);
            }
            var n = s.Split(',').Select(Preset.F).ToArray();
            return new Color(n[0], n[1], n[2], n.Length > 3 ? n[3] : 1f);
        }
    }

    // ====================================================================== HDRP / URP

    internal sealed class Srp : Backend
    {
        static readonly string[] Namespaces = { "UnityEngine.Rendering.HighDefinition", "UnityEngine.Rendering.Universal" };

        Type volumeType, profileType, componentType;
        string pipeline;
        public override string Name => pipeline;

        public static bool TryCreate(out Srp srp)
        {
            srp = null;
            var vol = FindType("UnityEngine.Rendering.Volume");
            var prof = FindType("UnityEngine.Rendering.VolumeProfile");
            var comp = FindType("UnityEngine.Rendering.VolumeComponent");
            if (vol == null || prof == null || comp == null) return false;

            string p = FindType("UnityEngine.Rendering.HighDefinition.HDRenderPipeline") != null ? "HDRP"
                     : FindType("UnityEngine.Rendering.Universal.UniversalRenderPipeline") != null ? "URP" : "SRP";
            srp = new Srp { volumeType = vol, profileType = prof, componentType = comp, pipeline = p };
            return true;
        }

        public override int Apply(Preset preset, Action<string> verbose)
        {
            EnsureObject("LibriaMods FX Volume");
            if (volume == null)
            {
                volume = go.AddComponent(volumeType);
                volumeType.GetField("isGlobal").SetValue(volume, true);
                volumeType.GetField("priority").SetValue(volume, 10000f); // above anything the game uses
            }

            // Fresh profile every time, so effects from the previous preset don't linger.
            if (profile is ScriptableObject old) UnityEngine.Object.Destroy(old);
            profile = ScriptableObject.CreateInstance(profileType);
            volumeType.GetField("sharedProfile").SetValue(volume, profile);

            var add = profileType.GetMethod("Add", new[] { typeof(Type), typeof(bool) });
            int count = 0;
            foreach (var fx in preset.Effects)
            {
                var t = FindEffectType(fx.Type, componentType, Namespaces);
                if (t == null) { verbose($"  [{fx.Type}] not available in {pipeline}, skipped"); continue; }
                var comp = add.Invoke(profile, new object[] { t, false });
                foreach (var kv in fx.Values)
                    if (SetParam(comp, kv.Key, kv.Value, verbose)) count++;
            }
            return count;
        }

        public override float Weight
        {
            get => volume == null ? 0f : (float)volumeType.GetField("weight").GetValue(volume);
            set { if (volume != null) volumeType.GetField("weight").SetValue(volume, value); }
        }

        public override void Destroy()
        {
            if (profile is ScriptableObject so) UnityEngine.Object.Destroy(so);
            base.Destroy();
        }
    }

    // ====================================================================== Post Processing Stack v2

    internal sealed class PpV2 : Backend
    {
        const string Ns = "UnityEngine.Rendering.PostProcessing";
        Type volumeType, profileType, settingsType, layerType;
        public override string Name => "Post Processing v2";

        public static bool TryCreate(out PpV2 pp)
        {
            pp = null;
            var vol = FindType(Ns + ".PostProcessVolume");
            var prof = FindType(Ns + ".PostProcessProfile");
            var set = FindType(Ns + ".PostProcessEffectSettings");
            if (vol == null || prof == null || set == null) return false;
            pp = new PpV2 { volumeType = vol, profileType = prof, settingsType = set, layerType = FindType(Ns + ".PostProcessLayer") };
            return true;
        }

        public override int Apply(Preset preset, Action<string> verbose)
        {
            EnsureObject("LibriaMods FX Volume");
            if (volume == null)
            {
                volume = go.AddComponent(volumeType);
                volumeType.GetField("isGlobal").SetValue(volume, true);
                volumeType.GetField("priority").SetValue(volume, 10000f);
            }
            MatchCameraLayer(verbose);

            if (profile is ScriptableObject old) UnityEngine.Object.Destroy(old);
            profile = ScriptableObject.CreateInstance(profileType);
            volumeType.GetField("sharedProfile").SetValue(volume, profile);

            var add = profileType.GetMethod("AddSettings", new[] { typeof(Type) });
            int count = 0;
            foreach (var fx in preset.Effects)
            {
                var t = FindEffectType(fx.Type, settingsType, Ns);
                if (t == null) { verbose($"  [{fx.Type}] not available in PPv2, skipped"); continue; }
                var s = add.Invoke(profile, new object[] { t });
                SetParam(s, "enabled", "true", _ => { });
                foreach (var kv in fx.Values)
                    if (SetParam(s, kv.Key, kv.Value, verbose)) count++;
            }
            return count;
        }

        // PPv2 only blends volumes on layers included in the camera's PostProcessLayer.volumeLayer mask.
        void MatchCameraLayer(Action<string> verbose)
        {
            var cam = Camera.main;
            if (cam == null || layerType == null) return;
            var ppl = cam.GetComponent(layerType);
            if (ppl == null) { verbose("  Main camera has no PostProcessLayer: effects won't show in this game."); return; }
            var mask = (LayerMask)layerType.GetField("volumeLayer").GetValue(ppl);
            for (int i = 0; i < 32; i++)
                if ((mask.value & (1 << i)) != 0) { go.layer = i; return; }
        }

        public override float Weight
        {
            get => volume == null ? 0f : (float)volumeType.GetField("weight").GetValue(volume);
            set { if (volume != null) volumeType.GetField("weight").SetValue(volume, value); }
        }

        public override void Destroy()
        {
            if (profile is ScriptableObject so) UnityEngine.Object.Destroy(so);
            base.Destroy();
        }
    }
}
