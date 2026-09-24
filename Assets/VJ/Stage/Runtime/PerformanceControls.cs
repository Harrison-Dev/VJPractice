using System;
using System.Linq;
using UnityEngine;
namespace VJPractice.Stage {
public sealed partial class VJStage {
 public int LyricMode;public float Fps{get;private set;}public float P95{get;private set;}int parameter;
 readonly float[] frameTimes=new float[180];int frameIndex,frameCount;float nextStats;GUIStyle[] cachedLabels=new GUIStyle[65];
 public void ApplyControl(string action,float value){if(ApplyMotionControl(action,value))return;switch(action){case "energy":Energy=Mathf.Clamp01(value);break;case "density":Density=Mathf.Clamp01(value);break;case "flow":Flow=Mathf.Clamp01(value);break;case "echo":Echo=Mathf.Clamp01(value);break;case "visual":SetTemplate(Mathf.Clamp((int)value,0,5));break;case "lyric":LyricMode=Mathf.Clamp((int)value,0,5);break;case "play":TogglePlay();break;case "seek":Seek(value);break;case "freeze":Frozen=!Frozen;break;case "blackout":Blackout=!Blackout;break;case "clean":CleanOutput=!CleanOutput;break;case "offset":Document.offsetSeconds+=Mathf.Clamp(value,-1,1);break;case "spotify":OpenSpotify();break;case "tap":double now=Time.unscaledTimeAsDouble,delta=now-lastTap;if(lastTap>=0&&delta>=.25&&delta<=1.5)Bpm=(float)(60/delta);lastTap=now;beat=0;break;}}
 void PerformanceInit(){var old=templates;templates=new StageTemplate[6];Array.Copy(old,templates,Mathf.Min(old.Length,6));string[] titles={"04 / 隧道 · Tunnel","05 / 矩陣 · Matrix","06 / 流體 · Fluid"};Color[] colors={new Color(.1f,.65f,1),new Color(.18f,.85f,.55f),new Color(.9f,.22f,.45f)};for(int i=3;i<6;i++){var p=ScriptableObject.CreateInstance<StageTemplate>();p.title=titles[i-3];p.primary=colors[i-3];p.accent=i==3?new Color(1,.35f,.12f):i==4?new Color(.7f,.95f,.95f):new Color(.3f,.3f,1);p.visualMode=i;p.energy=.65f;p.density=.6f;p.flow=.45f;p.echo=.6f;templates[i]=p;}}
 void PerformanceUpdate(){float ms=Time.unscaledDeltaTime*1000;if(ms>0){frameTimes[frameIndex++%frameTimes.Length]=ms;frameCount=Mathf.Min(frameCount+1,frameTimes.Length);}if(Time.unscaledTime>nextStats&&frameCount>0){nextStats=Time.unscaledTime+1;var copy=frameTimes.Take(frameCount).OrderBy(v=>v).ToArray();Fps=1000/Mathf.Max(1,copy.Average());P95=copy[Mathf.Clamp(Mathf.FloorToInt(copy.Length*.95f),0,copy.Length-1)];}}
 void Nudge(float amount){switch(parameter){case 0:Energy=Mathf.Clamp01(Energy+amount);break;case 1:Density=Mathf.Clamp01(Density+amount);break;case 2:Flow=Mathf.Clamp01(Flow+amount);break;case 3:Echo=Mathf.Clamp01(Echo+amount);break;}}
 void DrawPerformancePage(){
  Caption(1120,231,"TYPE × VISUAL / 現場混合",15,ink,298);
  bool overlay=KineticLyrics&&JizuraBlendStageVisuals&&JizuraStageBlend>=.995f;
  bool hybrid=KineticLyrics&&JizuraBlendStageVisuals&&JizuraStageBlend>.005f&&JizuraStageBlend<.995f;
  bool native=KineticLyrics&&(!JizuraBlendStageVisuals||JizuraStageBlend<=.005f);
  if(Action(1120,265,94,"7 文字疊加",overlay))SetJizuraLiveMode(0);
  if(Action(1222,265,94,"8 混合演出",hybrid))SetJizuraLiveMode(1);
  if(Action(1324,265,94,"9 JIZURA 全景",native))SetJizuraLiveMode(2);
  Caption(1120,305,$"原舞台可見度 {JizuraStageBlend:P0} · 左側 1–6 換特效",11,cyan,298);
  float nextBlend=GUI.HorizontalSlider(new Rect(1120,332,298,16),JizuraBlendStageVisuals?JizuraStageBlend:0f,0,1);
  if(Mathf.Abs(nextBlend-(JizuraBlendStageVisuals?JizuraStageBlend:0f))>.0001f){JizuraBlendStageVisuals=true;JizuraStageBlend=nextBlend;KineticLyrics=true;}
  string[] moods={"冷靜","流行","故障"};
  for(int i=0;i<3;i++)if(Action(1120+i*102,358,94,moods[i],KineticLyrics&&MotionMoodIndex==i))ApplyControl("motionMood",i);
  if(Action(1120,399,298,"K 文字 PV 開／關",KineticLyrics))ApplyControl("motion",KineticLyrics?0:1);
  if(Action(1120,440,145,"N 重抽歌詞"))ApplyControl("motionReroll",0);
  if(Action(1273,440,145,"L 鎖定本句",KineticLyrics&&MotionLocked))ApplyControl("motionLock",0);
  if(Action(1120,481,145,"F6 儲存配置"))ApplyControl("motionSave",0);
  if(Action(1273,481,145,"F7 載入配置"))ApplyControl("motionLoad",0);
  if(Action(1120,522,298,"F8 輸出最終貼圖 PNG"))CaptureMotionOutput();
  if(Action(1120,563,145,"JIZURA 編輯器"))OpenJizuraStudio();
  if(Action(1273,563,145,"匯入 .jizura.json"))PickJizuraProject();
  Caption(1120,605,"原本六種字幕模式 / Q W E R T Y",12,muted,298);
  string[] names={"Q 字幕","W 斜切","E 環繞","R 打字","T 字雨","Y 海報"};
  for(int i=0;i<6;i++)if(Action(1120+(i%3)*102,630+(i/3)*40,94,names[i],!KineticLyrics&&LyricMode==i))ApplyControl("lyric",i);
  Caption(1120,713,KineticLyrics?MotionStatus:"文字 PV 已關閉；選現場混合模式或按 K 啟用",11,cyan,298);
 }
 void SetJizuraLiveMode(int mode){
  KineticLyrics=true;
  JizuraBlendStageVisuals=mode!=2;
  JizuraStageBlend=mode==0?1f:mode==1?.55f:0f;
  Message=mode==0?"文字疊加：原舞台特效作背景。":mode==1?"混合演出：原舞台與 JIZURA 配色共同呈現。":"JIZURA 全景：使用原版配色背景。";
 }
}
}
