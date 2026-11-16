using System;
using System.Collections.Generic;
using UnityEngine;

namespace LibriaMods.Unity.Shared
{
    /// <summary>
    /// Old TV / video screen drawn as a full-screen overlay above the 3D scene and below the game's UI:
    /// scanlines, an RGB aperture-grille mask, a slow refresh bar, video snow and a rounded CRT bezel.
    /// Works in any Unity game (no shaders, no render pipeline needed).
    /// Driven by <see cref="Fx"/> through a [Screen] section in a preset.
    /// </summary>
    public sealed class ScreenOverlay
    {
        // ---- settings (0 = off)
        public float Scanlines = 0.3f;    // darkness of the scanlines (0..1)
        public float Lines = 270f;        // scanlines over the screen height (rounded to whole pixels per line)
        public float Mask = 0f;           // RGB aperture-grille stripes (0..1)
        public float MaskSize = 3f;       // screen pixels per RGB triad
        public float Roll = 0f;           // brightness of the slow refresh bar (0..1)
        public float RollSpeed = 0.12f;   // screen heights per second
        public float Snow = 0f;           // video noise (0..1)
        public float SnowFps = 24f;
        public float Bezel = 0f;          // rounded black corners of a curved tube (0..1 = corner radius)
        public int SortingOrder = -90;    // overlay canvas order: above FilmDamage, below the game's UI

        /// <summary>0..1, multiplied in by Fx for fading.</summary>
        public float Opacity = 1f;

        GameObject root;
        Component lineImg, maskImg, rollImg, snowImg, bezelImg;
        Texture2D lineTex, maskTex, rollTex, snowTex, bezelTex;
        Color32[] snowPx;
        Type rawImageType;
        System.Random rng = new System.Random();
        float rollY, snowTimer;
        int lineScreenH, linePx;
        bool built;

