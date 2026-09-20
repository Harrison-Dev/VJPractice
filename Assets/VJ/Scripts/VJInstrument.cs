using UnityEngine;
using UnityEngine.InputSystem;

namespace VJPractice {
[RequireComponent(typeof(AudioSource))]
public sealed class VJInstrument : MonoBehaviour {
    public Material visual;
    [Tooltip("Drag a local music clip here before Play.")] public AudioClip music;
    [Range(60,200)] public float bpm = 120;
    [Range(0,1)] public float energy = .35f;
    [Range(0,1)] public float density = .4f;
    [Range(0,1)] public float scale = .5f;
    [Range(0,1)] public float flow = .4f;
    [Range(0,1)] public float palette = .55f;
    [Range(0,1)] public float audioAmount = .6f;
    public bool Frozen { get; private set; }
    public bool Blackout { get; private set; }
    public float Low { get; private set; }
    public float Mid { get; private set; }
    public float High { get; private set; }
    public float Rms { get; private set; }
    public float VisualTime { get; private set; }
    AudioSource source; Material runtimeMaterial;
    readonly float[] spectrum = new float[1024], samples = new float[1024];
    double lastTap = -1; float beat, targetEnergy = .35f; bool hud = true, paused;
    public void SetState(int state) { targetEnergy = state == 0 ? .15f : state == 1 ? .45f : .85f; }
    void Start() {
        Application.targetFrameRate = 60;
        source = GetComponent<AudioSource>(); source.playOnAwake = false; source.loop = true;
        source.clip = music; if (music != null) source.Play();
        runtimeMaterial = new Material(visual);
        GetComponentInChildren<MeshRenderer>().sharedMaterial = runtimeMaterial;
        targetEnergy = energy;
    }
    void Update() {
        var k = Keyboard.current;
        if(k != null) {
            if(k.digit1Key.wasPressedThisFrame) SetState(0);
            if(k.digit2Key.wasPressedThisFrame) SetState(1);
            if(k.digit3Key.wasPressedThisFrame) SetState(2);
            if(k.fKey.wasPressedThisFrame) Frozen = !Frozen;
            if(k.bKey.wasPressedThisFrame) Blackout = !Blackout;
            if(k.hKey.wasPressedThisFrame) hud = !hud;
            if(k.spaceKey.wasPressedThisFrame && music != null) {
                paused = !paused; if(paused) source.Pause(); else source.UnPause();
            }
            if(k.tKey.wasPressedThisFrame) {
                double now = Time.unscaledTimeAsDouble, interval = now-lastTap;
                if(lastTap >= 0 && interval >= .3 && interval <= 1) bpm = (float)(60/interval);
                lastTap = now; beat = 0;
            }
            if(k.upArrowKey.isPressed) targetEnergy = Mathf.Clamp01(targetEnergy + Time.deltaTime*.35f);
            if(k.downArrowKey.isPressed) targetEnergy = Mathf.Clamp01(targetEnergy - Time.deltaTime*.35f);
            if(k.rKey.wasPressedThisFrame) { Frozen=false; Blackout=false; VisualTime=0; beat=0; targetEnergy=.35f; density=.4f; scale=.5f; flow=.4f; palette=.55f; audioAmount=.6f; bpm=120; }
        }
        source.GetSpectrumData(spectrum,0,FFTWindow.BlackmanHarris);
        source.GetOutputData(samples,0);
        float sum=0; foreach(float sample in samples) sum+=sample*sample;
        float smooth=1-Mathf.Exp(-Time.deltaTime*10);
        Rms=Mathf.Lerp(Rms,Mathf.Sqrt(sum/samples.Length),smooth);
        Low=Mathf.Lerp(Low,Band(30,250),smooth); Mid=Mathf.Lerp(Mid,Band(250,2000),smooth); High=Mathf.Lerp(High,Band(2000,12000),smooth);
        energy=Mathf.Lerp(energy,targetEnergy,1-Mathf.Exp(-Time.deltaTime*3));
        if(!Frozen) {
            VisualTime+=Time.deltaTime*Mathf.Lerp(.1f,1.2f,flow);
            beat+=Time.deltaTime*bpm/60;
            runtimeMaterial.SetVector("_Control",new Vector4(energy,density,scale,palette));
            runtimeMaterial.SetVector("_Audio",new Vector4(Low,Mid,High,Rms)*audioAmount);
            runtimeMaterial.SetVector("_Clock",new Vector4(VisualTime,beat,0,0));
        }
        runtimeMaterial.SetFloat("_Blackout",Blackout?1:0);
    }
    float Band(float from,float to) {
        float hz=(AudioSettings.outputSampleRate*.5f)/spectrum.Length;
        int a=Mathf.Clamp(Mathf.CeilToInt(from/hz),0,spectrum.Length-1), b=Mathf.Clamp(Mathf.FloorToInt(to/hz),a,spectrum.Length-1);
        float sum=0; for(int i=a;i<=b;i++) sum+=spectrum[i]*spectrum[i];
        return Mathf.Clamp01(Mathf.Sqrt(sum)*12);
    }
    void OnGUI() {
        if(!hud) return;
        GUILayout.BeginArea(new Rect(20,20,320,410),GUI.skin.box);
        GUILayout.Label("VJ PRACTICE / GEOMETRY STUDY 01");
        GUILayout.Label(music ? music.name : "Silent rehearsal / assign Music in Inspector");
        GUILayout.Label($"{bpm:F0} BPM    RMS {Rms:F3}    {(Frozen ? "FROZEN" : "LIVE")}");
        targetEnergy=Slider("Energy",targetEnergy); density=Slider("Density",density);
        scale=Slider("Scale",scale); flow=Slider("Flow",flow); palette=Slider("Palette",palette); audioAmount=Slider("Audio response",audioAmount);
        GUILayout.Label("1 / 2 / 3   intro / verse / chorus");
        GUILayout.Label("T tap tempo   Up/Down energy   Space audio");
        GUILayout.Label("F freeze   B blackout   R reset   H hide UI");
        GUILayout.EndArea();
    }
    float Slider(string label,float value) { GUILayout.Label(label); return GUILayout.HorizontalSlider(value,0,1); }
    void OnDestroy() { if(runtimeMaterial) Destroy(runtimeMaterial); }
}}
