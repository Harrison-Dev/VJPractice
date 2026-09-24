// Native C# port of JIZURA at 1b48bea2d74e60f9b0ff2c247aa5919ba03a6551.
// Sources: src/01_util.js (hash/rng), src/08_planner.js (parseLyrics, computeTiming,
// multi-cut planning, partition), src/04_styles.js (base scheme colours),
// src/06_layouts.js (core technique keys). MIT, copyright 2026 hakoniwa.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace VJPractice.Stage.Jizura
{
    public sealed class JizuraScheme
    {
        public string bg, fg, sub, accent, accent2;
        public JizuraScheme(string background, string foreground, string secondary, string highlight, string secondHighlight)
        { bg = background; fg = foreground; sub = secondary; accent = highlight; accent2 = secondHighlight; }
    }

    public sealed class JizuraLine
    {
        public int index;
        public string text, note;
        public float start, end, visEnd;
        public bool impact;
        public string[] emph, chunks;
        public uint seed;
    }

    public sealed class JizuraCut
    {
        public int index, line, scheme;
        public string text, lineText, note, layout, enter, exit, hold, bg, cam, treat, trans;
        public float start, end, inDur, outDur, stagger;
        public bool emph, recap;
        public uint seed;
        public string[] decor;
        public float dur { get { return end - start; } }
    }

    public sealed class JizuraPlan
    {
        public string title, artist, styleKey, aspect;
        public bool styleSupported;
        public int W, H, fps;
        public float duration;
        public JizuraFx fx;
        public readonly List<JizuraScheme> schemes = new List<JizuraScheme>();
        public readonly List<JizuraLine> lines = new List<JizuraLine>();
        public readonly List<JizuraCut> cuts = new List<JizuraCut>();
    }

    /// <summary>Deterministic absolute-time multi-cut planner; no browser or Unity object required.</summary>
    public static class JizuraPlanner
    {
        public static readonly string[] StyleKeys = { "noir", "crimson", "caution", "magenta", "paper", "hud", "mint", "specimen", "transit", "blueprint", "rouge", "mono" };
        // Core keys from src/06_layouts.js. Later expression packs are retained in imported overrides
        // but render only when a native implementation of that key exists.
        public static readonly string[] LayoutKeys = { "center", "mixed", "vcols", "marquee", "tile", "scatter", "ring", "wave", "huge", "labels", "condensed", "gloss", "type", "diag", "circle", "stack", "pill" };
        // Original src/07_decor.js IDs that currently have native Unity implementations.
        public static readonly string[] DecorKeys = { "brackets", "rings", "dots", "arrows", "slash", "grid", "stripes", "bars", "counter" };
        // Default picks are the core animations currently implemented in native renderer.
        // Fragment-only assemble/explode remain valid as explicit project overrides.
        static readonly string[] EnterKeys = { "cut", "slice", "type", "pop", "drop", "stretch", "wipe", "blur", "spin", "flicker", "scramble", "zoom" };
        static readonly string[] ExitKeys = { "cut", "fall", "drift", "slice", "wipe", "shrink", "blur", "stretch", "scatter", "glitch" };
        static readonly string[] HoldKeys = { "still", "jitter", "drift", "breathe", "wave", "glitchtick" };
        static readonly Regex Lrc = new Regex(@"^\[(\d+):(\d+(?:[.:]\d+)?)\]", RegexOptions.Compiled);
        static readonly Regex Meta = new Regex(@"^\[(ti|ar|al|by|offset):(.*)\]$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        static readonly Regex Emphasis = new Regex(@"\*([^*]+)\*", RegexOptions.Compiled);
        static readonly HashSet<char> Punct = new HashSet<char>("、。，．,.!?！？…‥・「」『』（）()【】〈〉《》〔〕［］[]'\"“”‘’ー〜～:：;；-—―");
        static readonly Dictionary<string, string[][]> Palettes = MakePalettes();

        sealed class SourceLine
        {
            public string text, note;
            public bool impact, gapBefore;
            public string[] emph, manual;
            public float? lrc;
        }
        sealed class Rng
        {
            uint s;
            public Rng(uint seed) { s = seed; }
            public double Next()
            {
                unchecked
                {
                    s += 0x6d2b79f5;
                    uint t = s;
                    t = (t ^ (t >> 15)) * (t | 1);
                    t ^= t + (t ^ (t >> 7)) * (t | 61);
                    return ((t ^ (t >> 14)) & 0xffffffffu) / 4294967296.0;
                }
            }
            public bool Chance(double p) { return Next() < p; }
            public float Range(float lo, float hi) { return lo + (hi - lo) * (float)Next(); }
            public int Int(int lo, int hi) { return lo + (int)Math.Floor(Next() * (hi - lo + 1)); }
            public string Pick(string[] values) { return values[Math.Min(values.Length - 1, (int)(Next() * values.Length))]; }
        }

        public static JizuraPlan Plan(JizuraProject project) { return Build(project); }

        public static JizuraPlan Build(JizuraProject project, float audioDuration = 0f, IList<float> beats = null)
        {
            if (project == null) throw new ArgumentNullException(nameof(project));
            var source = ParseLyrics(project.lyrics, out string metaTitle, out string metaArtist);
            if (source.Count > 10000) throw new FormatException("Too many JIZURA lyric lines");
            var p = new JizuraPlan { title = string.IsNullOrEmpty(project.title) ? metaTitle : project.title,
                artist = string.IsNullOrEmpty(project.artist) ? metaArtist : project.artist,
                styleKey = project.style ?? "noir",
                styleSupported = Array.IndexOf(StyleKeys, project.style) >= 0,
                aspect = project.aspect, fps = project.fps > 0 ? project.fps : 24, fx = project.fx ?? new JizuraFx() };
            Size(project.aspect, out p.W, out p.H);
            AddSchemes(p, project);
            float[] starts, ends;
            ComputeTiming(project, source, audioDuration, out starts, out ends, out p.duration);
            var history = new List<JizuraCut>();
            int scheme = 0;
            // Title-card positioning follows the source; native visual treatment is delegated to renderer.
            if (!string.IsNullOrEmpty(p.title) && source.Count > 0 && starts[0] >= 1.1f)
                p.cuts.Add(new JizuraCut { line = -1, text = p.title, lineText = p.title, note = p.artist,
                    start = .1f, end = starts[0] - .04f, layout = "title", enter = "blur", exit = "blur",
                    hold = "still", bg = "none", cam = "push", treat = "none", inDur = .3f, outDur = .25f,
                    decor = new string[0], seed = Hash(project.seed, 999, 1) });
            for (int li = 0; li < source.Count; li++)
            {
                var ln = source[li]; float s = starts[li], e = ends[li];
                var ov = project.GetOverride(li);
                uint lineSeed = ov != null && ov.locked && ov.hasLockedSeed ? ov.lockedSeed : Hash(project.seed, li + 1, ov != null ? ov.seed : 0);
                var rng = new Rng(lineSeed);
                int count = CountText(ln.text);
                float visEnd = Math.Min(e, s + Math.Max(3.6f, count * .5f + 1.2f));
                float span = Math.Max(.35f, visEnd - s);
                string[] chunks = ln.manual ?? Chunk(ln.text, project.lang);
                var line = new JizuraLine { index = li, text = ln.text, note = ln.note, start = s, end = e, visEnd = visEnd,
                    impact = ln.impact, emph = ln.emph, chunks = chunks, seed = lineSeed };
                p.lines.Add(line);
                float nominal = Lerp(1.3f, .5f, p.fx.density);
                int nCuts = (int)Math.Round(span / nominal, MidpointRounding.AwayFromZero);
                int maxCuts = chunks.Length + (chunks.Length >= 2 && span > 2f ? 1 : 0);
                nCuts = Math.Max(1, Math.Min(nCuts, Math.Max(1, maxCuts)));
                if (ov != null && ov.single) nCuts = 1;
                int groups = Math.Min(nCuts, chunks.Length);
                var texts = groups <= 1 ? new List<string> { ln.text } : Partition(chunks, groups);
                bool recap = nCuts > texts.Count && texts.Count >= 2;
                if (recap) texts.Add(ln.text);
                float[] bounds = Bounds(texts, s, visEnd, recap, project.timing, beats);
                if (p.schemes.Count > 1 && li > 0 && rng.Chance(p.fx.bgSwitch * (ln.impact ? 1.8f : 1f)))
                    scheme = (scheme + 1 + rng.Int(0, p.schemes.Count - 2)) % p.schemes.Count;
                for (int k = 0; k < texts.Count; k++)
                {
                    string txt = texts[k]; float cs = bounds[k], ce = bounds[k + 1], dur = ce - cs;
                    int n = CountText(txt);
                    bool isRecap = recap && k == texts.Count - 1;
                    bool emph = ln.impact && (k == 0 || isRecap) || ln.emph.Any(w => txt.Contains(w));
                    string layout = !string.IsNullOrEmpty(ov?.layout) ? ov.layout : PickLayout(project, p, rng, n, dur, history, emph, isRecap);
                    string enter = !string.IsNullOrEmpty(ov?.enter) ? ov.enter : PickEnter(project, rng, layout, dur, n, history, emph);
                    string exit = !string.IsNullOrEmpty(ov?.exit) ? ov.exit : PickExit(project, rng, layout, dur, k == texts.Count - 1, history);
                    string hold = !string.IsNullOrEmpty(ov?.hold) ? ov.hold : PickHold(project, rng, history);
                    float inDur = Clamp(dur * .36f, .12f, .6f);
                    if (enter == "type") inDur = Clamp(n * .055f + .1f, .15f, dur * .65f);
                    if (enter == "assemble") inDur = Clamp(dur * .45f, .22f, .75f);
                    if (enter == "cut") inDur = .12f;
                    float outDur = exit == "cut" ? 0f : Clamp(dur * .3f, .14f, .55f);
                    if (exit == "explode" || exit == "fall" || exit == "drift") outDur = Clamp(dur * .38f, .25f, .7f);
                    if (inDur + outDur > dur * .92f) { float f = dur * .92f / (inDur + outDur); inDur *= f; outDur *= f; }
                    int cutScheme = scheme;
                    if (p.schemes.Count > 1 && k > 0 && rng.Chance(.12f * p.fx.bgSwitch)) cutScheme = (scheme + 1) % p.schemes.Count;
                    // Original transitions are considered only at an adjacent cut boundary.
                    // The native renderer has no transition compositor yet, so only explicit
                    // override IDs are recorded and visibly reported as unsupported.
                    JizuraCut previous = p.cuts.Count > 0 ? p.cuts[p.cuts.Count - 1] : null;
                    bool canTrans = previous != null && Math.Abs(previous.end - cs) < .06f && previous.layout != "interlude" && dur > .5f;
                    var cut = new JizuraCut { line = li, text = txt, lineText = ln.text, note = ln.note,
                        start = cs, end = ce, layout = layout, enter = enter, exit = exit, hold = hold,
                        inDur = inDur, outDur = outDur, scheme = cutScheme, seed = Hash(unchecked((int)lineSeed), k, 17),
                        emph = emph, recap = isRecap, stagger = rng.Range(.025f, .06f),
                        bg = !string.IsNullOrEmpty(ov?.bg) ? ov.bg : "none",
                        cam = !string.IsNullOrEmpty(ov?.cam) ? ov.cam : "push",
                        treat = !string.IsNullOrEmpty(ov?.treat) ? ov.treat : "none",
                        trans = canTrans && !string.IsNullOrEmpty(ov?.trans) ? ov.trans : null,
                        decor = ov?.decor != null ? (string[])ov.decor.Clone() : PickDecor(project, p.fx, rng, layout, history) };
                    p.cuts.Add(cut); history.Add(cut);
                }
                if (li + 1 < source.Count && starts[li + 1] - visEnd > 1.3f)
                    p.cuts.Add(new JizuraCut { line = li, text = p.title ?? "", lineText = "", start = visEnd, end = starts[li + 1],
                        layout = "interlude", enter = "blur", exit = "blur", hold = "still", bg = "none", cam = "push",
                        treat = "none", inDur = .3f, outDur = .3f, scheme = scheme, seed = Hash(unchecked((int)lineSeed), 405), decor = new string[0] });
            }
            p.cuts.Sort((a, b) => a.start.CompareTo(b.start));
            for (int i = 0; i < p.cuts.Count; i++) p.cuts[i].index = i;
            return p;
        }

        static List<SourceLine> ParseLyrics(string raw, out string title, out string artist)
        {
            title = ""; artist = ""; bool gap = false; var lines = new List<SourceLine>();
            foreach (string row in (raw ?? "").Replace("\r", "").Split('\n'))
            {
                string src = row.Trim(); if (src.Length == 0) { if (lines.Count > 0) gap = true; continue; }
                if (src.StartsWith("#", StringComparison.Ordinal)) continue;
                var meta = Meta.Match(src);
                if (meta.Success) { if (meta.Groups[1].Value.Equals("ti", StringComparison.OrdinalIgnoreCase)) title = meta.Groups[2].Value.Trim();
                    if (meta.Groups[1].Value.Equals("ar", StringComparison.OrdinalIgnoreCase)) artist = meta.Groups[2].Value.Trim(); continue; }
                string s = src; var times = new List<float>(); Match m;
                while ((m = Lrc.Match(s)).Success)
                {
                    float sec = float.Parse(m.Groups[2].Value.Replace(':', '.'), CultureInfo.InvariantCulture);
                    times.Add(int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) * 60 + sec);
                    s = s.Substring(m.Length);
                }
                s = s.Trim(); string note = null; int bar = s.IndexOf('|');
                if (bar >= 0) { note = s.Substring(bar + 1).Trim(); s = s.Substring(0, bar).Trim(); }
                bool impact = s.Length > 1 && s.EndsWith("!", StringComparison.Ordinal);
                if (impact) s = s.Substring(0, s.Length - 1).Trim();
                var emph = new List<string>();
                s = Emphasis.Replace(s, mm => { emph.Add(mm.Groups[1].Value); return mm.Groups[1].Value; });
                string[] manual = null;
                if (s.Contains("/"))
                {
                    manual = s.Split('/').Select(x => x.Trim()).Where(x => x.Length > 0).ToArray();
                    bool latin = manual.Any(x => x.Any(c => c <= 127 && char.IsLetter(c)));
                    s = string.Join(latin ? " " : "", manual);
                }
                if (s.Length == 0) continue;
                var line = new SourceLine { text = s, note = note, impact = impact, emph = emph.ToArray(), manual = manual, gapBefore = gap };
                gap = false;
                if (times.Count > 0) foreach (float t in times) lines.Add(new SourceLine { text = s, note = note, impact = impact, emph = line.emph, manual = manual, gapBefore = line.gapBefore, lrc = t });
                else lines.Add(line);
            }
            if (lines.Any(l => l.lrc.HasValue)) lines = lines.OrderBy(l => l.lrc ?? float.MaxValue).ToList();
            return lines;
        }

        static void ComputeTiming(JizuraProject project, List<SourceLine> lines, float audioDuration, out float[] starts, out float[] ends, out float duration)
        {
            var t = project.timing ?? new JizuraTiming(); float beat = t.bpm > 0 ? 60f / t.bpm : 0f;
            bool allLrc = lines.Count > 0 && lines.All(l => l.lrc.HasValue);
            starts = new float[lines.Count]; ends = new float[lines.Count];
            for (int i = 0; i < lines.Count; i++)
            {
                float manual;
                if (allLrc) starts[i] = lines[i].lrc.Value;
                else if (t.lineTimes.TryGetValue(i, out manual)) starts[i] = manual;
                else if (i == 0) starts[i] = t.offset;
                else
                {
                    int n = Codepoints(lines[i - 1].text);
                    float d = Clamp(.8f + n * .17f, 1.3f, 5.2f) * (t.lineScale == 0 ? 1 : t.lineScale);
                    if (beat > 0) d = Math.Max(2f, (float)Math.Round(d / beat, MidpointRounding.AwayFromZero)) * beat;
                    starts[i] = starts[i - 1] + d + (lines[i].gapBefore ? (beat > 0 ? beat * 2f : .8f) : 0f);
                }
            }
            for (int i = 0; i < lines.Count; i++)
            {
                if (i + 1 < lines.Count) ends[i] = Math.Max(starts[i] + .35f, starts[i + 1]);
                else
                {
                    float d = Clamp(.8f + Codepoints(lines[i].text) * .17f, 1.5f, 5.2f) * (t.lineScale == 0 ? 1 : t.lineScale);
                    if (beat > 0) d = Math.Max(2f, (float)Math.Round(d / beat, MidpointRounding.AwayFromZero)) * beat;
                    ends[i] = starts[i] + d;
                }
            }
            duration = (ends.Length > 0 ? ends[ends.Length - 1] : 3f) + t.tail;
            if (audioDuration > 0 && t.useAudioLength) duration = Math.Max(audioDuration, ends.Length > 0 ? ends[ends.Length - 1] + .2f : 1f);
        }

        static string[] Chunk(string text, string lang)
        {
            if (string.IsNullOrWhiteSpace(text)) return new[] { text ?? "" };
            if (text.Contains(" "))
            {
                var words = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (lang == "en" || words.All(w => w.Any(c => c <= 127 && char.IsLetter(c))))
                {
                    var phrases = new List<string>(); var cur = new List<string>(); int letters = 0;
                    foreach (string w in words)
                    {
                        cur.Add(w); letters += w.Count(char.IsLetterOrDigit);
                        if (letters >= 9 || cur.Count >= 3 || ",.;:!?".Contains(w[w.Length - 1]))
                        { phrases.Add(string.Join(" ", cur)); cur.Clear(); letters = 0; }
                    }
                    if (cur.Count > 0) phrases.Add(string.Join(" ", cur));
                    if (phrases.Count >= 2 && phrases[phrases.Count - 1].Length <= 4)
                    { phrases[phrases.Count - 2] += " " + phrases[phrases.Count - 1]; phrases.RemoveAt(phrases.Count - 1); }
                    return phrases.ToArray();
                }
                return words;
            }
            // The browser version uses Intl.Segmenter. Native C# has no equivalent JPN/CHN
            // morphology; use bounded script runs, splitting long CJK runs into readable groups.
            var chunks = new List<string>(); var current = new StringBuilder(); int previous = -1;
            foreach (char c in text)
            {
                int kind = ScriptKind(c);
                if (kind == 0) { if (current.Length > 0) { chunks.Add(current.ToString()); current.Clear(); } chunks.Add(c.ToString()); previous = -1; continue; }
                if (current.Length > 0 && kind != previous && !(previous == 1 && kind == 2)) { chunks.Add(current.ToString()); current.Clear(); }
                current.Append(c); previous = kind;
            }
            if (current.Length > 0) chunks.Add(current.ToString());
            var outChunks = new List<string>();
            foreach (string chunk in chunks)
            {
                if (Punct.Contains(chunk[0]) && outChunks.Count > 0) { outChunks[outChunks.Count - 1] += chunk; continue; }
                if (chunk.Length > 4 && ScriptKind(chunk[0]) == 1)
                    for (int i = 0; i < chunk.Length; i += 3) outChunks.Add(chunk.Substring(i, Math.Min(3, chunk.Length - i)));
                else outChunks.Add(chunk);
            }
            for (int i = outChunks.Count - 1; i > 0; i--)
                if (outChunks[i].Length == 1 && ScriptKind(outChunks[i][0]) != 1)
                { outChunks[i - 1] += outChunks[i]; outChunks.RemoveAt(i); }
            return outChunks.Count > 0 ? outChunks.ToArray() : new[] { text };
        }

        static int ScriptKind(char c)
        {
            if (Punct.Contains(c)) return 0;
            if (c >= 0x3400 && c <= 0x9fff) return 1;
            if (c >= 0x3040 && c <= 0x309f) return 2;
            if (c >= 0x30a0 && c <= 0x30ff) return 3;
            if (char.IsLetterOrDigit(c)) return 4;
            return 5;
        }
        static int CountText(string text) { return Codepoints(new string((text ?? "").Where(c => !char.IsWhiteSpace(c)).ToArray())); }
        static int Codepoints(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            int n = 0;
            for (int i = 0; i < text.Length; i++)
            {
                if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1])) i++;
                n++;
            }
            return n;
        }

        static List<string> Partition(string[] chunks, int groups)
        {
            float total = chunks.Sum(c => Codepoints(c) + 1); float target = total / groups;
            var result = new List<List<string>>(); var cur = new List<string>(); float acc = 0; int remaining = groups;
            for (int i = 0; i < chunks.Length; i++)
            {
                float len = Codepoints(chunks[i]) + 1;
                if (cur.Count > 0 && (acc + len / 2f > target || chunks.Length - i < remaining) && result.Count < groups - 1)
                { result.Add(cur); cur = new List<string>(); acc = 0; remaining--; }
                cur.Add(chunks[i]); acc += len;
            }
            if (cur.Count > 0) result.Add(cur);
            return result.Select(g => string.Join(g.Any(x => x.Any(c => c <= 127 && char.IsLetter(c))) ? " " : "", g)).ToList();
        }

        static float[] Bounds(List<string> texts, float start, float end, bool recap, JizuraTiming timing, IList<float> beats)
        {
            var weights = texts.Select((x, i) => Codepoints(x) + 1.6f).ToArray();
            if (recap) weights[weights.Length - 1] = weights.Take(weights.Length - 1).Average() * 1.25f;
            float total = weights.Sum(); var b = new float[texts.Count + 1]; b[0] = start; float acc = start;
            for (int i = 0; i < texts.Count; i++) { acc += (end - start) * weights[i] / total; b[i + 1] = i == texts.Count - 1 ? end : acc; }
            for (int i = 1; i < b.Length - 1; i++) b[i] = Clamp(Snap(b[i], timing, beats), b[i - 1] + .22f, b[i + 1] - .22f);
            return b;
        }
        static float Snap(float time, JizuraTiming timing, IList<float> beats)
        {
            if (timing == null || !timing.snap || beats == null || beats.Count == 0) return time;
            float best = time, dist = .13f;
            foreach (float beat in beats)
                if (Math.Abs(beat - time) < dist) { best = beat; dist = Math.Abs(beat - time); }
            return best;
        }

        // Original first-version style recipe weights from src/04_styles.js.
        static readonly Dictionary<string, Dictionary<string, float>> StyleBias =
            new Dictionary<string, Dictionary<string, float>>
        {
            { "noir/layout", new Dictionary<string, float> { { "vcols", 2f }, { "condensed", 2f }, { "marquee", 1.6f }, { "tile", 1.4f }, { "center", 1.2f } } },
            { "noir/enter", new Dictionary<string, float> { { "assemble", 2.2f }, { "slice", 1.8f }, { "stretch", 1.4f } } },
            { "noir/exit", new Dictionary<string, float> { { "explode", 1.8f }, { "fall", 1.2f }, { "drift", 1.4f } } },
            { "crimson/layout", new Dictionary<string, float> { { "huge", 2f }, { "marquee", 1.6f }, { "scatter", 1.5f }, { "stack", 1.3f }, { "type", 1.3f } } },
            { "crimson/enter", new Dictionary<string, float> { { "scramble", 1.6f }, { "slice", 1.6f }, { "type", 1.3f } } },
            { "crimson/exit", new Dictionary<string, float> { { "glitch", 2f }, { "slice", 1.6f } } },
            { "caution/layout", new Dictionary<string, float> { { "ring", 2.2f }, { "mixed", 2f }, { "circle", 1.4f }, { "gloss", 1.2f } } },
            { "caution/enter", new Dictionary<string, float> { { "pop", 1.6f }, { "spin", 1.5f }, { "wipe", 1.2f } } },
            { "caution/exit", new Dictionary<string, float> { { "scatter", 1.5f }, { "shrink", 1.2f } } },
            { "magenta/layout", new Dictionary<string, float> { { "wave", 2.2f }, { "gloss", 1.6f }, { "huge", 1.6f }, { "pill", 1.4f }, { "scatter", 1.2f } } },
            { "magenta/enter", new Dictionary<string, float> { { "pop", 2f }, { "drop", 1.6f }, { "spin", 1.3f }, { "blur", 1.2f } } },
            { "magenta/exit", new Dictionary<string, float> { { "scatter", 1.6f }, { "shrink", 1.4f }, { "blur", 1.2f } } },
            { "paper/layout", new Dictionary<string, float> { { "stack", 2.2f }, { "mixed", 1.8f }, { "huge", 1.6f }, { "vcols", 1.4f }, { "circle", 1.2f } } },
            { "paper/enter", new Dictionary<string, float> { { "wipe", 1.6f }, { "stretch", 1.4f }, { "blur", 1.2f }, { "slice", 1.2f } } },
            { "paper/exit", new Dictionary<string, float> { { "drift", 1.6f }, { "wipe", 1.4f } } },
            { "hud/layout", new Dictionary<string, float> { { "circle", 2f }, { "ring", 1.6f }, { "vcols", 1.4f }, { "center", 1.2f }, { "gloss", 1f } } },
            { "hud/enter", new Dictionary<string, float> { { "blur", 1.6f }, { "type", 1.4f }, { "assemble", 1.3f } } },
            { "hud/exit", new Dictionary<string, float> { { "blur", 1.4f }, { "drift", 1.4f }, { "explode", 1.2f } } },
            { "mint/layout", new Dictionary<string, float> { { "labels", 2.4f }, { "tile", 1.6f }, { "marquee", 1.4f }, { "type", 1.4f }, { "diag", 1.2f } } },
            { "mint/enter", new Dictionary<string, float> { { "scramble", 1.8f }, { "type", 1.6f }, { "flicker", 1.4f } } },
            { "mint/exit", new Dictionary<string, float> { { "glitch", 1.6f }, { "slice", 1.4f } } },
            { "specimen/layout", new Dictionary<string, float> { { "gloss", 2.6f }, { "vcols", 1.8f }, { "mixed", 1.4f }, { "center", 1.2f }, { "tile", 1f } } },
            { "specimen/enter", new Dictionary<string, float> { { "type", 1.8f }, { "blur", 1.6f }, { "wipe", 1.2f } } },
            { "specimen/exit", new Dictionary<string, float> { { "blur", 1.6f }, { "drift", 1.2f }, { "wipe", 1.2f } } },
            { "transit/layout", new Dictionary<string, float> { { "mixed", 2f }, { "scatter", 1.6f }, { "diag", 1.4f }, { "huge", 1.2f } } },
            { "transit/enter", new Dictionary<string, float> { { "spin", 1.6f }, { "drop", 1.4f }, { "pop", 1.2f }, { "stretch", 1.2f } } },
            { "transit/exit", new Dictionary<string, float> { { "scatter", 1.4f }, { "stretch", 1.4f } } },
            { "blueprint/layout", new Dictionary<string, float> { { "diag", 2.2f }, { "labels", 1.4f }, { "huge", 1.4f }, { "condensed", 1.2f } } },
            { "blueprint/enter", new Dictionary<string, float> { { "wipe", 1.6f }, { "slice", 1.6f }, { "stretch", 1.3f } } },
            { "blueprint/exit", new Dictionary<string, float> { { "wipe", 1.6f }, { "slice", 1.4f }, { "glitch", 1.2f } } },
            { "rouge/layout", new Dictionary<string, float> { { "huge", 2.2f }, { "pill", 2f }, { "mixed", 1.4f }, { "labels", 1.2f }, { "center", 1.2f } } },
            { "rouge/enter", new Dictionary<string, float> { { "zoom", 1.6f }, { "wipe", 1.4f }, { "pop", 1.2f } } },
            { "rouge/exit", new Dictionary<string, float> { { "shrink", 1.6f }, { "wipe", 1.2f } } },
            { "mono/layout", new Dictionary<string, float> { { "circle", 1.8f }, { "ring", 1.6f }, { "pill", 1.4f }, { "tile", 1.4f }, { "vcols", 1.3f } } },
            { "mono/enter", new Dictionary<string, float> { { "assemble", 1.4f }, { "blur", 1.4f }, { "zoom", 1.3f } } },
            { "mono/exit", new Dictionary<string, float> { { "explode", 1.4f }, { "glitch", 1.4f }, { "blur", 1.2f } } },
        };
        static float StyleWeight(string style, string group, string key)
        {
            Dictionary<string, float> weights; float value;
            return StyleBias.TryGetValue((style ?? "noir") + "/" + group, out weights) &&
                weights.TryGetValue(key, out value) ? value : 1f;
        }
        static bool Fits(string key, int n)
        {
            // Exact first-version J.LAYOUTS.<key>.fits() from src/06_layouts.js.
            switch (key)
            {
                case "mixed": return n >= 2 && n <= 16;
                case "vcols": return n <= 18;
                case "marquee": case "tile": case "gloss": case "stack": return n <= 12;
                case "scatter": return n >= 2 && n <= 14;
                case "ring": case "wave": return n >= 2 && n <= 16;
                case "huge": return n <= 8;
                case "labels": return n <= 16;
                case "condensed": case "circle": return n <= 10;
                case "type": return n <= 28;
                case "diag": case "pill": return n <= 14;
                default: return true;
            }
        }
        static float PortraitWeight(string key)
        {
            switch (key)
            {
                case "vcols": return 1.9f; case "condensed": case "huge": return 1.3f;
                case "center": return 1.2f; case "stack": return 1.1f;
                case "mixed": return .7f; case "marquee": case "wave": return .6f;
                case "diag": case "type": return .8f; case "gloss": return .5f;
                default: return 1f;
            }
        }

        static string PickLayout(JizuraProject project, JizuraPlan plan, Rng rng, int n, float dur, List<JizuraCut> history, bool emph, bool recap)
        {
            var keys = new List<string>(); var weights = new List<float>();
            foreach (string key in LayoutKeys)
            {
                if (!project.IsEnabled("layout", key) || !Fits(key, n)) continue;
                float w = StyleWeight(project.style, "layout", key) * Novelty(history, key, "layout");
                if (plan.H > plan.W) w *= PortraitWeight(key);
                if (emph && (key == "huge" || key == "center" || key == "tile" || key == "marquee" || key == "condensed")) w *= 2f;
                if (recap && (key == "center" || key == "stack" || key == "marquee" || key == "tile" || key == "mixed" || key == "type" || key == "gloss")) w *= 1.8f;
                if (dur < .5f && (key == "wave" || key == "ring" || key == "labels" || key == "gloss" || key == "type" || key == "tile")) w *= .3f;
                if (dur < .5f && (key == "center" || key == "huge" || key == "condensed" || key == "vcols")) w *= 1.4f;
                keys.Add(key); weights.Add(w);
            }
            return Weighted(rng, keys, weights, "center");
        }
        static float EnterLayoutWeight(string layout, string key)
        {
            switch (layout)
            {
                case "type": if (key == "type") return 4f; if (key == "scramble") return 1.5f; break;
                case "ring": if (key == "pop" || key == "spin") return 2f; if (key == "slice" || key == "wipe") return .2f; break;
                case "labels": if (key == "cut") return 3f; if (key == "pop") return 1f; break;
                case "wave": if (key == "pop" || key == "drop") return 1.5f; if (key == "slice") return .3f; break;
                case "tile": if (key == "slice" || key == "zoom") return 1.4f; break;
                case "huge": if (key == "zoom" || key == "wipe" || key == "slice") return 1.5f; if (key == "stretch") return 1.3f; if (key == "type") return .2f; break;
                case "mixed": if (key == "pop" || key == "drop") return 1.6f; if (key == "spin") return 1.3f; break;
                case "scatter": if (key == "pop" || key == "spin") return 1.5f; if (key == "drop") return 1.2f; break;
                case "vcols": if (key == "type") return 1.2f; break;
                case "pill": if (key == "wipe") return 1.8f; if (key == "type") return 1.2f; break;
            }
            return 1f;
        }
        static string PickEnter(JizuraProject project, Rng rng, string layout, float dur, int n, List<JizuraCut> history, bool emph)
        {
            var keys = new List<string>(); var weights = new List<float>();
            foreach (string key in EnterKeys)
            {
                if (!project.IsEnabled("enter", key)) continue;
                float w = StyleWeight(project.style, "enter", key) * Novelty(history, key, "enter") * EnterLayoutWeight(layout, key);
                if (key == "cut") w *= .5f;
                if (dur < .45f && (key == "type" || key == "drop" || key == "spin" || key == "pop" || key == "flicker")) w *= .25f;
                if (dur < .45f && (key == "cut" || key == "slice" || key == "zoom" || key == "stretch")) w *= 1.8f;
                if (key == "type" && n > 18) w *= .3f;
                if (emph && (key == "zoom" || key == "slice")) w *= 1.8f;
                keys.Add(key); weights.Add(w);
            }
            return Weighted(rng, keys, weights, "cut");
        }
        static string PickExit(JizuraProject project, Rng rng, string layout, float dur, bool lastOfLine, List<JizuraCut> history)
        {
            var keys = new List<string>(); var weights = new List<float>();
            foreach (string key in ExitKeys)
            {
                if (!project.IsEnabled("exit", key)) continue;
                float w = StyleWeight(project.style, "exit", key) * Novelty(history, key, "exit");
                if (key == "cut") w *= dur < .6f ? 4f : lastOfLine ? 1.2f : 2.2f;
                if (dur < .6f && key != "cut") w *= .4f;
                if ((layout == "labels" || layout == "ring" || layout == "tile") && (key == "fall" || key == "drift")) w *= .3f;
                keys.Add(key); weights.Add(w);
            }
            return Weighted(rng, keys, weights, "cut");
        }
        static string PickHold(JizuraProject project, Rng rng, List<JizuraCut> history)
        {
            var keys = new List<string>(); var weights = new List<float>();
            JizuraFx fx = project.fx ?? new JizuraFx();
            foreach (string key in HoldKeys)
            {
                if (!project.IsEnabled("hold", key)) continue;
                float w = key == "jitter" ? 1.2f : key == "breathe" ? .7f : key == "wave" ? .4f : key == "glitchtick" ? .9f : 1f;
                if (key == "jitter") w *= .4f + fx.motion;
                if (key == "glitchtick") w *= fx.glitch;
                keys.Add(key); weights.Add(w * Novelty(history, key, "hold"));
            }
            return Weighted(rng, keys, weights, "still");
        }
        static string[] PickDecor(JizuraProject project, JizuraFx fx, Rng rng, string layout, List<JizuraCut> history)
        {
            // Ported selection from src/08_planner.js pickDecor(): density, style bias,
            // enabled switches, recent-use penalty and no repeat within one cut.
            // Limit to two because the native renderer has no original decorParams yet.
            int count = Math.Max(0, Math.Min(2, (int)Math.Round(fx.decor * 2.8f * rng.Range(.45f, 1.15f), MidpointRounding.AwayFromZero)));
            if (count == 0) return new string[0];
            bool busy = layout == "ring" || layout == "wave" || layout == "tile" || layout == "labels" || layout == "marquee";
            var keys = new List<string>(); var weights = new List<float>();
            foreach (string id in DecorKeys)
            {
                if (!project.IsEnabled("decor", id)) continue;
                if (busy && (id == "grid" || id == "stripes" || id == "bars" || id == "counter")) continue;
                float w = DecorWeight(project.style, id);
                for (int i = Math.Max(0, history.Count - 2); i < history.Count; i++)
                    if (history[i].decor != null && Array.IndexOf(history[i].decor, id) >= 0) { w *= .35f; break; }
                keys.Add(id); weights.Add(w);
            }
            var picked = new List<string>();
            for (int i = 0; i < count && keys.Count > 0; i++)
            {
                string id = Weighted(rng, keys, weights, keys[0]);
                int index = keys.IndexOf(id); keys.RemoveAt(index); weights.RemoveAt(index);
                picked.Add(id);
            }
            return picked.ToArray();
        }
        static float DecorWeight(string style, string id)
        {
            // Exact relevant style-pack decor tendencies from src/04_styles.js;
            // entries not named by a pack use the source pickDecor fallback of .35.
            switch (style)
            {
                case "noir": if (id == "rings") return .8f; if (id == "slash") return .6f; break;
                case "crimson": if (id == "arrows" || id == "rings") return .8f; break;
                case "caution": if (id == "rings" || id == "arrows") return 1f; if (id == "counter") return .8f; break;
                case "magenta": if (id == "counter") return 1f; break;
                case "paper": if (id == "bars") return 1f; break;
                case "hud": if (id == "rings" || id == "arrows") return 1f; if (id == "grid") return .8f; if (id == "slash") return .6f; break;
                case "mint": if (id == "grid") return .8f; break;
                case "specimen": if (id == "slash") return .8f; if (id == "rings") return .4f; break;
                case "transit": if (id == "arrows") return 1.4f; if (id == "counter") return .8f; if (id == "rings") return .6f; break;
                case "blueprint": if (id == "stripes" || id == "slash") return 1f; if (id == "grid") return .6f; break;
                case "rouge": if (id == "stripes") return .6f; break;
                case "mono": if (id == "rings") return 1.4f; if (id == "dots") return 1f; break;
            }
            return .35f;
        }
        static float Novelty(List<JizuraCut> history, string value, string group)
        {
            float w = 1f;
            for (int i = history.Count - 1, d = 0; i >= 0 && d < 6; i--, d++)
            {
                string old = group == "layout" ? history[i].layout : group == "enter" ? history[i].enter : group == "exit" ? history[i].exit : history[i].hold;
                if (old == value) w *= d < 2 ? .2f : .6f;
            }
            return w;
        }
        static string Weighted(Rng rng, List<string> keys, List<float> weights, string fallback)
        {
            if (keys.Count == 0) return fallback;
            float total = weights.Sum(); float x = (float)rng.Next() * total;
            for (int i = 0; i < keys.Count; i++) if ((x -= weights[i]) <= 0) return keys[i];
            return keys[keys.Count - 1];
        }

        public static uint Hash(int a, int b = 0, int c = 0, int d = 0, int e = 0)
        {
            // Bit-for-bit J.h from src/01_util.js, using 32-bit wrapping multiplication.
            unchecked
            {
                uint h = 0x9e3779b9u ^ (uint)a;
                h = (h ^ (h >> 16)) * 0x85ebca6bu;
                h += ((uint)b + 0x632be5abu) * 0xc2b2ae35u;
                h = (h ^ (h >> 13)) * 0xc2b2ae35u;
                h += ((uint)c + 0x5bd1e995u) * 0x27d4eb2fu;
                h = (h ^ (h >> 15)) * 0x165667b1u;
                h += ((uint)d + 0x1b873593u) * 0x85ebca6bu;
                h = (h ^ (h >> 16)) * 0x27d4eb2fu;
                h += ((uint)e + 0x68e31da4u) * 0x9e3779b1u;
                h ^= h >> 15; h *= 0x2c1b3c6du; h ^= h >> 12; h *= 0x297a2d39u; h ^= h >> 15;
                return h;
            }
        }
        static float Clamp(float x, float lo, float hi) { return x < lo ? lo : x > hi ? hi : x; }
        static float Lerp(float a, float b, float t) { return a + (b - a) * t; }
        static void Size(string aspect, out int width, out int height)
        {
            width = 1920; height = 1080;
            switch (aspect)
            {
                case "9:16": width = 1080; height = 1920; break;
                case "1:1": width = 1440; height = 1440; break;
                case "4:5": width = 1440; height = 1800; break;
                case "21:9": width = 2520; height = 1080; break;
                case "4:3": width = 1440; height = 1080; break;
                case "3:4": width = 1080; height = 1440; break;
            }
        }
        static void AddSchemes(JizuraPlan plan, JizuraProject project)
        {
            string[][] palette;
            if (!Palettes.TryGetValue(plan.styleKey, out palette)) palette = Palettes["noir"];
            foreach (string[] row in palette) plan.schemes.Add(new JizuraScheme(row[0], row[1], row[2], row[3], row[4]));
            object enabled;
            if (project.colors.TryGetValue("enabled", out enabled) && enabled is bool && (bool)enabled)
            {
                var s = plan.schemes[0];
                object v;
                if (project.colors.TryGetValue("bg", out v) && v is string) s.bg = (string)v;
                if (project.colors.TryGetValue("fg", out v) && v is string) s.fg = (string)v;
                if (project.colors.TryGetValue("sub", out v) && v is string) s.sub = (string)v;
            }
            object accentOn;
            if (project.colors.TryGetValue("accentOn", out accentOn) && accentOn is bool && (bool)accentOn)
            {
                foreach (var s in plan.schemes)
                {
                    object v; if (project.colors.TryGetValue("accent", out v) && v is string) s.accent = (string)v;
                    if (project.colors.TryGetValue("accent2", out v) && v is string) s.accent2 = (string)v;
                }
            }
        }
        static Dictionary<string, string[][]> MakePalettes()
        {
            // Exact first five colour roles of every original scheme in src/04_styles.js.
            return new Dictionary<string, string[][]>
            {
                { "noir", new[] { new[] { "#060607", "#F5EEEA", "#BDB6B2", "#F5A50C", "#16F4D4" }, new[] { "#F2EDE8", "#0B0B0C", "#4A4644", "#E0600C", "#0FAE98" } } },
                { "crimson", new[] { new[] { "#C8103F", "#FFFFFF", "#FFD9E2", "#140509", "#39F2C8" }, new[] { "#FF6F98", "#FFFFFF", "#FFE3EB", "#1A0710", "#39F2C8" }, new[] { "#150509", "#FF3D6E", "#FF9DB6", "#FFFFFF", "#39F2C8" } } },
                { "caution", new[] { new[] { "#F4D21F", "#141414", "#3A3510", "#E0231C", "#1F3FD8" }, new[] { "#E0231C", "#F4D21F", "#FFE9A0", "#141414", "#FFFFFF" }, new[] { "#18181A", "#F4D21F", "#DDD6B0", "#E0231C", "#FFFFFF" } } },
                { "magenta", new[] { new[] { "#FF0A8C", "#FFFFFF", "#FFD2EA", "#FFFFFF", "#2B2BD9" }, new[] { "#FFFFFF", "#FF0A8C", "#FF6DB6", "#2B2BD9", "#FF0A8C" }, new[] { "#2B2BD9", "#FFFFFF", "#C9C9FF", "#FF0A8C", "#FFFFFF" } } },
                { "paper", new[] { new[] { "#ECE9E3", "#1B2350", "#4D5270", "#C2185B", "#111111" }, new[] { "#151515", "#F0EDE7", "#B8B4AC", "#C2185B", "#1B2350" }, new[] { "#C2185B", "#FFFFFF", "#F6C6D8", "#1B2350", "#111111" }, new[] { "#1B2350", "#F0EDE7", "#AEB2CC", "#C2185B", "#FFFFFF" } } },
                { "hud", new[] { new[] { "#131315", "#EFEDEA", "#8E8B88", "#F25A2B", "#FFFFFF" }, new[] { "#0B0B0C", "#FFFFFF", "#9A9796", "#F25A2B", "#FFFFFF" } } },
                { "mint", new[] { new[] { "#0A0E0D", "#E6FFF5", "#7FB9A8", "#9CFF3A", "#2E8C74" }, new[] { "#3FAE93", "#0A0E0D", "#123A31", "#FFFFFF", "#9CFF3A" }, new[] { "#F2F2EE", "#0A0E0D", "#40504B", "#2E8C74", "#9CFF3A" } } },
                { "specimen", new[] { new[] { "#1B1A1C", "#F2F0EC", "#A19E99", "#F2F0EC", "#C8B98C" }, new[] { "#F2F0EC", "#1B1A1C", "#5E5B57", "#1B1A1C", "#8A7A4E" } } },
                { "transit", new[] { new[] { "#5B582B", "#FFFFFF", "#E6E2BC", "#E8C21A", "#1A1A1A" }, new[] { "#1A1A1A", "#FFFFFF", "#B8B5A0", "#E8C21A", "#FFFFFF" }, new[] { "#9C9A94", "#FFFFFF", "#F0EEE6", "#E8C21A", "#1A1A1A" } } },
                { "blueprint", new[] { new[] { "#1B1BE8", "#FFFFFF", "#C7C7FF", "#000000", "#FFFFFF" }, new[] { "#000000", "#FFFFFF", "#9A9AFF", "#1B1BE8", "#FFFFFF" }, new[] { "#FFFFFF", "#1B1BE8", "#5A5AF0", "#000000", "#1B1BE8" } } },
                { "rouge", new[] { new[] { "#E4E2E0", "#141414", "#6B6866", "#D40F1C", "#141414" }, new[] { "#140405", "#FFFFFF", "#C98A8E", "#E3141F", "#FFFFFF" } } },
                { "mono", new[] { new[] { "#3B3D41", "#FFFFFF", "#B9BBBF", "#FFFFFF", "#FFE34D" }, new[] { "#141517", "#FFFFFF", "#9EA0A4", "#FFE34D", "#FFFFFF" } } },
            };
        }
    }
}
