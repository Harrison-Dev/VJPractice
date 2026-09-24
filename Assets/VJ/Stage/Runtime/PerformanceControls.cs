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
  Caption(1120,231,"文字 PV / KINETIC LYRICS",15,ink,298);
  if(Action(1120,265,298,"K 自動文字 PV",KineticLyrics))ApplyControl("motion",KineticLyrics?0:1);
  string[] moods={"冷靜","流行","故障"};
  for(int i=0;i<3;i++)if(Action(1120+i*102,308,94,moods[i],KineticLyrics&&MotionMoodIndex==i))ApplyControl("motionMood",i);
  if(Action(1120,351,145,"N 重抽歌詞"))ApplyControl("motionReroll",0);
  if(Action(1273,351,145,"L 鎖定本句",KineticLyrics&&MotionLocked))ApplyControl("motionLock",0);
  if(Action(1120,395,145,"F6 儲存配置"))SaveMotionPlan();
  if(Action(1273,395,145,"F7 載入配置"))LoadMotionPlan();
  if(Action(1120,437,298,"F8 輸出最終貼圖 PNG"))CaptureMotionOutput();
  Caption(1120,482,KineticLyrics?MotionStatus:"文字 PV 已關閉；按 K 或選風格啟用",12,cyan,298);
  Caption(1120,537,"原本六種模式 / Q W E R T Y",13,muted,298);
  string[] names={"Q 字幕","W 斜切","E 環繞","R 打字","T 字雨","Y 海報"};
  for(int i=0;i<6;i++)if(Action(1120+(i%3)*102,572+(i/3)*40,94,names[i],!KineticLyrics&&LyricMode==i))ApplyControl("lyric",i);
  Caption(1120,707,$"{Fps:F0} FPS / P95 {P95:F1} ms\nM 換風格 · H 輸出 · F 凍結 · B 黑幕",12,cyan,298);
 }
}
}
