using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace LibriaMods.Shared
{
    /// <summary>
    /// A named set of post-processing overrides, grouped by effect (Unity component class name).
    /// File format (UTF-8 .ini):
    ///
    ///   name = Noir
    ///   [ColorAdjustments]        ; HDRP / URP
    ///   saturation = -100
    ///   contrast   = 30
    ///   colorFilter = #FFFFFF
    ///   [ColorGrading]            ; Post Processing Stack v2 (built-in pipeline)
    ///   saturation = -100
    ///
    /// Section = effect class name, keys = public parameter field names. Effects or parameters
    /// the current game's pipeline doesn't have are skipped, so one file can target all pipelines.
    /// </summary>
    public sealed class Preset
    {
        public string Name;
        public string FilePath;
        public readonly List<Effect> Effects = new List<Effect>();

        public sealed class Effect
        {
            public string Type;
            public readonly List<KeyValuePair<string, string>> Values = new List<KeyValuePair<string, string>>();
        }

        public static Preset Parse(string text, string fallbackName = "Preset")
        {
            var p = new Preset { Name = fallbackName };
            Effect cur = null;
            foreach (var raw in text.Split('\n'))
            {
                var line = StripComment(raw).Trim();
                if (line.Length == 0) continue;

                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    cur = new Effect { Type = line.Substring(1, line.Length - 2).Trim() };
                    p.Effects.Add(cur);
                    continue;
                }

                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                var key = line.Substring(0, eq).Trim();
                var val = line.Substring(eq + 1).Trim().Trim('"');

                if (cur == null) { if (key.Equals("name", StringComparison.OrdinalIgnoreCase)) p.Name = val; }
                else cur.Values.Add(new KeyValuePair<string, string>(key, val));
            }
            return p;
        }

        public static Preset Load(string path)
        {
            var p = Parse(File.ReadAllText(path), Path.GetFileNameWithoutExtension(path));
            p.FilePath = path;
            return p;
        }

        static string StripComment(string s)
        {
            // ';' and '#' start comments, except '#' used as a colour (#RRGGBB) after '='.
            int semi = s.IndexOf(';');
            if (semi >= 0) s = s.Substring(0, semi);
            int hash = s.IndexOf('#');
            if (hash >= 0 && (s.IndexOf('=') < 0 || hash < s.IndexOf('='))) s = s.Substring(0, hash);
            return s;
        }

        internal static float F(string s) => float.Parse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture);
    }
}
