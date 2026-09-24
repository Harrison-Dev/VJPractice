using System;
using System.Collections;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using VJPractice.Stage.Motion;

namespace VJPractice.Stage
{
    public sealed partial class VJStage
    {
        public bool KineticLyrics = true;
        public int KineticWidth = 1280, KineticHeight = 720;
        public bool KineticReady => KineticLyrics && motionCompositor != null && (motionRenderer != null || jizuraRenderer != null);
        public RenderTexture KineticOutput => KineticReady ? motionCompositor.Output : null;
        public int MotionMoodIndex => JizuraReady ? JizuraMoodIndex() : motionDirector == null ? 1 : (int)motionDirector.Plan.mood;
        public bool MotionLocked => JizuraReady ? JizuraLineLocked() : motionDirector?.Current != null && motionDirector.Current.locked;
        public bool MotionPending => !JizuraReady && motionDirector != null && motionDirector.HasPending;
        public string MotionStatus { get; private set; } = "先在音源頁選原創示範；M：風格 · N：重抽 · L：鎖定";
        KineticLyricRenderer motionRenderer;
        StageCompositor motionCompositor;
        LyricMotionDirector motionDirector;
        LyricDocument motionDocument;
        string motionPath;
        int requestedMood = 1;
        float motionLastPosition = float.NaN;

        void MotionInit()
        {
            try
            {
                motionRenderer = new KineticLyricRenderer(transform, outputCamera, japaneseFont);
                motionCompositor = new StageCompositor(outputCamera, () => Frozen, () => Blackout);
                try { JizuraInit(); }
                catch (Exception jizuraError)
                {
                    JizuraDispose();
                    Debug.LogException(jizuraError);
                    Message = "JIZURA 原生渲染初始化失敗，已保留舊文字模式：" + jizuraError.Message;
                }
                tab = 3;
                if (jizuraRenderer != null) Message = "JIZURA 原生文字 PV 已啟用。";
            }
            catch (Exception e)
            {
                MotionDispose(); KineticLyrics = false;
                Message = "文字 PV 初始化失敗，已保留原模式：" + e.Message;
                Debug.LogException(e);
            }
        }

        void PrepareMotionOutput()
        {
            try { motionCompositor?.Prepare(KineticReady, KineticWidth, KineticHeight); }
            catch (Exception e)
            {
                KineticLyrics = false; motionCompositor?.Prepare(false, KineticWidth, KineticHeight);
                Message = "文字 PV 輸出失敗，已退回原模式：" + e.Message;
            }
            if (motionRenderer != null) motionRenderer.Visible = KineticReady && !JizuraReady;
            if (jizuraRenderer != null) jizuraRenderer.Visible = JizuraReady;
            if (!KineticReady) return;
            // Do not carry the feedback framebuffer from a different song/seek into the new cue.
            if (!Frozen)
            {
                if (motionDocument != Document || (!float.IsNaN(motionLastPosition)
                    && (Position < motionLastPosition - .05f || Position - motionLastPosition > 1f)))
                { ClearMotionFeedback(history); ClearMotionFeedback(next); }
                motionLastPosition = Position;
            }
        }

        static void ClearMotionFeedback(RenderTexture texture)
        {
            if (!texture) return;
            var previous = RenderTexture.active;
            try { RenderTexture.active = texture; GL.Clear(true, true, Color.black); }
            finally { RenderTexture.active = previous; }
        }

        void UpdateMotion()
        {
            if (!KineticReady || Document == null || (Frozen && motionCompositor.HasFrame)) return;
            if (JizuraReady) { UpdateJizura(); return; }
            if (motionDocument != Document) BindMotionDocument();
            int index = Document.ActiveAt(Position);
            LyricCue cue = index >= 0 ? Document.lines[index] : null;
            MotionCut cut = motionDirector.Visit(index, cue?.text);
            motionRenderer.Render(cue, cut, Position - Document.offsetSeconds,
                Energy, Density, Flow, Echo, Audio == null ? 0 : Audio.Bands.x);
            string state = cut == null
                ? Audio != null && Audio.Source.clip == null && !Audio.External
                    ? "先到音源頁選原創示範"
                    : "目前無歌詞；請播放或跳到有字段落"
                : $"第 {index + 1} 句 / {cut.layout} / {(cut.locked ? "已鎖定" : "自動")}";
            MotionStatus = state + (MotionPending ? " · 下一句待重抽" : "")
                + (cut != null && motionRenderer.Truncated ? " · 長句預覽截斷（原文不變）" : "");
        }

