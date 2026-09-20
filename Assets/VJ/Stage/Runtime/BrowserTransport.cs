using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Collections.Concurrent;
using UnityEngine;
namespace VJPractice.Stage {
// Only the local companion page can exchange transport data. Audio stays in BlackHole.
public sealed class BrowserTransport:MonoBehaviour {
 [Serializable] public class Snapshot { public float position,duration;public int state,error; }
 [Serializable] class Command {public string action;public float value;}
 public Snapshot State=new Snapshot(); public string Error="";public bool Connected=>Time.realtimeSinceStartup-lastSeen<2;
 public string Url=>"http://127.0.0.1:32110/"+token+"/";
 HttpListener listener;Thread worker;readonly string token=Guid.NewGuid().ToString("N");
 readonly ConcurrentQueue<string> incoming=new ConcurrentQueue<string>();readonly ConcurrentQueue<string> commands=new ConcurrentQueue<string>();
 float lastSeen=-100;string page;
 public bool Launch(){if(listener!=null)return true;try{page=Resources.Load<TextAsset>("VJPlayer").text.Replace("__TOKEN__",token);listener=new HttpListener();listener.Prefixes.Add("http://127.0.0.1:32110/");listener.Start();worker=new Thread(Serve){IsBackground=true};worker.Start();return true;}catch(Exception e){Error=e.Message;listener?.Close();listener=null;return false;}}
 public void Send(string action,float value=0){commands.Enqueue(JsonUtility.ToJson(new Command{action=action,value=value}));}
 void Serve(){while(listener!=null&&listener.IsListening){HttpListenerContext c=null;try{c=listener.GetContext();var r=c.Request;string path=r.Url.AbsolutePath;string body="",type="application/json";if(r.Url.Host!="127.0.0.1"||!path.StartsWith("/"+token+"/",StringComparison.Ordinal)){c.Response.StatusCode=403;}else if(r.HttpMethod=="GET"&&path=="/"+token+"/"){body=page;type="text/html; charset=utf-8";}else if(r.HttpMethod=="POST"&&path.EndsWith("/state")&&r.ContentLength64>=0&&r.ContentLength64<2048&&r.Headers["Origin"]=="http://127.0.0.1:32110"){using(var reader=new StreamReader(r.InputStream))incoming.Enqueue(reader.ReadToEnd());if(!commands.TryDequeue(out body))body="{}";}else c.Response.StatusCode=404;c.Response.ContentType=type;c.Response.Headers["Cache-Control"]="no-store";c.Response.Headers["Referrer-Policy"]="strict-origin-when-cross-origin";byte[] bytes=Encoding.UTF8.GetBytes(body);c.Response.OutputStream.Write(bytes,0,bytes.Length);}catch(Exception){if(listener==null||!listener.IsListening)return;}finally{try{c?.Response.Close();}catch{}}}}
 void Update(){while(incoming.TryDequeue(out string json)){try{var s=JsonUtility.FromJson<Snapshot>(json);if(s!=null&&!float.IsNaN(s.position)&&!float.IsInfinity(s.position)&&s.position>=0&&s.duration>=0&&s.duration<86400){State=s;lastSeen=Time.realtimeSinceStartup;}}catch{}}}
 void OnDestroy(){var l=listener;listener=null;l?.Close();worker?.Join(300);}
}
}
