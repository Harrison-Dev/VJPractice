using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using VJPractice.Stage;
using VJPractice.Stage.Jizura;

internal static class Program
{
    static int passed, failed;
    static void Check(bool condition, string message = "Assertion failed")
    {
        if (!condition) throw new Exception(message);
    }
    static void Test(string name, Action action)
    {
        try { action(); passed++; Console.WriteLine("PASS " + name); }
        catch (Exception ex) { failed++; Console.Error.WriteLine("FAIL " + name + ": " + ex); }
    }
    static JizuraProject Demo() => JizuraProject.ParseJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "JizuraDemo.json")));
    static string Signature(JizuraPlan plan) => string.Join("|", plan.cuts.Select(c =>
        c.line + ":" + c.start + ":" + c.end + ":" + c.seed + ":" + c.scheme + ":" +
        c.layout + ":" + c.enter + ":" + c.exit + ":" + c.hold + ":" +
        c.bg + ":" + c.cam + ":" + c.treat + ":" + c.trans + ":" +
        string.Join(",", c.decor ?? Array.Empty<string>())));

    static int Main()
    {
        Test("upstream J.h parity", () => {
            Check(JizuraPlanner.Hash(20260922, 1, 0) == 539445175u);
            Check(JizuraPlanner.Hash(20260922, 1, 1234) == 1788766001u);
            Check(JizuraPlanner.Hash(-1, 42, 7, 3, 9) == 4176381785u);
        });
        Test("sample project imports into multiple timed cuts", () => {
            var plan = JizuraPlanner.Build(Demo());
            Check(plan.lines.Count == 10, "Expected ten lyric lines");
            Check(plan.cuts.Count > plan.lines.Count * 2, "Manual slash lines should create multiple cuts");
            Check(Math.Abs(plan.lines[0].start - 2) < .001f && Math.Abs(plan.lines[1].start - 6) < .001f);
            Check(plan.cuts.All(c => c.end > c.start));
        });
        Test("planning is deterministic including decor", () => {
            var project = Demo();
            string first = Signature(JizuraPlanner.Build(project));
            Check(first == Signature(JizuraPlanner.Build(project)));
            Check(first == Signature(JizuraPlanner.Build(JizuraProject.ParseJson(project.ToJson()))));
        });
        Test("automatic decor uses only supported native IDs, at most two", () => {
            var plan = JizuraPlanner.Build(Demo());
            var supported = new HashSet<string>(JizuraPlanner.DecorKeys);
            Check(plan.cuts.Any(c => c.decor != null && c.decor.Length > 0), "No automatic decor selected");
            Check(plan.cuts.All(c => c.decor != null && c.decor.Length <= 2));
            Check(plan.cuts.All(c => c.decor.Distinct().Count() == c.decor.Length));
            Check(plan.cuts.All(c => c.decor.All(supported.Contains)));
        });
        Test("decor density zero disables automatic picks", () => {
            var p = Demo(); p.fx.decor = 0;
            Check(JizuraPlanner.Build(p).cuts.All(c => c.decor.Length == 0));
        });
        Test("disabled decor IDs are never picked automatically", () => {
            var p = Demo(); var flags = new Dictionary<string, bool>();
            foreach (string id in JizuraPlanner.DecorKeys) flags[id] = false;
            p.enabled["decor"] = flags;
            Check(JizuraPlanner.Build(p).cuts.All(c => c.decor.Length == 0));
        });
        Test("explicit decor override survives disabled flags and density zero", () => {
            var p = Demo(); p.fx.decor = 0;
            p.enabled["decor"] = new Dictionary<string, bool> { ["rings"] = false };
            p.overrides[0].decor = new[] { "rings", "future_decor" };
            var cuts = JizuraPlanner.Build(p).cuts.Where(c => c.line == 0).ToList();
            Check(cuts.Count > 0 && cuts.All(c => c.decor.SequenceEqual(new[] { "rings", "future_decor" })));
        });
        Test("core layout choices obey original glyph count fits", () => {
            var p = new JizuraProject { lyrics = new string('字', 35) };
            p.overrides[0] = new JizuraLineOverride { single = true };
            var cuts = JizuraPlanner.Build(p).cuts.Where(c => c.line == 0).ToList();
            Check(cuts.Count == 1 && cuts[0].layout == "center");
        });
        Test("explicit effect IDs survive extra and wa gates, transition only at a cut boundary", () => {
            var p = JizuraProject.ParseJson("{\"version\":1,\"lyrics\":\"光/影/夜\",\"extra\":false,\"wa\":false,\"fx\":{\"density\":1},\"timing\":{\"lineScale\":3},\"overrides\":{\"0\":{\"bg\":\"seigaiha\",\"cam\":\"pullOut\",\"treat\":\"monoGrid\",\"trans\":\"wipe\"}}}");
            var cuts = JizuraPlanner.Build(p).cuts.Where(c => c.line == 0).ToList();
            Check(cuts.Count >= 2);
            Check(cuts.All(c => c.bg == "seigaiha" && c.cam == "pullOut" && c.treat == "monoGrid"));
            Check(cuts[0].trans == null && cuts[1].trans == "wipe");
            var q = JizuraProject.ParseJson(p.ToJson());
            Check(!q.extra && !q.wa && q.GetOverride(0).trans == "wipe");
            Check(Signature(JizuraPlanner.Build(q)) == Signature(JizuraPlanner.Build(p)));
        });
        Test("extra pack flags do not auto-select techniques absent from native renderer", () => {
            var p = Demo(); p.extra = true; p.wa = true;
            p.enabled["layout"] = new Dictionary<string, bool> { ["ema"] = true, ["wordCloud"] = true };
            var choices = new HashSet<string>(JizuraPlanner.LayoutKeys);
            Check(JizuraPlanner.Build(p).cuts.Where(c => c.line >= 0 && c.layout != "interlude").All(c => choices.Contains(c.layout)));
            Check(JizuraPlanner.Build(p).cuts.All(c => c.bg == "none" && c.cam == "push" && c.treat == "none" && c.trans == null));
        });
        Test("locked seed survives reroll but unlocked seed changes", () => {
            var p = Demo(); var ov = p.overrides[0];
            uint locked = JizuraPlanner.Build(p).lines[0].seed;
            ov.seed += 1;
            Check(JizuraPlanner.Build(p).lines[0].seed == locked);
            ov.locked = false;
            Check(JizuraPlanner.Build(p).lines[0].seed != locked);
        });
        Test("unknown project and override settings roundtrip", () => {
            const string json = "{\"version\":1,\"lyrics\":\"abc/def/ghi\",\"style\":\"future_style\",\"future\":{\"flag\":true},\"timing\":{\"lineTimes\":{\"0\":1.25}},\"overrides\":{\"0\":{\"layout\":\"future_layout\",\"decor\":[\"future_decor\"],\"lock\":true,\"lockedSeed\":4294967295,\"futureParam\":12}},\"enabled\":{\"layout\":{\"center\":false}},\"colors\":{\"enabled\":true,\"bg\":\"#123456\"},\"fonts\":{\"display\":\"future_font\"}}";
            var p = JizuraProject.ParseJson(json);
            var before = JizuraPlanner.Build(p);
            Check(before.styleKey == "future_style" && !before.styleSupported);
            Check(before.cuts[0].layout == "future_layout" && before.cuts[0].decor[0] == "future_decor");
            var q = JizuraProject.ParseJson(p.ToJson());
            Check(q.raw.ContainsKey("future") && q.GetOverride(0).lockedSeed == uint.MaxValue);
            Check(q.GetOverride(0).layout == "future_layout" && q.GetOverride(0).decor[0] == "future_decor");
            Check(q.timing.lineTimes[0] == 1.25f && !q.IsEnabled("layout", "center"));
            Check((string)q.fonts["display"] == "future_font");
            var rawOv = (Dictionary<string, object>)((Dictionary<string, object>)q.raw["overrides"])["0"];
            Check(rawOv.ContainsKey("futureParam"), "Unknown override field lost");
        });
        Test("LyricDocument conversion keeps line indices after untimed cues", () => {
            var d = new LyricDocument { title = "Song", artist = "Artist", offsetSeconds = .5f,
                lines = new List<LyricCue> {
                    new LyricCue { text = "untimed" },
                    new LyricCue { text = "timed", startTime = 3f },
                    new LyricCue { text = "last", startTime = 5f }
                } };
            var p = JizuraProject.FromLyricDocument(d);
            Check(p.timing.lineTimes.Count == 2 && p.timing.lineTimes[1] == 3.5f && p.timing.lineTimes[2] == 5.5f);
            Check(JizuraPlanner.Build(p).lines.Count == 3);
        });
        Console.WriteLine($"JIZURA planner tests: {passed} passed, {failed} failed");
        return failed == 0 ? 0 : 1;
    }
}
