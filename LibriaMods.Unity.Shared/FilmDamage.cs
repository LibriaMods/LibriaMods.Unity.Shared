using System;
using System.Collections.Generic;
using UnityEngine;

namespace LibriaMods.Unity.Shared
{
    /// <summary>
    /// Procedural old-film damage drawn as a full-screen overlay above the 3D scene and below the game's UI:
    /// moving tracking bands, tracking rolls, wandering scratches, dust, hairs, cigarette burns that bloom
    /// and fade, flicker and exposure pops. Works in any Unity game (no shaders, no render pipeline needed).
    ///
    /// The damage is generated on the CPU at a low resolution and film-like frame rate as a signed layer
    /// (lighten / darken), shown through two UI images: white for the light parts, black for the dark ones.
    /// Driven by <see cref="Fx"/> through a [FilmDamage] section in a preset.
    /// </summary>
    public sealed class FilmDamage
    {
        // ---- settings (0 = off, 1 = default strength; values above 1 allowed)
        public float Amount = 1f;       // master strength
        public float Bands = 1f;        // moving tracking bands
        public float Rolls = 1f;        // bright bar sweeping the frame
        public float Scratches = 1f;    // vertical scratches
        public float Dust = 1f;         // specks
        public float Hairs = 1f;        // the odd hair
        public float Burns = 1f;        // cigarette burns
        public float Flicker = 1f;      // constant light flicker
        public float Pops = 1f;         // occasional whole-frame exposure jumps
        public float Fps = 14f;         // damage frame rate (film-like stutter)
        public int Width = 640;         // damage layer resolution (height follows the screen aspect)
        public int SortingOrder = -100; // overlay canvas order: below the game's UI canvases
        public int Seed = 0;            // 0 = random

        /// <summary>0..1, multiplied in by Fx for fading.</summary>
        public float Opacity = 1f;

        int W, H;
        float scale;                    // sizes/speeds relative to the original 960x540 design
        float[] fr;
        Color32[] lightPx, darkPx;
        Texture2D lightTex, darkTex;
        float[][] burnTex;
        float[][] rowNoise;
        System.Random rng;

        GameObject root;
        Component lightImg, darkImg, flickerImg;
        Type rawImageType;
        float timer, flickerValue;
        bool built;

        readonly List<Band> bands = new List<Band>();
        readonly List<Scratch> scratches = new List<Scratch>();
        readonly List<Burn> burns = new List<Burn>();
        Roll roll;

        class Band { public float y, v, h, a; public int life, row; }
        class Scratch { public float x, a, y0, y1; public int life; }
        class Burn { public float cx, cy, R; public int age, dur, tex; }
        class Roll { public float y, v; }

        public bool IsBuilt => built;

        // ------------------------------------------------------------------ settings