        void BindMotionDocument()
        {
            motionDocument = Document;
            string key = MotionDocumentKey(Document);
            motionPath = Path.Combine(Application.persistentDataPath, "MotionPlan-" + key + ".json");
            motionDirector = new LyricMotionDirector(new LyricMotionPlan { documentKey = key });
            requestedMood = MotionMoodIndex;
            // Only previously explicit saves are restored. New songs never inherit another song's locks.
            if (File.Exists(motionPath)) LoadMotionPlan();
        }

        static string MotionDocumentKey(LyricDocument document)
        {
            var b = new StringBuilder();
            AppendKeyPart(b, document.title); AppendKeyPart(b, document.artist);
            foreach (LyricCue cue in document.lines) AppendKeyPart(b, cue.text);
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(b.ToString()))).Replace("-", "").ToLowerInvariant();
        }

        static void AppendKeyPart(StringBuilder b, string text)
        {
            text = text ?? "";
            b.Append(text.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(':').Append(text);
        }

        public void SaveMotionPlan()
        {
            if (motionDirector == null) { Message = "先載入文字 PV 歌詞。"; return; }
            try
            {
                string temp = motionPath + ".tmp";
                File.WriteAllText(temp, JsonUtility.ToJson(motionDirector.Plan, true), Encoding.UTF8);
                if (File.Exists(motionPath)) File.Replace(temp, motionPath, null);
                else File.Move(temp, motionPath);
                Message = "已儲存本曲文字配置與鎖定；尚待生效的操作不存檔。";
            }
            catch (Exception e) { Message = "文字配置儲存失敗：" + e.Message; }
        }

        public void LoadMotionPlan()
        {
            if (motionDirector == null) { Message = "先載入文字 PV 歌詞。"; return; }
            try
            {
                if (!File.Exists(motionPath)) { Message = "本曲尚未儲存文字配置。"; return; }
                if (new FileInfo(motionPath).Length > 4 * 1024 * 1024) throw new FormatException("配置檔案過大。");
                var plan = JsonUtility.FromJson<LyricMotionPlan>(File.ReadAllText(motionPath, Encoding.UTF8));
                LyricMotionPlanner.Validate(plan, motionDirector.Plan.documentKey);
                foreach (MotionCut cut in plan.cuts)
                    if (cut.lineIndex >= Document.lines.Count || cut.textHash != LyricMotionPlanner.TextHash(Document.lines[cut.lineIndex].text))
                        throw new FormatException("配置與歌詞內容不一致。");
                motionDirector = new LyricMotionDirector(plan); requestedMood = (int)plan.mood;
                Message = "已載入本曲文字配置。";
            }
            catch (Exception e) { Message = "文字配置未載入，維持目前設定：" + e.Message; }
        }

        public bool ApplyMotionControl(string action, float value)
        {
            if (!LyricDocument.Finite(value)) return true;
            // Selecting a classic style leaves the new renderer.
            if (action == "lyric") { KineticLyrics = false; return false; }
            if (ActiveJizuraPlan != null && ApplyJizuraControl(action, value)) return true;
            switch (action)
            {
                case "motion":
                    if (value >= .5f && (motionRenderer == null || motionCompositor == null))
                    { Message = "文字 PV 尚未就緒；請查看 Unity Console。"; return true; }
                    KineticLyrics = value >= .5f;
                    return true;
                case "motionMood":
                    if (!ActivateMotionControls()) return true;
                    requestedMood = Mathf.Clamp((int)value, 0, 2);
                    bool changed = motionDirector != null && motionDirector.SetMood((MotionMood)requestedMood);
                    Message = changed ? "已套用風格到目前歌詞。" : motionDirector?.Current?.locked == true
                        ? "目前歌詞已鎖定；新風格會套用到其他句。" : "已選風格；播放到歌詞時顯示。";
                    return true;
                case "motionReroll":
                    if (!ActivateMotionControls()) return true;
                    bool rerolled = motionDirector != null && motionDirector.RerollCurrentOrNext();
                    Message = rerolled ? "已重抽目前歌詞。" : motionDirector?.Current?.locked == true
                        ? "目前歌詞已鎖定；先解鎖才能重抽。" : "目前沒有歌詞；下一句將重抽。";
                    return true;
                case "motionLock":
                    if (!ActivateMotionControls()) return true;
                    Message = motionDirector?.Current == null ? "目前沒有可鎖定的歌詞。"
                        : motionDirector.ToggleLock() ? "已鎖定本句配置。" : "已解鎖本句配置。";
                    return true;
                case "motionSave": SaveMotionPlan(); return true;
                case "motionLoad": LoadMotionPlan(); return true;
                case "motionCapture": CaptureMotionOutput(); return true;
                default: return false;
            }
        }

        bool ActivateMotionControls()
        {
            if (motionRenderer == null || motionCompositor == null)
            { Message = "文字 PV 尚未就緒；請查看 Unity Console。"; return false; }
            KineticLyrics = true;
            if (Document == null) return true;
            if (motionDirector == null || motionDocument != Document) BindMotionDocument();
            int index = Document.ActiveAt(Position);
            motionDirector.Visit(index, index >= 0 ? Document.lines[index].text : null);
            return true;
        }

        bool HandleMotionKey(KeyCode key)
        {
            switch (key)
            {
                case KeyCode.K: return ApplyMotionControl("motion", KineticLyrics ? 0 : 1);
                case KeyCode.M: return ApplyMotionControl("motionMood", (MotionMoodIndex + 1) % 3);
                case KeyCode.N: return ApplyMotionControl("motionReroll", 0);
                case KeyCode.L: return ApplyMotionControl("motionLock", 0);
                case KeyCode.F6: return ApplyMotionControl("motionSave", 0);
                case KeyCode.F7: return ApplyMotionControl("motionLoad", 0);
                case KeyCode.F8: return ApplyMotionControl("motionCapture", 0);
                case KeyCode.Q: case KeyCode.W: case KeyCode.E: case KeyCode.R: case KeyCode.T: case KeyCode.Y:
                    KineticLyrics = false; return false;
                default: return false;
            }
        }

        bool DrawMotionOutput(Rect area)
        {
            if (!KineticReady) return false;
            // Stable 16:9 output, letterboxed independently of the operator panel dimensions.
            Fill(area, Color.black);
            if (KineticOutput) GUI.DrawTexture(area, KineticOutput, ScaleMode.ScaleToFit, false);
            return true;
        }

        public void CaptureMotionOutput() { StartCoroutine(CaptureMotion()); }
        IEnumerator CaptureMotion()
        {
            yield return new WaitForEndOfFrame();
            RenderTexture target = KineticOutput;
            if (!target) { Message = "請先開啟文字 PV（K）。"; yield break; }
            Texture2D image = null;
            var previous = RenderTexture.active;
            try
            {
                image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); image.Apply();
                string directory = Application.isEditor ? Path.GetFullPath(Path.Combine(Application.dataPath, "../Verification"))
                    : Path.Combine(Application.persistentDataPath, "Verification");
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, "Kinetic-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + ".png");
                File.WriteAllBytes(path, image.EncodeToPNG());
                Message = "已輸出最終貼圖：" + path; Debug.Log("VJ_KINETIC_CAPTURE_OK " + path);
            }
            catch (Exception e) { Message = "輸出失敗：" + e.Message; }
            finally { RenderTexture.active = previous; if (image) Destroy(image); }
        }

        void OnDisable()
        {
            if (motionRenderer != null) motionRenderer.Visible = false;
            if (jizuraRenderer != null) jizuraRenderer.Visible = false;
            motionCompositor?.Prepare(false, KineticWidth, KineticHeight);
        }

        void MotionDispose()
        {
            JizuraDispose();
            motionCompositor?.Dispose(); motionCompositor = null;
            motionRenderer?.Dispose(); motionRenderer = null;
        }
    }
}
