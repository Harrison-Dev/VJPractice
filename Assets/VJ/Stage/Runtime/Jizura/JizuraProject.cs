// Native project reader for 852wa/JIZURA at 1b48bea2d74e60f9b0ff2c247aa5919ba03a6551.
// Source: src/08_planner.js defaultProject(), src/12_ui.js mergeProject(). MIT, copyright 2026 hakoniwa.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace VJPractice.Stage.Jizura
{
    [Serializable]
    public sealed class JizuraFx
    {
        public float motion = .7f, glitch = .55f, chroma = .7f, decor = .5f, density = .55f, texture = .6f, bgSwitch = .35f;
        public bool flash = true, onTwos = true;
        public int koma = 12;
        public string hud = "auto";
    }

    [Serializable]
    public sealed class JizuraTiming
    {
        public float bpm, offset = .4f, tail = .9f, lineScale = 1f;
        public bool snap = true, useAudioLength = true;
        public readonly Dictionary<int, float> lineTimes = new Dictionary<int, float>();
    }

    [Serializable]
    public sealed class JizuraLineOverride
    {
        public bool single, locked;
        public int seed;
        public uint lockedSeed;
        public bool hasLockedSeed;
        public string layout, enter, exit, hold, bg, cam, treat, trans;
        public string[] decor;
    }

    /// <summary>
    /// Original .jizura.json schema v1. Dictionary-shaped fields are intentionally parsed,
    /// since Unity JsonUtility silently ignores them. Unknown source fields remain in raw.
    /// </summary>
    public sealed class JizuraProject
    {
        public int version = 1, seed = 20260922, fps = 24, res = 1080;
        public string title = "", artist = "", lyrics = "", style = "noir", mood, aspect = "16:9", lang = "auto", keyBg = "off";
        public bool extra, wa = true;
        public JizuraFx fx = new JizuraFx();
        public JizuraTiming timing = new JizuraTiming();
        public readonly Dictionary<int, JizuraLineOverride> overrides = new Dictionary<int, JizuraLineOverride>();
        public readonly Dictionary<string, Dictionary<string, bool>> enabled = new Dictionary<string, Dictionary<string, bool>>();
        public readonly Dictionary<string, object> colors = new Dictionary<string, object>();
        public readonly Dictionary<string, object> fonts = new Dictionary<string, object>();
        public Dictionary<string, object> raw = new Dictionary<string, object>();

        public bool IsEnabled(string group, string key)
        {
            Dictionary<string, bool> map;
            bool value;
            return !enabled.TryGetValue(group, out map) || !map.TryGetValue(key, out value) || value;
        }

        public JizuraLineOverride GetOverride(int line)
        {
            JizuraLineOverride value;
            return overrides.TryGetValue(line, out value) ? value : null;
        }

        public string ToJson()
        {
            // Update the imported object in place so unknown JIZURA settings survive a Unity edit/save.
            var r = new Dictionary<string, object>(raw);
            r["version"] = version; r["title"] = title ?? ""; r["artist"] = artist ?? ""; r["lyrics"] = lyrics ?? "";
            r["style"] = style ?? "noir"; r["mood"] = mood; r["aspect"] = aspect ?? "16:9";
            r["lang"] = lang ?? "auto"; r["keyBg"] = keyBg ?? "off"; r["seed"] = seed;
            r["fps"] = fps; r["res"] = res; r["extra"] = extra; r["wa"] = wa;
            var f = CopyObject(r, "fx");
            f["motion"] = fx.motion; f["glitch"] = fx.glitch; f["chroma"] = fx.chroma; f["decor"] = fx.decor;
            f["density"] = fx.density; f["texture"] = fx.texture; f["bgSwitch"] = fx.bgSwitch;
            f["flash"] = fx.flash; f["onTwos"] = fx.onTwos; f["koma"] = fx.koma; f["hud"] = fx.hud;
            r["fx"] = f;
            var t = CopyObject(r, "timing");
            t["bpm"] = timing.bpm; t["offset"] = timing.offset; t["tail"] = timing.tail;
            t["lineScale"] = timing.lineScale; t["snap"] = timing.snap; t["useAudioLength"] = timing.useAudioLength;
            var lineTimes = new Dictionary<string, object>();
            foreach (var kv in timing.lineTimes) lineTimes[kv.Key.ToString(CultureInfo.InvariantCulture)] = kv.Value;
            t["lineTimes"] = lineTimes; r["timing"] = t;
            var originalOverrides = Obj(raw, "overrides");
            var outputOverrides = new Dictionary<string, object>();
            foreach (var kv in overrides)
            {
                string key = kv.Key.ToString(CultureInfo.InvariantCulture); var ov = kv.Value;
                var old = Obj(originalOverrides, key);
                var o = new Dictionary<string, object>(old);
                o["single"] = ov.single; o["lock"] = ov.locked; o["seed"] = ov.seed;
                if (ov.hasLockedSeed) o["lockedSeed"] = ov.lockedSeed; else o.Remove("lockedSeed");
                PutOptional(o, "layout", ov.layout); PutOptional(o, "enter", ov.enter); PutOptional(o, "exit", ov.exit);
                PutOptional(o, "hold", ov.hold); PutOptional(o, "bg", ov.bg); PutOptional(o, "cam", ov.cam);
                PutOptional(o, "treat", ov.treat); PutOptional(o, "trans", ov.trans);
                if (ov.decor != null) o["decor"] = ov.decor; else o.Remove("decor");
                outputOverrides[key] = o;
            }
            r["overrides"] = outputOverrides;
            var outputEnabled = new Dictionary<string, object>();
            foreach (var kv in enabled)
            {
                var flags = new Dictionary<string, object>();
                foreach (var setting in kv.Value) flags[setting.Key] = setting.Value;
                outputEnabled[kv.Key] = flags;
            }
            r["enabled"] = outputEnabled;
            r["colors"] = colors; r["fonts"] = fonts;
            return JsonWriter.Write(r);
        }

        static Dictionary<string, object> CopyObject(Dictionary<string, object> r, string key)
        {
            return new Dictionary<string, object>(Obj(r, key));
        }
        static void PutOptional(Dictionary<string, object> o, string key, string value)
        {
            if (string.IsNullOrEmpty(value)) o.Remove(key); else o[key] = value;
        }

        public static JizuraProject ParseJson(string json) { return Parse(json); }

        public static JizuraProject FromLyricDocument(LyricDocument doc)
        {
            if (doc == null) throw new ArgumentNullException(nameof(doc));
            var p = new JizuraProject { title = doc.title ?? "", artist = doc.artist ?? "" };
            var lyrics = new StringBuilder();
            if (doc.lines != null)
            {
                int emitted = 0;
                for (int i = 0; i < doc.lines.Count; i++)
                {
                    var cue = doc.lines[i];
                    if (cue == null || string.IsNullOrWhiteSpace(cue.text)) continue;
                    if (lyrics.Length > 0) lyrics.Append('\n');
                    if (cue.startTime >= 0) p.timing.lineTimes[emitted] = cue.startTime + doc.offsetSeconds;
                    emitted++;
                    lyrics.Append(cue.text.Replace('\n', ' ').Replace('\r', ' '));
                    if (!string.IsNullOrEmpty(cue.translation)) lyrics.Append(" | ").Append(cue.translation.Replace('\n', ' '));
                }
            }
            p.lyrics = lyrics.ToString();
            return p;
        }

        public static JizuraProject Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new FormatException("Empty JIZURA project");
            json = json.TrimStart('\uFEFF');
            if (json.Length > 4 * 1024 * 1024) throw new FormatException("JIZURA project exceeds 4 MiB");
            var root = JsonReader.Read(json) as Dictionary<string, object>;
            if (root == null) throw new FormatException("JIZURA project must be an object");
            var p = new JizuraProject();
            p.raw = root;
            p.version = Int(root, "version", 1);
            if (p.version != 1) throw new FormatException("Unsupported JIZURA project version: " + p.version);
            p.title = Str(root, "title", ""); p.artist = Str(root, "artist", ""); p.lyrics = Str(root, "lyrics", "");
            p.style = Str(root, "style", "noir"); p.mood = Str(root, "mood", null);
            p.aspect = Str(root, "aspect", "16:9"); p.lang = Str(root, "lang", "auto"); p.keyBg = Str(root, "keyBg", "off");
            p.seed = Int(root, "seed", p.seed); p.fps = Int(root, "fps", 24); p.res = Int(root, "res", 1080);
            p.extra = Bool(root, "extra", false); p.wa = Bool(root, "wa", true);
            var f = Obj(root, "fx");
            p.fx.motion = Num(f, "motion", p.fx.motion); p.fx.glitch = Num(f, "glitch", p.fx.glitch);
            p.fx.chroma = Num(f, "chroma", p.fx.chroma); p.fx.decor = Num(f, "decor", p.fx.decor);
            p.fx.density = Num(f, "density", p.fx.density); p.fx.texture = Num(f, "texture", p.fx.texture);
            p.fx.bgSwitch = Num(f, "bgSwitch", p.fx.bgSwitch); p.fx.flash = Bool(f, "flash", true);
            p.fx.onTwos = Bool(f, "onTwos", true); p.fx.koma = Int(f, "koma", p.fx.koma); p.fx.hud = Str(f, "hud", "auto");
            var t = Obj(root, "timing");
            p.timing.bpm = Num(t, "bpm", 0); p.timing.offset = Num(t, "offset", .4f);
            p.timing.tail = Num(t, "tail", .9f); p.timing.lineScale = Num(t, "lineScale", 1);
            p.timing.snap = Bool(t, "snap", true); p.timing.useAudioLength = Bool(t, "useAudioLength", true);
            foreach (var kv in Obj(t, "lineTimes"))
            {
                int i; float v;
                if (int.TryParse(kv.Key, NumberStyles.Integer, CultureInfo.InvariantCulture, out i) && i >= 0 && Number(kv.Value, out v)) p.timing.lineTimes[i] = v;
            }
            foreach (var kv in Obj(root, "enabled"))
            {
                var sub = kv.Value as Dictionary<string, object>;
                if (sub == null) continue;
                var flags = new Dictionary<string, bool>();
                foreach (var flag in sub) if (flag.Value is bool) flags[flag.Key] = (bool)flag.Value;
                p.enabled[kv.Key] = flags;
            }
            foreach (var kv in Obj(root, "overrides"))
            {
                int index;
                if (!int.TryParse(kv.Key, NumberStyles.Integer, CultureInfo.InvariantCulture, out index) || index < 0) continue;
                var o = kv.Value as Dictionary<string, object>;
                if (o == null) continue;
                var ov = new JizuraLineOverride();
                ov.single = Bool(o, "single", false); ov.locked = Bool(o, "lock", false); ov.seed = Int(o, "seed", 0);
                double lockedSeed;
                if (o.ContainsKey("lockedSeed") && ExactUint(o["lockedSeed"], out lockedSeed)) { ov.lockedSeed = (uint)lockedSeed; ov.hasLockedSeed = true; }
                ov.layout = Str(o, "layout", null); ov.enter = Str(o, "enter", null); ov.exit = Str(o, "exit", null);
                ov.hold = Str(o, "hold", null); ov.bg = Str(o, "bg", null); ov.cam = Str(o, "cam", null);
                ov.treat = Str(o, "treat", null); ov.trans = Str(o, "trans", null);
                var ds = Arr(o, "decor");
                if (ds != null) { var list = new List<string>(); foreach (var item in ds) if (item is string) list.Add((string)item); ov.decor = list.ToArray(); }
                p.overrides[index] = ov;
            }
            foreach (var kv in Obj(root, "colors")) p.colors[kv.Key] = kv.Value;
            foreach (var kv in Obj(root, "fonts")) p.fonts[kv.Key] = kv.Value;
            return p;
        }

        static Dictionary<string, object> Obj(Dictionary<string, object> o, string key)
        {
            object value;
            return o != null && o.TryGetValue(key, out value) && value is Dictionary<string, object>
                ? (Dictionary<string, object>)value : new Dictionary<string, object>();
        }
        static List<object> Arr(Dictionary<string, object> o, string key)
        {
            object value; return o != null && o.TryGetValue(key, out value) ? value as List<object> : null;
        }
        static string Str(Dictionary<string, object> o, string key, string fallback)
        {
            object v; return o != null && o.TryGetValue(key, out v) && v is string ? (string)v : fallback;
        }
        static bool Bool(Dictionary<string, object> o, string key, bool fallback)
        {
            object v; return o != null && o.TryGetValue(key, out v) && v is bool ? (bool)v : fallback;
        }
        static int Int(Dictionary<string, object> o, string key, int fallback)
        {
            object v; float n;
            return o != null && o.TryGetValue(key, out v) && Number(v, out n) && n >= int.MinValue && n <= int.MaxValue ? (int)n : fallback;
        }
        static float Num(Dictionary<string, object> o, string key, float fallback)
        {
            object v; float n; return o != null && o.TryGetValue(key, out v) && Number(v, out n) ? n : fallback;
        }
        static bool ExactUint(object v, out double n)
        {
            n = 0;
            if (!(v is double)) return false;
            n = (double)v;
            return n >= 0 && n <= uint.MaxValue && Math.Floor(n) == n;
        }
        static bool Number(object v, out float n)
        {
            n = 0;
            if (!(v is double)) return false;
            double d = (double)v;
            if (double.IsNaN(d) || double.IsInfinity(d) || d < -float.MaxValue || d > float.MaxValue) return false;
            n = (float)d; return true;
        }

        static class JsonWriter
        {
            public static string Write(object value) { var b = new StringBuilder(); Append(b, value); return b.ToString(); }
            static void Append(StringBuilder b, object value)
            {
                if (value == null) { b.Append("null"); return; }
                var text = value as string;
                if (text != null) { Quoted(b, text); return; }
                if (value is bool) { b.Append((bool)value ? "true" : "false"); return; }
                var map = value as Dictionary<string, object>;
                if (map != null)
                {
                    b.Append('{'); bool first = true;
                    foreach (var kv in map) { if (!first) b.Append(','); first = false; Quoted(b, kv.Key); b.Append(':'); Append(b, kv.Value); }
                    b.Append('}'); return;
                }
                var list = value as System.Collections.IEnumerable;
                if (list != null) { b.Append('['); bool first = true; foreach (var item in list) { if (!first) b.Append(','); first = false; Append(b, item); } b.Append(']'); return; }
                if (value is IFormattable) { b.Append(((IFormattable)value).ToString(null, CultureInfo.InvariantCulture)); return; }
                throw new FormatException("Unsupported JIZURA JSON value type");
            }
            static void Quoted(StringBuilder b, string s)
            {
                b.Append('"');
                foreach (char c in s)
                {
                    switch (c)
                    {
                        case '"': b.Append("\\\""); break; case '\\': b.Append("\\\\"); break;
                        case '\b': b.Append("\\b"); break; case '\f': b.Append("\\f"); break;
                        case '\n': b.Append("\\n"); break; case '\r': b.Append("\\r"); break;
                        case '\t': b.Append("\\t"); break;
                        default: if (c < 0x20) b.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture)); else b.Append(c); break;
                    }
                }
                b.Append('"');
            }
        }

        // Bounded JSON tokenizer: object maps and arrays, strings including \u escapes, finite numbers.
        // It is deliberately local to avoid adding a package to this Unity project.
        sealed class JsonReader
        {
            readonly string s; int i; int depth;
            JsonReader(string text) { s = text; }
            public static object Read(string text)
            {
                var p = new JsonReader(text); object v = p.Value(); p.Ws();
                if (p.i != text.Length) throw new FormatException("Unexpected text after JIZURA JSON");
                return v;
            }
            void Ws() { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }
            char Take() { if (i >= s.Length) throw new FormatException("Unexpected end of JIZURA JSON"); return s[i++]; }
            object Value()
            {
                Ws(); if (i >= s.Length) throw new FormatException("Unexpected end of JIZURA JSON");
                char c = s[i];
                if (c == '{') return Object(); if (c == '[') return Array(); if (c == '"') return String();
                if (c == 't') { Literal("true"); return true; } if (c == 'f') { Literal("false"); return false; }
                if (c == 'n') { Literal("null"); return null; }
                if (c == '-' || (c >= '0' && c <= '9')) return Number();
                throw new FormatException("Invalid JIZURA JSON token at " + i);
            }
            void Literal(string lit)
            {
                for (int n = 0; n < lit.Length; n++) if (Take() != lit[n]) throw new FormatException("Invalid JIZURA JSON literal");
            }
            object Object()
            {
                if (++depth > 64) throw new FormatException("JIZURA JSON nesting too deep");
                Take(); var o = new Dictionary<string, object>(); Ws();
                if (i < s.Length && s[i] == '}') { i++; depth--; return o; }
                while (true)
                {
                    Ws(); if (Take() != '"') throw new FormatException("Expected JIZURA JSON property"); i--;
                    string key = String(); Ws(); if (Take() != ':') throw new FormatException("Expected colon");
                    o[key] = Value(); Ws(); char c = Take();
                    if (c == '}') break; if (c != ',') throw new FormatException("Expected comma");
                }
                depth--; return o;
            }
            object Array()
            {
                if (++depth > 64) throw new FormatException("JIZURA JSON nesting too deep");
                Take(); var a = new List<object>(); Ws();
                if (i < s.Length && s[i] == ']') { i++; depth--; return a; }
                while (true)
                {
                    a.Add(Value()); Ws(); char c = Take();
                    if (c == ']') break; if (c != ',') throw new FormatException("Expected comma");
                }
                depth--; return a;
            }
            string String()
            {
                if (Take() != '"') throw new FormatException("Expected string");
                var b = new StringBuilder();
                while (true)
                {
                    char c = Take(); if (c == '"') return b.ToString();
                    if (c < 0x20) throw new FormatException("Control character in JIZURA JSON string");
                    if (c != '\\') { b.Append(c); continue; }
                    c = Take();
                    switch (c)
                    {
                        case '"': b.Append('"'); break; case '\\': b.Append('\\'); break; case '/': b.Append('/'); break;
                        case 'b': b.Append('\b'); break; case 'f': b.Append('\f'); break; case 'n': b.Append('\n'); break;
                        case 'r': b.Append('\r'); break; case 't': b.Append('\t'); break;
                        case 'u':
                            if (i + 4 > s.Length) throw new FormatException("Short Unicode escape");
                            int cp; if (!int.TryParse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out cp)) throw new FormatException("Invalid Unicode escape");
                            b.Append((char)cp); i += 4; break;
                        default: throw new FormatException("Invalid JIZURA JSON escape");
                    }
                }
            }
            double Number()
            {
                int start = i;
                if (s[i] == '-') i++;
                if (i >= s.Length) throw new FormatException("Invalid number");
                if (s[i] == '0') i++;
                else { if (s[i] < '1' || s[i] > '9') throw new FormatException("Invalid number"); while (i < s.Length && s[i] >= '0' && s[i] <= '9') i++; }
                if (i < s.Length && s[i] == '.') { i++; int a = i; while (i < s.Length && s[i] >= '0' && s[i] <= '9') i++; if (i == a) throw new FormatException("Invalid decimal"); }
                if (i < s.Length && (s[i] == 'e' || s[i] == 'E'))
                {
                    i++; if (i < s.Length && (s[i] == '+' || s[i] == '-')) i++;
                    int a = i; while (i < s.Length && s[i] >= '0' && s[i] <= '9') i++; if (i == a) throw new FormatException("Invalid exponent");
                }
                double d;
                if (!double.TryParse(s.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture, out d) || double.IsInfinity(d)) throw new FormatException("Invalid JIZURA JSON number");
                return d;
            }
        }
    }
}
