using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using VJPractice.Stage.Motion;

internal static class Program
{
    static int passed, failed;
    static LyricMotionPlan Plan() => new LyricMotionPlan { documentKey = "original-demo" };
    static void Check(bool condition, string message = "Assertion failed") { if (!condition) throw new Exception(message); }
    static void Throws<T>(Action action) where T : Exception
    { try { action(); } catch (T) { return; } throw new Exception("Expected " + typeof(T).Name); }
    static string Signature(MotionCut c) => $"{c.lineIndex}:{c.textHash}:{c.revision}:{c.mood}:{c.layout}:{c.entrance}:{c.exit}:{c.variation}";
    static void Test(string name, Action action)
    {
        try { action(); passed++; Console.WriteLine("PASS " + name); }
        catch (Exception e) { failed++; Console.Error.WriteLine("FAIL " + name + ": " + e); }
    }

    static int Main()
    {
        Test("same seed and input reproduce the entire cut", () => {
            Check(Signature(LyricMotionPlanner.ForLine(Plan(), 0, "夜のひかり")) == Signature(LyricMotionPlanner.ForLine(Plan(), 0, "夜のひかり")));
        });
        Test("planning is independent of visit order", () => {
            var a = Plan(); var b = Plan();
            var expected = Signature(LyricMotionPlanner.ForLine(a, 20, "遠くへ"));
            for (int i = 0; i < 20; i++) LyricMotionPlanner.ForLine(b, i, "海");
            Check(expected == Signature(LyricMotionPlanner.ForLine(b, 20, "遠くへ")));
        });
        Test("reroll increments only the selected cut", () => {
            var p = Plan(); var original = LyricMotionPlanner.ForLine(p, 0, "ひかり");
            var other = Signature(LyricMotionPlanner.ForLine(p, 1, "影"));
            var next = LyricMotionPlanner.ForLine(p, 0, "ひかり", true);
            Check(next.revision == original.revision + 1); Check(next.variation != original.variation);
            Check(Signature(LyricMotionPlanner.ForLine(p, 1, "影")) == other);
        });
        Test("locked cuts survive mood changes and rerolls", () => {
            var p = Plan(); var c = LyricMotionPlanner.ForLine(p, 0, "ひかり"); c.locked = true;
            string before = Signature(c); p.mood = MotionMood.Glitch;
            Check(Signature(LyricMotionPlanner.ForLine(p, 0, "ひかり", true)) == before);
        });
        Test("edited lyric invalidates an obsolete lock", () => {
            var p = Plan(); LyricMotionPlanner.ForLine(p, 0, "古い").locked = true;
            var c = LyricMotionPlanner.ForLine(p, 0, "新しい");
            Check(!c.locked && c.textHash == LyricMotionPlanner.TextHash("新しい"));
        });
        Test("pending mood cannot change the currently displayed line", () => {
            var d = new LyricMotionDirector(Plan()); string before = Signature(d.Visit(0, "夜"));
            d.QueueMood(MotionMood.Calm); Check(Signature(d.Visit(0, "夜")) == before && d.HasPending);
            Check(d.Visit(1, "朝").mood == MotionMood.Calm && !d.HasPending);
        });
        Test("instrumental gaps do not consume commands", () => {
            var d = new LyricMotionDirector(Plan()); d.Visit(0, "夜"); d.QueueReroll();
            Check(d.Visit(-1, null) == null && d.HasPending);
            Check(d.Visit(1, " ") == null && d.HasPending);
            Check(d.Visit(2, "朝").revision == 1 && !d.HasPending);
        });
        Test("revisiting the same cue after a gap preserves pending commands", () => {
            var d = new LyricMotionDirector(Plan()); d.Visit(0, "夜"); d.QueueReroll(); d.Visit(-1, null);
            Check(d.Visit(0, "夜").revision == 0 && d.HasPending);
        });
        Test("explicit seek is a cue boundary for pending changes", () => {
            var d = new LyricMotionDirector(Plan()); d.Visit(20, "夜"); d.QueueMood(MotionMood.Calm);
            Check(d.Visit(2, "朝").mood == MotionMood.Calm && !d.HasPending);
        });
        Test("locked next cue consumes reroll without modifying the lock", () => {
            var p = Plan(); var c = LyricMotionPlanner.ForLine(p, 1, "朝"); c.locked = true;
            var d = new LyricMotionDirector(p); d.Visit(0, "夜"); d.QueueReroll();
            Check(d.Visit(1, "朝").revision == 0 && d.Current.locked && !d.HasPending);
        });
        Test("lock toggle is safe with no active lyric", () => {
            var d = new LyricMotionDirector(Plan()); Check(!d.ToggleLock()); d.Visit(0, "夜");
            Check(d.ToggleLock()); Check(!d.ToggleLock());
        });
        Test("absolute-time sampling is stable after backward seeks", () => {
            var first = LyricMotionPlanner.Sample(10.1f, 10, 12);
            LyricMotionPlanner.Sample(11.9f, 10, 12); LyricMotionPlanner.Sample(0, 0, 1);
            var second = LyricMotionPlanner.Sample(10.1f, 10, 12);
            Check(first.alpha == second.alpha && first.entrance == second.entrance && first.progress == second.progress);
        });
        Test("cue intervals are half-open with clean entrance and exit", () => {
            Check(LyricMotionPlanner.Sample(9, 10, 12).alpha == 0);
            Check(LyricMotionPlanner.Sample(10, 10, 12).alpha == 0);
            Check(LyricMotionPlanner.Sample(11, 10, 12).alpha == 1);
            Check(LyricMotionPlanner.Sample(12, 10, 12).alpha == 0);
        });
        Test("invalid and nonfinite times produce invisible samples", () => {
            foreach (float t in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
                Check(LyricMotionPlanner.Sample(t, 0, 1).alpha == 0);
            Check(LyricMotionPlanner.Sample(1, 1, 1).alpha == 0);
            Check(LyricMotionPlanner.Sample(1, 2, 1).alpha == 0);
            Check(LyricMotionPlanner.Sample(1, float.NaN, 2).alpha == 0);
        });
        Test("very short cues remain finite and bounded", () => {
            for (int i = 0; i <= 100; i++) {
                var s = LyricMotionPlanner.Sample(1 + i * .000001f, 1, 1.0001f);
                Check(LyricMotionPlanner.Finite(s.alpha) && s.alpha >= 0 && s.alpha <= 1);
            }
        });
        Test("Unicode prefixes preserve combining sequences and surrogate pairs", () => {
            const string s = "A\u0301𠮷あ";
            Check(LyricMotionPlanner.ElementCount(s) == 3);
            Check(LyricMotionPlanner.Prefix(s, 1) == "A\u0301");
            Check(LyricMotionPlanner.Prefix(s, 2) == "A\u0301𠮷");
            Check(LyricMotionPlanner.Prefix(s, 99) == s);
            Check(LyricMotionPlanner.Prefix(null, 1) == "");
        });
        Test("display cap does not mutate source lyrics", () => {
            string source = new string('光', 200); string display = LyricMotionPlanner.DisplayText(source);
            Check(source.Length == 200 && display.EndsWith("…") && LyricMotionPlanner.ElementCount(display) == 161);
        });
        Test("long text never receives vertical or tiled layouts", () => {
            for (int seed = 0; seed < 200; seed++) {
                var p = Plan(); p.seed = seed; p.mood = (MotionMood)(seed % 3);
                var c = LyricMotionPlanner.ForLine(p, 0, new string('光', 80));
                Check(c.layout != MotionLayout.Vertical && c.layout != MotionLayout.Grid);
            }
        });
        Test("serialized plan round trip preserves locks and variations", () => {
            var p = Plan(); var before = LyricMotionPlanner.ForLine(p, 1, "光", true); before.locked = true;
            var options = new JsonSerializerOptions { IncludeFields = true };
            var loaded = JsonSerializer.Deserialize<LyricMotionPlan>(JsonSerializer.Serialize(p, options), options);
            LyricMotionPlanner.Validate(loaded, p.documentKey);
            Check(Signature(loaded.cuts[0]) == Signature(before) && loaded.cuts[0].locked);
        });
        Test("wrong schema and document identity are rejected", () => {
            var p = Plan(); Throws<FormatException>(() => LyricMotionPlanner.Validate(p, "another-song"));
            p.schemaVersion = 2; Throws<FormatException>(() => LyricMotionPlanner.Validate(p, p.documentKey));
        });
        Test("duplicate cut indices and invalid enums are rejected", () => {
            var p = Plan(); var c = LyricMotionPlanner.ForLine(p, 0, "光"); p.cuts.Add(c);
            Throws<FormatException>(() => LyricMotionPlanner.Validate(p, p.documentKey)); p.cuts.RemoveAt(1);
            c.layout = (MotionLayout)999; Throws<FormatException>(() => LyricMotionPlanner.Validate(p, p.documentKey));
        });
        Test("negative and out-of-range line indices are rejected", () => {
            Throws<ArgumentOutOfRangeException>(() => LyricMotionPlanner.ForLine(Plan(), -1, "光"));
            Throws<ArgumentOutOfRangeException>(() => LyricMotionPlanner.ForLine(Plan(), LyricMotionPlanner.MaxCuts, "光"));
        });
        Test("culture changes cannot change seeded layout choices", () => {
            var old = CultureInfo.CurrentCulture;
            try {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US"); string a = Signature(LyricMotionPlanner.ForLine(Plan(), 4, "夜"));
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-SA"); Check(a == Signature(LyricMotionPlanner.ForLine(Plan(), 4, "夜")));
            } finally { CultureInfo.CurrentCulture = old; }
        });
        Test("invalid queued mood is rejected", () => {
            Throws<ArgumentOutOfRangeException>(() => new LyricMotionDirector(Plan()).QueueMood((MotionMood)99));
        });
        Console.WriteLine($"{passed} passed / {failed} failed. Core only; Unity rendering is not exercised.");
        return failed == 0 ? 0 : 1;
    }
}
