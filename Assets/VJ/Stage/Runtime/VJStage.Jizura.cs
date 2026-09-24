using System;
using System.IO;
using System.Text;
using UnityEngine;
using VJPractice.Stage.Jizura;

namespace VJPractice.Stage
{
    public sealed partial class VJStage
    {
        JizuraNativeRenderer jizuraRenderer;
        string jizuraProjectPath;
        // Mix the existing StageVisual/particle camera under native JIZURA type.
        // Zero keeps JIZURA's palette background; one exposes the stage fully.
        public bool JizuraBlendStageVisuals = true;
        [Range(0f, 1f)] public float JizuraStageBlend = .55f;
        [Serializable] sealed class JizuraLiveLook
        {
            public int version;
            public bool blendStage;
            public float stageBlend;
            public int templateIndex;
            public float energy, density, flow, echo, bpm;
        }
        static string JizuraLiveLookPath => Path.Combine(Application.persistentDataPath, "CurrentJizura.live.json");
        public JizuraProject ActiveJizuraProject { get; private set; }
        public JizuraPlan ActiveJizuraPlan { get; private set; }
        public bool JizuraReady => KineticLyrics && jizuraRenderer != null && ActiveJizuraPlan != null;

        void JizuraInit()
        {
            jizuraRenderer = new JizuraNativeRenderer(transform, outputCamera, japaneseFont);
            var demo = Resources.Load<TextAsset>("JizuraDemo");
            SetJizuraProject(demo ? JizuraProject.ParseJson(demo.text) : JizuraProject.FromLyricDocument(Document));
            // Open the Editor on an actual lyric cut so the native output can be
            // inspected immediately, even while the demo audio is already playing.
            if (Application.isEditor && demo && ActiveJizuraPlan.lines.Count > 0)
                Seek(ActiveJizuraPlan.lines[0].start + .35f);
            jizuraRenderer.Visible = false;
        }

        void JizuraDispose()
        {
            jizuraRenderer?.Dispose();
            jizuraRenderer = null;
        }

        public void SetJizuraProject(JizuraProject project)
        {
            if (project == null) throw new ArgumentNullException(nameof(project));
            var previousProject = ActiveJizuraProject;
            var previousPlan = ActiveJizuraPlan;
            ActiveJizuraProject = project;
            try { ReplanJizura(); }
            catch
            {
                ActiveJizuraProject = previousProject;
                ActiveJizuraPlan = previousPlan;
                throw;
            }
            KineticLyrics = true;
            Message = "已載入 JIZURA 專案：" + ActiveJizuraPlan.cuts.Count + " cuts / Unity 原生渲染";
        }

        public void ReplanJizura()
        {
            if (ActiveJizuraProject == null) throw new InvalidOperationException("No JIZURA project is loaded.");
            float audioLength = Audio != null && !Audio.External && Audio.Source != null && Audio.Source.clip
                ? Audio.Source.clip.length : 0f;
            JizuraPlan built = JizuraPlanner.Build(ActiveJizuraProject, audioLength);
            ActiveJizuraPlan = built;
            jizuraRenderer?.SetTimeline(built);
            // Existing lyric transport and timeline still use LyricDocument. These cues mirror
            // the original JIZURA planner's line timing; the cut renderer reads the full plan.
            var document = new LyricDocument
            {
                title = ActiveJizuraProject.title ?? "",
                artist = ActiveJizuraProject.artist ?? "",
                source = "JIZURA native project",
                sourceUri = jizuraProjectPath ?? "",
                importedUtc = DateTime.UtcNow.ToString("o"),
                timing = "JIZURA planned line timing",
                offsetSeconds = 0f
            };
            foreach (JizuraLine line in built.lines)
                document.lines.Add(new LyricCue
                {
                    text = line.text ?? "",
                    translation = line.note ?? "",
                    startTime = line.start,
                    endTime = line.end
                });
            Document = document;
            SelectedLine = Mathf.Clamp(SelectedLine, 0, Mathf.Max(0, document.lines.Count - 1));
            motionDocument = null;
        }

        public void LoadJizuraProject(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("JIZURA path is empty.", nameof(path));
            string raw = File.ReadAllText(path, Encoding.UTF8);
            var parsed = JizuraProject.ParseJson(raw);
            string oldPath = jizuraProjectPath;
            jizuraProjectPath = path;
            try { SetJizuraProject(parsed); }
            catch { jizuraProjectPath = oldPath; throw; }
        }

