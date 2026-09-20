using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using VJPractice.Stage;

public sealed class LyricStudio:EditorWindow {
    LyricDocument draft,observed; VJStage stage;Vector2 scroll,resultsScroll;bool busy;
    string track="Humanoid",artist="ZUTOMAYO",status="",timeText="0",endText="5";int selected;
    LrcResult[] results=Array.Empty<LrcResult>(); AudioClip cachedClip;float[] peaks;
    [Serializable] public class LrcResult {public int id;public string trackName,artistName,albumName,plainLyrics,syncedLyrics;public float duration;public bool instrumental;}
    [Serializable] class ResultList {public LrcResult[] items;}
    LyricDocument Doc=>stage!=null&&stage.Document!=null?stage.Document:draft;
    [MenuItem("VJ Practice/Lyric Studio")]
    public static void Open(){GetWindow<LyricStudio>("Lyric Studio").minSize=new Vector2(740,620);}
    void OnEnable(){EditorApplication.update+=Refresh;if(draft==null){var text=AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/VJ/Stage/Data/OriginalDemo.txt");draft=text?LyricParser.Parse(text.text,"OriginalDemo.lrc","Original practice text"):new LyricDocument();}}
    void OnDisable(){EditorApplication.update-=Refresh;}
    void Refresh(){if(EditorApplication.isPlaying)stage=UnityEngine.Object.FindFirstObjectByType<VJStage>();else stage=null;if(!ReferenceEquals(observed,Doc)){observed=Doc;selected=0;SyncFields();}Repaint();}
    void Apply(LyricDocument d){draft=d;if(stage)stage.SetDocument(d);selected=0;SyncFields();}
    void OnGUI(){
        EditorGUILayout.LabelField("夜航 / LYRIC STUDIO",EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(stage?"Live: edits affect the playing stage. Save session JSON to keep them after leaving Play Mode.":"Press Play in 02_LyricStage for audio transport and waveform. File import and source analysis work here without playback.",MessageType.Info);
        EditorGUILayout.BeginHorizontal();
        if(GUILayout.Button("Import LRC / SRT / VTT / TXT / JSON")){string p=EditorUtility.OpenFilePanel("Import lyrics","","");if(p.Length>0)Try(()=>Apply(LyricParser.Parse(File.ReadAllText(p),p)));}
        if(GUILayout.Button("Save session JSON")){string p=EditorUtility.SaveFilePanel("Save session","","PracticeSession","json");if(p.Length>0)Try(()=>File.WriteAllText(p,JsonUtility.ToJson(Doc,true)));}
        if(GUILayout.Button("Export LRC")){string p=EditorUtility.SaveFilePanel("Export aligned LRC","","PracticeLyrics","lrc");if(p.Length>0)Try(()=>File.WriteAllText(p,Doc.ToLrc()));}
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();track=EditorGUILayout.TextField("Track",track);artist=EditorGUILayout.TextField("Artist",artist);EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();GUI.enabled=!busy;if(GUILayout.Button("Search LRCLIB candidates"))Search();if(GUILayout.Button("Import from Folia on this Mac"))Folia();GUI.enabled=true;EditorGUILayout.EndHorizontal();
        if(busy)EditorGUILayout.LabelField("Fetching…");if(status.Length>0)EditorGUILayout.HelpBox(status,MessageType.None);
        if(results.Length>0){resultsScroll=EditorGUILayout.BeginScrollView(resultsScroll,GUILayout.Height(125));foreach(var r in results){EditorGUILayout.BeginHorizontal();float delta=stage&&stage.Audio.Source.clip?Mathf.Abs(r.duration-stage.Audio.Source.clip.length):0;EditorGUILayout.LabelField($"#{r.id} · {r.trackName} / {r.artistName} · {r.albumName}\n{r.duration:F1}s · {(string.IsNullOrEmpty(r.syncedLyrics)?"plain / untimed":"synced LRC")} {(delta>3?$" · duration Δ {delta:F1}s — check version":"")}",GUILayout.Height(38));if(GUILayout.Button("Use",GUILayout.Width(60)))Use(r);EditorGUILayout.EndHorizontal();}EditorGUILayout.EndScrollView();}
        if(Doc==null)return;
        EditorGUILayout.LabelField($"SOURCE  {Doc.source}  /  {Doc.timing}",EditorStyles.boldLabel);
        EditorGUILayout.SelectableLabel(Doc.sourceUri??"",GUILayout.Height(17));
        EditorGUILayout.LabelField($"SHA256 {Doc.sourceHash?.Substring(0,Mathf.Min(16,Doc.sourceHash.Length))} · {Doc.importedUtc}",EditorStyles.miniLabel);
        Doc.title=EditorGUILayout.TextField("Lyric title",Doc.title);Doc.artist=EditorGUILayout.TextField("Lyric artist",Doc.artist);
        EditorGUILayout.BeginHorizontal();if(GUILayout.Button("−100 ms",GUILayout.Width(85)))Doc.offsetSeconds-=.1f;Doc.offsetSeconds=EditorGUILayout.FloatField("Offset seconds (+ = later)",Doc.offsetSeconds);if(GUILayout.Button("+100 ms",GUILayout.Width(85)))Doc.offsetSeconds+=.1f;if(GUILayout.Button("Reset",GUILayout.Width(60)))Doc.offsetSeconds=0;EditorGUILayout.EndHorizontal();
        var audit=Doc.Audit(stage?stage.Duration:0);if(audit.Count>0)EditorGUILayout.HelpBox(string.Join(" · ",audit),MessageType.Warning);
        if(stage){
            EditorGUILayout.BeginHorizontal();if(GUILayout.Button(stage.Playing?"Pause":"Play"))stage.TogglePlay();if(GUILayout.Button("Restart clock"))stage.Seek(0);if(GUILayout.Button("Import music")){string p=EditorUtility.OpenFilePanel("Import local music","","wav,mp3,ogg");if(p.Length>0)stage.LoadAudio(p);}EditorGUILayout.LabelField(LyricDocument.Format(stage.Position));EditorGUILayout.EndHorizontal();
            float time=EditorGUILayout.Slider(stage.Position,0,stage.Duration);if(Mathf.Abs(time-stage.Position)>.1f)stage.Seek(time);Waveform();
        }
        if(Doc.lines.Count>0){
            selected=Mathf.Clamp(selected,0,Doc.lines.Count-1);var cue=Doc.lines[selected];
            EditorGUILayout.BeginHorizontal();EditorGUILayout.LabelField($"Selected line {selected+1}",GUILayout.Width(120));
            GUI.enabled=stage!=null;if(GUILayout.Button("STAMP NOW → next")){Try(()=>{Doc.Stamp(selected,stage.Position);selected=Mathf.Min(selected+1,Doc.lines.Count-1);SyncFields();});}GUI.enabled=true;
            if(GUILayout.Button("Clear timing from here")){for(int i=selected;i<Doc.lines.Count;i++){Doc.lines[i].startTime=-1;Doc.lines[i].endTime=-1;Doc.lines[i].words=Array.Empty<LyricWord>();}Doc.timing="untimed / manual alignment";SyncFields();}EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();timeText=EditorGUILayout.TextField("Start (seconds)",timeText);endText=EditorGUILayout.TextField("End",endText);if(GUILayout.Button("Apply times",GUILayout.Width(90)))Try(()=>{float a=float.Parse(timeText,System.Globalization.CultureInfo.InvariantCulture),b=float.Parse(endText,System.Globalization.CultureInfo.InvariantCulture);if(!LyricDocument.Finite(a)||!LyricDocument.Finite(b)||a<0||b<=a)throw new Exception("Use finite 0 ≤ start < end times.");float delta=a-cue.startTime;cue.startTime=a;cue.endTime=b;foreach(var w in cue.words){w.startTime+=delta;w.endTime+=delta;}Doc.timing="manually adjusted";});EditorGUILayout.EndHorizontal();
            cue.text=EditorGUILayout.TextField("Text",cue.text);cue.translation=EditorGUILayout.TextField("Translation",cue.translation);cue.romanization=EditorGUILayout.TextField("Romanization",cue.romanization);
        }
        scroll=EditorGUILayout.BeginScrollView(scroll);
        for(int i=0;i<Doc.lines.Count;i++){var c=Doc.lines[i];GUI.backgroundColor=i==selected?new Color(.35f,.8f,.7f):Color.white;EditorGUILayout.BeginHorizontal();if(GUILayout.Button($"{i+1:00}  {(c.startTime<0?"UNTIMED":LyricDocument.Format(c.startTime))}   {c.text}",EditorStyles.miniButtonLeft)){selected=i;SyncFields();if(stage)stage.SelectedLine=i;}GUI.enabled=stage&&c.startTime>=0;if(GUILayout.Button("Seek",EditorStyles.miniButtonRight,GUILayout.Width(48)))stage.Seek(c.startTime+Doc.offsetSeconds);GUI.enabled=true;EditorGUILayout.EndHorizontal();}GUI.backgroundColor=Color.white;
        EditorGUILayout.EndScrollView();
    }
    void SyncFields(){if(Doc==null||Doc.lines.Count==0)return;var c=Doc.lines[Mathf.Clamp(selected,0,Doc.lines.Count-1)];timeText=c.startTime.ToString(System.Globalization.CultureInfo.InvariantCulture);endText=c.endTime.ToString(System.Globalization.CultureInfo.InvariantCulture);}
    void Try(Action action){try{action();status="Done";}catch(Exception ex){status=ex.Message;}}
    async void Search(){busy=true;status="";try{using(var client=Client()){string q=Uri.EscapeDataString(track+" "+artist);var raw=await client.GetStringAsync("https://lrclib.net/api/search?q="+q);results=JsonUtility.FromJson<ResultList>("{\"items\":"+raw+"}").items??Array.Empty<LrcResult>();status=results.Length==0?"No matches. Try Humanoid + ZUTOMAYO, or import your own lyrics.":"Compare title, artist, album and duration before choosing. Search order is not a correctness guarantee.";}}catch(Exception ex){status="Search unavailable: "+ex.Message;}finally{busy=false;Repaint();}}
    async void Folia(){busy=true;try{using(var client=Client()){var raw=await client.GetStringAsync("http://127.0.0.1:32109/v1/lyric");if(raw.Trim()=="null")throw new Exception("Folia has no current lyrics.");Apply(LyricParser.Parse(raw,"http://127.0.0.1:32109/v1/lyric","Folia local API"));status="Imported lyrics. Folia API does not include playback position; align the stage clock manually.";}}catch(Exception ex){status="Folia: "+ex.Message+" Enable Settings → Connections → Lyric API in Folia.";}finally{busy=false;Repaint();}}
    HttpClient Client(){var c=new HttpClient{Timeout=TimeSpan.FromSeconds(15)};c.DefaultRequestHeaders.UserAgent.ParseAdd("VJPractice/0.2");return c;}
    void Use(LrcResult r){Try(()=>{string raw=!string.IsNullOrWhiteSpace(r.syncedLyrics)?r.syncedLyrics:r.plainLyrics;if(string.IsNullOrWhiteSpace(raw))throw new Exception("This candidate has no lyric text.");var d=LyricParser.Parse(raw,"https://lrclib.net/api/get/"+r.id,"LRCLIB #"+r.id);d.title=r.trackName;d.artist=r.artistName;Apply(d);});}
    void Waveform(){
        var clip=stage.Audio.External?null:stage.Audio.Source.clip;
        Rect r=GUILayoutUtility.GetRect(100,68,GUILayout.ExpandWidth(true));EditorGUI.DrawRect(r,new Color(.04f,.06f,.085f));
        if(clip!=cachedClip){cachedClip=clip;peaks=null;if(clip){peaks=new float[400];var chunk=new float[1024*clip.channels];for(int i=0;i<peaks.Length;i++){int offset=Mathf.Clamp((int)(clip.samples*i/(float)peaks.Length),0,Mathf.Max(0,clip.samples-1024));if(!clip.GetData(chunk,offset))break;foreach(float f in chunk)peaks[i]=Mathf.Max(peaks[i],Mathf.Abs(f));}}}
        if(peaks!=null)for(int i=0;i<peaks.Length;i++){float h=Mathf.Max(1,peaks[i]*r.height);EditorGUI.DrawRect(new Rect(r.x+i*r.width/peaks.Length,r.center.y-h*.5f,1,h),new Color(.25f,.6f,.65f));}
        float duration=stage.Duration;foreach(var c in Doc.lines){if(c.startTime<0)continue;float x=r.x+(c.startTime+Doc.offsetSeconds)/duration*r.width;if(x>=r.x&&x<=r.xMax)EditorGUI.DrawRect(new Rect(x,r.y,1,r.height),new Color(1,.6f,.4f,.5f));}
        float playX=r.x+stage.Position/duration*r.width;EditorGUI.DrawRect(new Rect(playX,r.y,2,r.height),Color.white);
        if(Event.current.type==EventType.MouseDown&&r.Contains(Event.current.mousePosition)){stage.Seek((Event.current.mousePosition.x-r.x)/r.width*duration);Event.current.Use();}
    }
}
