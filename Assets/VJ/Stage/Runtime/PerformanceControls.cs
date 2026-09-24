using System;
using System.Linq;
using UnityEngine;
namespace VJPractice.Stage {
public sealed partial class VJStage {
 public float Fps{get;private set;}public float P95{get;private set;}int parameter;
 readonly float[] frameTimes=new float[180];int frameIndex,frameCount;float nextStats;GUIStyle[] cachedLabels=new GUIStyle[65];
 public void ApplyControl(string action,float value){if(ApplyMotionControl(action,value))return;switch(action){case "energy":Energy=Mathf.Clamp01(value);break;case "density":Density=Mathf.Clamp01(value);break;case "flow":Flow=Mathf.Clamp01(value);break;case "echo":Echo=Mathf.Clamp01(value);break;case "visual":SetTemplate(Mathf.Clamp((int)value,0,5));break;case "play":TogglePlay();break;case "seek":Seek(value);break;case "freeze":Frozen=!Frozen;break;case "blackout":Blackout=!Blackout;break;case "clean":CleanOutput=!CleanOutput;break;case "offset":Document.offsetSeconds+=Mathf.Clamp(value,-1,1);break;case "spotify":OpenSpotify();break;case "tap":double now=Time.unscaledTimeAsDouble,delta=now-lastTap;if(lastTap>=0&&delta>=.25&&delta<=1.5)Bpm=(float)(60/delta);lastTap=now;beat=0;break;}}
 void PerformanceInit(){var old=templates;templates=new StageTemplate[6];Array.Copy(old,templates,Mathf.Min(old.Length,6));string[] titles={"04 / 隧道 · Tunnel","05 / 矩陣 · Matrix","06 / 流體 · Fluid"};Color[] colors={new Color(1f,.08f,.16f),new Color(.18f,.85f,.55f),new Color(.9f,.22f,.45f)};for(int i=3;i<6;i++){var p=ScriptableObject.CreateInstance<StageTemplate>();p.title=titles[i-3];p.background=new Color(.018f,.004f,.009f);p.primary=colors[i-3];p.accent=i==3?new Color(1,.39f,.14f):i==4?new Color(.7f,.95f,.95f):new Color(.3f,.3f,1);p.visualMode=i;p.energy=.65f;p.density=.6f;p.flow=.45f;p.echo=.6f;templates[i]=p;}}
 void PerformanceUpdate(){float ms=Time.unscaledDeltaTime*1000;if(ms>0){frameTimes[frameIndex++%frameTimes.Length]=ms;frameCount=Mathf.Min(frameCount+1,frameTimes.Length);}if(Time.unscaledTime>nextStats&&frameCount>0){nextStats=Time.unscaledTime+1;var copy=frameTimes.Take(frameCount).OrderBy(v=>v).ToArray();Fps=1000/Mathf.Max(1,copy.Average());P95=copy[Mathf.Clamp(Mathf.FloorToInt(copy.Length*.95f),0,copy.Length-1)];}}
 void Nudge(float amount){switch(parameter){case 0:Energy=Mathf.Clamp01(Energy+amount);break;case 1:Density=Mathf.Clamp01(Density+amount);break;case 2:Flow=Mathf.Clamp01(Flow+amount);break;case 3:Echo=Mathf.Clamp01(Echo+amount);break;}}
 void DrawPerformancePage(){
  Caption(1120,228,"JIZURA  /  LIVE LOOK",16,ink,230);
  if(Action(1330,225,88,KineticLyrics?"K  ON":"K  OFF",KineticLyrics))ApplyControl("motion",KineticLyrics?0:1);
  if(Action(1120,267,145,"A   AUTO",JizuraManualLook==0))SetJizuraManualLook(0);
  if(Action(1273,267,145,"MANUAL",JizuraManualLook>0))SetJizuraManualLook(JizuraManualLook>0?JizuraManualLook:1);
  Caption(1120,311,"演出手選 / 歌詞時間維持同步",11,muted,298);
  string[] keys={"Q","W","E","R","T","Y"};
  for(int i=0;i<6;i++)if(DeckPad(1120+(i%2)*153,337+(i/2)*68,145,keys[i],JizuraLookNames[i+1],JizuraManualLook==i+1))SetJizuraManualLook(i+1);
  Caption(1120,549,$"BLEND  /  原舞台 {JizuraStageBlend:P0}",12,ink,298);
  Fill(new Rect(1120,579,298,4),line);
  Fill(new Rect(1120,579,298*(JizuraBlendStageVisuals?JizuraStageBlend:0f),4),cyan);
  float nextBlend=GUI.HorizontalSlider(new Rect(1120,571,298,20),JizuraBlendStageVisuals?JizuraStageBlend:0f,0,1);
  if(Mathf.Abs(nextBlend-(JizuraBlendStageVisuals?JizuraStageBlend:0f))>.0001f){JizuraBlendStageVisuals=true;JizuraStageBlend=nextBlend;KineticLyrics=true;}
  bool overlay=KineticLyrics&&JizuraBlendStageVisuals&&JizuraStageBlend>=.995f;
  bool hybrid=KineticLyrics&&JizuraBlendStageVisuals&&JizuraStageBlend>.005f&&JizuraStageBlend<.995f;
  bool native=KineticLyrics&&(!JizuraBlendStageVisuals||JizuraStageBlend<=.005f);
  if(Action(1120,606,94,"7 疊加",overlay))SetJizuraLiveMode(0);
  if(Action(1222,606,94,"8 混合",hybrid))SetJizuraLiveMode(1);
  if(Action(1324,606,94,"9 全景",native))SetJizuraLiveMode(2);
  if(Action(1120,650,145,"N  重抽"))ApplyControl("motionReroll",0);
  if(Action(1273,650,145,"L  鎖定",KineticLyrics&&MotionLocked))ApplyControl("motionLock",0);
  if(Action(1120,690,145,"F6  儲存"))ApplyControl("motionSave",0);
  if(Action(1273,690,145,"F7  載入"))ApplyControl("motionLoad",0);
  if(Action(1120,730,298,"F8  輸出最終貼圖 PNG"))CaptureMotionOutput();
  if(Action(1120,770,145,"JIZURA Studio"))OpenJizuraStudio();
  if(Action(1273,770,145,"匯入專案"))PickJizuraProject();
 }
 void SetJizuraLiveMode(int mode){
  KineticLyrics=true;
  JizuraBlendStageVisuals=mode!=2;
  JizuraStageBlend=mode==0?1f:mode==1?.75f:0f;
  Message=mode==0?"文字疊加：原舞台特效作背景。":mode==1?"混合演出：原舞台與 JIZURA 配色共同呈現。":"JIZURA 全景：使用原版配色背景。";
 }
}
}
