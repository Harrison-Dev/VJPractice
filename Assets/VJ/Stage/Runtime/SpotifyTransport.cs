using System;
using System.IO;
using System.Diagnostics;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using UnityEngine;
namespace VJPractice.Stage {
// The macOS automation call runs in an owned helper process, never in Unity's render loop.
public sealed class SpotifyTransport:MonoBehaviour {
 [Serializable] class State {public bool connected,playing;public float position,duration;public string title,artist,trackId,error;}
 public bool Enabled,Connected,Playing;public float Position,Duration;public string Title="",Artist="",TrackId="",Error="";
 Process helper;readonly ConcurrentQueue<string> lines=new ConcurrentQueue<string>();float lastSeen=-10,basePosition,retryAfter;bool commandRunning;string helperError="";
 void StartHelper(){if(Application.platform!=RuntimePlatform.OSXEditor&&Application.platform!=RuntimePlatform.OSXPlayer){Error="Spotify 本機同步僅支援 macOS";Enabled=false;return;}try{var psi=new ProcessStartInfo("/usr/bin/osascript"){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=System.Text.Encoding.UTF8,StandardErrorEncoding=System.Text.Encoding.UTF8,CreateNoWindow=true};psi.Arguments="-l JavaScript \""+Path.Combine(Application.streamingAssetsPath,"SpotifyBridge.js").Replace("\"","\\\"")+"\"";helper=new Process{StartInfo=psi};helper.OutputDataReceived+=(s,e)=>{if(e.Data!=null)lines.Enqueue(e.Data);};helper.ErrorDataReceived+=(s,e)=>{if(e.Data!=null)helperError=e.Data;};helper.Start();helper.BeginOutputReadLine();helper.BeginErrorReadLine();}catch(Exception e){Error=e.Message;StopHelper();retryAfter=Time.realtimeSinceStartup+5;}}
 void Update(){if(!Enabled){if(helper!=null)StopHelper();Connected=false;return;}if(helper==null){if(Time.realtimeSinceStartup>=retryAfter)StartHelper();return;}if(helper.HasExited){Error=helperError;StopHelper();retryAfter=Time.realtimeSinceStartup+5;return;}string latest=null;while(lines.TryDequeue(out var l))latest=l;if(latest!=null){try{var s=JsonUtility.FromJson<State>(latest);if(s!=null){Connected=s.connected;Playing=s.playing;basePosition=s.position;Duration=s.duration;Title=s.title??"";Artist=s.artist??"";TrackId=s.trackId??"";Error=s.error??"";lastSeen=Time.realtimeSinceStartup;}}catch{}}if(Time.realtimeSinceStartup-lastSeen>3)Connected=false;Position=Mathf.Clamp(basePosition+(Connected&&Playing?Mathf.Min(.7f,Time.realtimeSinceStartup-lastSeen):0),0,Mathf.Max(1,Duration));}
 public void Command(string action,float value=0){if(!Enabled||commandRunning)return;string body=action=="seek"?"set player position to "+Mathf.Max(0,value).ToString(System.Globalization.CultureInfo.InvariantCulture):action=="play"?"play":"pause";commandRunning=true;Task.Run(()=>{try{var p=new Process{StartInfo=new ProcessStartInfo("/usr/bin/osascript"){UseShellExecute=false,RedirectStandardInput=true,RedirectStandardError=true,CreateNoWindow=true}};p.Start();p.StandardInput.Write("with timeout of 3 seconds\ntell application id \"com.spotify.client\"\n"+body+"\nend tell\nend timeout\n");p.StandardInput.Close();if(!p.WaitForExit(5000))p.Kill();p.Dispose();}catch(Exception e){helperError=e.Message;}finally{commandRunning=false;}});}
 void StopHelper(){if(helper!=null){try{if(!helper.HasExited)helper.Kill();helper.Dispose();}catch{}helper=null;}while(lines.TryDequeue(out _)){}Connected=false;}
 void OnDestroy(){StopHelper();}
}
}
