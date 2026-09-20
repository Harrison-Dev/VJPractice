using System;
using System.Linq;
using UnityEngine;
using Lasp;
namespace VJPractice.Stage {
[RequireComponent(typeof(AudioSource))]
public sealed class StageAudio:MonoBehaviour {
    public AudioClip demo;
    public AudioSource Source {get;private set;}
    public bool External {get;private set;}
    public string Status {get;private set;}="Demo";
    public Vector4 Bands {get;private set;}
    public DeviceDescriptor[] Devices=Array.Empty<DeviceDescriptor>();
    InputStream stream;
    readonly float[] fft=new float[1024],wave=new float[1024];
    public void Init(){Source=GetComponent<AudioSource>();Source.playOnAwake=false;Source.loop=true;Source.clip=demo;Source.volume=.3f;Source.Play();}
    public void Scan(){try{Devices=AudioSystem.InputDevices.ToArray();Status=Devices.Length+" input devices";}catch(Exception ex){Status="LASP: "+ex.Message;}}
    public void Select(int i){
        if(i<0||i>=Devices.Length)return;
        try{stream=AudioSystem.GetInputStream(Devices[i]);External=stream!=null; if(External){Source.Stop();Status="LASP / "+Devices[i].Name;}else Status="Input unavailable";}catch(Exception ex){Status=ex.Message;External=false;}
    }
    public void Local(AudioClip clip){External=false;stream=null;Source.Stop();Source.clip=clip;Source.Play();Status=clip?clip.name:"No audio";}
    void Update(){
        if(Source==null)return;Vector4 target=Vector4.zero;
        if(External){
            if(stream==null||!stream.IsValid){Status="Device disconnected — select an input again";stream=null;External=false;}
            else try { target=new Vector4(Level(FilterType.LowPass),Level(FilterType.BandPass),Level(FilterType.HighPass),Level(FilterType.Bypass)); }catch(Exception ex){Status="Input failed: "+ex.Message;External=false;stream=null;}
        }else if(Source.isPlaying){
            Source.GetSpectrumData(fft,0,FFTWindow.BlackmanHarris);Source.GetOutputData(wave,0);
            float sum=0;foreach(float f in wave)sum+=f*f;
            target=new Vector4(Band(30,250),Band(250,2000),Band(2000,12000),Mathf.Clamp01(Mathf.Sqrt(sum/wave.Length)*5));
        }
        Bands=Vector4.Lerp(Bands,target,1-Mathf.Exp(-Time.unscaledDeltaTime*9));
    }
    float Level(FilterType f){float peak=-100;int channels=Mathf.Min(2,stream.ChannelCount);for(int c=0;c<channels;c++)peak=Mathf.Max(peak,stream.GetChannelLevel(c,f));return Mathf.Clamp01((peak+55)/45);}
    float Band(float lo,float hi){float hz=AudioSettings.outputSampleRate*.5f/fft.Length;int a=Mathf.Clamp((int)(lo/hz),0,fft.Length-1),b=Mathf.Clamp((int)(hi/hz),a,fft.Length-1);float sum=0;for(int i=a;i<=b;i++)sum+=fft[i]*fft[i];return Mathf.Clamp01(Mathf.Sqrt(sum)*20);}
}
}
