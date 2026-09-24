// Native Unity editor for the JIZURA project workflow. No browser or WebView is used.
// Ported from 852wa/JIZURA src/12_ui.js (MIT, copyright 2026 hakoniwa).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using VJPractice.Stage;
using VJPractice.Stage.Jizura;

public sealed class JizuraStudio : EditorWindow
{
    static readonly string[] Tabs = { "專案／歌詞", "風格／特效", "逐行編排", "時間軸／預覽" };
    static readonly string[] StyleKeys = {
        "noir", "crimson", "caution", "magenta", "paper", "hud", "mint", "specimen",
        "transit", "blueprint", "rouge", "mono", "sakura", "ocean", "sunset", "forest",
        "vapor", "newsprint", "synth80", "kraft", "candy", "acid", "sumi", "gold"
    };
    static readonly string[] SupportedStyleKeys = {
        "noir", "crimson", "caution", "magenta", "paper", "hud", "mint", "specimen",
        "transit", "blueprint", "rouge", "mono"
    };
    static readonly string[] LayoutKeys = {
        "center", "mixed", "vcols", "marquee", "tile", "scatter", "ring", "wave", "huge",
        "labels", "condensed", "gloss", "type", "diag", "circle", "stack", "pill"
    };
    static readonly string[] SupportedLayoutKeys = LayoutKeys;
    static readonly string[] SupportedEnterKeys = {
        "cut", "slice", "type", "pop", "drop", "stretch", "wipe", "blur", "spin", "flicker", "scramble", "zoom"
    };
    static readonly string[] SupportedExitKeys = {
        "cut", "fall", "drift", "slice", "wipe", "shrink", "blur", "stretch", "scatter", "glitch"
    };
    static readonly MoodRecipe[] Moods = {
        new MoodRecipe("glitch",    "故障",     "noir crimson mint mono hud",               "center huge tile marquee scatter stack", "slice scramble flicker zoom stretch", "glitch slice fall",    .60f,.90f,.75f,1f,.75f,1f,.30f,.60f,.60f,.90f),
        new MoodRecipe("calm",      "冷靜",     "specimen paper hud noir",                  "center vcols type mixed stack",        "type blur wipe",                    "blur drift wipe shrink", .30f,.55f,.05f,.25f,.20f,.50f,.20f,.50f,.25f,.45f),
        new MoodRecipe("pop",       "流行",     "magenta caution transit blueprint rouge",  "mixed scatter huge diag center",       "pop drop spin stretch zoom",         "scatter shrink stretch blur", .70f,1f,.10f,.35f,.30f,.60f,.60f,1f,.50f,.80f),
        new MoodRecipe("graphic",   "圖像",     "blueprint caution rouge mint transit",      "diag marquee tile huge center",        "wipe slice stretch zoom",           "wipe slice stretch", .50f,.80f,.20f,.50f,.40f,.70f,.70f,1f,.50f,.80f),
        new MoodRecipe("editorial", "編排",     "specimen paper noir mono hud",             "vcols mixed stack type center",        "type blur wipe",                    "blur drift wipe",   .40f,.65f,.10f,.30f,.20f,.45f,.40f,.70f,.35f,.60f),
        new MoodRecipe("emotional", "情緒",     "noir paper hud mono crimson",             "huge center vcols stack mixed",        "blur zoom wipe slice",              "drift fall blur",   .55f,.85f,.30f,.60f,.50f,.85f,.30f,.60f,.40f,.70f),
        new MoodRecipe("chaos",     "混合",     "noir crimson caution magenta paper hud mint specimen transit blueprint rouge mono", "center mixed vcols marquee tile scatter huge type diag stack", "cut slice type pop drop stretch wipe blur spin flicker scramble zoom", "cut fall drift slice wipe shrink blur stretch scatter glitch", .50f,1f,.30f,1f,.40f,1f,.40f,1f,.45f,.90f)
    };
    const string AutoSaveName = "JizuraStudio.autosave.jizura.json";
    const string SampleLyrics = "夜明けの色を/覚えてる\nほどけた声が遠くで鳴った\nねえ、まだ間に合うかな\n*透明*なままじゃ終われない!";

    [SerializeField] string sourcePath = "";
    [SerializeField] bool syncToStage = true;
    [SerializeField] int tab, selectedLine;
    [SerializeField] float previewTime;
    [SerializeField] Vector2 scroll;
    [SerializeField] List<string> lookHistory = new List<string>();
    [SerializeField] int lookIndex = -1;
    JizuraProject project;
    JizuraPlan plan;
    VJStage stage;
    VJStage boundStage;
    JizuraProject boundProject;
    JizuraPlan boundPlan;
    double applyAt = -1;
    string status = "";
    MessageType statusType = MessageType.Info;

    sealed class MoodRecipe
    {
        public readonly string id, label;
        public readonly string[] styles, layouts, enters, exits;
        public readonly float motionMin, motionMax, glitchMin, glitchMax, chromaMin, chromaMax;
        public readonly float decorMin, decorMax, densityMin, densityMax;
        public MoodRecipe(string id, string label, string styles, string layouts, string enters, string exits,
            float motionMin, float motionMax, float glitchMin, float glitchMax, float chromaMin, float chromaMax,
            float decorMin, float decorMax, float densityMin, float densityMax)
        {
            this.id = id; this.label = label;
            this.styles = styles.Split(' '); this.layouts = layouts.Split(' ');
            this.enters = enters.Split(' '); this.exits = exits.Split(' ');
            this.motionMin = motionMin; this.motionMax = motionMax;
            this.glitchMin = glitchMin; this.glitchMax = glitchMax;
            this.chromaMin = chromaMin; this.chromaMax = chromaMax;
            this.decorMin = decorMin; this.decorMax = decorMax;
            this.densityMin = densityMin; this.densityMax = densityMax;
        }
    }