        public void OpenJizuraStudio()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.ExecuteMenuItem("VJ Practice/JIZURA Studio");
#else
            Message = "JIZURA Studio 需在 Unity Editor 使用。";
#endif
        }

        public void PickJizuraProject()
        {
#if UNITY_EDITOR
            string path = UnityEditor.EditorUtility.OpenFilePanel("匯入 JIZURA 專案", "", "json");
            if (!string.IsNullOrEmpty(path))
                try { LoadJizuraProject(path); }
                catch (Exception ex) { Message = "JIZURA 匯入失敗：" + ex.Message; }
#else
            Message = "請在 Unity Editor 匯入 .jizura.json。";
#endif
        }

        public void SaveJizuraProject(string path)
        {
            if (ActiveJizuraProject == null) throw new InvalidOperationException("No JIZURA project is loaded.");
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("JIZURA path is empty.", nameof(path));
            string temp = path + ".tmp";
            File.WriteAllText(temp, ActiveJizuraProject.ToJson(), Encoding.UTF8);
            if (File.Exists(path)) File.Replace(temp, path, null);
            else File.Move(temp, path);
            jizuraProjectPath = path;
            Message = "已儲存 JIZURA 專案：" + path;
        }

        void SaveJizuraLiveLook()
        {
            var look = new JizuraLiveLook
            {
                version = 1,
                blendStage = JizuraBlendStageVisuals,
                stageBlend = Mathf.Clamp01(JizuraStageBlend),
                templateIndex = TemplateIndex,
                energy = Mathf.Clamp01(Energy), density = Mathf.Clamp01(Density),
                flow = Mathf.Clamp01(Flow), echo = Mathf.Clamp01(Echo), bpm = Bpm
            };
            string path = JizuraLiveLookPath, temp = path + ".tmp";
            File.WriteAllText(temp, JsonUtility.ToJson(look, true), Encoding.UTF8);
            if (File.Exists(path)) File.Replace(temp, path, null);
            else File.Move(temp, path);
        }

        static JizuraLiveLook ReadJizuraLiveLook()
        {
            string path = JizuraLiveLookPath;
            if (!File.Exists(path)) return null;
            if (new FileInfo(path).Length > 65536) throw new FormatException("舞台配置檔案過大。");
            var look = JsonUtility.FromJson<JizuraLiveLook>(File.ReadAllText(path, Encoding.UTF8));
            if (look == null || look.version != 1 || !LyricDocument.Finite(look.stageBlend)
                || !LyricDocument.Finite(look.energy) || !LyricDocument.Finite(look.density)
                || !LyricDocument.Finite(look.flow) || !LyricDocument.Finite(look.echo)
                || !LyricDocument.Finite(look.bpm))
                throw new FormatException("舞台配置版本或數值無效。");
            return look;
        }

        void ApplyJizuraLiveLook(JizuraLiveLook look)
        {
            if (look == null) return;
            SetTemplate(look.templateIndex);
            JizuraBlendStageVisuals = look.blendStage;
            JizuraStageBlend = Mathf.Clamp01(look.stageBlend);
            Energy = Mathf.Clamp01(look.energy); Density = Mathf.Clamp01(look.density);
            Flow = Mathf.Clamp01(look.flow); Echo = Mathf.Clamp01(look.echo);
            Bpm = Mathf.Clamp(look.bpm, 30f, 240f);
        }

        void JizuraFromDocument(LyricDocument document)
        {
            if (document == null || jizuraRenderer == null) return;
            jizuraProjectPath = null;
            SetJizuraProject(JizuraProject.FromLyricDocument(document));
        }

        void UpdateJizura()
        {
            if (!JizuraReady) return;
            jizuraRenderer.Render(ActiveJizuraPlan, Position, new JizuraLiveInput
            {
                backgroundOpacity = JizuraBlendStageVisuals ? 1f - Mathf.Clamp01(JizuraStageBlend) : 1f,
                textOnly = JizuraBlendStageVisuals && JizuraStageBlend >= .995f,
                energy = Energy,
                density = Density,
                flow = Flow,
                echo = Echo,
                bands = Audio != null ? Audio.Bands : Vector4.zero
            });
            JizuraCut active = null;
            foreach (JizuraCut cut in ActiveJizuraPlan.cuts)
            {
                if (Position >= cut.start && Position < cut.end) { active = cut; break; }
            }
            MotionStatus = active == null ? "JIZURA / 等待下一個 cut"
                : "JIZURA / " + (active.line < 0 ? "標題" : "第 " + (active.line + 1) + " 行")
                    + " · " + active.layout + " / " + active.enter + " / " + active.exit;
            if (!string.IsNullOrEmpty(jizuraRenderer.CurrentUnsupported))
                MotionStatus += "\n未移植：" + jizuraRenderer.CurrentUnsupported;
        }

