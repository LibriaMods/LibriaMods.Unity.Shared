using System;
using UnityEngine;

namespace LibriaMods.Unity.Shared
{
    /// <summary>
    /// Small on-screen message (top centre, fades out), e.g. "Noir" or "Speed: 1.6x".
    /// Uses Unity UI through reflection, so it works in any game that has UnityEngine.UI.
    /// Call <see cref="Tick"/> every frame (the LibriaMods loader does it through Fx.Update).
    /// </summary>
    public static class Toast
    {
        public static bool Enabled = true;
        public static float DefaultSeconds = 1.8f;

        static GameObject root;
        static Component text, background;
        static Type textType, rawImageType;
        static float remaining, total;
        static bool unavailable;

        public static void Show(string message, float seconds = -1)
        {
            if (!Enabled || unavailable || string.IsNullOrEmpty(message)) return;
            if (root == null && !Build()) return;

            textType.GetProperty("text")?.SetValue(text, message, null);
            total = remaining = seconds > 0 ? seconds : DefaultSeconds;
            root.SetActive(true);
            Apply(1f);
        }

        public static void Tick()
        {
            if (root == null || remaining <= 0) return;
            remaining -= Time.unscaledDeltaTime;
            float fadeTime = Mathf.Min(0.5f, total * 0.4f);
            Apply(remaining <= 0 ? 0f : Mathf.Clamp01(remaining / fadeTime));
            if (remaining <= 0) root.SetActive(false);
        }

        public static void Destroy()
        {
            if (root != null) UnityEngine.Object.Destroy(root);
            root = null;
        }

        static void Apply(float alpha)
        {
            rawImageType.GetProperty("color")?.SetValue(background, new Color(0, 0, 0, 0.55f * alpha), null);
            textType.GetProperty("color")?.SetValue(text, new Color(1, 1, 1, alpha), null);
        }

        static bool Build()
        {
            textType = FindType("UnityEngine.UI.Text");
            rawImageType = FindType("UnityEngine.UI.RawImage");
            var fontType = FindType("UnityEngine.Font");
            if (textType == null || rawImageType == null || fontType == null) { unavailable = true; return false; }

            // Built-in font: "LegacyRuntime.ttf" on Unity 2022.2+, "Arial.ttf" before.
            object font = null;
            foreach (var name in new[] { "LegacyRuntime.ttf", "Arial.ttf" })
            {
                try { font = Resources.GetBuiltinResource(fontType, name); } catch { }
                if (font != null) break;
            }
            if (font == null) { unavailable = true; return false; }

            root = new GameObject("LibriaMods Toast");
            UnityEngine.Object.DontDestroyOnLoad(root);
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000; // above everything

            var bgGo = new GameObject("Background", typeof(RectTransform));
            bgGo.transform.SetParent(root.transform, false);
            var bgRt = (RectTransform)bgGo.transform;
            bgRt.anchorMin = bgRt.anchorMax = new Vector2(0.5f, 1f);
            bgRt.pivot = new Vector2(0.5f, 1f);
            bgRt.anchoredPosition = new Vector2(0, -40);
            bgRt.sizeDelta = new Vector2(420, 54);
            background = bgGo.AddComponent(rawImageType);
            rawImageType.GetProperty("raycastTarget")?.SetValue(background, false, null);

            var txtGo = new GameObject("Text", typeof(RectTransform));
            txtGo.transform.SetParent(bgGo.transform, false);
            var txtRt = (RectTransform)txtGo.transform;
            txtRt.anchorMin = Vector2.zero; txtRt.anchorMax = Vector2.one;
            txtRt.offsetMin = txtRt.offsetMax = Vector2.zero;
            text = txtGo.AddComponent(textType);
            textType.GetProperty("font")?.SetValue(text, font, null);
            textType.GetProperty("fontSize")?.SetValue(text, 26, null);
            var anchorType = FindType("UnityEngine.TextAnchor");
            if (anchorType != null) textType.GetProperty("alignment")?.SetValue(text, Enum.Parse(anchorType, "MiddleCenter"), null);
            textType.GetProperty("raycastTarget")?.SetValue(text, false, null);
            textType.GetProperty("resizeTextForBestFit")?.SetValue(text, true, null);
            textType.GetProperty("resizeTextMinSize")?.SetValue(text, 12, null);
            textType.GetProperty("resizeTextMaxSize")?.SetValue(text, 26, null);

            // Scale with resolution (designed for 1080p).
            var scalerType = FindType("UnityEngine.UI.CanvasScaler");
            if (scalerType != null)
            {
                var scaler = root.AddComponent(scalerType);
                var modeType = FindType("UnityEngine.UI.CanvasScaler+ScaleMode");
                if (modeType != null) scalerType.GetProperty("uiScaleMode")?.SetValue(scaler, Enum.Parse(modeType, "ScaleWithScreenSize"), null);
                scalerType.GetProperty("referenceResolution")?.SetValue(scaler, new Vector2(1920, 1080), null);
                scalerType.GetProperty("matchWidthOrHeight")?.SetValue(scaler, 1f, null);
            }

            root.SetActive(false);
            return true;
        }

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
