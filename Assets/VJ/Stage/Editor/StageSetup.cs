using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.VFX;
using VJPractice.Stage;

public static class StageSetup {
    const string Root="Assets/VJ/Stage";
    [MenuItem("VJ Practice/Open Playable Stage")]
    public static void Open(){EditorSceneManager.OpenScene(Root+"/02_LyricStage.unity");}
    public static void Create(){
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var camera=new GameObject("Stage Camera").AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=5;camera.transform.position=new Vector3(0,0,-10);camera.backgroundColor=Color.black;camera.clearFlags=CameraClearFlags.SolidColor;camera.allowHDR=true;camera.gameObject.AddComponent<AudioListener>();
        var data=camera.GetUniversalAdditionalCameraData();data.renderPostProcessing=true;data.antialiasing=AntialiasingMode.FastApproximateAntialiasing;
        var root=new GameObject("VJ Stage");var audio=root.AddComponent<StageAudio>();audio.demo=AssetDatabase.LoadAssetAtPath<AudioClip>(Root+"/Data/OriginalPulse.wav");
        var stage=root.AddComponent<VJStage>();stage.outputCamera=camera;stage.japaneseFont=AssetDatabase.LoadAssetAtPath<Font>(Root+"/Data/NotoSansCJKjp-Regular.otf");stage.demoLyrics=AssetDatabase.LoadAssetAtPath<TextAsset>(Root+"/Data/OriginalDemo.txt");
        stage.stageMaterial=Mat(Root+"/Data/StageVisual.mat","VJ/StageVisual");stage.surfaceMaterial=Mat(Root+"/Data/Surface.mat","Universal Render Pipeline/Unlit");
        var quad=GameObject.CreatePrimitive(PrimitiveType.Quad);quad.name="Stage Surface";quad.transform.position=new Vector3(0,0,10);quad.transform.localScale=new Vector3(17.78f,10,1);UnityEngine.Object.DestroyImmediate(quad.GetComponent<Collider>());quad.GetComponent<MeshRenderer>().sharedMaterial=stage.surfaceMaterial;
        var fx=new GameObject("Keijiro Audio Particles").AddComponent<VisualEffect>();fx.visualEffectAsset=AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(Root+"/ThirdParty/AudioParticles.vfx");fx.transform.localScale=Vector3.one*2.3f;fx.transform.position=new Vector3(2.5f,0,0);stage.particles=fx;
        var volume=new GameObject("Soft Bloom").AddComponent<Volume>();volume.isGlobal=true;var profile=ScriptableObject.CreateInstance<VolumeProfile>();var bloom=profile.Add<Bloom>(true);bloom.intensity.Override(.4f);bloom.threshold.Override(1.1f);AssetDatabase.CreateAsset(profile,Root+"/Data/Bloom.asset");volume.sharedProfile=profile;
        stage.templates=new[]{Preset("01_Drift","01 / 漂浮 · Drift","Sparse light / lyrical space",0,new Color(.02f,.035f,.06f),new Color(.16f,.66f,.76f),new Color(.96f,.68f,.38f),.32f,.5f,.35f,.65f),Preset("02_Prism","02 / 斜光 · Prism","Neon gates / kinetic type",1,new Color(.025f,.012f,.045f),new Color(.35f,.38f,1),new Color(1,.22f,.46f),.78f,.8f,.65f,.75f),Preset("03_Orbit","03 / 回聲 · Orbit","Filaments / orbiting words",2,new Color(.012f,.025f,.026f),new Color(.28f,.88f,.68f),new Color(.85f,.94f,.58f),.5f,.6f,.45f,.8f)};
        EditorSceneManager.SaveScene(scene,Root+"/02_LyricStage.unity");EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(scene.path,true)};
        PlayerSettings.productName="Nightflight VJ Practice";PlayerSettings.defaultScreenWidth=1440;PlayerSettings.defaultScreenHeight=900;PlayerSettings.runInBackground=true;PlayerSettings.macOS.microphoneUsageDescription="Read the selected audio input for music-reactive visuals.";
        PlayerSettings.insecureHttpOption=InsecureHttpOption.DevelopmentOnly;
        AssetDatabase.SaveAssets();Debug.Log("VJ_STAGE_SETUP_OK");
    }
    static Material Mat(string path,string shader){var m=new Material(Shader.Find(shader));AssetDatabase.CreateAsset(m,path);return m;}
    static StageTemplate Preset(string file,string name,string description,int mode,Color bg,Color primary,Color accent,float energy,float density,float flow,float echo){var p=ScriptableObject.CreateInstance<StageTemplate>();p.title=name;p.subtitle=description;p.visualMode=mode;p.lyricLayout=mode;p.background=bg;p.primary=primary;p.accent=accent;p.energy=energy;p.density=density;p.flow=flow;p.echo=echo;AssetDatabase.CreateAsset(p,Root+"/Data/"+file+".asset");return p;}
    [MenuItem("VJ Practice/Capture Stage Frame")]
    public static void Capture(){var stage=UnityEngine.Object.FindFirstObjectByType<VJStage>();if(stage&&Application.isPlaying)stage.CaptureFrame();}
    [MenuItem("VJ Practice/Verify Lyrics")]
    public static void Test(){
        int n=0;Action<bool,string> check=(ok,name)=>{if(!ok)throw new Exception("FAIL: "+name);n++;};
        var lrc=LyricParser.Parse("[ti:Test]\n[00:01.00]a\n[00:02.00]\n[00:04.00]b","x.lrc");
        check(lrc.ActiveAt(1.5f)==0,"active interval");check(lrc.ActiveAt(.9f)==-1,"before first");check(lrc.lines[lrc.ActiveAt(2.5f)].text=="","instrumental blank");check(lrc.ActiveAt(9)==-1,"last line expires");
        lrc.offsetSeconds=.5f;check(lrc.ActiveAt(1.2f)==-1,"positive delay");check(lrc.ActiveAt(1.7f)==0,"offset applied once");
        var round=LyricParser.Parse(lrc.ToLrc(),"round.lrc");check(round.ActiveAt(1.7f)==0,"LRC export bakes offset");
        var word=LyricParser.Parse("[00:01.00]<00:01.00>夜<00:01.50>空\n[00:03.00]","e.lrc");check(word.lines[0].words.Length==2&&word.lines[0].words[1].endTime==3,"enhanced word timing");
        var plain=LyricParser.Parse("First\nSecond","x.txt");check(plain.ActiveAt(10)==-1,"untimed never falsely active");plain.Stamp(0,2);plain.Stamp(1,4);check(plain.lines[0].endTime==4,"tap alignment updates prior end");
        bool invalid=false;try{plain.Stamp(1,1);}catch(InvalidOperationException){invalid=true;}check(invalid,"reject out of order stamps");
        var srt=LyricParser.Parse("1\n00:00:01,250 --> 00:00:02,500\nOne\nTwo","s.srt");check(srt.lines[0].startTime==1.25f&&srt.lines[0].text=="One\nTwo","SRT multiline");
        var folia=LyricParser.Parse("{\"offset\":250,\"wordByWord\":true,\"title\":\"Test\",\"lines\":[{\"text\":\"a\",\"startTime\":1,\"endTime\":2,\"words\":[{\"text\":\"a\",\"startTime\":1,\"endTime\":2}]}]}","folia.json");check(folia.offsetSeconds==.25f&&folia.timing=="native word timing","Folia units and provenance");
        var copy=LyricParser.Parse(JsonUtility.ToJson(folia),"saved.json");check(copy.lines[0].words.Length==1,"session roundtrip");
        check(new System.Globalization.StringInfo("か\u3099").LengthInTextElements==1,"combining kana");
        Debug.Log("VJ_LYRIC_TESTS_OK "+n);
    }
}
