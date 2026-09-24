using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace VJPractice.Stage.Motion
{
    /// <summary>Bounded native Canvas text pool. No browser, TMP resource import, or per-frame object creation.</summary>
    public sealed class KineticLyricRenderer : IDisposable
    {
        const int PoolSize = 32;
        readonly GameObject root;
        readonly Canvas canvas;
        readonly RectTransform canvasRect;
        readonly Text[] labels = new Text[PoolSize];
        readonly Image scrim, rule;
        int used;
        string sourceText, displayText = "", verticalText = "", typedText = "";
        int textElements, typedCount = -1;
        public bool Truncated { get; private set; }
        public int VisibleTextObjects => used;
        public bool Visible { get => root.activeSelf; set => root.SetActive(value); }

        public KineticLyricRenderer(Transform parent, Camera camera, Font font)
        {
            if (!font) throw new ArgumentException("A CJK font is required.", nameof(font));
            root = new GameObject("Kinetic lyrics (native output)", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            root.transform.SetParent(parent, false);
            canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = Mathf.Max(camera.nearClipPlane + .1f, 1f);
            canvas.sortingOrder = 100;
            canvasRect = root.GetComponent<RectTransform>();
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = .5f;
            scrim = MakeImage("Readability scrim");
            scrim.rectTransform.anchorMin = Vector2.zero;
            scrim.rectTransform.anchorMax = Vector2.one;
            scrim.rectTransform.offsetMin = scrim.rectTransform.offsetMax = Vector2.zero;
            rule = MakeImage("Accent rule");
            for (int i = 0; i < labels.Length; i++)
            {
                var go = new GameObject("Text " + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                go.transform.SetParent(root.transform, false);
                Text label = go.GetComponent<Text>();
                label.font = font; label.raycastTarget = false; label.supportRichText = false;
                label.alignment = TextAnchor.MiddleCenter;
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                label.verticalOverflow = VerticalWrapMode.Truncate;
                label.resizeTextForBestFit = true; label.resizeTextMinSize = 12;
                labels[i] = label;
                go.SetActive(false);
            }
            root.SetActive(false);
        }

        Image MakeImage(string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(root.transform, false);
            var image = go.GetComponent<Image>(); image.raycastTarget = false; return image;
        }

        void CacheText(string text)
        {
            if (sourceText == text) return;
            sourceText = text;
            Truncated = LyricMotionPlanner.ElementCount(text) > LyricMotionPlanner.MaxDisplayElements;
            displayText = LyricMotionPlanner.DisplayText(text).Replace("\n", " ");
            textElements = LyricMotionPlanner.ElementCount(displayText);
            var elements = new List<string>();
            var it = StringInfo.GetTextElementEnumerator(displayText);
            while (it.MoveNext()) elements.Add((string)it.Current);
            verticalText = string.Join("\n", elements);
            typedCount = -1;
        }

        public void Render(LyricCue cue, MotionCut cut, float time, float energy, float density, float flow, float echo, float bass)
        {
            used = 0;
            if (cue == null || cut == null) { Visible = false; return; }
            Visible = true;
            CacheText(cue.text);
            float w = Mathf.Max(1, canvasRect.rect.width), h = Mathf.Max(1, canvasRect.rect.height);
            MotionSample sample = LyricMotionPlanner.Sample(time, cue.startTime, cue.endTime);
            float local = Mathf.Max(0, time - cue.startTime);
            // Absolute song time, never Time.time: pause and seek do not accumulate drift.
            float motion = Mathf.Sin(local * Mathf.Lerp(.4f, 2.2f, flow)) * (.003f + .012f * energy);
            float kick = 1 + Mathf.Clamp01(bass) * energy * .045f;
            Color accent = cut.mood == MotionMood.Calm ? new Color(.53f, .90f, .86f)
                : cut.mood == MotionMood.Pop ? new Color(1f, .45f, .65f) : new Color(.72f, 1f, .22f);
            scrim.color = new Color(0, 0, 0, sample.alpha * (cut.mood == MotionMood.Glitch ? .48f : .25f));
            rule.color = new Color(accent.r, accent.g, accent.b, sample.alpha * .8f);
            Position(rule.rectTransform, .5f, .79f, .32f * sample.entrance, .003f, w, h, 0, 1);
            float x = .5f, y = .46f + motion, scale = kick, rotation = 0;
            if (cut.entrance == MotionEntrance.Slide) x += (1 - sample.entrance) * .14f;
            if (cut.entrance == MotionEntrance.Pop) scale *= .7f + .3f * sample.entrance;
            if (cut.exit == MotionExit.Drop) y += sample.exit * .12f;
            if (cut.exit == MotionExit.Shrink) scale *= 1 - sample.exit * .25f;
            float jitter = cut.mood == MotionMood.Glitch && sample.entrance < 1
                ? Mathf.Sin((int)(local * 18) * 13.7f + cut.variation % 97) * .012f * energy : 0;
            x += jitter;
            int fontSize = cut.layout == MotionLayout.Hero ? 170 : 132;
            if (cut.layout == MotionLayout.Diagonal) rotation = (cut.variation % 2 == 0 ? -1 : 1) * 7;
            if (cut.layout == MotionLayout.Vertical)
            {
                // Planner excludes long vertical lines; renderer also guards edited plans.
                if (textElements <= 14) DrawWithGhosts(verticalText, x, y, .65f, .62f, 110, rotation, scale, sample.alpha, accent, echo, w, h);
                else DrawWithGhosts(displayText, x, y, .82f, .48f, fontSize, 0, scale, sample.alpha, accent, echo, w, h);
            }
            else if (cut.layout == MotionLayout.Grid && textElements <= 32)
            {
                int rows = density > .65f ? 3 : 2;
                for (int row = 0; row < rows; row++)
                    for (int col = 0; col < 3; col++)
                        DrawWithGhosts(displayText, .19f + col * .31f + jitter, .27f + row * .18f + motion,
                            .28f, .16f, 62, (row % 2 == 0 ? -3 : 3), scale,
                            sample.alpha * (row == 1 ? 1f : .65f), accent, echo, w, h);
            }
            else
            {
                string text = displayText;
                if (cut.layout == MotionLayout.Typewriter)
                {
                    int count = RevealCount(cue, time, sample.progress);
                    if (count != typedCount) { typedText = LyricMotionPlanner.Prefix(displayText, count); typedCount = count; }
                    text = typedText;
                }
                DrawWithGhosts(text, x, y, .82f, .48f, fontSize, rotation, scale, sample.alpha, accent, echo, w, h);
            }
            // Translation remains secondary and does not participate in chromatic duplication.
            Put(LyricMotionPlanner.DisplayText(cue.translation), .5f, .86f, .8f, .09f, 29, 0, 1,
                new Color(accent.r, accent.g, accent.b, sample.alpha * .9f), w, h);
            for (int i = used; i < labels.Length; i++) if (labels[i].gameObject.activeSelf) labels[i].gameObject.SetActive(false);
        }

        int RevealCount(LyricCue cue, float now, float progress)
        {
            // Use imported word intervals only when they cover the same text; otherwise line timing is an estimate.
            if (cue.words != null && cue.words.Length > 0)
            {
                int visible = 0, total = 0;
                foreach (LyricWord word in cue.words)
                {
                    int n = LyricMotionPlanner.ElementCount(word.text);
                    total += n;
                    if (now >= word.endTime) visible += n;
                    else if (now >= word.startTime) visible += Mathf.CeilToInt(n * Mathf.InverseLerp(word.startTime, word.endTime, now));
                }
                if (total == LyricMotionPlanner.ElementCount(sourceText)) return Mathf.Clamp(visible, 0, textElements);
            }
            return Mathf.Clamp(Mathf.CeilToInt(progress * textElements * 1.2f), 0, textElements);
        }

        void DrawWithGhosts(string text, float x, float y, float width, float height, int size, float rotation,
            float scale, float alpha, Color accent, float echo, float w, float h)
        {
            float shift = .002f + echo * .009f;
            if (echo > .01f)
            {
                Put(text, x - shift, y + shift * .5f, width, height, size, rotation, scale,
                    new Color(.25f, .85f, 1, alpha * echo * .42f), w, h);
                Put(text, x + shift, y - shift * .5f, width, height, size, rotation, scale,
                    new Color(accent.r, accent.g, accent.b, alpha * echo * .5f), w, h);
            }
            Put(text, x, y, width, height, size, rotation, scale, new Color(.96f, .97f, 1, alpha), w, h);
        }

        void Put(string text, float x, float y, float width, float height, int size, float rotation, float scale, Color color, float w, float h)
        {
            if (string.IsNullOrEmpty(text) || used >= labels.Length) return;
            Text label = labels[used++];
            if (!label.gameObject.activeSelf) label.gameObject.SetActive(true);
            if (label.text != text) label.text = text;
            label.fontSize = size; label.resizeTextMaxSize = size; label.color = color;
            Position(label.rectTransform, x, y, width, height, w, h, rotation, scale);
        }

        static void Position(RectTransform r, float x, float y, float width, float height, float w, float h, float rotation, float scale)
        {
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, .5f);
            r.anchoredPosition = new Vector2((x - .5f) * w, (.5f - y) * h);
            r.sizeDelta = new Vector2(width * w, height * h);
            r.localRotation = Quaternion.Euler(0, 0, rotation);
            r.localScale = Vector3.one * scale;
        }

        public void Dispose() { if (root) UnityEngine.Object.Destroy(root); }
    }
}