    static string AutoSavePath => Path.GetFullPath(Path.Combine(Application.dataPath, "../Library", AutoSaveName));

    [MenuItem("VJ Practice/JIZURA Studio")]
    public static void Open()
    {
        var window = GetWindow<JizuraStudio>("JIZURA Studio");
        window.minSize = new Vector2(780, 620);
        window.Show();
    }

    void OnEnable()
    {
        EditorApplication.update += Tick;
        if (lookHistory == null) lookHistory = new List<string>();
        if (project == null)
        {
            try
            {
                // When opened during Play Mode, start from what the user is actually
                // seeing on stage. Offline edits still resume the autosaved draft.
                var live = EditorApplication.isPlaying ? UnityEngine.Object.FindFirstObjectByType<VJStage>() : null;
                project = live && live.ActiveJizuraProject != null
                    ? JizuraProject.ParseJson(live.ActiveJizuraProject.ToJson())
                    : File.Exists(AutoSavePath)
                        ? JizuraProject.ParseJson(File.ReadAllText(AutoSavePath, Encoding.UTF8))
                        : DemoProject();
            }
            catch (Exception e)
            {
                project = new JizuraProject { lyrics = SampleLyrics };
                Report("無法讀取編輯暫存，已開啟範例：" + e.Message, MessageType.Warning);
            }
            RebuildPlan();
        }
    }

    static JizuraProject DemoProject()
    {
        var demo = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/VJ/Stage/Resources/JizuraDemo.json");
        return demo ? JizuraProject.ParseJson(demo.text) : new JizuraProject { lyrics = SampleLyrics };
    }

    void OnDisable()
    {
        EditorApplication.update -= Tick;
        SaveAuto();
    }

    void Tick()
    {
        VJStage found = EditorApplication.isPlaying ? UnityEngine.Object.FindFirstObjectByType<VJStage>() : null;
        if (found != stage)
        {
            stage = found;
            boundStage = null;
            boundProject = null;
            boundPlan = null;
            Repaint();
        }
        if (stage && stage.Document != null && stage != boundStage)
        {
            boundStage = stage;
            if (syncToStage) ApplyToStage();
        }
        // F7 and the stage's own import replace ActiveJizuraProject. Follow that
        // explicit live load instead of pushing a stale Studio draft over it.
        if (stage && syncToStage && stage == boundStage && boundProject != null
            && stage.ActiveJizuraProject != null && stage.ActiveJizuraProject != boundProject)
            AdoptLiveProject();
        else if (stage && syncToStage && stage == boundStage && boundProject != null
            && stage.ActiveJizuraProject == boundProject && stage.ActiveJizuraPlan != null
            && stage.ActiveJizuraPlan != boundPlan && applyAt < 0)
        {
            // M/N/L can replan the same mutable project without changing its identity.
            boundPlan = stage.ActiveJizuraPlan;
            try
            {
                project = JizuraProject.ParseJson(stage.ActiveJizuraProject.ToJson());
                RebuildPlan();
                SaveAuto();
            }
            catch (Exception e) { Report("同步舞台編排失敗：" + e.Message, MessageType.Error); }
        }
        if (applyAt >= 0 && EditorApplication.timeSinceStartup >= applyAt)
        {
            applyAt = -1;
            RebuildPlan();
            SaveAuto();
            if (syncToStage) ApplyToStage();
        }
        if (stage || applyAt >= 0) Repaint();
    }

    void MarkChanged(bool immediate = false)
    {
        applyAt = EditorApplication.timeSinceStartup + (immediate ? 0 : .22);
        Repaint();
    }

    void RebuildPlan()
    {
        try
        {
            float audioDuration = 0;
            if (stage && stage.Audio != null && !stage.Audio.External && stage.Audio.Source != null && stage.Audio.Source.clip)
                audioDuration = stage.Audio.Source.clip.length;
            plan = JizuraPlanner.Build(project, audioDuration);
            if (plan != null && plan.lines != null)
                selectedLine = Mathf.Clamp(selectedLine, 0, Mathf.Max(0, plan.lines.Count - 1));
        }
        catch (Exception e)
        {
            plan = null;
            Report("規劃失敗：" + e.Message, MessageType.Error);
        }
    }

    void ApplyToStage()
    {
        if (!stage || stage.Document == null || project == null) return;
        try
        {
            stage.SetJizuraProject(project);
            boundProject = stage.ActiveJizuraProject;
            boundPlan = stage.ActiveJizuraPlan;
        }
        catch (Exception e) { Report("套用到舞台失敗：" + e.Message, MessageType.Error); }
    }

    void AdoptLiveProject()
    {
        try
        {
            project = JizuraProject.ParseJson(stage.ActiveJizuraProject.ToJson());
            boundProject = stage.ActiveJizuraProject;
            boundPlan = stage.ActiveJizuraPlan;
            applyAt = -1;
            sourcePath = ""; // The stage's load path is private; do not present the old file as this source.
            lookHistory.Clear(); lookIndex = -1;
            RebuildPlan();
            SaveAuto();
            Report("舞台載入了另一個 JIZURA 專案；編輯器已同步目前畫面。");
        }
        catch (Exception e) { Report("同步舞台載入專案失敗：" + e.Message, MessageType.Error); }
    }

