using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace VJPractice.Stage.Jizura
{
    /// <summary>Current VJPractice controls sampled for one JIZURA frame.</summary>
    public struct JizuraLiveInput
    {
        /// <summary>0 reveals the stage surface; 1 uses the opaque JIZURA palette.</summary>
        public float backgroundOpacity;
        /// <summary>Keep the lyric animation but omit JIZURA artwork for a clean Stage overlay.</summary>
        public bool textOnly;
        /// <summary>0 uses the planned cut; 1–6 apply a live performance look without changing its timing.</summary>
        public int manualLook;
        public float energy, density, flow, echo;
        public Vector4 bands;

        public static JizuraLiveInput Opaque
        {
            get { return new JizuraLiveInput { backgroundOpacity = 1f, energy = .5f,
                density = .5f, flow = .5f, echo = .5f, bands = Vector4.zero }; }
        }
    }

    /// <summary>
    /// Native Unity presentation of a JIZURA cut plan. The cut clock, palette,
    /// layout and whole-glyph motion follow src/03_text.js, 04_styles.js,
    /// 05_anim.js, 06_layouts.js, 07_decor.js and 09_render.js of JIZURA. No browser runs here.
    /// The original fragment and Canvas filter technique packs are not ported.
    /// A translucent palette and bounded contrast veil can composite this UI over
    /// VJPractice's live camera surface in the same StageCompositor frame.
    /// </summary>
    public sealed class JizuraNativeRenderer : IDisposable
    {
        const int DesignWidth = 1920;
        const int DesignHeight = 1080;
        const int MaxLabels = 192;
        const int MaxRects = 96;
        const int MaxFrontRects = 96;
        const int MaxGlyphs = 80;

        readonly GameObject root;
        readonly RectTransform rootRect;
        readonly Text[] labels = new Text[MaxLabels];
        readonly Image[] rects = new Image[MaxRects];
        readonly Image[] frontRects = new Image[MaxFrontRects];
        readonly Outline[] outlines = new Outline[MaxLabels];
        readonly Image background;
        readonly Image readabilityBackdrop;
        readonly Texture2D circleTexture, ringTexture, backdropTexture;
        readonly Sprite circleSprite, ringSprite, backdropSprite;
        readonly Dictionary<JizuraCut, string[]> glyphCache = new Dictionary<JizuraCut, string[]>();
        JizuraPlan timeline;
        readonly JizuraCut manualCut = new JizuraCut();
        JizuraCut manualSource;
        int manualLookIndex;
        int labelCount, rectCount, frontRectCount;
        bool frontLayer, decorLayer;
        JizuraLiveInput liveInput = JizuraLiveInput.Opaque;
        float chromaStrength = .7f;
        bool visible;

        public bool Visible
        {
            get { return visible; }
            set
            {
                if (visible == value && root && root.activeSelf == value) return;
                visible = value;
                if (root) root.SetActive(value);
            }
        }
        public int VisibleTextObjects { get { return labelCount; } }
        public JizuraCut CurrentCut { get; private set; }
        public string CurrentLayout { get; private set; } = "";
        /// <summary>Original IDs which currently use a visible native approximation or fallback.</summary>
        public string CurrentUnsupported { get; private set; } = "";

        public static bool SupportsLayout(string id)
        {
            switch (id)
            {
                case "center": case "mixed": case "vcols": case "marquee": case "tile":
                case "scatter": case "huge": case "type": case "diag": case "stack":
                case "ring": case "wave": case "labels": case "condensed":
                case "gloss": case "circle": case "pill":
                case "title": case "interlude": return true;
                default: return false;
            }
        }

        public static bool SupportsEnter(string id)
        {
            switch (id)
            {
                case "cut": case "type": case "pop": case "drop": case "spin":
                case "flicker": case "scramble": case "zoom": case "stretch":
                case "wipe": case "slice": case "blur": return true;
                default: return false;
            }
        }

        public static bool SupportsHold(string id)
        {
            switch (id)
            {
                case "still": case "jitter": case "drift": case "breathe":
                case "wave": case "glitchtick": return true;
                default: return false;
            }
        }

        public static bool SupportsExit(string id)
        {
            switch (id)
            {
                case "cut": case "shrink": case "fall": case "scatter":
                case "drift": case "wipe": case "slice": case "blur":
                case "stretch": case "glitch": return true;
                default: return false;
            }
        }

        public static bool SupportsDecor(string id)
        {
            switch (id)
            {
                case "grid": case "stripes": case "bars": case "counter":
                case "brackets": case "rings": case "dots": case "arrows":
                case "slash": return true;
                default: return false;
            }
        }

        public JizuraNativeRenderer(Transform parent, Camera camera, Font font)
        {
            if (!parent) throw new ArgumentNullException(nameof(parent));
            if (!camera) throw new ArgumentNullException(nameof(camera));
            if (!font) throw new ArgumentNullException(nameof(font));
            root = new GameObject("JIZURA native output", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            root.transform.SetParent(parent, false);
            rootRect = root.GetComponent<RectTransform>();
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = Mathf.Max(camera.nearClipPlane + .1f, 1f);
            canvas.sortingOrder = 101;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(DesignWidth, DesignHeight);
            scaler.matchWidthOrHeight = .5f;

            background = CreateImage("JIZURA background");
            background.rectTransform.anchorMin = Vector2.zero;
            background.rectTransform.anchorMax = Vector2.one;
            background.rectTransform.offsetMin = Vector2.zero;
            background.rectTransform.offsetMax = Vector2.zero;
            backdropTexture = MakeBackdropTexture();
            backdropSprite = Sprite.Create(backdropTexture, new UnityEngine.Rect(0, 0, 128, 128),
                new Vector2(.5f, .5f), 128f);
            readabilityBackdrop = CreateImage("JIZURA text contrast veil");
            readabilityBackdrop.sprite = backdropSprite;
            readabilityBackdrop.rectTransform.anchorMin = readabilityBackdrop.rectTransform.anchorMax = new Vector2(.5f, .5f);
            readabilityBackdrop.rectTransform.pivot = new Vector2(.5f, .5f);
            readabilityBackdrop.rectTransform.sizeDelta = new Vector2(DesignWidth * 1.05f, DesignHeight * .95f);
            for (int i = 0; i < rects.Length; i++) rects[i] = CreateImage("JIZURA shape " + i);
            circleTexture = MakeRoundTexture(false);
            ringTexture = MakeRoundTexture(true);
            circleSprite = Sprite.Create(circleTexture, new UnityEngine.Rect(0, 0, 128, 128), new Vector2(.5f, .5f), 128f);
            ringSprite = Sprite.Create(ringTexture, new UnityEngine.Rect(0, 0, 128, 128), new Vector2(.5f, .5f), 128f);
            for (int i = 0; i < labels.Length; i++)
            {
                var go = new GameObject("JIZURA glyph " + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Outline));
                go.transform.SetParent(root.transform, false);
                var text = go.GetComponent<Text>();
                text.font = font;
                text.raycastTarget = false;
                text.supportRichText = false;
                text.alignment = TextAnchor.MiddleCenter;
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                text.verticalOverflow = VerticalWrapMode.Overflow;
                text.resizeTextForBestFit = false;
                labels[i] = text;
                outlines[i] = go.GetComponent<Outline>();
                outlines[i].effectDistance = new Vector2(2f, -2f);
                outlines[i].enabled = false;
                go.SetActive(false);
            }
            for (int i = 0; i < frontRects.Length; i++) frontRects[i] = CreateImage("JIZURA foreground " + i);
            Visible = false;
        }

        Image CreateImage(string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(root.transform, false);
            var image = go.GetComponent<Image>();
            image.raycastTarget = false;
            if (name != "JIZURA background") go.SetActive(false);
            return image;
        }

        public void SetTimeline(JizuraPlan plan)
        {
            if (ReferenceEquals(timeline, plan)) return;
            timeline = plan;
            glyphCache.Clear();
            CurrentCut = null;
            manualSource = null;
        }

        /// <summary>Draws solely from song time. Pause and arbitrary backward seeks are deterministic.</summary>
        public void Seek(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds)) { Clear(); return; }
            Render(timeline, (float)Math.Max(0, Math.Min(seconds, float.MaxValue)), liveInput);
        }

        public void Render(JizuraPlan plan, float songTime)
        { Render(plan, songTime, JizuraLiveInput.Opaque); }

        public void Render(JizuraPlan plan, float songTime, JizuraLiveInput live)
        {
            SetTimeline(plan);
            liveInput = Sanitize(live);
            labelCount = rectCount = frontRectCount = 0;
            frontLayer = decorLayer = false;
            if (plan == null || plan.cuts == null || float.IsNaN(songTime) || float.IsInfinity(songTime))
            { Clear(); return; }

            // JIZURA stepDur(): koma drawings/sec, or project fps when koma is disabled.
            int rate = plan.fx != null && plan.fx.koma > 0 ? plan.fx.koma : Mathf.Max(1, plan.fps);
            float t = Mathf.Floor(Mathf.Max(0, songTime) * rate + 1e-5f) / rate;
            JizuraCut sourceCut = CutAt(plan.cuts, t);
            CurrentCut = sourceCut;
            JizuraCut cut = LiveCut(sourceCut, liveInput.manualLook);
            CurrentLayout = cut == null ? "" : cut.layout;
            CurrentUnsupported = Unsupported(cut);
            if (!Schemes.ContainsKey(plan.styleKey ?? ""))
                CurrentUnsupported += (CurrentUnsupported.Length > 0 ? "、" : "") + "風格 " + (plan.styleKey ?? "(null)");
            if (!string.IsNullOrEmpty(plan.aspect) && plan.aspect != "16:9")
                CurrentUnsupported += (CurrentUnsupported.Length > 0 ? "、" : "") + "畫面比例 " + plan.aspect;
            var sc = SchemeFor(plan, cut == null ? 0 : cut.scheme);
            chromaStrength = plan.fx == null ? .7f : Mathf.Clamp01(plan.fx.chroma);
            Color palette = sc.bg;
            palette.a = liveInput.backgroundOpacity;
            background.color = palette;
            float veilAlpha = (1f - liveInput.backgroundOpacity) * (.27f + liveInput.energy * .11f);
            Color veilColor = Luminance(sc.fg) > .48f ? Color.black : Color.white;
            veilColor.a = veilAlpha;
            readabilityBackdrop.color = veilColor;
            readabilityBackdrop.gameObject.SetActive(veilAlpha > .002f);
            Visible = true;
            if (cut != null && !string.IsNullOrEmpty(cut.text))
            {
                float lt = t - cut.start;
                float dur = Mathf.Max(.05f, cut.end - cut.start);
                float pIn = Mathf.Clamp01(lt / Mathf.Max(.01f, cut.inDur));
                float pOut = cut.outDur > 0 ? Mathf.Clamp01((lt - dur + cut.outDur) / cut.outDur) : 0;
                decorLayer = true;
                if (liveInput.textOnly)
                {
                    decorLayer = false;
                    string[] glyphs = Glyphs(cut);
                    if (glyphs.Length > 0)
                        DrawAnimated(cut, glyphs, DesignWidth * .5f, DesignHeight * .5f,
                            Mathf.Min(DesignHeight * .2f, DesignWidth * .72f / glyphs.Length),
                            0, sc.fg, sc, lt, dur, pIn, pOut, 1f, .05f);
                }
                else
                {
                    DrawStyleBack(plan, cut, sc, lt, pIn, pOut);
                    DrawDecor(cut, sc, lt, pIn, pOut, false);
                    decorLayer = false;
                    DrawLayout(plan, cut, sc, t, lt, dur, pIn, pOut);
                    frontLayer = true;
                    decorLayer = true;
                    DrawDecor(cut, sc, lt, pIn, pOut, true);
                    decorLayer = false;
                }
            }
            frontLayer = true;
            if (!liveInput.textOnly && WantsHud(plan)) DrawHud(plan, cut, sc, t);
            frontLayer = decorLayer = false;
            for (int i = labelCount; i < labels.Length; i++)
                if (labels[i].gameObject.activeSelf) labels[i].gameObject.SetActive(false);
            for (int i = rectCount; i < rects.Length; i++)
                if (rects[i].gameObject.activeSelf) rects[i].gameObject.SetActive(false);
            for (int i = frontRectCount; i < frontRects.Length; i++)
                if (frontRects[i].gameObject.activeSelf) frontRects[i].gameObject.SetActive(false);
        }

        public void Clear()
        {
            CurrentCut = null;
            CurrentLayout = "";
            CurrentUnsupported = "";
            labelCount = rectCount = frontRectCount = 0;
            frontLayer = decorLayer = false;
            for (int i = 0; i < labels.Length; i++) if (labels[i].gameObject.activeSelf) labels[i].gameObject.SetActive(false);
            for (int i = 0; i < rects.Length; i++) if (rects[i].gameObject.activeSelf) rects[i].gameObject.SetActive(false);
            for (int i = 0; i < frontRects.Length; i++) if (frontRects[i].gameObject.activeSelf) frontRects[i].gameObject.SetActive(false);
            Visible = false;
        }

        static JizuraLiveInput Sanitize(JizuraLiveInput value)
        {
            value.backgroundOpacity = Safe01(value.backgroundOpacity, 1f);
            value.energy = Safe01(value.energy, .5f);
            value.density = Safe01(value.density, .5f);
            value.flow = Safe01(value.flow, .5f);
            value.echo = Safe01(value.echo, .5f);
            value.bands.x = Safe01(value.bands.x, 0);
            value.bands.y = Safe01(value.bands.y, 0);
            value.bands.z = Safe01(value.bands.z, 0);
            value.bands.w = Safe01(value.bands.w, 0);
            value.manualLook = Mathf.Clamp(value.manualLook, 0, 6);
            return value;
        }

        static readonly string[] LiveLayouts = { "", "huge", "diag", "ring", "wave", "tile", "labels" };
        static readonly string[] LiveEnters = { "", "pop", "slice", "spin", "type", "scramble", "zoom" };
        static readonly string[] LiveHolds = { "", "jitter", "drift", "breathe", "wave", "glitchtick", "still" };
        static readonly string[] LiveExits = { "", "shrink", "slice", "drift", "fall", "glitch", "wipe" };
        static readonly string[][] LiveDecor = { Array.Empty<string>(), Array.Empty<string>(), new[] { "slash" },
            new[] { "rings" }, new[] { "dots" }, new[] { "grid" }, new[] { "brackets" } };

        JizuraCut LiveCut(JizuraCut source, int look)
        {
            if (source == null || source.line < 0 || look <= 0) return source;
            if (!ReferenceEquals(source, manualSource) || manualLookIndex != look)
            {
                glyphCache.Remove(manualCut);
                manualSource = source;
                manualLookIndex = look;
                manualCut.index = source.index; manualCut.line = source.line; manualCut.scheme = source.scheme;
                manualCut.text = source.text; manualCut.lineText = source.lineText; manualCut.note = source.note;
                manualCut.start = source.start; manualCut.end = source.end; manualCut.seed = source.seed;
                manualCut.emph = source.emph; manualCut.recap = source.recap; manualCut.stagger = source.stagger;
                manualCut.bg = source.bg; manualCut.cam = source.cam; manualCut.treat = source.treat;
                manualCut.trans = source.trans;
                manualCut.layout = LiveLayouts[look]; manualCut.enter = LiveEnters[look];
                manualCut.hold = LiveHolds[look]; manualCut.exit = LiveExits[look];
                manualCut.decor = LiveDecor[look];
                float duration = Mathf.Max(.05f, source.end - source.start);
                manualCut.inDur = Mathf.Min(source.inDur, duration * .42f);
                manualCut.outDur = Mathf.Min(source.outDur > 0 ? source.outDur : duration * .28f, duration * .42f);
            }
            return manualCut;
        }

        static float Safe01(float value, float fallback)
        { return float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp01(value); }

        static JizuraCut CutAt(List<JizuraCut> cuts, float t)
        {
            int lo = 0, hi = cuts.Count - 1, answer = -1;
            while (lo <= hi)
            {
                int mid = lo + ((hi - lo) >> 1);
                var c = cuts[mid];
                if (c != null && c.start <= t) { answer = mid; lo = mid + 1; }
                else hi = mid - 1;
            }
            if (answer < 0) return null;
            var cut = cuts[answer];
            return cut != null && t < cut.end ? cut : null;
        }

        static string Unsupported(JizuraCut cut)
        {
            if (cut == null) return "";
            string result = "";
            if (!SupportsLayout(cut.layout)) result = "版面 " + (cut.layout ?? "(null)");
            if (!SupportsEnter(cut.enter)) result += (result.Length > 0 ? "、" : "") + "進場 " + (cut.enter ?? "(null)");
            if (!SupportsHold(cut.hold)) result += (result.Length > 0 ? "、" : "") + "停留 " + (cut.hold ?? "(null)");
            if (!SupportsExit(cut.exit)) result += (result.Length > 0 ? "、" : "") + "退場 " + (cut.exit ?? "(null)");
            if (!string.IsNullOrEmpty(cut.bg) && cut.bg != "none") result += (result.Length > 0 ? "、" : "") + "背景技法 " + cut.bg;
            if (!string.IsNullOrEmpty(cut.treat) && cut.treat != "none") result += (result.Length > 0 ? "、" : "") + "文字處理 " + cut.treat;
            if (!string.IsNullOrEmpty(cut.cam) && cut.cam != "push" && cut.cam != "none") result += (result.Length > 0 ? "、" : "") + "鏡頭 " + cut.cam;
            if (!string.IsNullOrEmpty(cut.trans) && cut.trans != "cut" && cut.trans != "none") result += (result.Length > 0 ? "、" : "") + "轉場 " + cut.trans;
            if (cut.decor != null)
                foreach (string decor in cut.decor)
                    if (!SupportsDecor(decor)) result += (result.Length > 0 ? "、" : "") + "裝飾 " + (decor ?? "(null)");
            return result;
        }

        struct DecorBounds
        {
            public float x0, x1, y0, y1;
            public float cx { get { return (x0 + x1) * .5f; } }
            public float cy { get { return (y0 + y1) * .5f; } }
        }

        static DecorBounds Bounds(JizuraCut cut)
        {
            int n = StringInfo.ParseCombiningCharacters(cut.text ?? "").Length;
            float width = Mathf.Min(DesignWidth * .84f, n * Fit(n, .84f, .3f) * .94f);
            float height = DesignHeight * .3f;
            if (cut.layout == "vcols") { width = DesignWidth * .42f; height = DesignHeight * .72f; }
            else if (cut.layout == "ring" || cut.layout == "circle" || cut.layout == "labels")
            { width = height = DesignHeight * .7f; }
            else if (cut.layout == "huge" || cut.layout == "tile" || cut.layout == "condensed")
            { width = DesignWidth * .8f; height = DesignHeight * .55f; }
            return new DecorBounds { x0 = (DesignWidth - width) * .5f, x1 = (DesignWidth + width) * .5f,
                y0 = (DesignHeight - height) * .5f, y1 = (DesignHeight + height) * .5f };
        }

        void DrawStyleBack(JizuraPlan plan, JizuraCut cut, Scheme sc, float lt, float pIn, float pOut)
        {
            // Low-opacity editorial background accents use techniques favored by the
            // corresponding source style packs. Explicit cut decorations draw on top.
            float intensity = plan.fx == null ? .5f : Mathf.Clamp01(plan.fx.decor);
            if (intensity <= .01f) return;
            float alpha = intensity * OutCubic(Mathf.Clamp01(lt / .3f)) * (1f - InCubic(pOut));
            switch (plan.styleKey)
            {
                case "hud": case "mint": DrawGrid(sc, alpha * .35f); break;
                case "paper":
                    Rect(DesignWidth * .07f, DesignHeight * .14f, DesignWidth * .14f, 11, sc.accent, alpha * .38f);
                    Rect(DesignWidth * .91f, DesignHeight * .86f, DesignWidth * .12f, 7, sc.sub, alpha * .3f);
                    break;
                case "blueprint": DrawStripes(sc, lt, alpha * .2f); break;
                case "caution": case "transit":
                    Rect(DesignWidth * .09f, DesignHeight * .13f, DesignWidth * .16f, 8, sc.accent, alpha * .35f);
                    break;
                case "magenta": case "rouge":
                    Circle(DesignWidth * .91f, DesignHeight * .16f, DesignHeight * .04f, sc.accent, alpha * .14f);
                    break;
                case "noir": case "mono":
                    Line(DesignWidth * .08f, DesignHeight * .16f, DesignWidth * .08f, DesignHeight * .84f,
                        2, sc.sub, alpha * .17f);
                    break;
            }
        }

        void DrawDecor(JizuraCut cut, Scheme sc, float lt, float pIn, float pOut, bool front)
        {
            if (cut.decor == null || cut.decor.Length == 0) return;
            DecorBounds bb = Bounds(cut);
            for (int i = 0; i < cut.decor.Length; i++)
            {
                string id = cut.decor[i];
                if (!front)
                {
                    switch (id)
                    {
                        case "grid": DrawGrid(sc, .12f * OutCubic(Mathf.Clamp01(lt / .3f)) * (1f - InCubic(pOut))); break;
                        case "stripes": DrawStripes(sc, lt, OutCubic(Mathf.Clamp01(lt / .3f)) * (1f - InCubic(pOut))); break;
                        case "bars": DrawBars(cut, sc, lt, pOut); break;
                        case "counter":
                            DrawStatic((cut.index + 1).ToString("00", CultureInfo.InvariantCulture),
                                (cut.seed & 1u) == 0 ? DesignWidth * .14f : DesignWidth * .86f,
                                (cut.seed & 2u) == 0 ? DesignHeight * .3f : DesignHeight * .72f,
                                DesignHeight * .5f, sc.sub,
                                .11f * OutCubic(Mathf.Clamp01(lt / .3f)) * (1f - InCubic(pOut)), 0, 0);
                            break;
                    }
                }
                else
                {
                    switch (id)
                    {
                        case "brackets": DrawBrackets(bb, sc, lt, pOut); break;
                        case "rings": DrawRings(bb, cut, sc, lt, pOut); break;
                        case "dots": DrawDots(bb, sc, lt, pOut); break;
                        case "arrows": DrawArrows(bb, cut, sc, lt, pOut); break;
                        case "slash": DrawSlash(cut, sc, lt, pOut); break;
                    }
                }
            }
        }

        void DrawGrid(Scheme sc, float alpha)
        {
            if (alpha <= .002f) return;
            float step = DesignHeight / 8f;
            for (float x = (DesignWidth * .5f) % step; x <= DesignWidth; x += step)
                Line(x, 0, x, DesignHeight, 1, sc.sub, alpha);
            for (float y = (DesignHeight * .5f) % step; y <= DesignHeight; y += step)
                Line(0, y, DesignWidth, y, 1, sc.sub, alpha);
        }

        void DrawStripes(Scheme sc, float lt, float alpha)
        {
            if (alpha <= .002f) return;
            float width = DesignHeight * .04f;
            float x0 = DesignWidth * .84f, y0 = DesignHeight * .16f;
            for (int i = -6; i <= 6; i++)
                Rect(x0 + i * width * 1.35f + (lt * 40f) % (width * 2), y0 + i * width * .65f,
                    width, DesignHeight * .36f, sc.accent, alpha * .28f, 35f);
        }

        void DrawBars(JizuraCut cut, Scheme sc, float lt, float pOut)
        {
            for (int i = 0; i < 3; i++)
            {
                float e = OutExpo(Mathf.Clamp01((lt - i * .05f) / .3f)) * (1f - InCubic(pOut));
                if (e <= .002f) continue;
                bool top = Rand(cut.seed, i, 11) < .5f;
                float y = DesignHeight * (top ? .1f + Rand(cut.seed, i, 1) * .17f : .73f + Rand(cut.seed, i, 2) * .17f);
                float height = DesignHeight * (.03f + Rand(cut.seed, i, 3) * .05f);
                float width = DesignWidth * (.35f + Rand(cut.seed, i, 4) * .4f) * e;
                float x = Rand(cut.seed, i, 5) < .5f ? width * .5f - 10f : DesignWidth - width * .5f + 10f;
                Rect(x, y, width, height, i == 0 ? sc.accent : sc.fg, .82f);
                // Small offset chips keep the ragged-band rhythm of the source decor.
                Rect(x + Signed(cut.seed, i, 7) * width * .4f, y - height * .48f,
                    width * .13f, height * .12f, i == 0 ? sc.accent : sc.fg, .6f);
            }
        }

        void DrawBrackets(DecorBounds bb, Scheme sc, float lt, float pOut)
        {
            float e = OutExpo(Mathf.Clamp01(lt / .35f)) * (1f - InCubic(pOut));
            if (e <= .002f) return;
            float pad = 18f + (bb.y1 - bb.y0) * .12f;
            float x0 = Mathf.Lerp(bb.cx, bb.x0 - pad, e), x1 = Mathf.Lerp(bb.cx, bb.x1 + pad, e);
            float y0 = Mathf.Lerp(bb.cy, bb.y0 - pad, e), y1 = Mathf.Lerp(bb.cy, bb.y1 + pad, e);
            float length = Mathf.Min(x1 - x0, y1 - y0) * .16f + 8f;
            Color c = sc.accent;
            Line(x0, y0, x0 + length, y0, 2.2f, c, 1); Line(x0, y0, x0, y0 + length, 2.2f, c, 1);
            Line(x1, y0, x1 - length, y0, 2.2f, c, 1); Line(x1, y0, x1, y0 + length, 2.2f, c, 1);
            Line(x0, y1, x0 + length, y1, 2.2f, c, 1); Line(x0, y1, x0, y1 - length, 2.2f, c, 1);
            Line(x1, y1, x1 - length, y1, 2.2f, c, 1); Line(x1, y1, x1, y1 - length, 2.2f, c, 1);
        }

        void DrawRings(DecorBounds bb, JizuraCut cut, Scheme sc, float lt, float pOut)
        {
            float e = OutExpo(Mathf.Clamp01(lt / .5f)) * (1f - InCubic(pOut));
            if (e <= .002f) return;
            float r0 = Mathf.Max(bb.x1 - bb.x0, bb.y1 - bb.y0) * .55f + DesignHeight * .05f;
            for (int k = 0; k < 2; k++)
            {
                float radius = r0 * (1f + k * .28f + Rand(cut.seed, k, 1) * .1f);
                float start = Rand(cut.seed, k, 2) * 360f + lt * (k == 0 ? 10f : -14f);
                float sweep = 360f * e * (.55f + .45f * Rand(cut.seed, k, 3));
                Arc(bb.cx, bb.cy, radius, start, sweep, 14, sc.fg, .68f, 1.4f);
                float angle = (start + 40f) * Mathf.Deg2Rad;
                float px = bb.cx + Mathf.Cos(angle) * radius, py = bb.cy + Mathf.Sin(angle) * radius;
                Circle(px, py, 4, sc.accent, e);
            }
        }

        void Arc(float cx, float cy, float radius, float start, float sweep, int segments, Color color, float alpha, float width)
        {
            for (int i = 0; i < segments; i++)
            {
                float a = (start + sweep * i / segments) * Mathf.Deg2Rad;
                float b = (start + sweep * (i + 1) / segments) * Mathf.Deg2Rad;
                Line(cx + Mathf.Cos(a) * radius, cy + Mathf.Sin(a) * radius,
                    cx + Mathf.Cos(b) * radius, cy + Mathf.Sin(b) * radius,
                    width, color, alpha);
            }
        }

        void DrawDots(DecorBounds bb, Scheme sc, float lt, float pOut)
        {
            float e = OutCubic(Mathf.Clamp01(lt / .3f)) * (1f - InCubic(pOut));
            if (e <= .002f) return;
            float radius = Mathf.Max(bb.x1 - bb.x0, bb.y1 - bb.y0) * .62f + DesignHeight * .04f;
            int count = Mathf.CeilToInt(36 * e);
            for (int i = 0; i < count; i++)
            {
                float angle = (i / 36f * 360f + lt * 20f) * Mathf.Deg2Rad;
                Circle(bb.cx + Mathf.Cos(angle) * radius, bb.cy + Mathf.Sin(angle) * radius,
                    i % 6 == 0 ? 4f : 2.2f, i % 6 == 0 ? sc.accent : sc.fg, .85f);
            }
        }

        void DrawArrows(DecorBounds bb, JizuraCut cut, Scheme sc, float lt, float pOut)
        {
            float e = OutExpo(Mathf.Clamp01(lt / .4f)) * (1f - InCubic(pOut));
            if (e <= .002f) return;
            float size = DesignHeight * .03f;
            int step = Mathf.FloorToInt(lt * 24f);
            for (int side = -1; side <= 1; side += 2)
                for (int i = 0; i < 3; i++)
                {
                    float x = (side < 0 ? bb.x0 - size * 1.2f : bb.x1 + size * 1.2f)
                        + side * (i * size * .9f + (1f - e) * DesignWidth * .2f);
                    float d = -side;
                    float opacity = (step + i) % 3 == 0 ? .3f : 1f;
                    Color c = i == 0 ? sc.accent : sc.fg;
                    Line(x - d * size * .35f, bb.cy - size * .5f, x + d * size * .35f, bb.cy,
                        size * .14f, c, opacity);
                    Line(x + d * size * .35f, bb.cy, x - d * size * .35f, bb.cy + size * .5f,
                        size * .14f, c, opacity);
                }
        }

        void DrawSlash(JizuraCut cut, Scheme sc, float lt, float pOut)
        {
            float length = Mathf.Sqrt(DesignWidth * DesignWidth + DesignHeight * DesignHeight);
            for (int i = 0; i < 2; i++)
            {
                float e = OutExpo(Mathf.Clamp01((lt - i * .06f) / .35f));
                if (e <= .002f) continue;
                float angle = (-70f + Rand(cut.seed, i, 1) * 50f) * Mathf.Deg2Rad;
                float cx = DesignWidth * (.3f + Rand(cut.seed, i, 2) * .4f);
                float cy = DesignHeight * (.3f + Rand(cut.seed, i, 3) * .4f);
                float ux = Mathf.Cos(angle), uy = Mathf.Sin(angle);
                float x0 = cx - ux * length * .5f, y0 = cy - uy * length * .5f;
                float from = InCubic(pOut);
                Line(x0 + ux * length * from, y0 + uy * length * from,
                    x0 + ux * length * e, y0 + uy * length * e,
                    i == 0 ? 1.4f : 2f, i == 0 ? sc.fg : sc.accent, .9f);
            }
        }

        static bool WantsHud(JizuraPlan plan)
        {
            string overrideValue = plan.fx == null ? "auto" : plan.fx.hud;
            if (overrideValue == "on") return true;
            if (overrideValue == "off") return false;
            switch (plan.styleKey)
            {
                case "crimson": case "caution": case "hud": case "mint": case "rouge": return true;
                default: return false;
            }
        }

        void DrawHud(JizuraPlan plan, JizuraCut cut, Scheme sc, float t)
        {
            float m = DesignHeight * .045f, l = DesignHeight * .035f, fs = Mathf.Clamp(DesignHeight * .016f, 10, 18);
            float x0 = m, x1 = DesignWidth - m, y0 = m, y1 = DesignHeight - m;
            Line(x0, y0 + l, x0, y0, 1.4f, sc.sub, .9f); Line(x0, y0, x0 + l, y0, 1.4f, sc.sub, .9f);
            Line(x1 - l, y0, x1, y0, 1.4f, sc.sub, .9f); Line(x1, y0, x1, y0 + l, 1.4f, sc.sub, .9f);
            Line(x0, y1 - l, x0, y1, 1.4f, sc.sub, .9f); Line(x0, y1, x0 + l, y1, 1.4f, sc.sub, .9f);
            Line(x1 - l, y1, x1, y1, 1.4f, sc.sub, .9f); Line(x1, y1, x1, y1 - l, 1.4f, sc.sub, .9f);
            string title = (string.IsNullOrEmpty(plan.title) ? "UNTITLED" : plan.title)
                + (string.IsNullOrEmpty(plan.artist) ? "" : " / " + plan.artist);
            DrawStatic(title, m + l * .6f + 170f, m + l * .9f, fs, sc.sub, 1f, 0, .12f);
            if (Mathf.FloorToInt(t * 24f) % 4 < 2)
                Circle(DesignWidth - m - l * 2.6f, m + l * .9f, fs * .32f, sc.accent, 1f);
            DrawStatic("REC", DesignWidth - m - l * 1.25f, m + l * .9f, fs, sc.sub, 1f, 0, .1f);
            int seconds = Mathf.FloorToInt(t);
            int frame = Mathf.FloorToInt((t - seconds) * Mathf.Max(1, plan.fps));
            string timecode = (seconds / 60).ToString("00", CultureInfo.InvariantCulture) + ":"
                + (seconds % 60).ToString("00", CultureInfo.InvariantCulture) + ":"
                + frame.ToString("00", CultureInfo.InvariantCulture);
            DrawStatic(timecode, m + l * .6f + 60f, DesignHeight - m - l * .9f, fs, sc.sub, 1f, 0, .1f);
            DrawStatic("LYRIC " + (cut == null ? 0 : cut.line + 1).ToString("00", CultureInfo.InvariantCulture)
                + "/" + plan.lines.Count.ToString("00", CultureInfo.InvariantCulture),
                DesignWidth - m - l * .6f - 85f, DesignHeight - m - l * .9f, fs, sc.sub, 1f, 0, .1f);
            float py = DesignHeight - m - l * .9f;
            Line(DesignWidth * .3f, py, DesignWidth * .7f, py, 1f, sc.sub, .35f);
            Line(DesignWidth * .3f, py,
                Mathf.Lerp(DesignWidth * .3f, DesignWidth * .7f,
                    plan.duration <= 0 ? 0 : Mathf.Clamp01(t / plan.duration)), py,
                2f, sc.accent, .9f);
        }

        void DrawLayout(JizuraPlan plan, JizuraCut cut, Scheme sc, float t, float lt, float dur, float pIn, float pOut)
        {
            string[] glyphs = Glyphs(cut);
            int n = glyphs.Length;
            if (n == 0) return;
            string layout = cut.layout ?? "center";
            float w = DesignWidth, h = DesignHeight;
            switch (layout)
            {
                case "mixed":
                    {
                        int rows = n > 9 ? 2 : 1;
                        int per = Mathf.CeilToInt((float)n / rows);
                        float baseSize = Mathf.Min(rows > 1 ? h * .29f : h * .38f, w * .84f / Mathf.Max(1, per));
                        for (int i = 0; i < n && i < MaxGlyphs; i++)
                        {
                            int row = i / per, col = i % per;
                            int inRow = Mathf.Min(per, n - row * per);
                            float x = w * .5f + (col - (inRow - 1) * .5f) * baseSize * .95f;
                            float y = h * .5f + (row - (rows - 1) * .5f) * baseSize * 1.08f;
                            float k = CharScale(glyphs[i]) * (.78f + .23f * Rand(cut.seed, i, 11));
                            Color colr = i == (int)(cut.seed % (uint)n) ? sc.accent : sc.fg;
                            DrawGlyph(cut, glyphs[i], i, n, x, y, baseSize * k,
                                Signed(cut.seed, i, 12) * 8f, colr, sc, lt, dur, pIn, pOut, 1f);
                        }
                        break;
                    }
                case "vcols":
                    {
                        int per = Mathf.Clamp(Mathf.CeilToInt(n / 3f), 3, 7);
                        int cols = Mathf.CeilToInt(n / (float)per);
                        float size = Mathf.Min(h * .77f / per, w * .79f / (cols * 1.45f));
                        for (int i = 0; i < n && i < MaxGlyphs; i++)
                        {
                            int col = i / per, row = i % per;
                            float x = w * .5f + ((cols - 1) * .5f - col) * size * 1.45f;
                            float y = h * .5f + (row - (Mathf.Min(per, n - col * per) - 1) * .5f) * size * 1.04f;
                            float rot = IsLatin(glyphs[i]) ? -90f : 0;
                            DrawGlyph(cut, glyphs[i], i, n, x, y, size, rot, sc.fg, sc, lt, dur, pIn, pOut, 1f);
                        }
                        break;
                    }
                case "marquee":
                    {
                        float size = Fit(n, .84f, .25f);
                        float rowSize = size * .35f;
                        for (int row = 0; row < 4; row++)
                        {
                            float yy = h * (.22f + row * .19f);
                            float move = (lt * (row % 2 == 0 ? -1 : 1) * w * .09f) % w;
                            for (int copy = -1; copy <= 1; copy++)
                                DrawStatic(cut.text, w * .5f + move + copy * w * .9f, yy, rowSize,
                                    sc.sub, .25f * pIn * (1f - pOut), 0, .05f);
                        }
                        DrawAnimated(cut, glyphs, w * .5f, h * .5f, size, 0, sc.fg, sc, lt, dur, pIn, pOut, 1f, .05f);
                        break;
                    }
                case "tile":
                    {
                        float alpha = Mathf.Clamp01(pIn * 1.5f) * (1f - pOut);
                        for (int row = 0; row < 14; row++)
                        {
                            if (Rand(cut.seed, (int)(t * 24), row) < .10f) continue;
                            float x = w * (.22f + .56f * Rand(cut.seed, row, 8));
                            DrawStatic(cut.text, x, (row + .5f) * h / 14f, 50,
                                sc.sub, .42f * alpha, 0, .02f);
                        }
                        Rect(w * .5f, h * .5f, w * .82f, h * .34f, sc.bg, alpha);
                        DrawAnimated(cut, glyphs, w * .5f, h * .5f, Fit(n, .76f, .3f), 0,
                            sc.fg, sc, lt, dur, pIn, pOut, 1f, .04f);
                        break;
                    }
                case "scatter":
                    {
                        float baseSize = Fit(n, .9f, .29f);
                        for (int i = 0; i < n && i < MaxGlyphs; i++)
                        {
                            float x = w * (.1f + .8f * (i + .5f) / n) + Signed(cut.seed, i, 71) * w * .035f;
                            float y = h * .5f + Signed(cut.seed, i, 72) * h * .18f;
                            float k = .62f + Rand(cut.seed, i, 73) * .85f;
                            DrawGlyph(cut, glyphs[i], i, n, x, y, baseSize * k,
                                Signed(cut.seed, i, 74) * 24f,
                                Rand(cut.seed, i, 75) < .15f ? sc.accent : sc.fg,
                                sc, lt, dur, pIn, pOut, 1f);
                        }
                        break;
                    }
                case "ring":
                    {
                        float radius = Mathf.Min(h * .31f, w * .4f);
                        float ringSize = Mathf.Min(h * .07f, Mathf.PI * 2f * radius / ((n + 1) * 1.25f));
                        int count = Mathf.Clamp(Mathf.FloorToInt(Mathf.PI * 2f * radius / (ringSize * 1.2f)), n + 1, 32);
                        Circle(w * .5f, h * .5f, radius * .86f, sc.sub, .55f, true);
                        Circle(w * .5f, h * .5f, radius * 1.15f, sc.sub, .35f, true);
                        if ((cut.seed & 1u) == 0)
                            Circle(w * .5f, h * .5f, radius * .7f * OutBack(pIn) * (1f - InCubic(pOut)), sc.accent, 1f);
                        DrawAnimated(cut, glyphs, w * .5f, h * .5f,
                            Mathf.Min(h * .2f, radius * 1.25f / Mathf.Max(1, n * .9f)), 0,
                            (cut.seed & 1u) == 0 ? sc.bg : sc.fg, sc, lt, dur, pIn, pOut, 1f, .03f);
                        for (int i = 0; i < count; i++)
                        {
                            float angle = (i / (float)count) * Mathf.PI * 2f + lt * ((cut.seed & 2u) == 0 ? 8f : -8f) * Mathf.Deg2Rad - Mathf.PI * .5f;
                            string ch = i % (n + 1) == n ? "・" : glyphs[i % (n + 1)];
                            DrawGlyph(cut, ch, i % n, n, w * .5f + Mathf.Cos(angle) * radius,
                                h * .5f + Mathf.Sin(angle) * radius, ringSize,
                                angle * Mathf.Rad2Deg + 90f, ch == "・" ? sc.accent : sc.fg,
                                sc, lt, dur, pIn, pOut, .92f);
                        }
                        break;
                    }
                case "wave":
                    {
                        float size = Mathf.Min(h * .2f, w * .72f / n);
                        float unit = size * 1.05f / w;
                        float amp = .08f + Rand(cut.seed, 41) * .09f;
                        float freq = .8f + Rand(cut.seed, 42) * .8f;
                        float travel = ((cut.seed & 1u) == 0 ? 1f : -1f) * (.25f + Rand(cut.seed, 43) * .25f);
                        float u0 = .5f - (lt / dur - .5f) * travel;
                        for (int trail = 5; trail >= 1; trail--)
                            for (int i = 0; i < n; i++)
                            {
                                float u = u0 + (i - (n - 1) * .5f) * unit + trail * unit * .2f * Mathf.Sign(travel);
                                float yy = h * (.5f + amp * Mathf.Sin(Mathf.PI * 2f * freq * u + lt * 1.3f));
                                Put(glyphs[i], u * w, yy, size * (1f - trail * .035f), 0, 1f,
                                    WithAlpha(sc.sub, .5f * (1f - trail / 6f) * OutCubic(pIn) * (1f - pOut)));
                            }
                        for (int i = 0; i < n; i++)
                        {
                            float u = u0 + (i - (n - 1) * .5f) * unit;
                            float phase = Mathf.PI * 2f * freq * u + lt * 1.3f;
                            float yy = h * (.5f + amp * Mathf.Sin(phase));
                            float angle = -Mathf.Atan(h / w * amp * Mathf.PI * 2f * freq * Mathf.Cos(phase)) * Mathf.Rad2Deg;
                            DrawGlyph(cut, glyphs[i], i, n, u * w, yy, size, angle,
                                sc.fg, sc, lt, dur, pIn, pOut, 1f);
                        }
                        break;
                    }
                case "labels":
                    {
                        int variant = (int)(cut.seed % 3u);
                        float fade = 1f - InCubic(pOut);
                        if (variant == 0)
                        {
                            float radius = Mathf.Min(h * .3f, w * .36f);
                            Circle(w * .5f, h * .5f, radius * .52f * OutBack(pIn) * fade, sc.accent, .95f);
                            int m = Mathf.Max(n, 10);
                            for (int i = 0; i < m && i < 20; i++)
                            {
                                float angle = i / (float)m * Mathf.PI * 2f + lt * 7f * Mathf.Deg2Rad - Mathf.PI * .5f;
                                float q = OutBack(Mathf.Clamp01((lt - i * .025f) / .22f)) * fade;
                                LabelPlate(glyphs[i % n], w * .5f + Mathf.Cos(angle) * radius,
                                    h * .5f + Mathf.Sin(angle) * radius, h * .062f, angle * Mathf.Rad2Deg,
                                    q, sc);
                            }
                        }
                        else if (variant == 1)
                        {
                            float fs = Mathf.Min(h * .1f, h * .7f / (n * 1.5f));
                            for (int i = 0; i < n; i++)
                            {
                                float q = OutBack(Mathf.Clamp01((lt - i * .05f) / .22f)) * fade;
                                LabelPlate(glyphs[i], w * .5f + Signed(cut.seed, i, 5) * w * .12f,
                                    h * .5f + (i - (n - 1) * .5f) * fs * 1.55f,
                                    fs, Signed(cut.seed, i, 6) * 4f, q, sc);
                            }
                        }
                        else
                        {
                            for (int i = 0; i < n; i++)
                            {
                                float q = OutBack(Mathf.Clamp01((lt - i * .05f) / .22f)) * fade;
                                LabelPlate(glyphs[i], w * (.15f + .7f * (i + .5f) / n) + Signed(cut.seed, i, 7) * w * .04f,
                                    h * .5f + Signed(cut.seed, i, 8) * h * .25f,
                                    h * .085f * (.8f + Rand(cut.seed, i, 10) * .5f),
                                    Signed(cut.seed, i, 9) * 22f, q, sc);
                            }
                        }
                        break;
                    }
                case "condensed":
                    {
                        int copies = n <= 4 ? 3 : n <= 7 ? 2 : 1;
                        float slot = w * .92f / copies;
                        float sx = .42f + Rand(cut.seed, 51) * .16f;
                        float sy = 1.1f + Rand(cut.seed, 52) * .2f;
                        float size = Mathf.Min(h * .62f, slot * .92f / Mathf.Max(1, n * sx * .95f));
                        for (int copy = 0; copy < copies; copy++)
                            DrawAnimated(cut, glyphs,
                                w * .5f + (copy - (copies - 1) * .5f) * slot,
                                h * .5f, size, 0, sc.fg, sc, lt, dur, pIn, pOut,
                                1f, .04f, null, sx, sy);
                        break;
                    }
                case "gloss":
                    {
                        bool right = (cut.seed & 1u) == 0;
                        if ((cut.seed & 2u) == 0)
                        {
                            for (int row = 0; row < 3; row++)
                                DrawStatic(cut.text, w * .5f + lt * 20f * (row % 2 == 0 ? -1 : 1),
                                    h * (.18f + row * .32f), h * .3f, sc.sub, .13f, 0, 0);
                        }
                        float size = Fit(n, .5f, .24f);
                        float center = right ? w * .4f : w * .6f;
                        DrawAnimated(cut, glyphs, center, h * .54f, size, 0,
                            sc.fg, sc, lt, dur, pIn, pOut, 1f, .03f);
                        float e = OutExpo(Mathf.Clamp01((lt - cut.inDur * .4f) / .45f)) * (1f - InCubic(pOut));
                        if (e > .002f)
                        {
                            float edge = center + (right ? 1f : -1f) * n * size * .5f;
                            float anchorX = edge + (right ? 1f : -1f) * size * .1f;
                            float anchorY = h * .47f;
                            float noteX = right ? w * .9f : w * .1f;
                            float noteY = h * .26f;
                            Line(anchorX, anchorY, Mathf.Lerp(anchorX, noteX, .45f), anchorY, 2, sc.sub, e);
                            Line(Mathf.Lerp(anchorX, noteX, .45f), anchorY, noteX, noteY, 2, sc.sub, e);
                            Circle(anchorX, anchorY, 5, sc.accent, e);
                            DrawStatic("【" + cut.text + "】", noteX, noteY - 38, 30, sc.fg, e, 0, 0);
                            DrawStatic(string.IsNullOrEmpty(cut.note) ? cut.lineText : cut.note,
                                noteX, noteY + 26, 26, sc.sub, e, 0, .08f);
                            DrawStatic("No." + (cut.line + 1).ToString("00", CultureInfo.InvariantCulture),
                                noteX, noteY + 75, 22, sc.accent, e, 0, 0);
                        }
                        break;
                    }
                case "huge":
                    {
                        float size = Mathf.Min(h * .9f, w * 1.25f / Mathf.Max(1, n * .92f));
                        float shift = (.5f - lt / dur) * w * .16f * ((cut.seed & 1) == 0 ? 1 : -1);
                        DrawAnimated(cut, glyphs, w * .5f + shift, h * .52f, size, 0,
                            sc.fg, sc, lt, dur, pIn, pOut, 1f, -.02f);
                        DrawStatic(cut.text, w * .14f, h * .87f, 29, sc.accent, (1f - pOut) * pIn, 0, .12f);
                        break;
                    }
                case "type":
                    {
                        float size = Fit(n, .74f, .11f);
                        Rect(w * .12f, h * .5f, 8, size * 1.1f, sc.accent, pIn * (1f - pOut));
                        DrawAnimated(cut, glyphs, w * .52f, h * .5f, size, 0,
                            sc.fg, sc, lt, dur, pIn, pOut, 1f, .06f,
                            cut.enter == "cut" ? "type" : null);
                        DrawStatic("LINE " + (cut.line + 1).ToString("00", CultureInfo.InvariantCulture),
                            w * .19f, h * .8f, 22, sc.sub, .8f * (1f - pOut), 0, .1f);
                        break;
                    }
                case "diag":
                    {
                        float angle = ((cut.seed & 1) == 0 ? 1 : -1) * (10 + Rand(cut.seed, 31) * 12);
                        float size = Fit(n, .72f, .2f);
                        // A rotated plate expresses the original diagonal-band layout.
                        Rect(w * .5f, h * .5f, w * 1.5f, size * 1.8f, sc.accent,
                            OutExpo(pIn) * (1f - pOut), -angle);
                        DrawAnimated(cut, glyphs, w * .5f, h * .5f, size, -angle,
                            Luminance(sc.accent) > .55f ? sc.bg : sc.fg,
                            sc, lt, dur, pIn, pOut, 1f, .05f);
                        break;
                    }
                case "circle":
                    {
                        int variant = (int)(cut.seed % 3u);
                        float cx = w * (.5f + Signed(cut.seed, 61) * .1f), cy = h * .5f;
                        float radius = Mathf.Min(h * .3f, w * .4f);
                        float e = OutBack(pIn) * (1f - InCubic(pOut));
                        if (variant == 0) Circle(cx, cy, radius * e, sc.accent, 1f);
                        else if (variant == 1)
                        {
                            // Eclipse: foreground text, a dark moving disc and luminous outer ring.
                            Circle(w * (.42f + .16f * lt / dur), cy + h * .12f, h * .19f * e, sc.fg, .85f, true);
                            Circle(w * (.42f + .16f * lt / dur), cy + h * .12f, h * .18f * e, sc.bg, 1f);
                        }
                        else Circle(cx, cy, radius * e, sc.fg, 1f, true);
                        DrawAnimated(cut, glyphs, cx, cy,
                            Mathf.Min(radius * .8f, radius * 1.45f / Mathf.Max(1, n * .9f)),
                            0, variant == 0 ? sc.bg : sc.fg, sc, lt, dur, pIn, pOut, 1f, .03f);
                        break;
                    }
                case "stack":
                    {
                        float size = Fit(n, .8f, .19f);
                        for (int k = 3; k >= 0; k--)
                        {
                            DrawAnimated(cut, glyphs, w * .5f + k * w * .015f,
                                h * .5f + (k - 1.5f) * size * .9f, size, 0,
                                k == 0 ? sc.fg : sc.sub, sc, lt, dur, pIn, pOut,
                                k == 0 ? 1f : .6f * Mathf.Pow(.58f, k - 1), .03f);
                        }
                        break;
                    }
                case "pill":
                    {
                        float size = Fit(n, .62f, .17f);
                        float fullWidth = n * size * .92f + size * 1.3f;
                        float height = size * 1.6f;
                        float progress = OutExpo(Mathf.Clamp01(lt / Mathf.Max(.01f, cut.inDur * .9f))) * (1f - InCubic(pOut));
                        float activeWidth = Mathf.Max(height, fullWidth * progress);
                        float left = w * .5f - activeWidth * .5f, right = w * .5f + activeWidth * .5f;
                        Rect(w * .5f, h * .5f, Mathf.Max(1f, activeWidth - height), height, sc.accent, 1f);
                        Circle(left + height * .5f, h * .5f, height * .5f, sc.accent, 1f);
                        Circle(right - height * .5f, h * .5f, height * .5f, sc.accent, 1f);
                        DrawAnimated(cut, glyphs, w * .5f, h * .5f, size, 0,
                            Luminance(sc.accent) > .55f ? sc.bg : Color.white,
                            sc, lt, dur, pIn, pOut, 1f, .04f);
                        DrawStatic("No." + (cut.line + 1).ToString("00", CultureInfo.InvariantCulture),
                            w * .5f + fullWidth * .43f, h * .5f - height * .88f, 24,
                            sc.sub, pIn * (1f - pOut), 0, .1f);
                        break;
                    }
                case "title":
                case "interlude":
                    {
                        float size = Fit(n, .7f, .16f);
                        DrawAnimated(cut, glyphs, w * .5f, h * .5f, size, 0,
                            sc.fg, sc, lt, dur, pIn, pOut, 1f, .08f);
                        if (!string.IsNullOrEmpty(cut.note))
                            DrawStatic(cut.note, w * .5f, h * .64f, 32, sc.sub,
                                Mathf.Clamp01((lt - .3f) / .4f) * (1f - pOut), 0, .1f);
                        break;
                    }
                default:
                    {
                        float size = Fit(n, .84f, .33f);
                        DrawAnimated(cut, glyphs, w * .5f, h * .5f, size, 0,
                            sc.fg, sc, lt, dur, pIn, pOut, 1f, .06f);
                        if (!string.IsNullOrEmpty(cut.lineText) && cut.lineText != cut.text)
                            DrawStatic(cut.lineText, w * .5f, h * .73f, 32, sc.sub,
                                Mathf.Clamp01((lt - cut.inDur * .5f) / .3f) * (1f - pOut), 0, .08f);
                        Rect(w * .5f, h * .79f, w * .32f * OutExpo(pIn) * (1f - pOut), 4, sc.accent, 1);
                        break;
                    }
            }
        }

        string[] Glyphs(JizuraCut cut)
        {
            string[] cached;
            if (glyphCache.TryGetValue(cut, out cached)) return cached;
            string source = (cut.text ?? "").Replace("\r", "").Replace("\n", " ");
            var starts = StringInfo.ParseCombiningCharacters(source);
            int count = Mathf.Min(starts.Length, MaxGlyphs);
            var glyphs = new string[count];
            for (int i = 0; i < count; i++)
                glyphs[i] = source.Substring(starts[i], (i + 1 < starts.Length ? starts[i + 1] : source.Length) - starts[i]);
            glyphCache[cut] = glyphs;
            return glyphs;
        }

        void DrawAnimated(JizuraCut cut, string[] glyphs, float x, float y, float size, float rotation,
            Color color, Scheme sc, float lt, float dur, float pIn, float pOut, float alpha, float track,
            string enterOverride = null, float scaleX = 1f, float scaleY = 1f)
        {
            float total = 0;
            for (int i = 0; i < glyphs.Length; i++) total += Advance(glyphs[i]) * size * scaleX + (i < glyphs.Length - 1 ? track * size : 0);
            float cursor = -total * .5f;
            float theta = rotation * Mathf.Deg2Rad;
            float cos = Mathf.Cos(theta), sin = Mathf.Sin(theta);
            for (int i = 0; i < glyphs.Length; i++)
            {
                float adv = Advance(glyphs[i]) * size * scaleX;
                float px = cursor + adv * .5f;
                DrawGlyph(cut, glyphs[i], i, glyphs.Length,
                    x + px * cos, y - px * sin, size, rotation,
                    color, sc, lt, dur, pIn, pOut, alpha, enterOverride, scaleX, scaleY);
                cursor += adv + track * size;
            }
        }

        void DrawGlyph(JizuraCut cut, string glyph, int index, int count, float x, float y, float size,
            float rotation, Color color, Scheme sc, float lt, float dur, float pIn, float pOut, float alpha,
            string enterOverride = null, float scaleX = 1f, float scaleY = 1f)
        {
            if (labelCount + 3 >= labels.Length || string.IsNullOrWhiteSpace(glyph)) return;
            var state = new GlyphState { text = glyph, x = x, y = y, size = size, rotation = rotation, alpha = alpha, scale = 1 };
            SampleGlyph(ref state, cut, index, count, lt, dur, pIn, pOut, enterOverride);
            if (state.alpha <= .002f || state.scale <= .002f) return;
            float bassPulse = liveInput.energy * liveInput.bands.x;
            state.scale *= 1f + .075f * bassPulse;
            state.y += Mathf.Sin(lt * (2f + 3f * liveInput.flow) + index * .62f)
                * state.size * .008f * liveInput.energy * liveInput.flow;
            float echo = chromaStrength * (.28f + .72f * liveInput.echo)
                * (1f + liveInput.bands.z * .35f);
            float chroma = .006f * DesignWidth * echo
                * Mathf.Clamp01(1f - Mathf.Abs(Signed(cut.seed, index, 33)) * .2f);
            Put(state.text, state.x - chroma, state.y + chroma * .4f, state.size,
                state.rotation, state.scale, WithAlpha(sc.ghostB, state.alpha * .3f * echo), -1, scaleX, scaleY);
            Put(state.text, state.x + chroma, state.y - chroma * .4f, state.size,
                state.rotation, state.scale, WithAlpha(sc.ghostA, state.alpha * .38f * echo), -1, scaleX, scaleY);
            Put(state.text, state.x, state.y, state.size, state.rotation, state.scale,
                WithAlpha(color, state.alpha), -1, scaleX, scaleY, true);
        }

        static void SampleGlyph(ref GlyphState g, JizuraCut cut, int i, int n,
            float lt, float dur, float pIn, float pOut, string enterOverride)
        {
            float delay = i * Mathf.Max(0, cut.stagger);
            float localIn = lt - delay;
            float qIn = Mathf.Clamp01(localIn / Mathf.Max(.01f, cut.inDur));
            string enter = enterOverride ?? cut.enter ?? "cut";
            if (localIn < 0) { g.alpha = 0; return; }
            switch (enter)
            {
                case "type": if (i >= Mathf.CeilToInt(qIn * n)) g.alpha = 0; break;
                case "pop":
                    {
                        float q = Mathf.Clamp01((qIn - (n > 1 ? (float)i / (n - 1) * .45f : 0)) / .55f);
                        g.scale *= OutBack(q);
                        g.rotation += (1f - OutCubic(q)) * Signed(cut.seed, i, 9) * 28f;
                        if (q <= 0) g.alpha = 0;
                        break;
                    }
                case "drop":
                    {
                        float q = Mathf.Clamp01((qIn - Rand(cut.seed, i, 4) * .5f) / .5f);
                        g.y -= (1f - Bounce(q)) * g.size * 2.4f;
                        if (q <= 0) g.alpha = 0;
                        break;
                    }
                case "spin":
                    {
                        float q = Mathf.Clamp01((qIn - (n > 1 ? (float)i / (n - 1) * .4f : 0)) / .6f);
                        g.rotation += (1f - OutExpo(q)) * (Rand(cut.seed, i, 10) > .5f ? 200f : -200f);
                        g.scale *= Mathf.Lerp(.15f, 1f, OutExpo(q));
                        g.alpha *= Mathf.Min(1f, q * 3f);
                        break;
                    }
                case "flicker": if (qIn < 1 && Rand(cut.seed, Mathf.FloorToInt(lt * 24), i) >= qIn * 1.25f) g.alpha = 0; break;
                case "scramble":
                    {
                        float settle = .25f + .75f * (n > 1 ? (float)i / (n - 1) : 1);
                        if (qIn < settle)
                        {
                            const string pool = "アイウエオカキクケコサシスセソタチツテトナニヌネノハヒフヘホマミムメモヤユヨラリルレロワヲン愛夢声光影空夜星雨涙心恋罪神★◆▲●■01234567ABCDEFGHJKLMNPQRSTUVWXYZ";
                            g.text = pool[Mathf.FloorToInt(Rand(cut.seed, i, Mathf.FloorToInt(lt * 24)) * pool.Length)].ToString();
                        }
                        break;
                    }
                case "zoom": g.scale *= Mathf.Lerp(1.7f, 1f, OutExpo(qIn)); g.alpha *= Mathf.Min(1f, qIn * 4f); break;
                case "stretch": g.scale *= Mathf.Lerp(2.3f, 1f, OutExpo(qIn)); break;
                case "wipe": if ((float)i / Mathf.Max(1, n) > qIn) g.alpha = 0; break;
                case "slice": g.x += (i % 2 == 0 ? -1 : 1) * (1f - OutExpo(qIn)) * DesignWidth * .85f; break;
                case "blur": g.alpha *= Mathf.Pow(OutCubic(qIn), .7f); g.scale *= 1f + .18f * (1f - qIn); break;
                case "assemble":
                    {
                        float q = Mathf.Clamp01((qIn - Rand(cut.seed, i, 22) * .35f) / .65f);
                        float distance = (1f - OutExpo(q)) * g.size * 3.2f;
                        float angle = Rand(cut.seed, i, 23) * Mathf.PI * 2f;
                        g.x += Mathf.Cos(angle) * distance;
                        g.y += Mathf.Sin(angle) * distance;
                        g.rotation += Signed(cut.seed, i, 24) * 190f * (1f - q);
                        g.alpha *= Mathf.Min(1f, q * 3f);
                        break;
                    }
                default: break;
            }

            float holdAmount = Mathf.Clamp01((localIn - cut.inDur * .85f) / .25f) * (1f - pOut);
            switch (cut.hold)
            {
                case "jitter":
                    {
                        int step = Mathf.FloorToInt(lt * 24f);
                        g.x += Signed(cut.seed, step, i, 1) * g.size * .025f * holdAmount;
                        g.y += Signed(cut.seed, step, i, 2) * g.size * .025f * holdAmount;
                        g.rotation += Signed(cut.seed, step, i, 3) * 4f * holdAmount;
                        break;
                    }
                case "drift": g.x += ((cut.seed & 1) == 0 ? -1 : 1) * (lt / dur - .5f) * DesignWidth * .035f * holdAmount; break;
                case "breathe": g.scale *= 1f + .035f * Mathf.Sin(lt * Mathf.PI * 1.8f) * holdAmount; break;
                case "wave": g.y += Mathf.Sin(lt * 7f + i * .75f) * g.size * .07f * holdAmount; g.rotation += Mathf.Cos(lt * 7f + i * .75f) * 5f * holdAmount; break;
                case "glitchtick":
                    if (Rand(cut.seed, Mathf.FloorToInt(lt * 24), i, 17) < .22f * holdAmount)
                        g.x += Signed(cut.seed, i, Mathf.FloorToInt(lt * 24), 18) * g.size * .35f;
                    break;
            }

            switch (cut.exit)
            {
                case "shrink": g.scale *= 1f - InCubic(pOut) * .96f; g.alpha *= 1f - pOut * pOut; break;
                case "fall": g.y += pOut * pOut * DesignHeight * 1.2f; g.rotation += Signed(cut.seed, i, 19) * 90f * pOut; break;
                case "scatter":
                case "explode":
                    {
                        float angle = Rand(cut.seed, i, 52) * Mathf.PI * 2f;
                        float e = InCubic(pOut);
                        g.x += Mathf.Cos(angle) * DesignWidth * .7f * e;
                        g.y += Mathf.Sin(angle) * DesignWidth * .45f * e;
                        g.rotation += Signed(cut.seed, i, 53) * 540f * e;
                        g.alpha *= 1f - pOut * pOut;
                        break;
                    }
                case "drift": g.y -= InCubic(pOut) * g.size * 1.6f; g.alpha *= 1f - pOut * pOut; break;
                case "wipe": if ((float)i / Mathf.Max(1, n) < pOut) g.alpha = 0; break;
                case "slice": g.x += (i % 2 == 0 ? 1 : -1) * InCubic(pOut) * DesignWidth; break;
                case "blur": g.alpha *= 1f - pOut; g.scale *= 1f + pOut * .2f; break;
                case "stretch": g.scale *= 1f + InCubic(pOut) * 2f; g.alpha *= 1f - pOut; break;
                case "glitch":
                    if (Rand(cut.seed, Mathf.FloorToInt(lt * 24), i, 61) < .5f * pOut)
                        g.x += Signed(cut.seed, i, Mathf.FloorToInt(lt * 24), 62) * g.size * (1f + pOut);
                    g.alpha *= 1f - pOut;
                    break;
            }
        }

        void DrawStatic(string value, float x, float y, float size, Color color, float alpha, float rotation, float track)
        {
            if (string.IsNullOrEmpty(value) || alpha <= .002f) return;
            // Secondary editorial text stays a single native Text mesh, as in JIZURA's non-main items.
            Put(value, x, y, size, rotation, 1f, WithAlpha(color, alpha),
                Mathf.Min(DesignWidth * 2f, value.Length * size * (.8f + track) + size * 2f));
        }

        void Put(string value, float x, float y, float size, float rotation, float scale, Color color,
            float width = -1, float scaleX = 1f, float scaleY = 1f, bool legibilityOutline = false)
        {
            if (decorLayer) color.a = Mathf.Clamp01(color.a * DecorGain());
            if (labelCount >= labels.Length || string.IsNullOrEmpty(value) || color.a <= .002f) return;
            int slot = labelCount++;
            var label = labels[slot];
            if (!label.gameObject.activeSelf) label.gameObject.SetActive(true);
            if (label.text != value) label.text = value;
            label.fontSize = Mathf.Clamp(Mathf.RoundToInt(size), 1, 1000);
            label.color = color;
            var rt = label.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(.5f, .5f);
            rt.anchoredPosition = DesignPosition(x, y);
            rt.sizeDelta = new Vector2(width > 0 ? width : size * 1.7f, size * 1.7f);
            rt.localRotation = Quaternion.Euler(0, 0, rotation);
            rt.localScale = new Vector3(scale * scaleX, scale * scaleY, 1f);
            var outline = outlines[slot];
            outline.enabled = legibilityOutline && liveInput.backgroundOpacity < .98f;
            if (outline.enabled)
            {
                float a = (1f - liveInput.backgroundOpacity) * .8f;
                outline.effectColor = Luminance(color) > .48f ? new Color(0, 0, 0, a) : new Color(1, 1, 1, a);
            }
        }

        void LabelPlate(string value, float x, float y, float size, float rotation, float progress, Scheme sc)
        {
            if (progress <= .002f) return;
            float width = size * (Advance(value) + .7f) * progress;
            float height = size * 1.36f * progress;
            Rect(x, y, width, height, sc.fg, 1f, rotation);
            Put(value, x, y, size, rotation, progress, sc.bg);
        }

        void Rect(float x, float y, float width, float height, Color color, float alpha, float rotation = 0)
        {
            if (decorLayer) alpha *= DecorGain();
            if (width <= 0 || height <= 0 || alpha <= .002f) return;
            Image image;
            if (frontLayer)
            {
                if (frontRectCount >= frontRects.Length) return;
                image = frontRects[frontRectCount++];
            }
            else
            {
                if (rectCount >= rects.Length) return;
                image = rects[rectCount++];
            }
            if (!image.gameObject.activeSelf) image.gameObject.SetActive(true);
            image.sprite = null;
            image.color = WithAlpha(color, alpha);
            var rt = image.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(.5f, .5f);
            rt.anchoredPosition = DesignPosition(x, y);
            rt.sizeDelta = new Vector2(width, height);
            rt.localRotation = Quaternion.Euler(0, 0, rotation);
        }

        void Circle(float x, float y, float radius, Color color, float alpha, bool outline = false)
        {
            if (decorLayer) alpha *= DecorGain();
            if (radius <= 0 || alpha <= .002f) return;
            Image image;
            if (frontLayer)
            {
                if (frontRectCount >= frontRects.Length) return;
                image = frontRects[frontRectCount++];
            }
            else
            {
                if (rectCount >= rects.Length) return;
                image = rects[rectCount++];
            }
            if (!image.gameObject.activeSelf) image.gameObject.SetActive(true);
            image.sprite = outline ? ringSprite : circleSprite;
            image.color = WithAlpha(color, alpha);
            var rt = image.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(.5f, .5f);
            rt.anchoredPosition = DesignPosition(x, y);
            rt.sizeDelta = new Vector2(radius * 2f, radius * 2f);
            rt.localRotation = Quaternion.identity;
        }

        void Line(float x0, float y0, float x1, float y1, float width, Color color, float alpha)
        {
            float dx = x1 - x0, dy = y1 - y0;
            Rect((x0 + x1) * .5f, (y0 + y1) * .5f,
                Mathf.Sqrt(dx * dx + dy * dy), width, color, alpha,
                -Mathf.Atan2(dy, dx) * Mathf.Rad2Deg);
        }

        float DecorGain()
        {
            // Stage Density sets how assertive JIZURA ornament feels without
            // changing the cut plan or its deterministic absolute-time geometry.
            return Mathf.Lerp(.35f, 1.15f, liveInput.density)
                * (1f + liveInput.bands.y * liveInput.energy * .18f);
        }

        Vector2 DesignPosition(float x, float y)
        {
            // CanvasScaler's reference coordinates are centered regardless of output RT resolution.
            float scaleX = rootRect.rect.width > 1 ? rootRect.rect.width / DesignWidth : 1f;
            float scaleY = rootRect.rect.height > 1 ? rootRect.rect.height / DesignHeight : 1f;
            return new Vector2((x - DesignWidth * .5f) * scaleX, (DesignHeight * .5f - y) * scaleY);
        }

        static float Fit(int n, float widthFraction, float heightFraction)
        { return Mathf.Min(DesignHeight * heightFraction, DesignWidth * widthFraction / Mathf.Max(1, n * .94f)); }
        static float Advance(string glyph)
        {
            if (string.IsNullOrEmpty(glyph)) return 0;
            char c = glyph[0];
            if (c == ' ' || c == '\t') return .45f;
            if (char.IsLetterOrDigit(c) && c < 128) return .62f;
            if (char.IsPunctuation(c) || "、。，．！？「」『』（）".IndexOf(c) >= 0) return .55f;
            return 1f;
        }
        static bool IsLatin(string glyph) { return glyph.Length == 1 && glyph[0] < 128 && char.IsLetterOrDigit(glyph[0]); }
        static float CharScale(string glyph)
        { return IsLatin(glyph) ? .78f : char.IsPunctuation(glyph[0]) ? .48f : 1f; }
        static Color WithAlpha(Color color, float alpha) { color.a = Mathf.Clamp01(color.a * alpha); return color; }
        static float Luminance(Color c) { return c.r * .2126f + c.g * .7152f + c.b * .0722f; }
        static float OutCubic(float x) { x = Mathf.Clamp01(x); return 1f - Mathf.Pow(1f - x, 3); }
        static float InCubic(float x) { x = Mathf.Clamp01(x); return x * x * x; }
        static float OutExpo(float x) { x = Mathf.Clamp01(x); return x >= 1 ? 1 : 1 - Mathf.Pow(2f, -10f * x); }
        static float OutBack(float x)
        { x = Mathf.Clamp01(x); float a = x - 1f; return 1f + 3.6f * a * a * a + 2.6f * a * a; }
        static float Bounce(float x)
        {
            x = Mathf.Clamp01(x);
            if (x < 1f / 2.75f) return 7.5625f * x * x;
            if (x < 2f / 2.75f) { x -= 1.5f / 2.75f; return 7.5625f * x * x + .75f; }
            if (x < 2.5f / 2.75f) { x -= 2.25f / 2.75f; return 7.5625f * x * x + .9375f; }
            x -= 2.625f / 2.75f; return 7.5625f * x * x + .984375f;
        }
        static float Rand(uint seed, int a = 0, int b = 0, int c = 0, int d = 0)
        {
            unchecked
            {
                uint h = seed ^ 0x9e3779b9u;
                h = Mix(h, a); h = Mix(h, b); h = Mix(h, c); h = Mix(h, d);
                h ^= h >> 15; h *= 0x846ca68bu; h ^= h >> 16;
                return (h & 0xFFFFFFu) / 16777216f;
            }
        }
        static uint Mix(uint h, int value)
        {
            unchecked { h ^= (uint)value + 0x9e3779b9u + (h << 6) + (h >> 2); h ^= h >> 16; return h * 0x7feb352du; }
        }
        static float Signed(uint seed, int a = 0, int b = 0, int c = 0, int d = 0) { return Rand(seed, a, b, c, d) * 2f - 1f; }

        struct GlyphState
        {
            public string text;
            public float x, y, size, rotation, alpha, scale;
        }

        struct Scheme
        {
            public Color bg, fg, sub, accent, ghostA, ghostB;
            public Scheme(string bg, string fg, string sub, string accent, string a, string b)
            { this.bg = Hex(bg); this.fg = Hex(fg); this.sub = Hex(sub); this.accent = Hex(accent); ghostA = Hex(a); ghostB = Hex(b); }
        }

        static Color Hex(string value)
        { Color c; return ColorUtility.TryParseHtmlString(value, out c) ? c : Color.white; }

        static readonly Dictionary<string, Scheme[]> Schemes = new Dictionary<string, Scheme[]>(StringComparer.OrdinalIgnoreCase)
        {
            { "noir", new[] { new Scheme("#060607", "#F5EEEA", "#BDB6B2", "#F5A50C", "#F5A50C", "#16F4D4"), new Scheme("#F2EDE8", "#0B0B0C", "#4A4644", "#E0600C", "#F5A50C", "#16C4B4") } },
            { "crimson", new[] { new Scheme("#C8103F", "#FFFFFF", "#FFD9E2", "#140509", "#FFFFFF", "#39F2C8"), new Scheme("#150509", "#FF3D6E", "#FF9DB6", "#FFFFFF", "#FF3D6E", "#39F2C8") } },
            { "caution", new[] { new Scheme("#F4D21F", "#141414", "#3A3510", "#E0231C", "#E0231C", "#1F3FD8"), new Scheme("#18181A", "#F4D21F", "#DDD6B0", "#E0231C", "#E0231C", "#1F3FD8") } },
            { "magenta", new[] { new Scheme("#FF0A8C", "#FFFFFF", "#FFD2EA", "#FFFFFF", "#FF8CC8", "#2B2BD9"), new Scheme("#2B2BD9", "#FFFFFF", "#C9C9FF", "#FF0A8C", "#FF0A8C", "#FFFFFF") } },
            { "paper", new[] { new Scheme("#ECE9E3", "#1B2350", "#4D5270", "#C2185B", "#C2185B", "#1B2350"), new Scheme("#151515", "#F0EDE7", "#B8B4AC", "#C2185B", "#C2185B", "#3A4690") } },
            { "hud", new[] { new Scheme("#131315", "#EFEDEA", "#8E8B88", "#F25A2B", "#F25A2B", "#7FD7FF"), new Scheme("#0B0B0C", "#FFFFFF", "#9A9796", "#F25A2B", "#F25A2B", "#FFFFFF") } },
            { "mint", new[] { new Scheme("#0A0E0D", "#E6FFF5", "#7FB9A8", "#9CFF3A", "#FF3B6B", "#2EE6C8"), new Scheme("#3FAE93", "#0A0E0D", "#123A31", "#FFFFFF", "#FFFFFF", "#0A0E0D") } },
            { "specimen", new[] { new Scheme("#1B1A1C", "#F2F0EC", "#A19E99", "#F2F0EC", "#6E6A66", "#C8B98C"), new Scheme("#F2F0EC", "#1B1A1C", "#5E5B57", "#1B1A1C", "#B9B4AD", "#8A7A4E") } },
            { "transit", new[] { new Scheme("#5B582B", "#FFFFFF", "#E6E2BC", "#E8C21A", "#E8C21A", "#1A1A1A"), new Scheme("#1A1A1A", "#FFFFFF", "#B8B5A0", "#E8C21A", "#E8C21A", "#7C7A55") } },
            { "blueprint", new[] { new Scheme("#1B1BE8", "#FFFFFF", "#C7C7FF", "#000000", "#000000", "#8C8CFF"), new Scheme("#000000", "#FFFFFF", "#9A9AFF", "#1B1BE8", "#1B1BE8", "#FFFFFF") } },
            { "rouge", new[] { new Scheme("#E4E2E0", "#141414", "#6B6866", "#D40F1C", "#D40F1C", "#6B6866"), new Scheme("#140405", "#FFFFFF", "#C98A8E", "#E3141F", "#E3141F", "#FFFFFF") } },
            { "mono", new[] { new Scheme("#3B3D41", "#FFFFFF", "#B9BBBF", "#FFFFFF", "#FF2A2A", "#2AA8FF"), new Scheme("#141517", "#FFFFFF", "#9EA0A4", "#FFE34D", "#FF2A2A", "#2AFF7A") } },
        };

        static Scheme SchemeFor(JizuraPlan plan, int index)
        {
            Scheme[] entries;
            if (!Schemes.TryGetValue(plan.styleKey ?? "", out entries)) entries = Schemes["noir"];
            Scheme fallback = entries[(index & int.MaxValue) % entries.Length];
            if (plan.schemes == null || plan.schemes.Count == 0) return fallback;
            JizuraScheme source = plan.schemes[(index & int.MaxValue) % plan.schemes.Count];
            if (source == null) return fallback;
            // The planner preserves all original schemes and imported colour overrides.
            // JIZURA ghostA/B remain the style defaults in this subset.
            return new Scheme(source.bg, source.fg, source.sub, source.accent,
                "#" + ColorUtility.ToHtmlStringRGB(fallback.ghostA),
                "#" + ColorUtility.ToHtmlStringRGB(fallback.ghostB));
        }

        public void Dispose()
        {
            glyphCache.Clear();
            if (root) UnityEngine.Object.Destroy(root);
            if (circleSprite) UnityEngine.Object.Destroy(circleSprite);
            if (ringSprite) UnityEngine.Object.Destroy(ringSprite);
            if (backdropSprite) UnityEngine.Object.Destroy(backdropSprite);
            if (circleTexture) UnityEngine.Object.Destroy(circleTexture);
            if (ringTexture) UnityEngine.Object.Destroy(ringTexture);
            if (backdropTexture) UnityEngine.Object.Destroy(backdropTexture);
        }

        static Texture2D MakeBackdropTexture()
        {
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "JIZURA native readability veil";
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + .5f - size * .5f) / (size * .5f);
                    float dy = (y + .5f - size * .5f) / (size * .5f);
                    float r = Mathf.Clamp01(dx * dx + dy * dy);
                    float alpha = (1f - r) * (1f - r);
                    pixels[y * size + x] = new Color32(255, 255, 255,
                        (byte)Mathf.RoundToInt(alpha * 255f));
                }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        static Texture2D MakeRoundTexture(bool outline)
        {
            const int size = 128;
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            t.name = outline ? "JIZURA ring UI" : "JIZURA disc UI";
            t.filterMode = FilterMode.Bilinear;
            t.wrapMode = TextureWrapMode.Clamp;
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + .5f - size * .5f) / (size * .5f);
                    float dy = (y + .5f - size * .5f) / (size * .5f);
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = outline
                        ? Mathf.Clamp01((r - .91f) * 80f) * Mathf.Clamp01((1f - r) * 80f)
                        : Mathf.Clamp01((1f - r) * 80f);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            t.SetPixels32(px);
            t.Apply(false, true);
            return t;
        }
    }
}
