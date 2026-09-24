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
 readonly Color ink=new Color(.92f,.95f,1f),muted=new Color(.58f,.65f,.73f),cyan=new Color(1f,.07f,.12f),amber=new Color(1f,.43f,.13f),panel=new Color(.018f,.024f,.033f),line=new Color(.17f,.22f,.27f);
 GUIStyle field,flat,mini;Texture2D flatTex,hoverTex;
 readonly GUIStyle[] centeredLabels=new GUIStyle[65];readonly Texture2D[] iconMasks=new Texture2D[12];float venueResume=.65f;
 AudioClip waveformClip;readonly float[] waveformPeaks=new float[256];float[] waveformSamples;int waveformScan;bool waveformAvailable;
 void ConsoleInit(){PerformanceInit();spotify=gameObject.AddComponent<SpotifyTransport>();transport=gameObject.AddComponent<BrowserTransport>();Audio.Scan();Playing=false;Audio.Source.Pause();Message="先在 Spotify 播放 Humanoid，再按連接；或到音源選原創示範。";}
 void ConsoleStyles(){if(flat!=null)return;flatTex=Solid(new Color(.055f,.065f,.08f));hoverTex=Solid(new Color(.14f,.035f,.045f));flat=new GUIStyle{font=jpFont,fontSize=13,alignment=TextAnchor.MiddleCenter,padding=new RectOffset(6,6,3,3)};flat.normal.textColor=ink;flat.hover.textColor=Color.white;flat.active.textColor=Color.white;flat.focused.textColor=ink;field=new GUIStyle(flat){alignment=TextAnchor.MiddleLeft,fontSize=15};field.normal.background=flatTex;field.focused.background=hoverTex;mini=new GUIStyle(label){fontSize=11};button=flat;}
 Texture2D Solid(Color c){var t=new Texture2D(1,1);t.SetPixel(0,0,c.linear);t.Apply();return t;}
 void Caption(float x,float y,string value,int size=12,Color? color=null,float width=290){size=Mathf.Clamp(size,1,64);var s=cachedLabels[size]??(cachedLabels[size]=new GUIStyle(label){fontSize=size,wordWrap=true});s.normal.textColor=color??ink;GUI.Label(new Rect(x,y,width,78),value,s);}
 void CenterCaption(float x,float y,float w,string value,int size=12,Color? color=null,float h=22){size=Mathf.Clamp(size,1,64);var s=centeredLabels[size]??(centeredLabels[size]=new GUIStyle(label){fontSize=size,alignment=TextAnchor.MiddleCenter,wordWrap=false});s.normal.textColor=color??ink;GUI.Label(new Rect(x,y,w,h),value,s);}
 void Rule(float x,float y,float w,Color c){Fill(new Rect(x,y,w,1),c);}
 void Frame(Rect r,Color c,float thickness=1){Fill(new Rect(r.x,r.y,r.width,thickness),c);Fill(new Rect(r.x,r.yMax-thickness,r.width,thickness),c);Fill(new Rect(r.x,r.y,thickness,r.height),c);Fill(new Rect(r.xMax-thickness,r.y,thickness,r.height),c);}
 void ConsoleDispose(){for(int i=0;i<iconMasks.Length;i++)if(iconMasks[i])Destroy(iconMasks[i]);}
 static void MaskLine(Color32[] pixels,float x1,float y1,float x2,float y2,float thickness=1.6f){float dx=x2-x1,dy=y2-y1,len=Mathf.Max(.001f,dx*dx+dy*dy),radius=thickness*.5f+.8f;int minX=Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(x1,x2)-radius),0,63),maxX=Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(x1,x2)+radius),0,63),minY=Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(y1,y2)-radius),0,63),maxY=Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(y1,y2)+radius),0,63);for(int y=minY;y<=maxY;y++)for(int x=minX;x<=maxX;x++){float t=Mathf.Clamp01(((x+.5f-x1)*dx+(y+.5f-y1)*dy)/len),xx=x1+t*dx,yy=y1+t*dy,d=Mathf.Sqrt((x+.5f-xx)*(x+.5f-xx)+(y+.5f-yy)*(y+.5f-yy));byte alpha=(byte)(Mathf.Clamp01(thickness*.5f+.65f-d)*255);int at=y*64+x;if(alpha>pixels[at].a)pixels[at]=new Color32(255,255,255,alpha);}}
 static void MaskArc(Color32[] p,float x,float y,float radius,float start=0,float end=Mathf.PI*2,int steps=32,float thickness=1.5f){float px=x+Mathf.Cos(start)*radius,py=y+Mathf.Sin(start)*radius;for(int i=1;i<=steps;i++){float a=Mathf.Lerp(start,end,i/(float)steps),nx=x+Mathf.Cos(a)*radius,ny=y+Mathf.Sin(a)*radius;MaskLine(p,px,py,nx,ny,thickness);px=nx;py=ny;}}
 static void MaskCube(Color32[] p){MaskLine(p,32,9,48,19);MaskLine(p,48,19,32,29);MaskLine(p,32,29,16,19);MaskLine(p,16,19,32,9);MaskLine(p,16,19,16,47);MaskLine(p,48,19,48,47);MaskLine(p,16,47,32,56);MaskLine(p,48,47,32,56);MaskLine(p,32,29,32,56);}
 Texture2D MakeIcon(int kind){var p=new Color32[64*64];switch(kind){
  case 0:MaskLine(p,32,8,9,48,2);MaskLine(p,32,8,55,48,2);MaskLine(p,9,48,55,48,2);MaskLine(p,32,8,32,48);MaskLine(p,9,48,32,30);MaskLine(p,55,48,32,30);break;
  case 1:MaskArc(p,32,32,23);MaskArc(p,32,32,17);MaskArc(p,32,32,11);break;
  case 2:for(int j=0;j<58;j++){float t=j/57f*4.6f*Mathf.PI,tn=(j+1)/57f*4.6f*Mathf.PI,r=4+23*j/57f,rn=4+23*(j+1)/57f;MaskLine(p,32+Mathf.Cos(t)*r,32+Mathf.Sin(t)*r,32+Mathf.Cos(tn)*rn,32+Mathf.Sin(tn)*rn,1.6f);}break;
  case 3:MaskCube(p);break;
  case 4:for(int j=0;j<72;j++){float a=j*2.399963f,r=26*Mathf.Sqrt(j/71f),x=32+Mathf.Cos(a)*r,y=32+Mathf.Sin(a)*r;MaskLine(p,x,y,x,y,j%9==0?2.5f:1.3f);}break;
  case 5:MaskArc(p,25,36,17);MaskArc(p,39,29,17);break;
  case 6:for(int j=0;j<4;j++){float x=10+j*11,h=13+(j%3)*7;MaskLine(p,x-7,49,x+4,49-h);MaskLine(p,x+4,49-h,x+15,49);}MaskLine(p,5,49,59,49);break;
  case 7:for(int j=0;j<5;j++){float y=18+j*7;for(int k=0;k<32;k++){float x=8+k*1.5f,nx=x+1.5f;MaskLine(p,x,y+Mathf.Sin(k*.34f+j*.26f)*4,nx,y+Mathf.Sin((k+1)*.34f+j*.26f)*4,1.3f);}}break;
  case 8:MaskCube(p);MaskLine(p,32,8,32,55);break;
  case 9:for(int j=-11;j<=11;j++){float x=32+j*2.2f,span=Mathf.Sqrt(Mathf.Max(0,24*24-(j*2.2f)*(j*2.2f)));for(float y=-span;y<=span;y+=4)MaskLine(p,x,32+y,x,32+y,1.3f);}break;
  case 10:for(int j=0;j<4;j++)MaskArc(p,32,32,7+j*6);break;
  case 11:MaskLine(p,21,50,34,7);MaskLine(p,34,7,51,41);MaskLine(p,51,41,21,50);MaskLine(p,21,50,43,25);MaskLine(p,43,25,51,41);MaskLine(p,6,40,20,17);MaskLine(p,20,17,28,54);break;
 }var maskTexture=new Texture2D(64,64,TextureFormat.RGBA32,false,true){filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave};maskTexture.SetPixels32(p);maskTexture.Apply(false,true);return maskTexture;}
 void Chrome(Rect r,bool selected,bool hot){Color edge=selected?cyan:hot?new Color(.42f,.56f,.68f):line;Fill(new Rect(r.x-4,r.y-4,r.width+8,r.height+8),selected?new Color(1f,.02f,.03f,.08f):Color.clear);Fill(new Rect(r.x-2,r.y-2,r.width+4,r.height+4),selected?new Color(1f,.03f,.06f,.24f):Color.clear);Fill(r,selected?new Color(.15f,.015f,.025f):hot?new Color(.075f,.088f,.105f):new Color(.04f,.047f,.058f));Frame(r,edge,selected?2:1);Rule(r.x+4,r.y+4,r.width-8,selected?new Color(1f,.34f,.36f,.38f):new Color(.55f,.67f,.77f,.13f));if(selected){Fill(new Rect(r.x+8,r.yMax-3,r.width-16,2),new Color(1f,.10f,.12f,.6f));Fill(new Rect(r.x+3,r.y+3,2,2),Color.white);}}
 bool Action(float x,float y,float w,string text,bool selected=false){Rect r=new Rect(x,y,w,34);Chrome(r,selected,r.Contains(Event.current.mousePosition));return GUI.Button(r,text,flat);}
 bool DeckPad(float x,float y,float w,string number,string name,bool selected,bool look=false,float height=94){Rect r=new Rect(x,y,w,height);Chrome(r,selected,r.Contains(Event.current.mousePosition));Color icon=selected?new Color(1f,.16f,.18f):look&&number=="E"?amber:new Color(.69f,.85f,.99f);Caption(x+8,y+4,number,10,selected?cyan:muted,w-16);DrawCardIcon(x+w*.5f,y+(look?height*.38f:39),look?22:27,look,look?0:int.Parse(number)-1,icon,look?number:null);CenterCaption(x+3,y+height-23,w-6,name,look?13:14,selected?ink:new Color(.82f,.88f,.96f),20);return GUI.Button(r,GUIContent.none,GUIStyle.none);}
 void DrawCardIcon(float x,float y,float s,bool look,int index,Color c,string key){if(Event.current.type!=EventType.Repaint)return;if(look)index=key=="Q"?0:key=="W"?1:key=="E"?2:key=="R"?3:key=="T"?4:5;int id=(look?6:0)+index;if(iconMasks[id]==null)iconMasks[id]=MakeIcon(id);Color previous=GUI.color;GUI.color=c.linear;GUI.DrawTexture(new Rect(x-s,y-s,s*2,s*2),iconMasks[id],ScaleMode.StretchToFill,true);GUI.color=previous;}
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
 float DragSlider(Rect r,float value){var e=Event.current;int id=GUIUtility.GetControlID(FocusType.Passive,r);if(e.type==EventType.MouseDown&&r.Contains(e.mousePosition)){GUIUtility.hotControl=id;value=Mathf.Clamp01((e.mousePosition.x-r.x)/r.width);e.Use();}if(GUIUtility.hotControl==id&&e.type==EventType.MouseDrag){value=Mathf.Clamp01((e.mousePosition.x-r.x)/r.width);e.Use();}if(e.rawType==EventType.MouseUp&&GUIUtility.hotControl==id)GUIUtility.hotControl=0;return value;}
 float DeckFader(float y,string name,float value,bool venue=false){Caption(22,y,name,11,muted,150);if(!venue)Caption(189,y,value.ToString("F2"),12,cyan,49);Rect r=new Rect(22,y+22,216,21);for(int i=0;i<25;i++)Fill(new Rect(22+i*9,y+27,1,5),new Color(.24f,.30f,.36f));Fill(new Rect(r.x,r.y+9,r.width,3),line);Fill(new Rect(r.x,r.y+9,r.width*value,3),venue?amber:cyan);float hx=r.x+r.width*value;Fill(new Rect(hx-5,r.y+1,10,20),new Color(.63f,.70f,.78f));Fill(new Rect(hx-3,r.y+3,6,15),ink);Fill(new Rect(hx-3,r.y+16,6,2),value>0?cyan:muted);return DragSlider(r,value);}
 void DrawConsole(){
  ConsoleStyles();
  Fill(new Rect(0,0,1440,66),new Color(.014f,.018f,.025f));
  Fill(new Rect(0,66,260,834),panel);Fill(new Rect(1100,66,340,834),panel);
  Fill(new Rect(260,728,840,172),new Color(.025f,.028f,.035f));
  Fill(new Rect(260,66,1,834),line);Fill(new Rect(1099,66,1,834),line);
  Fill(new Rect(0,65,1440,1),line);Fill(new Rect(0,62,1440,2),new Color(.36f,.04f,.06f));
  Caption(22,8,"NIGHTFLIGHT",30,ink,260);
  Fill(new Rect(304,19,3,25),cyan);Caption(333,15,"LIVE",21,cyan,80);Caption(423,23,"VISUALS / MUSIC / HIGHER TOGETHER",10,muted,350);
  Fill(new Rect(790,26,7,7),Playing?cyan:muted);Caption(807,20,spotifyMode?(spotify.Connected?"SPOTIFY SYNC":"SPOTIFY WAITING"):browserMode?(transport.Connected?"PLAYER SYNC":"PLAYER WAITING"):Audio.External?"EXTERNAL AUDIO":"LOCAL AUDIO",12,ink,280);
  for(int i=0;i<3;i++)Fill(new Rect(1137+i*10,20,3,22),cyan);
  if(Action(1210,16,208,"OUTPUT ONLY   H"))CleanOutput=true;

  Caption(22,85,"FX",21,ink,75);Rule(67,106,171,line);
  string[] names={"漂浮","斜光","回聲","隧道","矩陣","流體"};
  for(int i=0;i<6;i++)if(DeckPad(22+(i%2)*113,122+(i/2)*105,103,(i+1).ToString("00"),names[i],TemplateIndex==i))SetTemplate(i);
  Caption(22,442,"LIVE MODULATION",11,muted,216);Rule(22,459,216,line);
  Energy=DeckFader(465,"ENERGY / 強度",Energy);Density=DeckFader(510,"DENSITY / 密度",Density);
  Flow=DeckFader(555,"FLOW / 流速",Flow);Echo=DeckFader(600,"ECHO / 殘影",Echo);
  VenueBackdropMix=DeckFader(645,"VENUE / 背景",VenueBackdropMix,true);
  if(VenueBackdropMix>.005f)venueResume=VenueBackdropMix;
  Rect venueSwitch=new Rect(169,643,69,21);Fill(venueSwitch,VenueBackdropMix>.005f?new Color(.22f,.04f,.05f):new Color(.05f,.06f,.07f));Frame(venueSwitch,VenueBackdropMix>.005f?cyan:line);if(GUI.Button(venueSwitch,VenueBackdropMix>.005f?"ON":"OFF",flat)){if(VenueBackdropMix>.005f){venueResume=VenueBackdropMix;VenueBackdropMix=0;}else VenueBackdropMix=Mathf.Max(.05f,venueResume);}
  if(Action(22,693,103,"FREEZE  F",Frozen))Frozen=!Frozen;
  if(Action(135,693,103,"BLACKOUT  B",Blackout))Blackout=!Blackout;
  Caption(22,737,Audio.External?"SIGNAL / EXTERNAL":"SIGNAL / LOCAL",11,muted,216);
  for(int i=0;i<3;i++){float v=Mathf.Clamp01(Audio.Bands[i]);Caption(22,761+i*23,new[]{"LOW","MID","HIGH"}[i],10,muted,45);Fill(new Rect(68,772+i*23,132,4),line);Fill(new Rect(68,772+i*23,132*v,4),i==2?amber:cyan);Caption(208,760+i*23,v.ToString("F1"),10,ink,40);}
  Caption(22,831,$"{Bpm:F0} BPM  /  V TAP",18,cyan,216);
  Caption(22,861,$"{Fps:F0} FPS   ·   P95 {P95:F1} ms",10,muted,216);

  Fill(new Rect(272,78,816,1),line);Fill(new Rect(272,681,816,1),line);
  Caption(283,85,"CAM 1  /  MAIN OUTPUT",11,ink,300);Caption(792,85,templates[TemplateIndex].title,11,amber,285);
  Rule(283,117,12,ink);Fill(new Rect(283,117,1,12),ink);Rule(1064,117,12,ink);Fill(new Rect(1075,117,1,12),ink);
  Rule(283,665,12,ink);Fill(new Rect(283,653,1,12),ink);Rule(1064,665,12,ink);Fill(new Rect(1075,653,1,12),ink);
  Caption(283,696,spotifyMode?spotify.Title+" / "+spotify.Artist:browserMode?"HUMANOID / ZUTOMAYO":Audio.External?"EXTERNAL AUDIO":Audio.Source.clip==Audio.demo?"ORIGINAL DEMO / 原創練習音軌":Audio.Source.clip?.name??"NO AUDIO",12,ink,780);
  DrawLibrary();DrawTransport();
  Caption(22,885,"NIGHTFLIGHT  v1.1  /  UNITY LIVE VJ",9,muted,440);
  Caption(1161,885,"FOR A BRIGHTER NIGHT",9,muted,260);Rule(1386,894,32,line);
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
  Caption(283,743,"BPM",10,muted,100);Caption(283,756,$"{Bpm:F0}",31,cyan,120);
  for(int i=0;i<5;i++)Fill(new Rect(374,760+i*5,3,2),i<Mathf.Clamp(Mathf.RoundToInt(Audio.Bands[0]*5),0,5)?cyan:line);
  Caption(474,750,LyricDocument.Format(Position),23,ink,180);
  Caption(674,759,"/ "+LyricDocument.Format(Duration),13,muted,170);
  if(Action(870,750,101,Playing?"❚❚  PAUSE":"▶  PLAY",Playing))TogglePlay();
  if(Action(979,750,98,"HOME"))Seek(0);
  Rect track=new Rect(283,791,793,43);
  Fill(track,new Color(.012f,.017f,.024f));Frame(track,line);
  Fill(new Rect(track.x+2,track.y+21,track.width-4,1),new Color(.20f,.25f,.30f));
  float progress=Mathf.Clamp01(Position/Duration);
  if(waveformAvailable){float step=(track.width-4)/waveformPeaks.Length;for(int i=0;i<waveformScan;i++){float h=2+waveformPeaks[i]*31;float x=track.x+2+i*step;Color wave=i/(float)waveformPeaks.Length<progress?i%3==0?amber:cyan:new Color(.66f,.77f,.88f);Fill(new Rect(x,track.y+21-h*.5f,Mathf.Max(1,step-1),h),wave);}}
  else CenterCaption(track.x,track.y+13,track.width,Audio.External?"EXTERNAL SIGNAL  /  NO TRACK WAVEFORM":"LOAD LOCAL AUDIO FOR WAVEFORM",10,muted,17);
  Fill(new Rect(track.x+2,track.y+40,(track.width-4)*progress,2),cyan);
  for(int i=0;i<Document.lines.Count;i++){var c=Document.lines[i];if(c.startTime<0)continue;float x=track.x+track.width*Mathf.Clamp01((c.startTime+Document.offsetSeconds)/Duration);Fill(new Rect(x,track.y+44,1,4),i==Document.ActiveAt(Position)?amber:muted);}
  if(Document.lines.Count>0)for(int i=0;i<4;i++){var c=Document.lines[Mathf.Clamp((i+1)*Document.lines.Count/5,0,Document.lines.Count-1)];if(c.startTime<0)continue;float x=track.x+track.width*Mathf.Clamp01((c.startTime+Document.offsetSeconds)/Duration);Color cueColor=i==1?amber:i==2?new Color(.4f,.78f,1f):ink;Fill(new Rect(x-7,track.y-8,14,13),cueColor);CenterCaption(x-7,track.y-9,14,(i+1).ToString(),9,new Color(.02f,.03f,.04f),13);Fill(new Rect(x,track.y+5,1,32),cueColor);}
  Fill(new Rect(track.x+track.width*progress-1,track.y,2,43),ink);
  var e=Event.current;if((e.type==EventType.MouseDown||e.type==EventType.MouseDrag)&&track.Contains(e.mousePosition)){Seek((e.mousePosition.x-track.x)/track.width*Duration);e.Use();}
  if(Action(283,850,114,"− 0.1 SEC"))Document.offsetSeconds-=.1f;
  Caption(411,859,$"OFFSET {Document.offsetSeconds:+0.00;-0.00;0.00}s",12,amber,200);
  if(Action(625,850,114,"+ 0.1 SEC"))Document.offsetSeconds+=.1f;
  if(Action(747,850,175,"NEXT CUE / Enter"))Stamp();
  Caption(937,859,"[ / ]  調整",11,muted,139);
 }
 void ConnectBlackHole(){int i=Array.FindIndex(Audio.Devices,d=>d.Name.IndexOf("BlackHole",StringComparison.OrdinalIgnoreCase)>=0);if(i>=0){Audio.Select(i);Message="已選 BlackHole；系統輸出需為 VJ Monitor + BlackHole。";}else Message="找不到 BlackHole，請確認已安裝並重開 Unity。";}
 public void OpenSpotify(){browserMode=false;spotifyMode=true;Audio.Source.Stop();Audio.Scan();ConnectBlackHole();spotify.Enabled=true;Message="macOS 若詢問，請允許 Unity 控制 Spotify。";lastSpotifyTrack="";}
 public void OpenHumanoid(){spotifyMode=false;spotify.Enabled=false;browserMode=true;Playing=false;Position=0;Audio.Source.Stop();Audio.Scan();ConnectBlackHole();if(transport.Launch()){Application.OpenURL(transport.Url);Message="請在瀏覽器按播放。聲音與播放位置將分別接入。";}else Message="播放器開啟失敗："+transport.Error;if(!searching)StartCoroutine(SearchLyrics(true));}
 public IEnumerator SearchLyrics(bool autoLoad=false){searching=true;Message="正在搜尋完整來源…";string q=query;float expected=spotifyMode&&spotify.Connected?spotify.Duration:260;using(var req=UnityWebRequest.Get("https://lrclib.net/api/search?q="+UnityWebRequest.EscapeURL(q))){req.timeout=20;req.SetRequestHeader("User-Agent","NightflightVJ/0.3");yield return req.SendWebRequest();if(req.result!=UnityWebRequest.Result.Success){Message="搜尋失敗："+req.error;searching=false;yield break;}try{results=JsonUtility.FromJson<SearchResponse>("{\"items\":"+req.downloadHandler.text+"}").items??Array.Empty<SearchResult>();results=results.OrderBy(r=>string.IsNullOrEmpty(r.syncedLyrics)?1:0).ThenBy(r=>Mathf.Abs(r.duration-expected)).ToArray();Message=$"找到 {results.Length} 個版本，載入時保留來源的所有歌詞行。";if(autoLoad){var match=(q=="Humanoid ZUTOMAYO"?results.FirstOrDefault(r=>r.id==3672778):null)??results.FirstOrDefault(r=>!string.IsNullOrEmpty(r.syncedLyrics)&&Mathf.Abs(r.duration-expected)<3);if(match!=null){UseResult(match);tab=1;}}}catch(Exception ex){Message="來源格式錯誤："+ex.Message;}}searching=false;}
 void SyncSpotifyTrack(){if(!spotify.Connected||spotify.TrackId==lastSpotifyTrack||searching)return;lastSpotifyTrack=spotify.TrackId;query=spotify.Title.Contains("ヒューマノイド")&&spotify.Artist.Contains("ZUTOMAYO")?"Humanoid ZUTOMAYO":spotify.Title+" "+spotify.Artist;SetDocument(new LyricDocument{title=spotify.Title,artist=spotify.Artist,source="等待歌詞來源",timing="尚未載入"});StartCoroutine(SearchLyrics(true));}
 void UseResult(SearchResult r){bool timed=!string.IsNullOrWhiteSpace(r.syncedLyrics);string text=timed?r.syncedLyrics:r.plainLyrics;if(string.IsNullOrWhiteSpace(text)){Message="此版本沒有歌詞，請選其他來源。";return;}var doc=LyricParser.Parse(text,timed?"remote.lrc":"remote.txt","LRCLIB / "+r.id);doc.title=r.trackName;doc.artist=r.artistName;doc.sourceUri="https://lrclib.net/api/get/"+r.id;SetDocument(doc);Debug.Log("VJ_SOURCE_LOADED id="+r.id+" cues="+doc.lines.Count+" duration="+r.duration);songDuration=Mathf.Max(1,r.duration);Message=$"完整來源已載入：{doc.lines.Count(c=>!string.IsNullOrWhiteSpace(c.text))} 行。逐字動畫依行時間估算，可用 offset 微調。";}
}
}
