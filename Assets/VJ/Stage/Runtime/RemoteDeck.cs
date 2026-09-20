using System;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Concurrent;
using UnityEngine;
namespace VJPractice.Stage {
public sealed class RemoteDeck:MonoBehaviour {
 [Serializable] class Command {public string action;public float value;}
 [Serializable] class State {public float energy,density,flow,echo,position,duration,bpm,fps,p95,low,mid,high;public int visual,lyric;public bool playing,frozen,blackout;public string title,status;}
 public string Url="",Error=""; public bool Online=>listener!=null&&listener.IsListening;
 HttpListener listener;Thread worker;VJStage stage;string token,page;volatile string stateJson="{}";readonly ConcurrentQueue<string> queue=new ConcurrentQueue<string>();float nextPublish;
 public void Begin(VJStage owner){stage=owner;if(Online)return;try{string host=FindAddress();token=PlayerPrefs.GetString("RemoteDeckToken","");if(token.Length!=32){token=Guid.NewGuid().ToString("N");PlayerPrefs.SetString("RemoteDeckToken",token);PlayerPrefs.Save();}Url="http://"+host+":32111/"+token+"/";page=Resources.Load<TextAsset>("RemoteDeck").text;listener=new HttpListener();listener.Prefixes.Add("http://"+host+":32111/");if(host!="127.0.0.1")listener.Prefixes.Add("http://127.0.0.1:32111/");listener.Start();worker=new Thread(Serve){IsBackground=true};worker.Start();Debug.Log("VJ_REMOTE_URL "+Url);}catch(Exception e){Error=e.Message;listener?.Close();listener=null;}}
 static string FindAddress(){
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
 for(int i=0;i<4;i++){try{using(var p=new System.Diagnostics.Process()){p.StartInfo=new System.Diagnostics.ProcessStartInfo("/usr/sbin/ipconfig","getifaddr en"+i){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true};p.Start();if(!p.WaitForExit(500)){p.Kill();continue;}string ip=p.StandardOutput.ReadToEnd().Trim();if(IPAddress.TryParse(ip,out var address)&&address.AddressFamily==AddressFamily.InterNetwork)return ip;}}catch(Exception){}}
 return "127.0.0.1";
#else
 foreach(var n in NetworkInterface.GetAllNetworkInterfaces()){if(n.OperationalStatus!=OperationalStatus.Up||n.NetworkInterfaceType==NetworkInterfaceType.Loopback)continue;foreach(var a in n.GetIPProperties().UnicastAddresses){if(a.Address.AddressFamily==AddressFamily.InterNetwork)return a.Address.ToString();}}return "127.0.0.1";
#endif
 }
 void Serve(){while(listener!=null&&listener.IsListening){HttpListenerContext c=null;try{c=listener.GetContext();var r=c.Request;string body="",type="application/json";string root="/"+token+"/";if(!r.Url.AbsolutePath.StartsWith(root,StringComparison.Ordinal))c.Response.StatusCode=403;else if(r.HttpMethod=="GET"&&r.Url.AbsolutePath==root){body=page;type="text/html; charset=utf-8";}else if(r.HttpMethod=="GET"&&r.Url.AbsolutePath==root+"state")body=stateJson;else if(r.HttpMethod=="POST"&&r.Url.AbsolutePath==root+"command"&&r.ContentLength64>=0&&r.ContentLength64<256&&r.Headers["Origin"]==r.Url.GetLeftPart(UriPartial.Authority)&&queue.Count<64){using(var reader=new StreamReader(r.InputStream))queue.Enqueue(reader.ReadToEnd());body="{}";}else c.Response.StatusCode=400;c.Response.Headers["Cache-Control"]="no-store";c.Response.Headers["X-Content-Type-Options"]="nosniff";c.Response.Headers["Referrer-Policy"]="no-referrer";c.Response.ContentType=type;byte[] b=Encoding.UTF8.GetBytes(body);c.Response.OutputStream.Write(b,0,b.Length);}catch{if(listener==null||!listener.IsListening)return;}finally{try{c?.Response.Close();}catch{}}}}
 void Update(){if(!stage)return;for(int i=0;i<32&&queue.TryDequeue(out var json);i++){try{var c=JsonUtility.FromJson<Command>(json);if(c!=null&&LyricDocument.Finite(c.value))stage.ApplyControl(c.action,c.value);}catch{}}if(Time.unscaledTime<nextPublish)return;nextPublish=Time.unscaledTime+.15f;var a=stage.Audio;stateJson=JsonUtility.ToJson(new State{energy=stage.Energy,density=stage.Density,flow=stage.Flow,echo=stage.Echo,position=stage.Position,duration=stage.Duration,bpm=stage.Bpm,fps=stage.Fps,p95=stage.P95,low=a.Bands.x,mid=a.Bands.y,high=a.Bands.z,visual=stage.TemplateIndex,lyric=stage.LyricMode,playing=stage.Playing,frozen=stage.Frozen,blackout=stage.Blackout,title=stage.Document.title,status=a.Status});}
 void OnDestroy(){var l=listener;listener=null;l?.Close();worker?.Join(200);}
}
}
