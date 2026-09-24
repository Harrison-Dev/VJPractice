using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Networking;
using UnityEngine.VFX;

namespace VJPractice.Stage {
[RequireComponent(typeof(StageAudio))]
public sealed partial class VJStage:MonoBehaviour {
    public StageTemplate[] templates;
    public Material stageMaterial,surfaceMaterial;
    public TextAsset demoLyrics;
    public Font japaneseFont;
    public Camera outputCamera;
    public VisualEffect particles;
    public StageAudio Audio {get;private set;}
    public LyricDocument Document {get;private set;}
    public bool Playing=true, CleanOutput, Frozen, Blackout;
    public float Energy=.45f,Density=.6f,Flow=.5f,Echo=.65f,Bpm=120;
    public float Position {get;private set;}
    public float Duration=>spotifyMode&&spotify&&spotify.Connected?Mathf.Max(1,spotify.Duration):browserMode?(transport&&transport.State.duration>0?transport.State.duration:songDuration):Audio!=null&&!Audio.External&&Audio.Source.clip?Audio.Source.clip.length:Mathf.Max(300,Document?.lines.Where(c=>c.endTime>=0).Select(c=>c.endTime+Document.offsetSeconds).DefaultIfEmpty(0).Max()??0);
    public int TemplateIndex {get;private set;}
    public int SelectedLine;
    public string Message="Original demo — not Humanoid audio or lyrics";
    public RenderTexture Output=>KineticOutput?KineticOutput:history;
    Transform stageSurface; RenderTexture history,next; Material renderMaterial,surface;
    float visualTime,beat;double lastTap=-1;Color primary,accent,background;
    Font jpFont; GUIStyle label,titleStyle,small,button,lyricStyle;
    bool ready,textEditing; Vector2 panelScroll; float smoothEnergy;
    string importPath="",sessionPath="";
    void Start(){
        Audio=GetComponent<StageAudio>();Audio.Init();Document=LyricParser.Parse(demoLyrics.text,"OriginalDemo.lrc","Original practice text");
        stageSurface=GameObject.Find("Stage Surface").transform;renderMaterial=new Material(stageMaterial);surface=new Material(surfaceMaterial);
        GameObject.Find("Stage Surface").GetComponent<MeshRenderer>().sharedMaterial=surface;
        jpFont=japaneseFont;
        sessionPath=Path.Combine(Application.persistentDataPath,"PracticeSession.json");
        SetTemplate(0);primary=templates[0].primary;accent=templates[0].accent;background=templates[0].background;
        Application.targetFrameRate=60;Application.runInBackground=true;ready=true;ConsoleInit();SetTemplate(3);MotionInit();
    }
    public void SetTemplate(int index){if(templates==null||templates.Length==0)return;TemplateIndex=Mathf.Clamp(index,0,templates.Length-1);var p=templates[TemplateIndex];Energy=p.energy;Density=p.density;Flow=p.flow;Echo=p.echo;}
    public void SetDocument(LyricDocument doc){Document=doc;SelectedLine=0;Message="Loaded "+doc.lines.Count+" lyric cues / "+doc.timing;JizuraFromDocument(doc);}
    public void Seek(float seconds){Position=Mathf.Clamp(seconds,0,Duration);if(spotifyMode){spotify.Command("seek",Position);return;}if(browserMode){transport.Send("seek",Position);return;}if(!Audio.External&&Audio.Source.clip)Audio.Source.time=Mathf.Min(Position,Audio.Source.clip.length-.01f);}
    public void TogglePlay(){if(spotifyMode){spotify.Command(Playing?"pause":"play");return;}if(browserMode){transport.Send(Playing?"pause":"play");return;}Playing=!Playing;if(!Audio.External){if(Playing)Audio.Source.UnPause();else Audio.Source.Pause();}}
    public void Stamp(){try{Document.Stamp(SelectedLine,Position);SelectedLine=Mathf.Min(SelectedLine+1,Document.lines.Count-1);Message="Stamped / "+LyricDocument.Format(Position);}catch(Exception ex){Message=ex.Message;}}
    public void SaveSession(){try{File.WriteAllText(sessionPath,JsonUtility.ToJson(Document,true));Message="Saved: "+sessionPath;}catch(Exception ex){Message=ex.Message;}}
    public void LoadSession(){try{SetDocument(LyricParser.Parse(File.ReadAllText(sessionPath),sessionPath));}catch(Exception ex){Message=ex.Message;}}
    public void LoadLyrics(string path){try{SetDocument(LyricParser.Parse(File.ReadAllText(path),path));}catch(Exception ex){Message=ex.Message;}}
    public void CaptureFrame(){StartCoroutine(Capture());}
    IEnumerator Capture(){yield return new WaitForEndOfFrame();var t=new Texture2D(Screen.width,Screen.height,TextureFormat.RGB24,false);t.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0);t.Apply();string directory=(Application.isEditor?Path.GetFullPath(Path.Combine(Application.dataPath,"../Verification")):Path.Combine(Application.persistentDataPath,"Verification"));Directory.CreateDirectory(directory);string path=Path.Combine(directory,"Stage-"+TemplateIndex+".png");File.WriteAllBytes(path,t.EncodeToPNG());Destroy(t);Message="Frame saved: "+path;Debug.Log("VJ_CAPTURE_OK "+path);}
    public void LoadAudio(string path){StartCoroutine(ReadAudio(path));}
    IEnumerator ReadAudio(string path){
        string uri;try{uri=new Uri(Path.GetFullPath(path)).AbsoluteUri;}catch(Exception ex){Message=ex.Message;yield break;}
        AudioType type=Path.GetExtension(path).ToLowerInvariant()==".mp3"?AudioType.MPEG:Path.GetExtension(path).ToLowerInvariant()==".ogg"?AudioType.OGGVORBIS:AudioType.WAV;
        using(var req=UnityWebRequestMultimedia.GetAudioClip(uri,type)){yield return req.SendWebRequest();if(req.result!=UnityWebRequest.Result.Success){Message=req.error;yield break;}var clip=DownloadHandlerAudioClip.GetContent(req);clip.name=Path.GetFileName(path);browserMode=false;spotifyMode=false;spotify.Enabled=false;Audio.Local(clip);Position=0;Playing=true;Message="Loaded local audio / "+clip.name;}
    }
    void Update(){
        if(!ready)return;PerformanceUpdate();
        if(spotifyMode){if(spotify.Connected){Position=spotify.Position;Playing=spotify.Playing;SyncSpotifyTrack();}else{Playing=false;Message=spotify.Error;}}else if(browserMode){if(transport.Connected){Position=transport.State.position;Playing=transport.State.state==1;if(transport.State.error!=0)Message="YouTube 播放錯誤 "+transport.State.error+"，請檢查瀏覽器。";}else Playing=false;}else if(!Audio.External&&Audio.Source.clip)Position=Audio.Source.time;else if(Playing)Position=Mathf.Min(Duration,Position+Time.unscaledDeltaTime);
        PrepareMotionOutput();
        if(!KineticReady)outputCamera.rect=CleanOutput?new Rect(0,0,1,1):new Rect(260f/1440,172f/900,840f/1440,662f/900);
        stageSurface.localScale=new Vector3(10*outputCamera.aspect,10,1);
        if(!Frozen){visualTime+=Time.unscaledDeltaTime*Mathf.Lerp(.1f,1.5f,Flow);beat+=Time.unscaledDeltaTime*Bpm/60;}
        if(particles){particles.pause=Frozen;particles.playRate=Mathf.Lerp(.2f,1.5f,Flow);particles.gameObject.SetActive(!Blackout);if(particles.HasFloat("Amplitude"))particles.SetFloat("Amplitude",.08f+Audio.Bands.x*Energy*.6f);if(particles.HasFloat("Radius"))particles.SetFloat("Radius",1.3f+Density);if(particles.HasVector4("Base Color"))particles.SetVector4("Base Color",primary*1.2f);if(particles.HasVector4("Highlight"))particles.SetVector4("Highlight",accent*(1.5f+Audio.Bands.z*2));}
        RenderVisual();UpdateMotion();
    }
    void HandleKey(KeyCode key){
        if(HandleMotionKey(key)){Event.current.Use();return;}
        switch(key){
            case KeyCode.Alpha1:SetTemplate(0);break;case KeyCode.Alpha2:SetTemplate(1);break;case KeyCode.Alpha3:SetTemplate(2);break;case KeyCode.Alpha4:SetTemplate(3);break;case KeyCode.Alpha5:SetTemplate(4);break;case KeyCode.Alpha6:SetTemplate(5);break;case KeyCode.UpArrow:parameter=(parameter+3)%4;break;case KeyCode.DownArrow:parameter=(parameter+1)%4;break;case KeyCode.LeftArrow:Nudge(Event.current.shift?-.1f:-.02f);break;case KeyCode.RightArrow:Nudge(Event.current.shift?.1f:.02f);break;
            case KeyCode.H:CleanOutput=!CleanOutput;break;case KeyCode.Escape:CleanOutput=false;textEditing=false;GUI.FocusControl(null);break;
            case KeyCode.B:Blackout=!Blackout;break;case KeyCode.F:Frozen=!Frozen;break;
            case KeyCode.Space:TogglePlay();break;case KeyCode.Home:Seek(0);break;case KeyCode.Return:Stamp();break;
            case KeyCode.LeftBracket:Document.offsetSeconds-=.1f;break;case KeyCode.RightBracket:Document.offsetSeconds+=.1f;break;
            case KeyCode.P:CaptureFrame();break;case KeyCode.F9:WriteDiagnostics();break;
            case KeyCode.V:double now=Time.unscaledTimeAsDouble,interval=now-lastTap;if(lastTap>=0&&interval>=.25&&interval<=1.5)Bpm=(float)(60/interval);lastTap=now;beat=0;break;
            default:return;
        }
        Event.current.Use();
    }
    void RenderVisual(){
        int width=Mathf.Clamp(outputCamera.pixelWidth,320,1920),height=Mathf.Clamp(outputCamera.pixelHeight,180,1080);
        if(history==null||history.width!=width||history.height!=height){Release();history=Make(width,height);next=Make(width,height);surface.mainTexture=history;}
        if(Frozen)return;
        float lerp=1-Mathf.Exp(-Time.unscaledDeltaTime*2);var preset=templates[TemplateIndex];
        primary=Color.Lerp(primary,preset.primary,lerp);accent=Color.Lerp(accent,preset.accent,lerp);background=Color.Lerp(background,preset.background,lerp);smoothEnergy=Mathf.Lerp(smoothEnergy,Energy,lerp);
        renderMaterial.SetFloat("_Aspect",width/(float)height);renderMaterial.SetFloat("_Mode",preset.visualMode);renderMaterial.SetFloat("_Echo",Echo);
        renderMaterial.SetVector("_Control",new Vector4(smoothEnergy,Density,Flow,0));renderMaterial.SetVector("_Bands",Audio.Bands);
        renderMaterial.SetVector("_Clock",new Vector4(visualTime,beat,0,0));renderMaterial.SetColor("_Primary",primary);renderMaterial.SetColor("_Accent",accent);renderMaterial.SetColor("_Background",background);
        Graphics.Blit(history,next,renderMaterial);var old=history;history=next;next=old;surface.mainTexture=history;
    }
    RenderTexture Make(int w,int h){var rt=new RenderTexture(w,h,0,RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.Linear);rt.Create();var previous=RenderTexture.active;RenderTexture.active=rt;GL.Clear(true,true,Color.black);RenderTexture.active=previous;return rt;}
    void Release(){if(history){history.Release();Destroy(history);}if(next){next.Release();Destroy(next);}}
    void OnDestroy(){MotionDispose();Release();if(renderMaterial)Destroy(renderMaterial);if(surface)Destroy(surface);if(flatTex)Destroy(flatTex);if(hoverTex)Destroy(hoverTex);}
    void Styles(){if(label!=null)return;label=new GUIStyle(GUI.skin.label){font=jpFont,fontSize=15,wordWrap=true};label.normal.textColor=new Color(.85f,.87f,.91f);small=new GUIStyle(label){fontSize=12};titleStyle=new GUIStyle(label){fontSize=25,fontStyle=FontStyle.Bold};button=new GUIStyle(GUI.skin.button){font=jpFont,fontSize=14,fixedHeight=30};lyricStyle=new GUIStyle(label){alignment=TextAnchor.MiddleCenter,fontSize=48,richText=false};}
    void OnGUI(){
        if(!ready)return;Styles();if(Event.current.type==EventType.KeyDown&&!Event.current.command&&!Event.current.control&&(!textEditing||Event.current.keyCode==KeyCode.Escape))HandleKey(Event.current.keyCode);GUI.matrix=Matrix4x4.TRS(Vector3.zero,Quaternion.identity,new Vector3(Screen.width/1440f,Screen.height/900f,1));
        Rect stage=CleanOutput?new Rect(0,0,1440,900):new Rect(260,66,840,662);
        if(!DrawMotionOutput(stage)){if(Blackout){Fill(stage,Color.black);}else DrawLyrics(stage);}
        if(CleanOutput){textEditing=false;if(Event.current.mousePosition.x<150&&Event.current.mousePosition.y<45){if(GUI.Button(new Rect(8,8,138,30),"Controls / Esc",button))CleanOutput=false;}return;}
        DrawConsole();
    }
    static void Fill(Rect r,Color c){var old=GUI.color;GUI.color=c.linear;GUI.DrawTexture(r,Texture2D.whiteTexture);GUI.color=old;}
    void PickFile(){
#if UNITY_EDITOR
        string p=UnityEditor.EditorUtility.OpenFilePanel("Import audio or lyrics","","");if(p.Length>0)ImportPath(p);
#else
        Message="Paste a local file path, then Load path.";
#endif
    }
    void ImportPath(string p){string ext=Path.GetExtension(p).ToLowerInvariant();if(p.EndsWith(".jizura.json",StringComparison.OrdinalIgnoreCase)){try{LoadJizuraProject(p);}catch(Exception ex){Message="JIZURA 匯入失敗："+ex.Message;}return;}if(ext==".wav"||ext==".mp3"||ext==".ogg")LoadAudio(p);else LoadLyrics(p);}
    void WriteDiagnostics(){string directory=(Application.isEditor?Path.GetFullPath(Path.Combine(Application.dataPath,"../Verification")):Path.Combine(Application.persistentDataPath,"Verification"));Directory.CreateDirectory(directory);File.WriteAllText(Path.Combine(directory,"LiveDiagnostics.txt"),$"time={DateTime.UtcNow:o}\nsource={Audio.Status}\nexternal={Audio.External}\nspotifyConnected={spotify.Connected}\nspotifyPlaying={spotify.Playing}\nposition={Position:F3}\nduration={Duration:F3}\nbands={Audio.Bands}\nparticles={particles.aliveParticleCount}\nlyricSource={Document.source}\ncues={Document.lines.Count}\nactiveCue={Document.ActiveAt(Position)}\ntemplate={TemplateIndex}\njizuraManualLook={JizuraManualLook}\nfps={Fps:F1}\np95ms={P95:F2}\n");Message="診斷已儲存。";}
    // Minimal fail-safe caption when the native JIZURA compositor is unavailable.
    // The selectable legacy subtitle techniques have been removed from the live deck.
    void DrawLyrics(Rect area){
        int index=Document==null?-1:Document.ActiveAt(Position);
        if(index<0)return;
        var cue=Document.lines[index];
        if(string.IsNullOrWhiteSpace(cue.text))return;
        float now=Position-Document.offsetSeconds;
        float alpha=Mathf.Clamp01((now-cue.startTime)*3)*Mathf.Clamp01((cue.endTime-now)*3);
        GUI.BeginGroup(area);
        Text(new Rect(50,area.height*.38f,area.width-100,area.height*.24f),cue.text,
            Mathf.Clamp(64-cue.text.Length,24,50),new Color(.96f,.96f,.98f,alpha));
        GUI.EndGroup();
    }
    void Text(Rect r,string s,int size,Color c){lyricStyle.fontSize=size;lyricStyle.normal.textColor=c;GUI.Label(r,s??"",lyricStyle);}

}
}
