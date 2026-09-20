using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using VJPractice.Stage;
[InitializeOnLoad]
public static class StageSmoke {
    static int frames,errors;static double started;
    static StageSmoke(){EditorApplication.update+=Tick;Application.logMessageReceived+=(m,s,t)=>{if(t==LogType.Error||t==LogType.Exception)errors++;};}
    public static void Run(){SessionState.SetBool("StageSmoke",true);StageSetup.Open();EditorApplication.EnterPlaymode();}
    static void Tick(){
        if(!SessionState.GetBool("StageSmoke",false)||!EditorApplication.isPlaying)return;
        if(frames++==0)started=EditorApplication.timeSinceStartup;
        var stage=UnityEngine.Object.FindFirstObjectByType<VJStage>();
        if(stage==null||stage.Output==null){if(EditorApplication.timeSinceStartup-started>60)Fail("Stage did not initialize");return;}
        if(frames==90||frames==180||frames==270){
            int index=frames/90-1;stage.SetTemplate(index);stage.Seek(8+index*8);
        }
        if(frames==150||frames==240||frames==330){
            var previous=RenderTexture.active;RenderTexture.active=stage.Output;var texture=new Texture2D(stage.Output.width,stage.Output.height,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0);texture.Apply();RenderTexture.active=previous;
            var pixels=texture.GetPixels();float min=1,max=0;foreach(var p in pixels){float v=p.grayscale;min=Mathf.Min(min,v);max=Mathf.Max(max,v);}if(max-min<.03f){Fail("Flat GPU output");return;}
            Directory.CreateDirectory("Verification");File.WriteAllBytes("Verification/template-"+stage.TemplateIndex+".png",texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);
            Debug.Log($"VJ_GPU_TEMPLATE_OK {stage.TemplateIndex} contrast={max-min:F3} particles={stage.particles.aliveParticleCount}");
        }
        if(frames<360)return;
        if(errors>0){Fail("Runtime errors: "+errors);return;}
        if(stage.Audio.Bands.w<=0){Fail("Demo audio analysis silent");return;}
        Debug.Log("VJ_STAGE_PLAY_OK / 3 GPU backgrounds + demo audio; particle rendering requires interactive Game View");SessionState.SetBool("StageSmoke",false);EditorApplication.Exit(0);
    }
    static void Fail(string m){SessionState.SetBool("StageSmoke",false);Debug.LogError("VJ_STAGE_SMOKE_FAILED "+m);EditorApplication.Exit(1);}
}
