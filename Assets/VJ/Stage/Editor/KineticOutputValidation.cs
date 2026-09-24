using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using VJPractice.Stage.Motion;

namespace VJPractice.Stage.Editor
{
    public static class KineticOutputValidation
    {
        static bool running;

        [MenuItem("VJ Practice/Kinetic/Verify output freeze and blackout (Play Mode)")]
        public static void Run()
        {
            var stage = UnityEngine.Object.FindFirstObjectByType<VJStage>();
            if (!Application.isPlaying || !stage || running)
            { Debug.LogWarning("Open Playable Stage and enter Play Mode first; only one output probe may run."); return; }
            int cueIndex = stage.Document == null ? -1 : stage.Document.ActiveAt(stage.Position);
            if (stage.Playing || cueIndex < 0 || string.IsNullOrWhiteSpace(stage.Document.lines[cueIndex].text))
            { Debug.LogWarning("Pause playback in the middle of a nonempty lyric before running the output probe."); return; }
            stage.StartCoroutine(Probe(stage));
        }

        static IEnumerator Probe(VJStage stage)
        {
            running = true;
            GameObject marker = null;
            bool oldKinetic = stage.KineticLyrics, oldFrozen = stage.Frozen, oldBlackout = stage.Blackout;
            try
            {
                var plan = new LyricMotionPlan { documentKey = "unity-probe" };
                LyricMotionPlanner.ForLine(plan, 0, "夜のひかり").locked = true;
                var roundTrip = JsonUtility.FromJson<LyricMotionPlan>(JsonUtility.ToJson(plan));
                LyricMotionPlanner.Validate(roundTrip, "unity-probe");
                Require(roundTrip.cuts[0].locked, "Unity JSON did not preserve the lock.");

                stage.KineticLyrics = true; stage.Frozen = false; stage.Blackout = false;
                yield return new WaitForEndOfFrame();
                yield return new WaitForEndOfFrame();
                Require(stage.KineticOutput && stage.KineticOutput.IsCreated(), "Final output was not allocated.");
                Transform nativeCanvas = stage.transform.Find(stage.JizuraReady
                    ? "JIZURA native output" : "Kinetic lyrics (native output)");
                Require(nativeCanvas && nativeCanvas.gameObject.activeInHierarchy, "Native lyric Canvas is not visible.");
                marker = new GameObject("Kinetic output probe marker", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                marker.transform.SetParent(nativeCanvas, false);
                var markerImage = marker.GetComponent<Image>(); markerImage.color = Color.magenta; markerImage.raycastTarget = false;
                var rect = marker.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(.48f, .48f); rect.anchorMax = new Vector2(.52f, .52f);
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                yield return new WaitForEndOfFrame();
                byte[] marked = Read(stage.KineticOutput, false);
                byte[] cameraFrame = Read(stage.outputCamera.targetTexture, false);
                int pixel = ((stage.KineticOutput.height / 2) * stage.KineticOutput.width + stage.KineticOutput.width / 2) * 3;
                Require(marked[pixel] > 130 && marked[pixel + 2] > 130 && marked[pixel + 1] < 100,
                    "Native Canvas marker is missing from the final texture. Camera center RGB: "
                    + cameraFrame[pixel] + "," + cameraFrame[pixel + 1] + "," + cameraFrame[pixel + 2]
                    + "; final center RGB: " + marked[pixel] + "," + marked[pixel + 1] + "," + marked[pixel + 2] + ".");
                UnityEngine.Object.Destroy(marker); marker = null;
                yield return new WaitForEndOfFrame();
                yield return new WaitForEndOfFrame();
                stage.Frozen = true;
                yield return new WaitForEndOfFrame();
                byte[] baseline = Read(stage.KineticOutput, true);
                Require(baseline.Any(b => b > 8), "Baseline is blank; a black texture cannot prove working output.");
                string frozenHash = Hash(baseline);
                for (int i = 0; i < 4; i++) yield return new WaitForEndOfFrame();
                Require(Hash(Read(stage.KineticOutput, false)) == frozenHash, "Final texture changed while frozen.");
                stage.Blackout = true;
                yield return new WaitForEndOfFrame();
                Require(Read(stage.KineticOutput, false).All(b => b == 0), "Blackout output was not black.");
                stage.Blackout = false;
                yield return new WaitForEndOfFrame();
                Require(Hash(Read(stage.KineticOutput, false)) == frozenHash, "Blackout destroyed the held frame.");
                Debug.Log("VJ_KINETIC_OUTPUT_PROBE_PASS: Unity JSON, native Canvas marker, nonblank output, held-frame hash, blackout, and restore. "
                    + "Inspect Verification/Kinetic-Probe.png for actual typography; this probe does not prove visual quality or 60 FPS.");
            }
            finally
            {
                if (marker) UnityEngine.Object.Destroy(marker);
                if (stage) { stage.KineticLyrics = oldKinetic; stage.Frozen = oldFrozen; stage.Blackout = oldBlackout; }
                running = false;
            }
        }

        static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        static string Hash(byte[] bytes)
        { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)); }
        static byte[] Read(RenderTexture target, bool save)
        {
            var previous = RenderTexture.active;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            try
            {
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); image.Apply();
                if (save)
                {
                    string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Verification"));
                    Directory.CreateDirectory(directory);
                    File.WriteAllBytes(Path.Combine(directory, "Kinetic-Probe.png"), image.EncodeToPNG());
                }
                return image.GetRawTextureData<byte>().ToArray();
            }
            finally { RenderTexture.active = previous; UnityEngine.Object.Destroy(image); }
        }
    }
}
