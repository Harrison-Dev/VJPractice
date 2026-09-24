using System;
using System.Collections.Generic;
using System.Globalization;

namespace VJPractice.Stage.Motion
{
    public enum MotionMood { Calm, Pop, Glitch }
    public enum MotionLayout { Hero, Vertical, Diagonal, Grid, Typewriter }
    public enum MotionEntrance { Fade, Slide, Pop }
    public enum MotionExit { Fade, Drop, Shrink }

    [Serializable]
    public sealed class MotionCut
    {
        public int lineIndex, revision;
        public string textHash = "";
        public bool locked;
        public MotionMood mood;
        public MotionLayout layout;
        public MotionEntrance entrance;
        public MotionExit exit;
        public int variation;
    }

    // Presentation data only: deliberately not a replacement for LyricDocument.
    [Serializable]
    public sealed class LyricMotionPlan
    {
        public int schemaVersion = 1;
        public int seed = 85228;
        public string documentKey = "";
        public MotionMood mood = MotionMood.Pop;
        public List<MotionCut> cuts = new List<MotionCut>();
    }

    public struct MotionSample
    {
        public float alpha, entrance, exit, progress;
    }

    /// <summary>Pure, portable planning and absolute-time sampling. No Unity or global RNG.</summary>
    public static class LyricMotionPlanner
    {
        public const int MaxDisplayElements = 160;
        public const int MaxCuts = 10000;

        public static uint Hash(string text)
        {
            // Explicit UTF-16 FNV-1a: stable across processes, runtimes and locales.
            unchecked
            {
                uint h = 2166136261;
                foreach (char c in text ?? "") { h = (h ^ (byte)c) * 16777619; h = (h ^ (byte)(c >> 8)) * 16777619; }
                return h;
            }
        }

        public static string TextHash(string text) => Hash(text).ToString("x8", CultureInfo.InvariantCulture);
        public static int ElementCount(string text) => StringInfo.ParseCombiningCharacters(text ?? "").Length;

        public static string Prefix(string text, int count)
        {
            text = text ?? "";
            if (count <= 0) return "";
            int[] starts = StringInfo.ParseCombiningCharacters(text);
            return count >= starts.Length ? text : text.Substring(0, starts[count]);
        }

        public static string DisplayText(string text)
        {
            text = (text ?? "").Replace("\r", "");
            return ElementCount(text) <= MaxDisplayElements ? text : Prefix(text, MaxDisplayElements) + "…";
        }

        public static MotionCut ForLine(LyricMotionPlan plan, int index, string text, bool reroll = false)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (index < 0 || index >= MaxCuts) throw new ArgumentOutOfRangeException(nameof(index));
            string fingerprint = TextHash(text);
            MotionCut cut = plan.cuts.Find(c => c.lineIndex == index);
            bool sameText = cut != null && cut.textHash == fingerprint;
            // A lock cannot preserve an obsolete layout after the lyric content changes.
            if (sameText && cut.locked) return cut;
            int revision = sameText ? cut.revision : 0;
            if (reroll) revision = revision == int.MaxValue ? 0 : revision + 1;
            if (sameText && !reroll && cut.mood == plan.mood) return cut;
            if (cut != null) plan.cuts.Remove(cut);
            uint rng = Hash(plan.seed.ToString(CultureInfo.InvariantCulture) + ":" + index.ToString(CultureInfo.InvariantCulture)
                + ":" + fingerprint + ":" + revision.ToString(CultureInfo.InvariantCulture) + ":" + (int)plan.mood);
            int variation = (int)(rng & 0x7fffffff);
            MotionLayout[] choices = plan.mood == MotionMood.Calm
                ? new[] { MotionLayout.Hero, MotionLayout.Vertical, MotionLayout.Typewriter }
                : plan.mood == MotionMood.Pop
                    ? new[] { MotionLayout.Hero, MotionLayout.Diagonal, MotionLayout.Grid, MotionLayout.Vertical }
                    : new[] { MotionLayout.Hero, MotionLayout.Diagonal, MotionLayout.Grid, MotionLayout.Typewriter };
            MotionLayout layout = choices[Next(ref rng) % (uint)choices.Length];
            int n = ElementCount(text);
            if ((layout == MotionLayout.Vertical && n > 14) || (layout == MotionLayout.Grid && n > 32)) layout = MotionLayout.Hero;
            cut = new MotionCut
            {
                lineIndex = index, revision = revision, textHash = fingerprint, mood = plan.mood,
                variation = variation, layout = layout,
                entrance = plan.mood == MotionMood.Calm ? MotionEntrance.Fade : (MotionEntrance)(Next(ref rng) % 3),
                exit = plan.mood == MotionMood.Calm ? MotionExit.Fade : (MotionExit)(Next(ref rng) % 3)
            };
            plan.cuts.Add(cut);
            return cut;
        }