    void SaveAuto()
    {
        if (project == null) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(AutoSavePath));
            File.WriteAllText(AutoSavePath, project.ToJson(), Encoding.UTF8);
        }
        catch (Exception e) { Report("編輯暫存失敗：" + e.Message, MessageType.Warning); }
    }

    void Report(string message, MessageType type = MessageType.Info)
    {
        status = message;
        statusType = type;
        Repaint();
    }

    void OnGUI()
    {
        if (project == null) return;
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("JIZURA / UNITY EDITOR", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("原生編輯器 · 專案格式：.jizura.json", EditorStyles.miniLabel);
        DrawToolbar();
        if (!string.IsNullOrEmpty(status)) EditorGUILayout.HelpBox(status, statusType);
        if (!stage) EditorGUILayout.HelpBox("在 Unity Editor 開啟 02_LyricStage 並按 Play，可即時觀看舞台輸出和拖曳時間軸。專案、歌詞與編排可先離線編輯。", MessageType.Info);
        tab = GUILayout.Toolbar(tab, Tabs, GUILayout.Height(27));
        scroll = EditorGUILayout.BeginScrollView(scroll);
        switch (tab)
        {
            case 0: DrawProject(); break;
            case 1: DrawLook(); break;
            case 2: DrawLines(); break;
            case 3: DrawTimeline(); break;
        }
        EditorGUILayout.EndScrollView();
    }

    void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("開啟 .jizura.json", GUILayout.Height(28))) Import();
        if (GUILayout.Button("儲存 .jizura.json", GUILayout.Height(28))) Export();
        if (GUILayout.Button("載入示範", GUILayout.Width(82), GUILayout.Height(28)))
        {
            project = DemoProject();
            sourcePath = "Assets/VJ/Stage/Resources/JizuraDemo.json";
            selectedLine = 0;
            lookHistory.Clear(); lookIndex = -1;
            MarkChanged(true);
        }
        if (GUILayout.Button("新專案", GUILayout.Width(70), GUILayout.Height(28)))
        {
            if (EditorUtility.DisplayDialog("建立新專案", "目前編輯內容會被替換；請先另存需要保留的版本。", "建立", "取消"))
            {
                project = new JizuraProject { lyrics = SampleLyrics };
                sourcePath = "";
                selectedLine = 0;
                lookHistory.Clear(); lookIndex = -1;
                MarkChanged(true);
            }
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
        bool nextSync = EditorGUILayout.ToggleLeft("即時同步到 Play Mode 舞台", syncToStage, GUILayout.Width(210));
        if (nextSync != syncToStage) { syncToStage = nextSync; if (syncToStage) ApplyToStage(); }
        GUI.enabled = stage;
        if (GUILayout.Button("立即套用到舞台", GUILayout.Width(130)))
        {
            RebuildPlan();
            ApplyToStage();
        }
        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();
        if (!string.IsNullOrEmpty(sourcePath)) EditorGUILayout.SelectableLabel(sourcePath, EditorStyles.miniLabel, GUILayout.Height(19));
    }

    void Import()
    {
        string path = EditorUtility.OpenFilePanel("開啟 JIZURA 專案", "", "json");
        if (string.IsNullOrEmpty(path)) return;
        try
        {
            var loaded = JizuraProject.ParseJson(File.ReadAllText(path, Encoding.UTF8));
            project = loaded;
            sourcePath = path;
            selectedLine = 0;
            lookHistory.Clear(); lookIndex = -1;
            MarkChanged(true);
            Report("已載入原版 JIZURA 專案。", MessageType.Info);
        }
        catch (Exception e) { Report("專案載入失敗：" + e.Message, MessageType.Error); }
    }

    void Export()
    {
        string name = SafeFileName(string.IsNullOrWhiteSpace(project.title) ? "JIZURA" : project.title) + ".jizura.json";
        string path = EditorUtility.SaveFilePanel("儲存 JIZURA 專案", string.IsNullOrEmpty(sourcePath) ? "" : Path.GetDirectoryName(sourcePath), name, "json");
        if (string.IsNullOrEmpty(path)) return;
        try
        {
            File.WriteAllText(path, project.ToJson(), Encoding.UTF8);
            sourcePath = path;
            SaveAuto();
            Report("已儲存 " + path);
        }
        catch (Exception e) { Report("專案儲存失敗：" + e.Message, MessageType.Error); }
    }

    static string SafeFileName(string s)
    {
        foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
        return s.Length > 60 ? s.Substring(0, 60) : s;
    }

    void DrawProject()
    {
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("歌曲與歌詞", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        project.title = EditorGUILayout.TextField("標題", project.title ?? "");
        project.artist = EditorGUILayout.TextField("作者", project.artist ?? "");
        project.lang = PopupValue("歌詞語言／分詞", project.lang, new[] { "auto", "ja", "zh-Hant", "zh-Hans", "ko", "en" });
        EditorGUILayout.HelpBox("一行一句。原版語法可用 [mm:ss.xx]、斜線分段 /、*強調*、句尾 !，以及 | 備註。直接編輯下方來源文字會重新計算分鏡。", MessageType.None);
        EditorGUILayout.LabelField("原始歌詞", EditorStyles.boldLabel);
        project.lyrics = EditorGUILayout.TextArea(project.lyrics ?? "", GUILayout.MinHeight(240), GUILayout.ExpandHeight(true));
        if (EditorGUI.EndChangeCheck()) MarkChanged();
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("時間計算", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        project.timing.bpm = Mathf.Max(0, EditorGUILayout.FloatField("BPM（0 = 自動）", project.timing.bpm));
        project.timing.offset = Mathf.Max(0, EditorGUILayout.FloatField("起始偏移（秒）", project.timing.offset));
        project.timing.lineScale = Mathf.Clamp(EditorGUILayout.FloatField("行長比例", project.timing.lineScale), .3f, 4f);
        project.timing.tail = Mathf.Max(0, EditorGUILayout.FloatField("片尾長度（秒）", project.timing.tail));
        project.timing.snap = EditorGUILayout.Toggle("吸附節拍", project.timing.snap);
        if (EditorGUI.EndChangeCheck()) MarkChanged();
        if (GUILayout.Button("清除逐行手動時間"))
        {
            project.timing.lineTimes.Clear();
            MarkChanged(true);
        }
        if (plan != null) EditorGUILayout.LabelField($"{plan.lines.Count} 行 · {plan.cuts.Count} 個分鏡 · {plan.duration:F2} 秒", EditorStyles.miniLabel);
    }

    void DrawLook()
    {
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("整體風格", EditorStyles.boldLabel);
        DrawVariationControls();
        EditorGUI.BeginChangeCheck();
        project.style = PopupValue("Style", project.style, StyleKeys, null, SupportedStyleKeys);
        project.seed = EditorGUILayout.IntField("Seed", project.seed);
        project.extra = EditorGUILayout.Toggle("追加技法（保留原版設定）", project.extra);
        project.wa = EditorGUILayout.Toggle("和風技法（保留原版設定）", project.wa);
        project.aspect = PopupValue("比例", project.aspect, new[] { "16:9", "9:16", "1:1", "4:3" });
        project.keyBg = PopupValue("原版 Key 背景（保留）", project.keyBg, new[] { "off", "green", "black" });
        project.fps = Mathf.Clamp(EditorGUILayout.IntField("動畫計時 FPS", project.fps), 1, 120);
        project.res = Mathf.Clamp(EditorGUILayout.IntField("原版輸出高度（保留）", project.res), 240, 4320);
        if (EditorGUI.EndChangeCheck()) MarkChanged();
        EditorGUILayout.HelpBox("語言會影響分詞，比例會影響分鏡計算，FPS 影響動畫計時。追加／和風開關、Key 背景和原版輸出高度目前只保留在 JSON；Unity 預覽使用舞台輸出解析度。", MessageType.None);
        if (!SupportedStyleKeys.Contains(project.style))
            EditorGUILayout.HelpBox("此 Style 可以原樣讀寫，但 Unity 本版尚未移植其原版配色；預覽會使用基本配色。", MessageType.Warning);
        if (GUILayout.Button("重新抽整體 Seed")) { project.seed = UnityEngine.Random.Range(1, int.MaxValue); MarkChanged(true); }
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("JIZURA 特效 · Unity 已接入", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Motion／Glitch 目前只影響 jitter／glitchtick 停留技法的抽選；其他強度已接入各自的原生分鏡或渲染。", MessageType.None);
        EditorGUI.BeginChangeCheck();
        project.fx.motion = Slider("Motion（jitter 抽選）", project.fx.motion);
        project.fx.glitch = Slider("Glitch（glitchtick 抽選）", project.fx.glitch);
        project.fx.chroma = Slider("色散 Chroma", project.fx.chroma);
        project.fx.decor = Slider("裝飾 Decor", project.fx.decor);
        project.fx.density = Slider("密度 Density", project.fx.density);
        project.fx.bgSwitch = Slider("換景 BgSwitch", project.fx.bgSwitch);
        project.fx.koma = EditorGUILayout.IntSlider("動畫格數 Koma（0 = 每格）", project.fx.koma, 0, 24);
        project.fx.hud = PopupValue("HUD", project.fx.hud, new[] { "auto", "on", "off" });
        if (EditorGUI.EndChangeCheck()) { project.mood = null; MarkChanged(); }
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("原版 FX 資料 · Unity 預覽未接入", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("下列欄位可編輯並保存回原版 JIZURA 專案；目前不改變 Unity 畫面。", MessageType.None);
        EditorGUI.BeginChangeCheck();
        project.fx.texture = Slider("Texture（保留）", project.fx.texture);
        project.fx.flash = EditorGUILayout.Toggle("Flash（保留）", project.fx.flash);
        if (EditorGUI.EndChangeCheck()) { project.mood = null; MarkChanged(); }
        if (!string.IsNullOrEmpty(project.mood)) EditorGUILayout.LabelField("來源 Mood: " + project.mood, EditorStyles.miniLabel);
        EditorGUILayout.Space(6);
        if (GUILayout.Button("設定 VJPractice 舞台背景與即時效果", GUILayout.Height(25))) tab = 3;
        DrawPaletteAndFonts();
        DrawEnabledSettings();
    }

    void DrawVariationControls()
    {
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("おまかせ／整套重抽", GUILayout.Height(28))) RollAll();
        if (GUILayout.Button("只換風格", GUILayout.Width(90), GUILayout.Height(28)))
        {
            RememberLook();
            var pool = SupportedStyleKeys.Where(x => x != project.style).ToArray();
            if (pool.Length > 0) project.style = pool[UnityEngine.Random.Range(0, pool.Length)];
            project.colors["enabled"] = false;
            CommitLook();
            MarkChanged(true);
        }
        if (GUILayout.Button("只換分鏡", GUILayout.Width(90), GUILayout.Height(28)))
        {
            RememberLook();
            project.seed = UnityEngine.Random.Range(1, int.MaxValue);
            CommitLook();
            MarkChanged(true);
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
        GUI.enabled = lookIndex > 0;
        if (GUILayout.Button("◀ 上個方案", GUILayout.Width(100))) GoLook(-1);
        GUI.enabled = lookIndex >= 0 && lookIndex < lookHistory.Count - 1;
        if (GUILayout.Button("下個方案 ▶", GUILayout.Width(100))) GoLook(1);
        GUI.enabled = true;
        GUILayout.Label(lookHistory.Count > 1 ? $"{lookIndex + 1} / {lookHistory.Count} 個方案" : "方案歷史", EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.HelpBox("整套重抽參照原版情緒的風格與特效範圍，並重抽 Unity 已移植的基礎技法。已鎖定的行會保留。方案歷史只還原外觀，不會回復歌詞、時間或輸出設定。", MessageType.None);
    }

    void RollAll()
    {
        RememberLook();
        MoodRecipe[] options = Moods.Where(x => x.id != project.mood).ToArray();
        MoodRecipe mood = options[UnityEngine.Random.Range(0, options.Length)];
        project.mood = mood.id;
        string[] stylePool = mood.styles.Where(x => SupportedStyleKeys.Contains(x) && x != project.style).ToArray();
        if (stylePool.Length == 0) stylePool = mood.styles.Where(x => SupportedStyleKeys.Contains(x)).ToArray();
        if (stylePool.Length > 0) project.style = stylePool[UnityEngine.Random.Range(0, stylePool.Length)];
        project.seed = UnityEngine.Random.Range(1, int.MaxValue);
        project.fx.motion = Roll(mood.motionMin, mood.motionMax);
        project.fx.glitch = Roll(mood.glitchMin, mood.glitchMax);
        project.fx.chroma = Roll(mood.chromaMin, mood.chromaMax);
        project.fx.decor = Roll(mood.decorMin, mood.decorMax);
        project.fx.density = Roll(mood.densityMin, mood.densityMax);
        project.fx.texture = Roll(.3f, .9f);
        project.fx.bgSwitch = Roll(.1f, .8f);
        project.fx.koma = new[] { 0, 8, 12 }[UnityEngine.Random.Range(0, 3)];
        project.fx.onTwos = project.fx.koma > 0;
        project.fx.flash = UnityEngine.Random.value < .65f;
        project.fx.hud = new[] { "auto", "auto", "on", "off" }[UnityEngine.Random.Range(0, 4)];
        SelectTechniques("layout", SupportedLayoutKeys, mood.layouts, 5);
        SelectTechniques("enter", SupportedEnterKeys, mood.enters, 5);
        SelectTechniques("exit", SupportedExitKeys, mood.exits, 4);
        foreach (int line in project.overrides.Keys.ToArray())
            if (!project.overrides[line].locked) project.overrides.Remove(line);
        CommitLook();
        MarkChanged(true);
        if (stage) stage.Seek(0);
        Report("已產生 " + mood.label + " × " + project.style + " 方案；已鎖定的行維持原編排。");
    }

    static float Roll(float min, float max) => (float)Math.Round(UnityEngine.Random.Range(min, max), 2);

    void SelectTechniques(string group, string[] known, string[] preferred, int minimum)
    {
        if (!project.enabled.TryGetValue(group, out Dictionary<string, bool> flags))
            project.enabled[group] = flags = new Dictionary<string, bool>();
        var chosen = new HashSet<string>();
        foreach (string key in known)
            if (preferred.Contains(key) || UnityEngine.Random.value < .22f) chosen.Add(key);
        while (chosen.Count < Mathf.Min(minimum, known.Length))
            chosen.Add(known[UnityEngine.Random.Range(0, known.Length)]);
        foreach (string key in known) flags[key] = chosen.Contains(key);
        if (group == "enter" || group == "exit") flags["cut"] = true;
    }

    void RememberLook()
    {
        string snapshot = LookSnapshot();
        if (lookIndex >= 0 && lookIndex < lookHistory.Count && lookHistory[lookIndex] == snapshot) return;
        if (lookIndex + 1 < lookHistory.Count) lookHistory.RemoveRange(lookIndex + 1, lookHistory.Count - lookIndex - 1);
        lookHistory.Add(snapshot);
        lookIndex = lookHistory.Count - 1;
    }

    void CommitLook()
    {
        string snapshot = LookSnapshot();
        if (lookIndex >= 0 && lookIndex < lookHistory.Count && lookHistory[lookIndex] == snapshot) return;
        if (lookIndex + 1 < lookHistory.Count) lookHistory.RemoveRange(lookIndex + 1, lookHistory.Count - lookIndex - 1);
        lookHistory.Add(snapshot);
        if (lookHistory.Count > 80) lookHistory.RemoveRange(0, lookHistory.Count - 80);
        lookIndex = lookHistory.Count - 1;
    }

    string LookSnapshot()
    {
        // A history entry contains only the original editor's HKEYS. Lyric, timing and
        // output edits therefore never create history entries or disappear on undo.
        var look = new JizuraProject { style = project.style, mood = project.mood, seed = project.seed, fx = project.fx };
        foreach (var kv in project.enabled) look.enabled[kv.Key] = kv.Value;
        foreach (var kv in project.colors) look.colors[kv.Key] = kv.Value;
        foreach (var kv in project.fonts) look.fonts[kv.Key] = kv.Value;
        foreach (var kv in project.overrides) look.overrides[kv.Key] = kv.Value;
        return look.ToJson();
    }

    void GoLook(int direction)
    {
        RememberLook();
        int target = lookIndex + direction;
        if (target < 0 || target >= lookHistory.Count) return;
        try
        {
            var saved = JizuraProject.ParseJson(lookHistory[target]);
            project.style = saved.style;
            project.mood = saved.mood;
            project.seed = saved.seed;
            project.fx = saved.fx;
            project.enabled.Clear();
            foreach (var kv in saved.enabled) project.enabled[kv.Key] = kv.Value;
            project.colors.Clear();
            foreach (var kv in saved.colors) project.colors[kv.Key] = kv.Value;
            project.fonts.Clear();
            foreach (var kv in saved.fonts) project.fonts[kv.Key] = kv.Value;
            project.overrides.Clear();
            foreach (var kv in saved.overrides) project.overrides[kv.Key] = kv.Value;
            lookIndex = target;
            MarkChanged(true);
        }
        catch (Exception e) { Report("方案歷史無法載入：" + e.Message, MessageType.Error); }
    }

    void DrawPaletteAndFonts()
    {
        EditorGUILayout.LabelField("配色", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("背景、文字和重點色會進入 Unity 原生預覽。Ghost 色與字型欄位會保存在原版 JSON；字型尚未接入 Unity 渲染。", MessageType.None);
        DrawColorToggle("enabled", "自訂背景與文字");
        if (ColorFlag("enabled"))
        {
            DrawColorText("bg", "背景 BG");
            DrawColorText("fg", "主文字 FG");
            DrawColorText("sub", "次文字 SUB");
        }
        DrawColorToggle("accentOn", "自訂重點色");
        if (ColorFlag("accentOn"))
        {
            DrawColorText("accent", "Accent");
            DrawColorText("accent2", "Accent 2");
            DrawColorText("ghostA", "Ghost A（保留）");
            DrawColorText("ghostB", "Ghost B（保留）");
        }
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("原版字型指定（僅保留專案資料）", EditorStyles.boldLabel);
        foreach (string role in new[] { "display", "serif", "body" })
        {
            string current = project.fonts.TryGetValue(role, out object raw) && raw is string ? (string)raw : "";
            string next = EditorGUILayout.TextField(role, current);
            if (next == current) continue;
            if (string.IsNullOrWhiteSpace(next)) project.fonts.Remove(role);
            else project.fonts[role] = next.Trim();
            MarkChanged();
        }
    }

    bool ColorFlag(string key) => project.colors.TryGetValue(key, out object raw) && raw is bool && (bool)raw;

    void DrawColorToggle(string key, string label)
    {
        bool current = ColorFlag(key);
        bool next = EditorGUILayout.Toggle(label, current);
        if (next != current) { project.colors[key] = next; MarkChanged(); }
    }

    void DrawColorText(string key, string label)
    {
        string current = project.colors.TryGetValue(key, out object raw) && raw is string ? (string)raw : "";
        string next = EditorGUILayout.TextField(label, current);
        if (next == current) return;
        if (string.IsNullOrWhiteSpace(next)) project.colors.Remove(key);
        else project.colors[key] = next.Trim();
        MarkChanged();
    }

    static float Slider(string label, float value) => EditorGUILayout.Slider(label, value, 0, 1);

    void DrawEnabledSettings()
    {
        if (project.enabled.Count == 0) return;
        EditorGUILayout.LabelField("來源專案的技法開關", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("layout、enter、exit 會影響 Unity 原生分鏡抽選。其他技法群的開關會保存在 JSON，Unity 預覽尚未使用。", MessageType.None);
        foreach (string group in project.enabled.Keys.OrderBy(x => x).ToArray())
        {
            var values = project.enabled[group];
            bool expanded = SessionState.GetBool("JizuraStudio." + group, false);
            bool nativeGroup = group == "layout" || group == "enter" || group == "exit";
            bool next = EditorGUILayout.Foldout(expanded, group + "（" + values.Count + "）" + (nativeGroup ? "" : " · 僅保留資料"), true);
            if (next != expanded) SessionState.SetBool("JizuraStudio." + group, next);
            if (!next) continue;
            EditorGUI.indentLevel++;
            foreach (string key in values.Keys.OrderBy(x => x).ToArray())
            {
                bool enabled = EditorGUILayout.ToggleLeft(key, values[key]);
                if (enabled != values[key]) { values[key] = enabled; MarkChanged(); }
            }
            EditorGUI.indentLevel--;
        }
    }

    void DrawLines()
    {
        if (plan == null || plan.lines == null)
        {
            EditorGUILayout.HelpBox("先輸入有效歌詞，才能編輯逐行編排。", MessageType.Warning);
            return;
        }
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField($"逐行編排 · {plan.lines.Count} 行 / {plan.cuts.Count} 個分鏡", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("手動時間、指定版型、重抽和鎖定沿用 JIZURA 專案欄位。點 Seek 可在 Play Mode 跳到該句。", MessageType.None);
        foreach (var line in plan.lines)
        {
            int i = line.index;
            JizuraLineOverride ov = project.GetOverride(i);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button((selectedLine == i ? "● " : "○ ") + (i + 1).ToString("00") + "  " + Short(line.text, 46), EditorStyles.label)) selectedLine = i;
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField($"{line.start:F2}–{line.end:F2}s", GUILayout.Width(110));
            GUI.enabled = stage;
            if (GUILayout.Button("Seek", GUILayout.Width(48))) stage.Seek(line.start + .001f);
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();
            if (selectedLine == i)
            {
                EditorGUILayout.BeginHorizontal();
                bool manual = project.timing.lineTimes.TryGetValue(i, out float manualTime);
                float entered = EditorGUILayout.FloatField("開始（秒）", manual ? manualTime : line.start);
                if (Mathf.Abs(entered - (manual ? manualTime : line.start)) > .0001f && LyricDocument.Finite(entered))
                {
                    project.timing.lineTimes[i] = Mathf.Max(0, entered);
                    MarkChanged();
                }
                GUI.enabled = manual;
                if (GUILayout.Button("自動", GUILayout.Width(52))) { project.timing.lineTimes.Remove(i); MarkChanged(true); }
                GUI.enabled = true;
                EditorGUILayout.EndHorizontal();
                string oldLayout = ov == null ? null : ov.layout;
                string layout = PopupValue("指定版型", oldLayout, LayoutKeys, "自動", SupportedLayoutKeys);
                if (layout != oldLayout) { EnsureOverride(i).layout = layout; MarkChanged(true); }
                if (!string.IsNullOrEmpty(layout) && !SupportedLayoutKeys.Contains(layout))
                    EditorGUILayout.HelpBox("此版型會保存在原版專案中，但 Unity 預覽尚未移植，會以 center 顯示。", MessageType.Warning);
                bool single = ov != null && ov.single;
                bool newSingle = EditorGUILayout.Toggle("整句單一分鏡", single);
                if (newSingle != single) { EnsureOverride(i).single = newSingle; MarkChanged(true); }
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("重抽本行"))
                {
                    ov = EnsureOverride(i);
                    ov.seed++;
                    ov.locked = false;
                    ov.hasLockedSeed = false;
                    MarkChanged(true);
                    if (stage) stage.Seek(line.start + .001f);
                }
                bool locked = ov != null && ov.locked;
                if (GUILayout.Button(locked ? "解除鎖定" : "鎖定此行"))
                {
                    ov = EnsureOverride(i);
                    ov.locked = !locked;
                    ov.hasLockedSeed = ov.locked;
                    if (ov.locked) ov.lockedSeed = line.seed;
                    MarkChanged(true);
                }
                EditorGUILayout.EndHorizontal();
                if (ov != null && ov.locked) EditorGUILayout.LabelField("已鎖定 Seed: " + ov.lockedSeed, EditorStyles.miniLabel);
                int count = plan.cuts.Count(c => c.line == i);
                EditorGUILayout.LabelField($"{count} 個分鏡 · 原始 Seed {line.seed}", EditorStyles.miniLabel);
                foreach (var cut in plan.cuts.Where(c => c.line == i))
                {
                    bool partial = !JizuraNativeRenderer.SupportsLayout(cut.layout)
                        || !JizuraNativeRenderer.SupportsEnter(cut.enter)
                        || !JizuraNativeRenderer.SupportsHold(cut.hold)
                        || !JizuraNativeRenderer.SupportsExit(cut.exit)
                        || (!string.IsNullOrEmpty(cut.bg) && cut.bg != "none")
                        || (!string.IsNullOrEmpty(cut.treat) && cut.treat != "none")
                        || (!string.IsNullOrEmpty(cut.cam) && cut.cam != "push" && cut.cam != "none")
                        || (!string.IsNullOrEmpty(cut.trans) && cut.trans != "cut" && cut.trans != "none")
                        || (cut.decor != null && cut.decor.Any(x => !JizuraNativeRenderer.SupportsDecor(x)));
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button($"{cut.start:F2}–{cut.end:F2}s  {cut.layout} · {cut.enter} / {cut.hold} / {cut.exit}" + (partial ? "  ⚠ 部分呈現" : ""), EditorStyles.miniLabel))
                        if (stage) stage.Seek(cut.start + .001f);
                    EditorGUILayout.EndHorizontal();
                }
            }
            EditorGUILayout.EndVertical();
        }
    }

    JizuraLineOverride EnsureOverride(int index)
    {
        if (!project.overrides.TryGetValue(index, out JizuraLineOverride ov))
            project.overrides[index] = ov = new JizuraLineOverride();
        return ov;
    }

    static string Short(string s, int length)
    {
        s = (s ?? "").Replace('\n', ' ');
        return s.Length > length ? s.Substring(0, length - 1) + "…" : s;
    }

    void DrawTimeline()
    {
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("舞台與時間軸", EditorStyles.boldLabel);
        if (stage)
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(stage.Playing ? "暫停" : "播放", GUILayout.Width(80))) stage.TogglePlay();
            if (GUILayout.Button("回到開頭", GUILayout.Width(90))) stage.Seek(0);
            EditorGUILayout.LabelField($"{stage.Position:F2} / {stage.Duration:F2} 秒", GUILayout.Width(160));
            EditorGUILayout.EndHorizontal();
            float time = EditorGUILayout.Slider("時間", stage.Position, 0, Mathf.Max(1, stage.Duration));
            if (Mathf.Abs(time - stage.Position) > .02f) stage.Seek(time);
            DrawStageMixControls();
            if (stage.KineticOutput)
            {
                float width = Mathf.Max(100, position.width - 34);
                Rect view = GUILayoutUtility.GetRect(width, width * 9f / 16f, GUILayout.ExpandWidth(true));
                EditorGUI.DrawRect(view, Color.black);
                GUI.DrawTexture(view, stage.KineticOutput, ScaleMode.ScaleToFit, false);
                if (!string.IsNullOrEmpty(stage.MotionStatus))
                    EditorGUILayout.HelpBox(stage.MotionStatus,
                        stage.MotionStatus.Contains("未移植") ? MessageType.Warning : MessageType.None);
                if (stage.JizuraBlendStageVisuals && stage.JizuraStageBlend >= .995f)
                    EditorGUILayout.HelpBox("文字疊加模式會刻意隱藏原 JIZURA 版面、裝飾與 HUD。切換到混合演出或全景，才可檢查這些來源技巧的 Unity 呈現。", MessageType.None);
            }
            else EditorGUILayout.HelpBox("舞台輸出尚未初始化；請確認已進入 Play Mode 且場景有 VJStage。", MessageType.Warning);
        }
        else EditorGUILayout.HelpBox("進入 Play Mode 後，這裡會顯示 Unity 原生渲染的 RenderTexture。", MessageType.Info);
        DrawCutTimeline();
    }

    void DrawStageMixControls()
    {
        EditorGUILayout.Space(7);
        EditorGUILayout.LabelField("JIZURA × VJPractice 舞台視覺", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("0 = JIZURA 全景（原配色與完整編排）；中間 = 混合演出（JIZURA 編排疊在原舞台）；最右端 = 文字疊加（只留置中歌詞與進退場動畫，隱藏 JIZURA 裝飾和 HUD）。", MessageType.Info);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("文字疊加", GUILayout.Height(25))) SetLiveMode(0);
        if (GUILayout.Button("混合演出", GUILayout.Height(25))) SetLiveMode(1);
        if (GUILayout.Button("JIZURA 全景", GUILayout.Height(25))) SetLiveMode(2);
        EditorGUILayout.EndHorizontal();
        stage.JizuraBlendStageVisuals = EditorGUILayout.Toggle("混合原有舞台視覺", stage.JizuraBlendStageVisuals);
        GUI.enabled = stage.JizuraBlendStageVisuals;
        stage.JizuraStageBlend = EditorGUILayout.Slider("原舞台背景比例", stage.JizuraStageBlend, 0, 1);
        GUI.enabled = true;
        if (!stage.JizuraBlendStageVisuals)
            EditorGUILayout.LabelField("目前僅顯示 JIZURA 原背景與文字。", EditorStyles.miniLabel);
        EditorGUILayout.LabelField("Play Mode 即時調整；舞台 F6／F7 同時存取 CurrentJizura.jizura.json 與 CurrentJizura.live.json。上方『儲存 .jizura.json』只存原版專案，不包含舞台混合配置。", EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.SelectableLabel(Path.Combine(Application.persistentDataPath, "CurrentJizura.live.json"), EditorStyles.miniLabel, GUILayout.Height(18));
        StageTemplate[] templates = stage.templates;
        if (templates != null && templates.Length > 0)
        {
            string[] names = templates.Select((preset, i) => preset && !string.IsNullOrWhiteSpace(preset.title)
                ? (i + 1) + " · " + preset.title : (i + 1) + " · 模板").ToArray();
            int selected = Mathf.Clamp(stage.TemplateIndex, 0, templates.Length - 1);
            int next = EditorGUILayout.Popup("原舞台模板", selected, names);
            if (next != selected) stage.SetTemplate(next);
            StageTemplate active = templates[Mathf.Clamp(stage.TemplateIndex, 0, templates.Length - 1)];
            if (active && !string.IsNullOrWhiteSpace(active.subtitle))
                EditorGUILayout.LabelField(active.subtitle, EditorStyles.miniLabel);
        }
        EditorGUILayout.LabelField("共用舞台參數 · shader／粒子／JIZURA 文字", EditorStyles.boldLabel);
        stage.Energy = EditorGUILayout.Slider("Energy／能量", stage.Energy, 0, 1);
        stage.Density = EditorGUILayout.Slider("Density／密度", stage.Density, 0, 1);
        stage.Flow = EditorGUILayout.Slider("Flow／流動", stage.Flow, 0, 1);
        stage.Echo = EditorGUILayout.Slider("Echo／殘影", stage.Echo, 0, 1);
        stage.Bpm = EditorGUILayout.Slider("Stage 節拍 BPM", stage.Bpm, 30, 240);
        EditorGUILayout.Space(7);
    }

    void SetLiveMode(int mode)
    {
        stage.KineticLyrics = true;
        stage.JizuraBlendStageVisuals = mode != 2;
        stage.JizuraStageBlend = mode == 0 ? 1f : mode == 1 ? .55f : 0f;
    }

    void DrawCutTimeline()
    {
        if (plan == null || plan.cuts == null) return;
        float duration = Mathf.Max(1, plan.duration);
        Rect area = GUILayoutUtility.GetRect(1, 72, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(area, new Color(.075f, .085f, .11f));
        float top = area.y + 10;
        foreach (var cut in plan.cuts)
        {
            float x = area.x + Mathf.Clamp01(cut.start / duration) * area.width;
            float xEnd = area.x + Mathf.Clamp01(cut.end / duration) * area.width;
            if (xEnd <= x) continue;
            var rect = new Rect(x, top + (cut.line % 2 == 0 ? 0 : 20), Mathf.Max(1, xEnd - x - 1), 18);
            var color = cut.line < 0 ? new Color(.65f, .55f, .3f) : Color.HSVToRGB((cut.line * .17f) % 1, .44f, .76f);
            EditorGUI.DrawRect(rect, color);
            if (rect.width > 42) GUI.Label(rect, Short(cut.layout, Mathf.Max(1, (int)(rect.width / 7))));
        }
        float cursor = stage ? stage.Position : previewTime;
        float cursorX = area.x + Mathf.Clamp01(cursor / duration) * area.width;
        EditorGUI.DrawRect(new Rect(cursorX, area.y, 2, area.height), Color.white);
        GUI.Label(new Rect(area.x + 4, area.yMax - 18, 250, 17), $"{cursor:F2} / {duration:F2}s  ·  {plan.cuts.Count} cuts", EditorStyles.whiteMiniLabel);
        if (Event.current.type == EventType.MouseDown && area.Contains(Event.current.mousePosition))
        {
            previewTime = Mathf.Clamp01((Event.current.mousePosition.x - area.x) / area.width) * duration;
            if (stage) stage.Seek(previewTime);
            Event.current.Use();
            Repaint();
        }
    }

    static string PopupValue(string label, string current, string[] choices, string auto = null, string[] supported = null)
    {
        var keys = new List<string>();
        var names = new List<string>();
        if (auto != null) { keys.Add(null); names.Add(auto); }
        foreach (string key in choices)
        {
            keys.Add(key);
            names.Add(supported != null && !supported.Contains(key) ? key + "（Unity 預覽未移植）" : key);
        }
        if (current != null && !keys.Contains(current))
        {
            keys.Add(current);
            names.Add(current + "（來源專案；本版可能未實作）");
        }
        int index = keys.IndexOf(current);
        if (index < 0) index = 0;
        int next = EditorGUILayout.Popup(label, index, names.ToArray());
        return keys[Mathf.Clamp(next, 0, keys.Count - 1)];
    }
}
