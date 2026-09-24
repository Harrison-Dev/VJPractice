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

        [MenuItem("VJ Practice/JIZURA/Verify Stage blend (Play Mode)")]
        public static void VerifyStageBlend()
        {
            var stage = UnityEngine.Object.FindFirstObjectByType<VJStage>();
            if (!Application.isPlaying || !stage || running)
            { Debug.LogWarning("Open the playable stage in Editor Play Mode before verifying the JIZURA × Stage blend."); return; }
            stage.StartCoroutine(BlendProbe(stage));
        }

        static IEnumerator BlendProbe(VJStage stage)
        {
            running = true;
            bool oldKinetic = stage.KineticLyrics, oldFrozen = stage.Frozen, oldBlackout = stage.Blackout;
            bool oldBlendOn = stage.JizuraBlendStageVisuals, wasPlaying = stage.Playing;
            float oldBlend = stage.JizuraStageBlend, oldPosition = stage.Position;
            float oldEnergy = stage.Energy, oldDensity = stage.Density, oldFlow = stage.Flow, oldEcho = stage.Echo;
            int oldTemplate = stage.TemplateIndex;
            try
            {
                Require(stage.ActiveJizuraPlan != null && stage.ActiveJizuraPlan.cuts.Count > 0, "Stage has no JIZURA timeline.");
                if (stage.Playing) stage.TogglePlay();
                stage.KineticLyrics = true; stage.Frozen = false; stage.Blackout = false;
                stage.JizuraBlendStageVisuals = true;
                JizuraCut cut = stage.ActiveJizuraPlan.cuts.FirstOrDefault(c => c.line >= 0 && c.layout == "vcols")
                    ?? stage.ActiveJizuraPlan.cuts.First(c => c.line >= 0);
                stage.Seek(cut.start + Math.Min(.25f, (cut.end - cut.start) * .4f));
                stage.JizuraStageBlend = 0;
                yield return new WaitForEndOfFrame();
                yield return new WaitForEndOfFrame();
                Require(stage.JizuraReady && stage.KineticOutput && stage.KineticOutput.IsCreated(), "Native RenderTexture was not ready.");
                byte[] jizuraOnly = Read(stage.KineticOutput, "Jizura-blend-zero.png");
                stage.JizuraStageBlend = .55f;
                yield return new WaitForEndOfFrame();
                yield return new WaitForEndOfFrame();
                byte[] stageBackground = Read(stage.KineticOutput, "Jizura-blend-half.png");
                float blendDifference = MeanDifference(jizuraOnly, stageBackground);
                Require(blendDifference > 2f, "Blend 0 and blend 0.55 rendered almost identical frames (mean RGB difference "
                    + blendDifference.ToString("F2") + ").");

                // Blend 1 is a separate text-only mode. Compare it against 0.99,
                // with an equal-time baseline to discount the moving Stage shader.
                stage.JizuraStageBlend = .99f;
                yield return new WaitForEndOfFrame();
                yield return new WaitForEndOfFrame();
                byte[] nearTextA = Read(stage.KineticOutput, "Jizura-blend-near-text-a.png");
                yield return new WaitForEndOfFrame();
                yield return new WaitForEndOfFrame();
                byte[] nearTextB = Read(stage.KineticOutput, "Jizura-blend-near-text-b.png");
                float movingBaseline = MeanDifference(nearTextA, nearTextB);
                stage.JizuraStageBlend = 1f;
                yield return new WaitForEndOfFrame();
                yield return new WaitForEndOfFrame();
                byte[] textOnly = Read(stage.KineticOutput, "Jizura-text-only.png");
                float textOnlyDifference = MeanDifference(nearTextB, textOnly);
                Require(textOnlyDifference > movingBaseline + 1f,
                    "The text-only endpoint did not differ from near-full blend beyond Stage motion ("
                    + textOnlyDifference.ToString("F2") + " vs " + movingBaseline.ToString("F2") + ").");

                string templateNote = "one Stage template available";
                if (stage.templates != null && stage.templates.Length > 1)
                {
                    stage.JizuraStageBlend = .55f;
                    int first = 0, second = 1;
                    stage.SetTemplate(first);
                    for (int i = 0; i < 12; i++) yield return new WaitForEndOfFrame();
                    byte[] templateA = Read(stage.KineticOutput, "Jizura-stage-template-a.png");
                    for (int i = 0; i < 12; i++) yield return new WaitForEndOfFrame();
                    byte[] sameTemplate = Read(stage.KineticOutput, "Jizura-stage-template-a2.png");
                    float animationDelta = MeanDifference(templateA, sameTemplate);
                    stage.SetTemplate(second);
                    for (int i = 0; i < 12; i++) yield return new WaitForEndOfFrame();
                    byte[] templateB = Read(stage.KineticOutput, "Jizura-stage-template-b.png");
                    float templateDelta = MeanDifference(templateA, templateB);
                    Require(templateDelta > animationDelta + 1f,
                        "Changing the Stage template did not measurably change blended output (template delta "
                        + templateDelta.ToString("F2") + ", animation delta " + animationDelta.ToString("F2") + ").");
                    templateNote = "template delta " + templateDelta.ToString("F2")
                        + " > animation delta " + animationDelta.ToString("F2");
                }
                Debug.Log("VJ_JIZURA_STAGE_BLEND_PASS: blend 0 ↔ 0.55 RGB difference "
                    + blendDifference.ToString("F2") + "; text-only edge " + textOnlyDifference.ToString("F2")
                    + " > moving baseline " + movingBaseline.ToString("F2") + "; " + templateNote + ".");
            }
            finally
            {
                if (stage)
                {
                    stage.SetTemplate(oldTemplate);
                    stage.Energy = oldEnergy; stage.Density = oldDensity; stage.Flow = oldFlow; stage.Echo = oldEcho;
                    stage.JizuraBlendStageVisuals = oldBlendOn; stage.JizuraStageBlend = oldBlend;
                    stage.KineticLyrics = oldKinetic; stage.Frozen = oldFrozen; stage.Blackout = oldBlackout;
                    stage.Seek(oldPosition);
                    if (stage.Playing != wasPlaying) stage.TogglePlay();
                }
                running = false;
            }
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
        static float MeanDifference(byte[] a, byte[] b)
        {
            Require(a != null && b != null && a.Length == b.Length && a.Length > 0, "Captured frame sizes differ.");
            double sum = 0;
            int count = 0;
            // Sample every fourth pixel to keep the Editor probe cheap at 1080p.
            for (int i = 0; i + 2 < a.Length; i += 12)
            {
                sum += Math.Abs(a[i] - b[i]) + Math.Abs(a[i + 1] - b[i + 1]) + Math.Abs(a[i + 2] - b[i + 2]);
                count += 3;
            }
            return (float)(sum / Math.Max(1, count));
        }
        static void Require(bool valid, string message)
        { if (!valid) throw new InvalidOperationException(message); }
    }
}