        /// <summary>Apply a [FilmDamage] preset section (key = field name, case-insensitive).</summary>
        public void Configure(IEnumerable<KeyValuePair<string, string>> values, Action<string> verbose)
        {
            foreach (var kv in values)
            {
                var f = GetType().GetField(kv.Key, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
                if (f == null || f.Name == nameof(Opacity)) { verbose($"    FilmDamage.{kv.Key}: unknown setting"); continue; }
                try
                {
                    f.SetValue(this, f.FieldType == typeof(int) ? (object)(int)Preset.F(kv.Value) : Preset.F(kv.Value));
                    verbose($"    FilmDamage.{f.Name} = {kv.Value}");
                }
                catch { verbose($"    FilmDamage.{kv.Key} = {kv.Value}: not a number"); }
            }
        }

        // ------------------------------------------------------------------ lifecycle

        bool Build(Action<string> log)
        {
            rawImageType = FindType("UnityEngine.UI.RawImage");
            if (rawImageType == null) { log("FilmDamage needs Unity UI (UnityEngine.UI), not found in this game."); return false; }

            W = Mathf.Clamp(Width, 160, 1920);
            float aspect = Screen.height > 0 ? (float)Screen.width / Screen.height : 16f / 9f;
            H = Mathf.Max(90, Mathf.RoundToInt(W / aspect));
            scale = W / 960f;

            rng = Seed != 0 ? new System.Random(Seed) : new System.Random();
            fr = new float[W * H];
            lightPx = Blank(); darkPx = Blank();
            lightTex = NewTex(lightPx); darkTex = NewTex(darkPx);
            burnTex = new[] { SmoothNoise(24), SmoothNoise(24), SmoothNoise(24) };
            rowNoise = new float[64][];
            for (int i = 0; i < 64; i++) { rowNoise[i] = new float[W]; for (int x = 0; x < W; x++) rowNoise[i][x] = Gauss(); }

            root = new GameObject("LibriaMods FilmDamage");
            UnityEngine.Object.DontDestroyOnLoad(root);
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;

            flickerImg = AddImage("Flicker", null, Color.clear);
            darkImg = AddImage("Dark", darkTex, Color.black);
            lightImg = AddImage("Light", lightTex, Color.white);
            built = true;
            return true;
        }

        public void Destroy()
        {
            if (root != null) UnityEngine.Object.Destroy(root);
            if (lightTex != null) UnityEngine.Object.Destroy(lightTex);
            if (darkTex != null) UnityEngine.Object.Destroy(darkTex);
            root = null; lightTex = darkTex = null; built = false;
            bands.Clear(); scratches.Clear(); burns.Clear(); roll = null;
        }

        /// <summary>Call every frame while active.</summary>
        public void Tick(float dt, Action<string> log)
        {
            if (!built && !Build(log)) return;

            float alpha = Mathf.Clamp01(Opacity);
            root.SetActive(alpha > 0.001f);
            if (alpha <= 0.001f) return;

            timer += dt;
            float step = 1f / Mathf.Max(1f, Fps);
            if (timer >= step)
            {
                timer %= step;
                GenerateFrame();
                flickerValue = (float)(rng.NextDouble() * 2 - 1) * 0.035f * Flicker;
                if (Pops > 0 && rng.NextDouble() < 0.02 * Pops) flickerValue += Sign() * Range(0.04f, 0.11f);
            }

            float a = alpha * Amount;
            SetColor(lightImg, new Color(1, 1, 1, Mathf.Clamp01(a)));
            SetColor(darkImg, new Color(0, 0, 0, Mathf.Clamp01(a)));
            SetColor(flickerImg, flickerValue >= 0
                ? new Color(1, 1, 1, Mathf.Clamp01(flickerValue * alpha))
                : new Color(0, 0, 0, Mathf.Clamp01(-flickerValue * alpha)));
        }

        // ------------------------------------------------------------------ damage generation
        // Port of make_damage.py (CinemaCove "botched film" layer); values are in 0..255 "grey levels".

        void GenerateFrame()
        {
            Array.Clear(fr, 0, fr.Length);
            float s = scale;

            // tracking bands: spawn, drift up or down at random speeds, wobble, die
            if (Bands > 0 && bands.Count < 3 && rng.NextDouble() < 0.10 * Bands)
                bands.Add(new Band
                {
                    y = Range(0, H), v = Sign() * Range(40, 420) / 14f * s, h = Range(2, 26) * s,
                    a = (rng.NextDouble() < 0.66 ? 1 : -1) * Range(30, 80), life = rng.Next(6, 50), row = rng.Next(64)
                });
            foreach (var b in bands)
            {
                b.y = Mod(b.y + b.v + Gauss() * 2 * s, H);
                int y0 = Mathf.FloorToInt(b.y - b.h), y1 = Mathf.CeilToInt(b.y + b.h);
                var streak = rowNoise[rng.Next(64)];
                for (int yy = y0; yy <= y1; yy++)
                {
                    float d = Mathf.Abs(yy - b.y);
                    float prof = Mathf.Pow(Mathf.Clamp01(1 - d / b.h), 1.5f) * b.a * Bands;
                    if (prof == 0) continue;
                    int row = ((yy % H) + H) % H * W;
                    for (int x = 0; x < W; x++) fr[row + x] += prof * (0.6f + 0.4f * streak[x]);
                }
                b.life--;
            }
            bands.RemoveAll(b => b.life <= 0);

            // tracking roll: a bright smeared bar that sweeps the whole frame fast
            if (Rolls > 0 && roll == null && rng.NextDouble() < 0.012 * Rolls) roll = new Roll { y = -60 * s, v = Range(60, 130) * s };
            if (roll != null)
            {
                var streak = rowNoise[rng.Next(64)];
                for (int yy = Mathf.Max(0, (int)(roll.y - 45 * s)); yy < Mathf.Min(H, (int)(roll.y + 70 * s)); yy++)
                {
                    float bright = 70 * Mathf.Clamp01(1 - Mathf.Abs(yy - roll.y) / (45 * s));
                    float dark = 25 * Mathf.Clamp01(1 - Mathf.Abs(yy - roll.y - 55 * s) / (10 * s));
                    int row = yy * W;
                    for (int x = 0; x < W; x++) fr[row + x] += (bright * (0.7f + 0.3f * streak[x]) - dark) * Rolls;
                }
                roll.y += roll.v;
                if (roll.y > H + 60 * s) roll = null;
            }

            // vertical scratches that live a few frames and wander
            if (Scratches > 0 && rng.NextDouble() < 0.25 * Scratches)
                scratches.Add(new Scratch
                {
                    x = Range(0, W), life = rng.Next(2, 14), a = Sign() * Range(25, 70),
                    y0 = Range(-0.2f, 0.5f) * H, y1 = Range(0.5f, 1.2f) * H
                });
            foreach (var sc in scratches)
            {
                sc.x += Gauss() * 1.5f * s;
                int x = ((int)sc.x % W + W) % W;
                int y0 = Mathf.Max(0, (int)sc.y0), y1 = Mathf.Min(H, (int)sc.y1);
                for (int yy = y0; yy < y1; yy++)
                {
                    fr[yy * W + x] += sc.a;
                    if (x + 1 < W) fr[yy * W + x + 1] += sc.a * 0.4f;
                }
                sc.life--;
            }
            scratches.RemoveAll(sc => sc.life <= 0);

            // dust and specks, fresh every frame
            if (Dust > 0)
            {
                int n = Mathf.RoundToInt(rng.Next(4, 22) * Dust);
                for (int i = 0; i < n; i++)
                {
                    float cx = Range(0, W), cy = Range(0, H), r = Range(0.8f, 3.5f) * s, a = Sign() * Range(60, 120);
                    for (int yy = Mathf.Max(0, (int)(cy - r - 1)); yy < Mathf.Min(H, (int)(cy + r + 2)); yy++)
                        for (int xx = Mathf.Max(0, (int)(cx - r - 1)); xx < Mathf.Min(W, (int)(cx + r + 2)); xx++)
                            if ((xx - cx) * (xx - cx) + (yy - cy) * (yy - cy) < r * r) fr[yy * W + xx] += a;
                }
            }

            // the odd hair
            if (Hairs > 0 && rng.NextDouble() < 0.08 * Hairs)
            {
                float cx = Range(0, W), cy = Range(0, H), ang = Range(0, Mathf.PI), L = Range(20, 70) * s, a = Sign() * 90;
                for (int i = 0; i < 200; i++)
                {
                    float t = i / 199f;
                    int px = (((int)(cx + Mathf.Cos(ang) * L * t + 8 * s * Mathf.Sin(t * 6 + ang))) % W + W) % W;
                    int py = (((int)(cy + Mathf.Sin(ang) * L * t + 8 * s * Mathf.Cos(t * 5))) % H + H) % H;
                    fr[py * W + px] += a;
                }
            }

            // burns: bloom from a point, white-hot centre, dark scorched rim, then fade
            if (Burns > 0 && burns.Count < 2 && rng.NextDouble() < 0.035 * Burns)
                burns.Add(new Burn
                {
                    cx = Range(0.1f, 0.9f) * W, cy = Range(0.1f, 0.9f) * H, age = 0,
                    dur = rng.Next(5, 12), R = Range(25, 90) * s, tex = rng.Next(3)
                });
            foreach (var b in burns)
            {
                float prog = (float)b.age / b.dur;
                float r = b.R * (0.25f + 0.95f * prog);
                float fade = prog < 0.7f ? 1f : Mathf.Max(0, 1 - (prog - 0.7f) / 0.3f);
                var tex = burnTex[b.tex];
                float reach = r * 1.6f;
                for (int yy = Mathf.Max(0, (int)(b.cy - reach)); yy < Mathf.Min(H, (int)(b.cy + reach)); yy++)
                    for (int xx = Mathf.Max(0, (int)(b.cx - reach)); xx < Mathf.Min(W, (int)(b.cx + reach)); xx++)
                    {
                        int i = yy * W + xx;
                        float d = Mathf.Sqrt((xx - b.cx) * (xx - b.cx) + (yy - b.cy) * (yy - b.cy)) + tex[i] * r * 0.35f;
                        float core = Mathf.Clamp01(1 - d / r);
                        float rim = Mathf.Clamp01(1 - Mathf.Abs(d - r) / (r * 0.18f));
                        fr[i] += fade * (190 * Mathf.Pow(core, 0.6f) - 95 * rim) * Burns;
                    }
                b.age++;
            }
            burns.RemoveAll(b => b.age > b.dur);

            // signed layer → light (white, alpha) and dark (black, alpha) textures
            const float k = 1.4f / 160f;
            for (int i = 0; i < fr.Length; i++)
            {
                float v = fr[i] * k;
                lightPx[i].a = v > 0 ? (byte)(Mathf.Min(1f, v) * 255) : (byte)0;
                darkPx[i].a = v < 0 ? (byte)(Mathf.Min(1f, -v) * 255) : (byte)0;
            }
            lightTex.SetPixels32(lightPx); lightTex.Apply(false);
            darkTex.SetPixels32(darkPx); darkTex.Apply(false);
        }

        // ------------------------------------------------------------------ helpers

        Color32[] Blank()
        {
            var px = new Color32[W * H];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 0); // rgb stays white; alpha carries the damage
            return px;
        }

