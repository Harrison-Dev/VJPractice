using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VJPractice;

[InitializeOnLoad]
public static class PracticeSmoke {
    static int frames;
    static PracticeSmoke() { EditorApplication.update += Tick; }
    public static void Run() {
        SessionState.SetBool("VJSmoke",true);
        EditorSceneManager.OpenScene("Assets/VJ/Scenes/01_GeometryPractice.unity");
        EditorApplication.EnterPlaymode();
    }
    static void Tick() {
        if(!SessionState.GetBool("VJSmoke",false) || !EditorApplication.isPlaying) return;
        frames++;
        if(frames < 180) return;
        var instrument=Object.FindFirstObjectByType<VJInstrument>();
        bool ok=instrument != null && instrument.VisualTime > 0 && instrument.visual.shader.isSupported;
        if(instrument != null) {
            foreach(var message in ShaderUtil.GetShaderMessages(instrument.visual.shader)) {
                if(message.severity.ToString()=="Error") { Debug.LogError(message.message); ok=false; }
            }
        }
        Debug.Log(ok ? "VJ_PLAY_SMOKE_OK: animation running, shader supported, no shader errors" : "VJ_PLAY_SMOKE_FAILED");
        SessionState.SetBool("VJSmoke",false);
        EditorApplication.Exit(ok?0:1);
    }
}