        public void Configure(IEnumerable<KeyValuePair<string, string>> values, Action<string> verbose)
        {
            foreach (var kv in values)
            {
                var f = GetType().GetField(kv.Key, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
                if (f == null || f.Name == nameof(Opacity)) { verbose($"    Screen.{kv.Key}: unknown setting"); continue; }
                try
                {
                    f.SetValue(this, f.FieldType == typeof(int) ? (object)(int)Preset.F(kv.Value) : Preset.F(kv.Value));
                    verbose($"    Screen.{f.Name} = {kv.Value}");
                }
                catch { verbose($"    Screen.{kv.Key} = {kv.Value}: not a number"); }
            }
        }

        bool Build(Action<string> log)
        {
            rawImageType = FindType("UnityEngine.UI.RawImage");
            if (rawImageType == null) { log("Screen overlay needs Unity UI (UnityEngine.UI), not found in this game."); return false; }

            root = new GameObject("LibriaMods Screen");
            UnityEngine.Object.DontDestroyOnLoad(root);
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;

            if (Snow > 0)
            {
                snowTex = new Texture2D(480, 270, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Repeat };
                snowPx = new Color32[480 * 270];
                snowImg = AddImage("Snow", snowTex, Color.white);
            }
            if (Roll > 0)
            {
                // soft bright bump in the middle of a tall strip; scrolled through uvRect
                var px = new Color32[256];
                for (int y = 0; y < 256; y++)
                {
                    float d = (y - 128) / 14f;
                    px[y] = new Color32(255, 255, 255, (byte)(255 * Mathf.Exp(-d * d)));
                }
                rollTex = Tex(1, 256, px, FilterMode.Bilinear, TextureWrapMode.Clamp);
                rollImg = AddImage("Roll", rollTex, Color.white);
            }
            if (Scanlines > 0) lineImg = AddImage("Scanlines", null, Color.white); // texture built in Tick (follows the screen height)
            if (Mask > 0)
            {
                var px = new[] { new Color32(255, 40, 40, 255), new Color32(40, 255, 40, 255), new Color32(40, 40, 255, 255) };
                maskTex = Tex(3, 1, px, FilterMode.Point, TextureWrapMode.Repeat);
                maskImg = AddImage("Mask", maskTex, Color.white);
            }
            if (Bezel > 0)
            {
                bezelTex = BezelTexture(Mathf.Clamp01(Bezel));
                bezelImg = AddImage("Bezel", bezelTex, Color.white);
            }
            built = true;
            return true;
        }

        public void Destroy()
        {
            if (root != null) UnityEngine.Object.Destroy(root);
            foreach (var t in new[] { lineTex, maskTex, rollTex, snowTex, bezelTex })
                if (t != null) UnityEngine.Object.Destroy(t);
            root = null; lineTex = maskTex = rollTex = snowTex = bezelTex = null;
            lineImg = maskImg = rollImg = snowImg = bezelImg = null;
            built = false;
        }

        public void Tick(float dt, Action<string> log)
        {
            if (!built && !Build(log)) return;

            float a = Mathf.Clamp01(Opacity);
            root.SetActive(a > 0.001f);
            if (a <= 0.001f) return;

            if (lineImg != null)
            {
                // Whole screen pixels per line, point-sampled: anything else beats against the pixel grid
                // and shows up as broad bright/dark bands (moiré).
                if (lineScreenH != Screen.height || lineTex == null)
                {
                    lineScreenH = Screen.height;
                    linePx = Mathf.Max(2, Mathf.RoundToInt(Screen.height / Mathf.Max(1f, Lines)));
                    var px = new Color32[linePx];
                    for (int i = 0; i < linePx; i++) px[i] = new Color32(0, 0, 0, 0);
                    px[0] = new Color32(0, 0, 0, 255);                         // the dark gap between lines
                    if (linePx >= 4) px[1] = px[linePx - 1] = new Color32(0, 0, 0, 110);
                    if (lineTex != null) UnityEngine.Object.Destroy(lineTex);
                    lineTex = Tex(1, linePx, px, FilterMode.Point, TextureWrapMode.Repeat);
                    rawImageType.GetProperty("texture")?.SetValue(lineImg, lineTex, null);
                }
                SetUv(lineImg, new Rect(0, 0, 1, Screen.height / (float)linePx));
                SetColor(lineImg, new Color(1, 1, 1, Mathf.Clamp01(Scanlines) * a));
            }
            if (maskImg != null)
            {
                SetUv(maskImg, new Rect(0, 0, Screen.width / Mathf.Max(1f, MaskSize), 1));
                SetColor(maskImg, new Color(1, 1, 1, Mathf.Clamp01(Mask) * 0.12f * a));
            }
            if (rollImg != null)
            {
                // one soft bar travelling from above the top to below the bottom, then again
                rollY = (rollY + RollSpeed * dt) % 1.4f;
                const float h = 3f;                       // screen = 3 texture heights → thin bar
                float v = 1.2f - rollY;                   // bar position in screen space (1 = top)
                SetUv(rollImg, new Rect(0, 0.5f - v * h, 1, h));
                SetColor(rollImg, new Color(1, 1, 1, Mathf.Clamp01(Roll) * 0.10f * a));
            }
            if (snowImg != null)
            {
                snowTimer += dt;
                if (snowTimer >= 1f / Mathf.Max(1f, SnowFps))
                {
                    snowTimer = 0;
                    for (int i = 0; i < snowPx.Length; i++)
                    {
                        byte v = (byte)rng.Next(256);
                        snowPx[i] = new Color32(v, v, v, (byte)rng.Next(256));
                    }
                    snowTex.SetPixels32(snowPx); snowTex.Apply(false);
                }
                SetColor(snowImg, new Color(1, 1, 1, Mathf.Clamp01(Snow) * 0.25f * a));
            }
            if (bezelImg != null) SetColor(bezelImg, new Color(0, 0, 0, a));
        }

        // ------------------------------------------------------------------ helpers

        static Texture2D BezelTexture(float radius)
        {
            const int W = 512, H = 288;
            float r = radius * 0.5f * H;           // corner radius in texels
            float edge = 3f;                       // anti-aliased edge
            float shade = 0.06f * H;               // soft inner shadow along the glass edge
            var px = new Color32[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    // signed distance to a rounded rectangle covering the whole texture
                    float qx = Mathf.Abs(x + 0.5f - W / 2f) - (W / 2f - r);
                    float qy = Mathf.Abs(y + 0.5f - H / 2f) - (H / 2f - r);
                    float outside = new Vector2(Mathf.Max(qx, 0), Mathf.Max(qy, 0)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0) - r;
                    float alpha = Mathf.Clamp01((outside + edge) / edge);                  // 1 outside the glass
                    alpha = Mathf.Max(alpha, 0.35f * Mathf.Clamp01(1 + outside / shade)); // inner shadow
                    px[y * W + x] = new Color32(255, 255, 255, (byte)(alpha * 255));
                }
            return Tex(W, H, px, FilterMode.Bilinear, TextureWrapMode.Clamp);
        }

        static Texture2D Tex(int w, int h, Color32[] px, FilterMode filter, TextureWrapMode wrap)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = filter, wrapMode = wrap };
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
            rawImageType.GetProperty("raycastTarget")?.SetValue(img, false, null);
            SetColor(img, color);
            return img;
        }

        void SetColor(Component img, Color c) => rawImageType.GetProperty("color")?.SetValue(img, c, null);
        void SetUv(Component img, Rect r) => rawImageType.GetProperty("uvRect")?.SetValue(img, r, null);

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
