using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using VJPractice.Stage.Jizura;

namespace VJPractice.Stage.Editor
{
    /// <summary>Editor-only acceptance probes for original-format import and visible native output.</summary>
    public static class JizuraValidation
    {
        static bool running;

        [MenuItem("VJ Practice/JIZURA/Verify project and planner")]
        public static void VerifyProject()
        {
            TextAsset source = Resources.Load<TextAsset>("JizuraDemo");
            Require(source, "JIZURA demo project was not imported as a TextAsset.");
            JizuraProject project = JizuraProject.ParseJson(source.text);
            JizuraPlan plan = JizuraPlanner.Build(project);
            Require(plan.lines.Count == 10, "Original lyric syntax lost a line.");
            Require(plan.cuts.Count > plan.lines.Count, "Planner did not split lyrics into multiple cuts.");
            Require(plan.cuts.Any(c => c.line == 0 && c.layout == "vcols"), "Per-line layout override was lost.");
            Require(plan.lines[3].impact && plan.lines[3].emph.Contains("息"), "Impact or emphasis syntax was lost.");
            Require(project.GetOverride(0)?.locked == true, "Locked line was not imported.");
            string canonical = project.ToJson();
            var roundTrip = JizuraProject.ParseJson(canonical);
            Require(roundTrip.GetOverride(0)?.locked == true && roundTrip.GetOverride(0).layout == "vcols",
                "Original project settings did not survive JSON round trip.");
            string first = Fingerprint(plan);
            Require(first == Fingerprint(JizuraPlanner.Build(roundTrip)), "Seeded planning changed after save/reopen.");
            var reroll = roundTrip.GetOverride(1);
            if (reroll == null) { reroll = new JizuraLineOverride(); roundTrip.overrides[1] = reroll; }
            reroll.seed++;
            Require(first != Fingerprint(JizuraPlanner.Build(roundTrip)), "Per-line reroll did not change its plan.");
            Debug.Log("VJ_JIZURA_PLAN_PASS: original JSON import/export, multi-cut, syntax, lock, deterministic replay, reroll.");
        }

        [MenuItem("VJ Practice/JIZURA/Verify native output (Play Mode)")]
        public static void VerifyOutput()
        {
            var stage = UnityEngine.Object.FindFirstObjectByType<VJStage>();
            if (!Application.isPlaying || !stage || running)
            { Debug.LogWarning("Open the playable stage in Editor Play Mode before verifying JIZURA output."); return; }
            stage.StartCoroutine(OutputProbe(stage));
        }

        static IEnumerator OutputProbe(VJStage stage)
        {
            running = true;
            bool oldKinetic = stage.KineticLyrics, oldFrozen = stage.Frozen, oldBlackout = stage.Blackout;
            bool wasPlaying = stage.Playing;
            float oldPosition = stage.Position;
            try
            {
                VerifyProject();
                Require(stage.ActiveJizuraPlan != null && stage.ActiveJizuraPlan.cuts.Count > 2, "Stage has no JIZURA timeline.");
                if (stage.Playing) stage.TogglePlay();
                stage.KineticLyrics = true; stage.Frozen = false; stage.Blackout = false;
                var first = stage.ActiveJizuraPlan.cuts.First(c => c.line == 0);
                var later = stage.ActiveJizuraPlan.cuts.First(c => c.line >= 3 && c.layout == "huge");
                stage.Seek(first.start + Math.Min(.4f, (first.end - first.start) * .5f));
                yield return new WaitForEndOfFrame();
                yield return new WaitForEndOfFrame();
                Require(stage.JizuraReady && stage.KineticOutput && stage.KineticOutput.IsCreated(), "Native RenderTexture was not ready.");
                Transform nativeCanvas = stage.transform.Find("JIZURA native output");
                Require(nativeCanvas && nativeCanvas.gameObject.activeInHierarchy, "JIZURA native Canvas is hidden.");
                Require(nativeCanvas.GetComponentsInChildren<Text>(false).Any(t => !string.IsNullOrEmpty(t.text)),
                    "The active cut produced no native glyph meshes.");
                byte[] a = Read(stage.KineticOutput, "Jizura-native-first.png");
                stage.Seek(later.start + Math.Min(.4f, (later.end - later.start) * .5f));
                yield return new WaitForEndOfFrame();
                yield return new WaitForEndOfFrame();
                byte[] b = Read(stage.KineticOutput, "Jizura-native-later.png");
                Require(a.Any(v => v > 16) && b.Any(v => v > 16), "Captured output is blank.");
                Require(Hash(a) != Hash(b), "Two different original cuts produced identical output.");
                Debug.Log("VJ_JIZURA_OUTPUT_PASS: native Canvas glyphs, nonblank URP output, distinct timed cuts. "
                    + "Inspect Verification/Jizura-native-*.png for visual quality.");
            }
            finally
            {
                if (stage)
                {
                    stage.KineticLyrics = oldKinetic; stage.Frozen = oldFrozen; stage.Blackout = oldBlackout;
                    stage.Seek(oldPosition);
                    if (stage.Playing != wasPlaying) stage.TogglePlay();
                }
                running = false;
            }
        }

        static string Fingerprint(JizuraPlan plan)
        {
            return string.Join("|", plan.cuts.Select(c => c.line + ":" + c.text + ":" + c.layout + ":"
                + c.enter + ":" + c.exit + ":" + c.seed + ":" + c.start.ToString("R", System.Globalization.CultureInfo.InvariantCulture)));
        }

        static byte[] Read(RenderTexture target, string filename)
        {
            var previous = RenderTexture.active;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            try
            {
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                image.Apply();
                string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Verification"));
                Directory.CreateDirectory(directory);
                File.WriteAllBytes(Path.Combine(directory, filename), image.EncodeToPNG());
                return image.GetRawTextureData<byte>().ToArray();
            }
            finally { RenderTexture.active = previous; UnityEngine.Object.Destroy(image); }
        }

        static string Hash(byte[] bytes)
        { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)); }
        static void Require(bool valid, string message)
        { if (!valid) throw new InvalidOperationException(message); }
    }
}
