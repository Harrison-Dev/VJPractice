using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
namespace VJPractice.Stage {
public sealed partial class VJStage {
 [Serializable] public class SearchResult {public int id;public string trackName,artistName,albumName,syncedLyrics,plainLyrics;public float duration;}
 [Serializable] class SearchResponse {public SearchResult[] items;}
 SearchResult[] results=Array.Empty<SearchResult>();string lastSpotifyTrack="";string query="Humanoid ZUTOMAYO";bool searching;int tab=3,lastLyric=-1;float manualScrollUntil;Vector2 libraryScroll;
 SpotifyTransport spotify;bool spotifyMode;BrowserTransport transport;bool browserMode;float songDuration=260;
 readonly Color ink=new Color(.95f,.94f,.95f),muted=new Color(.57f,.57f,.63f),cyan=new Color(1f,.19f,.20f),amber=new Color(1f,.55f,.32f),panel=new Color(.035f,.035f,.045f),line=new Color(.18f,.17f,.19f);
 GUIStyle field,flat,mini;Texture2D flatTex,hoverTex;
 AudioClip waveformClip;readonly float[] waveformPeaks=new float[256];float[] waveformSamples;int waveformScan;bool waveformAvailable;
 void ConsoleInit(){PerformanceInit();spotify=gameObject.AddComponent<SpotifyTransport>();transport=gameObject.AddComponent<BrowserTransport>();Audio.Scan();Playing=false;Audio.Source.Pause();Message="先在 Spotify 播放 Humanoid，再按連接；或到音源選原創示範。";}
 void ConsoleStyles(){if(flat!=null)return;flatTex=Solid(new Color(.09f,.085f,.095f));hoverTex=Solid(new Color(.19f,.075f,.08f));flat=new GUIStyle{font=jpFont,fontSize=14,alignment=TextAnchor.MiddleCenter,padding=new RectOffset(9,9,4,4)};flat.normal.background=flatTex;flat.normal.textColor=ink;flat.hover.background=hoverTex;flat.hover.textColor=ink;flat.active.background=hoverTex;flat.active.textColor=ink;flat.focused=flat.hover;field=new GUIStyle(flat){alignment=TextAnchor.MiddleLeft,fontSize=15};mini=new GUIStyle(label){fontSize=11};button=flat;}
 Texture2D Solid(Color c){var t=new Texture2D(1,1);t.SetPixel(0,0,c.linear);t.Apply();return t;}
 void Caption(float x,float y,string value,int size=12,Color? color=null,float width=290){size=Mathf.Clamp(size,1,64);var s=cachedLabels[size]??(cachedLabels[size]=new GUIStyle(label){fontSize=size,wordWrap=true});s.normal.textColor=color??ink;GUI.Label(new Rect(x,y,width,78),value,s);}
 bool Action(float x,float y,float w,string text,bool selected=false){Fill(new Rect(x-1,y-1,w+2,36),selected?cyan:line);return GUI.Button(new Rect(x,y,w,34),text,flat);}
 bool DeckPad(float x,float y,float w,string number,string name,bool selected){Fill(new Rect(x-1,y-1,w+2,65),selected?cyan:line);Fill(new Rect(x,y,w,63),selected?new Color(.18f,.035f,.045f):new Color(.055f,.055f,.065f));Caption(x+10,y+5,number,11,selected?cyan:muted,w-18);Caption(x+10,y+28,name,16,ink,w-18);return GUI.Button(new Rect(x,y,w,63),GUIContent.none,GUIStyle.none);}
 void ScanWaveform(){
  AudioClip clip=Audio!=null&&!Audio.External&&Audio.Source?Audio.Source.clip:null;
  if(clip!=waveformClip){waveformClip=clip;waveformScan=0;waveformAvailable=clip&&clip.samples>=512;Array.Clear(waveformPeaks,0,waveformPeaks.Length);}
  if(!waveformAvailable||waveformScan>=waveformPeaks.Length||clip.loadState!=AudioDataLoadState.Loaded)return;
  int channels=Mathf.Max(1,clip.channels),length=512*channels;
  if(waveformSamples==null||waveformSamples.Length!=length)waveformSamples=new float[length];
  for(int batch=0;batch<8&&waveformScan<waveformPeaks.Length;batch++,waveformScan++){
   int offset=Mathf.Clamp((int)((long)clip.samples*waveformScan/waveformPeaks.Length),0,clip.samples-512);
   if(!clip.GetData(waveformSamples,offset)){waveformAvailable=false;return;}
   float peak=0;for(int j=0;j<length;j++)peak=Mathf.Max(peak,Mathf.Abs(waveformSamples[j]));
   waveformPeaks[waveformScan]=Mathf.Sqrt(Mathf.Clamp01(peak));
  }
 }
 float Fader(float y,string name,float value){Caption(22,y,name,11,parameter==Mathf.RoundToInt((y-350)/56)?cyan:muted,160);Caption(184,y,value.ToString("F2"),12,amber,55);Rect r=new Rect(22,y+29,216,18);Fill(new Rect(r.x,r.y+7,r.width,3),line);Fill(new Rect(r.x,r.y+7,r.width*value,3),cyan);Fill(new Rect(r.x+r.width*value-3,r.y+2,6,13),ink);var e=Event.current;int id=GUIUtility.GetControlID(FocusType.Passive,r);if(e.type==EventType.MouseDown&&r.Contains(e.mousePosition)){GUIUtility.hotControl=id;e.Use();}if(GUIUtility.hotControl==id&&(e.type==EventType.MouseDrag||e.type==EventType.MouseDown)){value=Mathf.Clamp01((e.mousePosition.x-r.x)/r.width);e.Use();}if(e.rawType==EventType.MouseUp&&GUIUtility.hotControl==id)GUIUtility.hotControl=0;return value;}
 void DrawConsole(){
  ConsoleStyles();
  Fill(new Rect(0,0,1440,66),new Color(.025f,.024f,.03f));
  Fill(new Rect(0,66,260,834),panel);Fill(new Rect(1100,66,340,834),panel);
  Fill(new Rect(260,728,840,172),new Color(.032f,.03f,.038f));
  Fill(new Rect(260,66,1,834),line);Fill(new Rect(1099,66,1,834),line);
  Fill(new Rect(0,65,1440,1),line);Fill(new Rect(0,62,1440,2),new Color(.32f,.055f,.07f));
  Caption(22,12,"NIGHTFLIGHT",26,ink,250);
  Fill(new Rect(282,24,16,2),cyan);Caption(310,22,"LIVE VJ / LASER DECK",12,amber,340);
  Fill(new Rect(786,25,7,7),Playing?cyan:muted);
  Caption(805,21,spotifyMode?(spotify.Connected?"SPOTIFY SYNC":"SPOTIFY WAITING"):browserMode?(transport.Connected?"PLAYER SYNC":"PLAYER WAITING"):Audio.External?"EXTERNAL AUDIO":"LOCAL AUDIO",12,ink,300);
  if(Action(1210,16,208,"OUTPUT ONLY   H"))CleanOutput=true;

  Caption(22,89,"STAGE FX  /  1–6",12,ink,216);Fill(new Rect(22,113,216,1),line);
  string[] names={"漂浮","斜光","回聲","隧道","矩陣","流體"};
  for(int i=0;i<6;i++)if(DeckPad(22+(i%2)*113,126+(i/2)*71,103,(i+1).ToString("00"),names[i],TemplateIndex==i))SetTemplate(i);
  Caption(22,347,"LIVE MODULATION",11,muted,216);
  Energy=Fader(370,"ENERGY / 強度",Energy);Density=Fader(426,"DENSITY / 密度",Density);
  Flow=Fader(482,"FLOW / 流速",Flow);Echo=Fader(538,"ECHO / 殘影",Echo);
  if(Action(22,599,103,"FREEZE  F",Frozen))Frozen=!Frozen;
  if(Action(135,599,103,"BLACKOUT  B",Blackout))Blackout=!Blackout;
  Caption(22,646,Audio.External?"SIGNAL  /  EXTERNAL":"SIGNAL  /  LOCAL",11,muted,216);
  for(int i=0;i<3;i++){float v=Mathf.Clamp01(Audio.Bands[i]);Caption(22,674+i*34,new[]{"LOW","MID","HIGH"}[i],11,muted,60);Fill(new Rect(78,683+i*34,126,5),line);Fill(new Rect(78,683+i*34,126*v,5),i==2?amber:cyan);Caption(211,673+i*34,v.ToString("F1"),11,ink,40);}
  Caption(22,806,$"{Bpm:F0} BPM  /  V TAP",20,cyan,216);
  Caption(22,839,$"{Fps:F0} FPS   ·   P95 {P95:F1} ms",11,muted,216);

  Fill(new Rect(272,78,816,1),line);Fill(new Rect(272,681,816,1),line);
  Caption(283,85,"CAM 1  /  MAIN OUTPUT",11,muted,300);
  Caption(790,85,templates[TemplateIndex].title,11,amber,285);
  Caption(283,695,spotifyMode?spotify.Title+" / "+spotify.Artist:browserMode?"HUMANOID / ZUTOMAYO":Audio.External?"EXTERNAL AUDIO":Audio.Source.clip==Audio.demo?"ORIGINAL DEMO / 原創練習音軌":Audio.Source.clip?.name??"NO AUDIO",12,ink,780);
  DrawLibrary();DrawTransport();
  textEditing=GUI.GetNameOfFocusedControl()=="Search"||GUI.GetNameOfFocusedControl()=="ImportPath";
 }
 void DrawLibrary(){Caption(1120,91,"SOURCE  /  TRACK SYNC",12,ink);if(Action(1120,122,298,"SPOTIFY  /  連接與同步"))OpenSpotify();string[] tabs={"搜尋","歌詞","音源","演出"};for(int i=0;i<4;i++)if(Action(1120+i*76,176,70,tabs[i],tab==i)){tab=i;libraryScroll=Vector2.zero;}
 if(tab==0){GUI.SetNextControlName("Search");query=GUI.TextField(new Rect(1120,228,298,36),query,field);GUI.enabled=!searching;if(Action(1120,278,298,searching?"搜尋中…":"搜尋完整歌詞 / LRCLIB"))StartCoroutine(SearchLyrics());GUI.enabled=true;Caption(1120,325,"依歌曲版本選擇；含逐行時間戳可直接同步。",12,muted,298);libraryScroll=GUI.BeginScrollView(new Rect(1118,379,310,395),libraryScroll,new Rect(0,0,284,results.Length*122));for(int i=0;i<results.Length;i++){var r=results[i];float y=i*122;Caption(8,y,r.trackName,15,ink,273);Caption(8,y+35,$"{r.artistName} · {r.duration:F0}s\n{r.albumName}",11,muted,273);if(Action(8,y+79,270,string.IsNullOrEmpty(r.syncedLyrics)?"載入完整歌詞 · 尚未對時":"載入完整歌詞 · 已有時間戳")){UseResult(r);tab=1;libraryScroll=Vector2.zero;}}GUI.EndScrollView();}
 else if(tab==1){Caption(1120,228,Document.title.Length>30?Document.title.Substring(0,30)+"…":Document.title,17,ink,298);int count=Document.lines.Count(c=>!string.IsNullOrWhiteSpace(c.text));Caption(1120,278,$"{count} 行 / {Document.timing}\n{Document.source}",11,muted,298);libraryScroll=GUI.BeginScrollView(new Rect(1118,340,310,396),libraryScroll,new Rect(0,0,284,Document.lines.Count*65));int active=Document.ActiveAt(Position);if(Event.current.type==EventType.ScrollWheel&&Event.current.mousePosition.x>1100)manualScrollUntil=Time.unscaledTime+6;if(active!=lastLyric&&Time.unscaledTime>manualScrollUntil){libraryScroll.y=Mathf.Max(0,active*65-130);lastLyric=active;}for(int i=0;i<Document.lines.Count;i++){var c=Document.lines[i];float y=i*65;if(i==active)Fill(new Rect(0,y,284,63),new Color(.06f,.16f,.20f));Caption(8,y+4,c.startTime<0?"未對時":LyricDocument.Format(c.startTime),10,i==active?cyan:muted,65);Caption(72,y+3,c.text,13,ink,204);if(GUI.Button(new Rect(0,y,284,63),GUIContent.none,GUIStyle.none)){SelectedLine=i;if(c.startTime>=0)Seek(c.startTime+Document.offsetSeconds);}}GUI.EndScrollView();if(Action(1120,745,142,"儲存對齊"))SaveSession();if(Action(1272,745,146,"載入儲存"))LoadSession();}
 else if(tab==3){DrawPerformancePage();}
 else {Caption(1120,228,Audio.Status,13,cyan,298);if(Action(1120,293,298,"連接 BlackHole / 重新偵測")){Audio.Scan();ConnectBlackHole();}if(Action(1120,339,298,"外部音源 · 手動計時")){browserMode=false;spotifyMode=false;spotify.Enabled=false;Playing=false;Position=0;Audio.Scan();}float y=388;foreach(var p in Audio.Devices.Select((d,i)=>(d,i))){if(Action(1120,y,298,p.d.Name)){browserMode=false;spotifyMode=false;spotify.Enabled=false;Audio.Select(p.i);Playing=false;Position=0;}y+=42;}if(Action(1120,y+12,142,"原創示範")){browserMode=false;spotifyMode=false;spotify.Enabled=false;Audio.Local(Audio.demo);SetDocument(LyricParser.Parse(demoLyrics.text,"OriginalDemo.lrc","Original practice text"));Playing=true;Seek(0);}if(Action(1272,y+12,146,"匯入音樂 / 歌詞"))PickFile();Caption(1120,y+60,"本機檔案路徑",11,muted,298);GUI.SetNextControlName("ImportPath");importPath=GUI.TextField(new Rect(1120,y+88,298,32),importPath,field);if(Action(1120,y+130,298,"載入路徑"))ImportPath(importPath);}
 Caption(1120,817,Message,11,amber,298);
 }
 void DrawTransport(){
  if(Event.current.type==EventType.Repaint)ScanWaveform();
  Fill(new Rect(260,728,840,1),line);
  Caption(283,747,$"{Bpm:F0}",32,cyan,110);Caption(371,764,"BPM",11,muted,80);
  Caption(506,756,LyricDocument.Format(Position),24,ink,180);
  Caption(700,765,"/ "+LyricDocument.Format(Duration),13,muted,150);
  if(Action(870,750,101,Playing?"PAUSE":"PLAY"))TogglePlay();
  if(Action(979,750,98,"HOME"))Seek(0);
  Rect track=new Rect(283,790,793,45);
  Fill(new Rect(track.x,track.y+36,track.width,2),line);
  for(int i=0;i<17;i++)Fill(new Rect(track.x+track.width*i/16f,track.y+39,1,5),line);
  float progress=Mathf.Clamp01(Position/Duration);
  if(waveformAvailable){float step=track.width/waveformPeaks.Length;for(int i=0;i<waveformScan;i++){float h=2+waveformPeaks[i]*29;Fill(new Rect(track.x+i*step,track.y+18-h*.5f,Mathf.Max(1,step-1),h),i/(float)waveformPeaks.Length<progress?cyan:muted);}}
  Fill(new Rect(track.x,track.y+36,track.width*progress,2),cyan);
  for(int i=0;i<Document.lines.Count;i++){var c=Document.lines[i];if(c.startTime<0)continue;float x=track.x+track.width*Mathf.Clamp01((c.startTime+Document.offsetSeconds)/Duration);Fill(new Rect(x,track.y+39,2,7),i==Document.ActiveAt(Position)?amber:muted);}
  Fill(new Rect(track.x+track.width*progress-1,track.y-3,2,43),ink);
  var e=Event.current;if((e.type==EventType.MouseDown||e.type==EventType.MouseDrag)&&track.Contains(e.mousePosition)){Seek((e.mousePosition.x-track.x)/track.width*Duration);e.Use();}
  if(Action(283,846,114,"− 0.1 SEC"))Document.offsetSeconds-=.1f;
  Caption(411,855,$"OFFSET {Document.offsetSeconds:+0.00;-0.00;0.00}s",12,amber,200);
  if(Action(625,846,114,"+ 0.1 SEC"))Document.offsetSeconds+=.1f;
  if(Action(747,846,175,"NEXT CUE / Enter"))Stamp();
  Caption(937,855,"[ / ]  調整",11,muted,139);
 }
 void ConnectBlackHole(){int i=Array.FindIndex(Audio.Devices,d=>d.Name.IndexOf("BlackHole",StringComparison.OrdinalIgnoreCase)>=0);if(i>=0){Audio.Select(i);Message="已選 BlackHole；系統輸出需為 VJ Monitor + BlackHole。";}else Message="找不到 BlackHole，請確認已安裝並重開 Unity。";}
 public void OpenSpotify(){browserMode=false;spotifyMode=true;Audio.Source.Stop();Audio.Scan();ConnectBlackHole();spotify.Enabled=true;Message="macOS 若詢問，請允許 Unity 控制 Spotify。";lastSpotifyTrack="";}
 public void OpenHumanoid(){spotifyMode=false;spotify.Enabled=false;browserMode=true;Playing=false;Position=0;Audio.Source.Stop();Audio.Scan();ConnectBlackHole();if(transport.Launch()){Application.OpenURL(transport.Url);Message="請在瀏覽器按播放。聲音與播放位置將分別接入。";}else Message="播放器開啟失敗："+transport.Error;if(!searching)StartCoroutine(SearchLyrics(true));}
 public IEnumerator SearchLyrics(bool autoLoad=false){searching=true;Message="正在搜尋完整來源…";string q=query;float expected=spotifyMode&&spotify.Connected?spotify.Duration:260;using(var req=UnityWebRequest.Get("https://lrclib.net/api/search?q="+UnityWebRequest.EscapeURL(q))){req.timeout=20;req.SetRequestHeader("User-Agent","NightflightVJ/0.3");yield return req.SendWebRequest();if(req.result!=UnityWebRequest.Result.Success){Message="搜尋失敗："+req.error;searching=false;yield break;}try{results=JsonUtility.FromJson<SearchResponse>("{\"items\":"+req.downloadHandler.text+"}").items??Array.Empty<SearchResult>();results=results.OrderBy(r=>string.IsNullOrEmpty(r.syncedLyrics)?1:0).ThenBy(r=>Mathf.Abs(r.duration-expected)).ToArray();Message=$"找到 {results.Length} 個版本，載入時保留來源的所有歌詞行。";if(autoLoad){var match=(q=="Humanoid ZUTOMAYO"?results.FirstOrDefault(r=>r.id==3672778):null)??results.FirstOrDefault(r=>!string.IsNullOrEmpty(r.syncedLyrics)&&Mathf.Abs(r.duration-expected)<3);if(match!=null){UseResult(match);tab=1;}}}catch(Exception ex){Message="來源格式錯誤："+ex.Message;}}searching=false;}
 void SyncSpotifyTrack(){if(!spotify.Connected||spotify.TrackId==lastSpotifyTrack||searching)return;lastSpotifyTrack=spotify.TrackId;query=spotify.Title.Contains("ヒューマノイド")&&spotify.Artist.Contains("ZUTOMAYO")?"Humanoid ZUTOMAYO":spotify.Title+" "+spotify.Artist;SetDocument(new LyricDocument{title=spotify.Title,artist=spotify.Artist,source="等待歌詞來源",timing="尚未載入"});StartCoroutine(SearchLyrics(true));}
 void UseResult(SearchResult r){bool timed=!string.IsNullOrWhiteSpace(r.syncedLyrics);string text=timed?r.syncedLyrics:r.plainLyrics;if(string.IsNullOrWhiteSpace(text)){Message="此版本沒有歌詞，請選其他來源。";return;}var doc=LyricParser.Parse(text,timed?"remote.lrc":"remote.txt","LRCLIB / "+r.id);doc.title=r.trackName;doc.artist=r.artistName;doc.sourceUri="https://lrclib.net/api/get/"+r.id;SetDocument(doc);Debug.Log("VJ_SOURCE_LOADED id="+r.id+" cues="+doc.lines.Count+" duration="+r.duration);songDuration=Mathf.Max(1,r.duration);Message=$"完整來源已載入：{doc.lines.Count(c=>!string.IsNullOrWhiteSpace(c.text))} 行。逐字動畫依行時間估算，可用 offset 微調。";}
}
}