        int JizuraMoodIndex()
        {
            switch (ActiveJizuraProject?.mood)
            {
                case "calm": return 0;
                case "glitch": return 2;
                default: return 1;
            }
        }

        int ActiveJizuraLine()
        {
            if (ActiveJizuraPlan == null) return -1;
            foreach (JizuraCut cut in ActiveJizuraPlan.cuts)
                if (Position >= cut.start && Position < cut.end && cut.line >= 0) return cut.line;
            return -1;
        }

        bool JizuraLineLocked()
        {
            int line = ActiveJizuraLine();
            return line >= 0 && ActiveJizuraProject?.GetOverride(line)?.locked == true;
        }

        bool ApplyJizuraControl(string action, float value)
        {
            if (ActiveJizuraProject == null) return false;
            switch (action)
            {
                case "motionMood":
                    KineticLyrics = true;
                    int mood = Mathf.Clamp((int)value, 0, 2);
                    ActiveJizuraProject.mood = new[] { "calm", "pop", "glitch" }[mood];
                    // JIZURA's style palette is an independent selection. These three
                    // performance shortcuts choose a matching original style explicitly.
                    ActiveJizuraProject.style = new[] { "paper", "magenta", "crimson" }[mood];
                    ReplanJizura(); Message = "JIZURA 風格已套用。"; return true;
                case "motionReroll":
                    KineticLyrics = true;
                    int rerollLine = ActiveJizuraLine();
                    if (rerollLine >= 0)
                    {
                        var ov = ActiveJizuraProject.GetOverride(rerollLine);
                        if (ov == null) { ov = new JizuraLineOverride(); ActiveJizuraProject.overrides[rerollLine] = ov; }
                        if (ov.locked) { Message = "此行已鎖定；先解鎖再重抽。"; return true; }
                        ov.seed = ov.seed == int.MaxValue ? 0 : ov.seed + 1;
                    }
                    else ActiveJizuraProject.seed = ActiveJizuraProject.seed == int.MaxValue ? 0 : ActiveJizuraProject.seed + 1;
                    ReplanJizura(); Message = "JIZURA 配置已重抽。"; return true;
                case "motionLock":
                    KineticLyrics = true;
                    int lockLine = ActiveJizuraLine();
                    if (lockLine < 0) { Message = "目前沒有可鎖定的歌詞。"; return true; }
                    var selected = ActiveJizuraProject.GetOverride(lockLine);
                    if (selected == null) { selected = new JizuraLineOverride(); ActiveJizuraProject.overrides[lockLine] = selected; }
                    selected.locked = !selected.locked;
                    if (selected.locked)
                    {
                        selected.lockedSeed = ActiveJizuraPlan.lines[lockLine].seed;
                        selected.hasLockedSeed = true;
                    }
                    ReplanJizura(); Message = selected.locked ? "已鎖定 JIZURA 本行配置。" : "已解鎖 JIZURA 本行配置。";
                    return true;
                case "motionSave":
                    try
                    {
                        SaveJizuraProject(Path.Combine(Application.persistentDataPath, "CurrentJizura.jizura.json"));
                        SaveJizuraLiveLook();
                        Message = "已儲存 JIZURA 分鏡與舞台混合配置。";
                    }
                    catch (Exception ex) { Message = "JIZURA 儲存失敗：" + ex.Message; }
                    return true;
                case "motionLoad":
                    try
                    {
                        JizuraLiveLook look = ReadJizuraLiveLook();
                        LoadJizuraProject(Path.Combine(Application.persistentDataPath, "CurrentJizura.jizura.json"));
                        ApplyJizuraLiveLook(look);
                        Message = look == null ? "已載入 JIZURA 分鏡；沒有另外儲存的舞台配置。"
                            : "已載入 JIZURA 分鏡與舞台混合配置。";
                    }
                    catch (Exception ex) { Message = "JIZURA 載入失敗：" + ex.Message; }
                    return true;
                default: return false;
            }
        }
    }
}