        Texture2D NewTex(Color32[] px)
        {
            var t = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            t.SetPixels32(px); t.Apply(false);
            return t;
        }

        Component AddImage(string name, Texture tex, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(root.transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var img = go.AddComponent(rawImageType);
            rawImageType.GetProperty("texture")?.SetValue(img, tex, null);
            rawImageType.GetProperty("raycastTarget")?.SetValue(img, false, null); // never block clicks
            SetColor(img, color);
            return img;
        }

        void SetColor(Component img, Color c) => rawImageType.GetProperty("color")?.SetValue(img, c, null);

        float[] SmoothNoise(int cell)
        {
            int gw = W / cell + 2, gh = H / cell + 2;
            var g = new float[gw * gh];
            for (int i = 0; i < g.Length; i++) g[i] = Gauss();
            var o = new float[W * H];
            double sum = 0, sum2 = 0;
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float fx = (float)x / cell, fy = (float)y / cell;
                    int ix = (int)fx, iy = (int)fy; float tx = fx - ix, ty = fy - iy;
                    tx = tx * tx * (3 - 2 * tx); ty = ty * ty * (3 - 2 * ty);
                    float a = g[iy * gw + ix], b = g[iy * gw + ix + 1], c = g[(iy + 1) * gw + ix], d = g[(iy + 1) * gw + ix + 1];
                    float v = Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
                    o[y * W + x] = v; sum += v; sum2 += v * v;
                }
            float std = (float)Math.Sqrt(Math.Max(1e-6, sum2 / o.Length - (sum / o.Length) * (sum / o.Length)));
            for (int i = 0; i < o.Length; i++) o[i] /= std;
            return o;
        }

        float Range(float a, float b) => a + (float)rng.NextDouble() * (b - a);
        int Sign() => rng.NextDouble() < 0.5 ? -1 : 1;
        float Gauss()
        {
            double u1 = 1.0 - rng.NextDouble(), u2 = rng.NextDouble();
            return (float)(Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2));
        }
        static float Mod(float a, float m) => ((a % m) + m) % m;

        static Type FindType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t;
                try { t = asm.GetType(fullName, false); } catch { continue; }
                if (t != null) return t;
            }
            return null;
        }
    }
}