        static uint Next(ref uint state)
        {
            unchecked { if (state == 0) state = 0x9e3779b9; state ^= state << 13; state ^= state >> 17; state ^= state << 5; return state; }
        }

        public static MotionSample Sample(float time, float start, float end)
        {
            if (!Finite(time) || !Finite(start) || !Finite(end) || end <= start || time < start || time >= end)
                return new MotionSample();
            float duration = end - start;
            float enter = Clamp01((time - start) / Math.Min(.38f, duration * .3f));
            float exit = Clamp01((time - (end - Math.Min(.28f, duration * .3f))) / Math.Min(.28f, duration * .3f));
            return new MotionSample { alpha = Smooth(enter) * (1 - Smooth(exit)), entrance = Smooth(enter), exit = Smooth(exit), progress = (time - start) / duration };
        }

        public static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static float Clamp01(float value) => Math.Max(0, Math.Min(1, value));
        static float Smooth(float value) => value * value * (3 - 2 * value);

        public static void Validate(LyricMotionPlan plan, string expectedDocumentKey)
        {
            if (plan == null || plan.schemaVersion != 1) throw new FormatException("Unsupported motion plan version.");
            if (plan.documentKey != expectedDocumentKey) throw new FormatException("Motion plan belongs to a different lyric document.");
            if (!Enum.IsDefined(typeof(MotionMood), plan.mood) || plan.cuts == null || plan.cuts.Count > MaxCuts)
                throw new FormatException("Invalid motion plan.");
            var indices = new HashSet<int>();
            foreach (MotionCut c in plan.cuts)
            {
                if (c == null || c.lineIndex < 0 || c.lineIndex >= MaxCuts || !indices.Add(c.lineIndex) || c.revision < 0
                    || c.variation < 0 || c.textHash == null || c.textHash.Length != 8
                    || !Enum.IsDefined(typeof(MotionMood), c.mood) || !Enum.IsDefined(typeof(MotionLayout), c.layout)
                    || !Enum.IsDefined(typeof(MotionEntrance), c.entrance) || !Enum.IsDefined(typeof(MotionExit), c.exit))
                    throw new FormatException("Invalid or duplicate motion cut.");
            }
        }
    }

    /// <summary>Plans active cues and preserves queued commands across instrumental gaps.</summary>
    public sealed class LyricMotionDirector
    {
        public LyricMotionPlan Plan { get; }
        public bool HasPending => pendingMood.HasValue || rerollNext;
        public MotionCut Current { get; private set; }
        MotionMood? pendingMood;
        bool rerollNext;
        int lastLine = -1;
        string lastHash = "", lastText = "";

        public LyricMotionDirector(LyricMotionPlan plan) { Plan = plan ?? throw new ArgumentNullException(nameof(plan)); }
        public void QueueMood(MotionMood mood)
        {
            if (!Enum.IsDefined(typeof(MotionMood), mood)) throw new ArgumentOutOfRangeException(nameof(mood));
            pendingMood = mood;
        }
        public void QueueReroll() { rerollNext = true; }
        public bool SetMood(MotionMood mood)
        {
            if (!Enum.IsDefined(typeof(MotionMood), mood)) throw new ArgumentOutOfRangeException(nameof(mood));
            Plan.mood = mood;
            pendingMood = null;
            if (Current == null || Current.locked || Current.mood == mood) return false;
            Current = LyricMotionPlanner.ForLine(Plan, lastLine, lastText);
            return true;
        }
        public bool RerollCurrentOrNext()
        {
            if (Current == null) { QueueReroll(); return false; }
            if (Current.locked) return false;
            rerollNext = false;
            Current = LyricMotionPlanner.ForLine(Plan, lastLine, lastText, true);
            return true;
        }
        public bool ToggleLock() { if (Current == null) return false; Current.locked = !Current.locked; return Current.locked; }
        public MotionCut Visit(int index, string text)
        {
            // Instrumental gaps do not consume a queued change.
            if (index < 0 || string.IsNullOrWhiteSpace(text)) { Current = null; return null; }
            string hash = LyricMotionPlanner.TextHash(text);
            if (index == lastLine && hash == lastHash && Current != null) return Current;
            bool boundary = index != lastLine || hash != lastHash;
            bool reroll = false;
            if (boundary)
            {
                if (pendingMood.HasValue) { Plan.mood = pendingMood.Value; pendingMood = null; }
                reroll = rerollNext; rerollNext = false;
            }
            Current = LyricMotionPlanner.ForLine(Plan, index, text, reroll);
            lastLine = index; lastHash = hash; lastText = text;
            return Current;
        }
    }
}
