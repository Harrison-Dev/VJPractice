using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Input;
using UnityEngine;
using UnityEngine.Rendering;
using VJPractice.Stage;

/// <summary>
/// Editor-only capture of the actual VJ output (without the operator console) and
/// audio played by Unity. The stable mirror also follows the stage's blackout RT.
/// </summary>
[InitializeOnLoad]
public static class VJStageRecorder
{
    static RecorderController controller;
    static RecorderControllerSettings controllerSettings;
    static MovieRecorderSettings movieSettings;
    static RenderTexture mirror;
    static VJStage stage;
    static string outputPath;

    static VJStageRecorder()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        AssemblyReloadEvents.beforeAssemblyReload += Stop;
    }

    [MenuItem("VJ Practice/Recording/Start VJ + Local Music")]
    public static void Start()
    {
        if (!EditorApplication.isPlaying || EditorApplication.isPaused)
        {
            EditorUtility.DisplayDialog("VJ 錄影", "先進入 Play Mode，並播放本地音樂。", "了解");
            return;
        }
        if (controller != null) return;

        stage = UnityEngine.Object.FindFirstObjectByType<VJStage>();
        if (!stage || !stage.KineticOutput || !stage.KineticReady)
        {
            EditorUtility.DisplayDialog("VJ 錄影", "找不到可錄製的 VJ 最終畫面。請先開啟 VJ Stage。", "了解");
            stage = null;
            return;
        }
        if (!stage.Audio || stage.Audio.External || !stage.Audio.Source || !stage.Audio.Source.isPlaying)
        {
            EditorUtility.DisplayDialog("VJ 錄影", "目前沒有透過 Unity 播放的本地音樂。請先在音源頁播放本地歌曲；Spotify 和外部輸入不會被這個錄影流程收進影片。", "了解");
            stage = null;
            return;
        }

        try
        {
            var source = stage.KineticOutput;
            mirror = new RenderTexture(source.width, source.height, 0, RenderTextureFormat.ARGB32)
            {
                name = "VJ Recorder final output",
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear
            };
            if (!mirror.Create()) throw new InvalidOperationException("無法建立錄影畫面貼圖。");
            Graphics.Blit(source, mirror);

            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Recordings"));
            Directory.CreateDirectory(directory);
            outputPath = Path.Combine(directory, "Nightflight-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));

            controllerSettings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
            controllerSettings.SetRecordModeToManual();
            controllerSettings.FrameRate = 30;
            controllerSettings.CapFrameRate = false;
            controllerSettings.ExitPlayMode = false;

            movieSettings = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            movieSettings.name = "Nightflight VJ + Unity audio";
            movieSettings.Enabled = true;
            movieSettings.OutputFile = outputPath;
            movieSettings.ImageInputSettings = new RenderTextureInputSettings { RenderTexture = mirror };
            movieSettings.AudioInputSettings.PreserveAudio = true;
#pragma warning disable CS0618
            movieSettings.OutputFormat = MovieRecorderSettings.VideoRecorderOutputFormat.MP4;
#pragma warning restore CS0618

            controllerSettings.AddRecorderSettings(movieSettings);
            controller = new RecorderController(controllerSettings);
            // StageCompositor subscribed to this event at Play start. This later
            // subscription copies its completed output, including current blackout.
            RenderPipelineManager.endContextRendering += MirrorFinalOutput;
            controller.PrepareRecording();
            if (!controller.StartRecording())
                throw new InvalidOperationException("Unity Recorder 無法開始；請查看 Console 的詳細錯誤。");

            Debug.Log("VJ_RECORDING_STARTED " + outputPath + ".mp4");
        }
        catch (Exception ex)
        {
            Stop();
            EditorUtility.DisplayDialog("VJ 錄影失敗", ex.Message, "了解");
            Debug.LogException(ex);
        }
    }

    [MenuItem("VJ Practice/Recording/Start VJ + Local Music", true)]
    static bool ValidateStart() => controller == null;

    [MenuItem("VJ Practice/Recording/Stop Recording")]
    public static void Stop()
    {
        RenderPipelineManager.endContextRendering -= MirrorFinalOutput;
        try
        {
            if (controller != null)
            {
                controller.StopRecording();
                Debug.Log("VJ_RECORDING_SAVED " + outputPath + ".mp4");
            }
        }
        catch (Exception ex) { Debug.LogException(ex); }
        finally
        {
            controller = null;
            stage = null;
            if (movieSettings) UnityEngine.Object.DestroyImmediate(movieSettings);
            if (controllerSettings) UnityEngine.Object.DestroyImmediate(controllerSettings);
            movieSettings = null;
            controllerSettings = null;
            if (mirror) { mirror.Release(); UnityEngine.Object.DestroyImmediate(mirror); }
            mirror = null;
            outputPath = null;
        }
    }

    [MenuItem("VJ Practice/Recording/Stop Recording", true)]
    static bool ValidateStop() => controller != null;

    static void MirrorFinalOutput(ScriptableRenderContext context, List<Camera> cameras)
    {
        if (stage && mirror && stage.KineticOutput)
            Graphics.Blit(stage.KineticOutput, mirror);
    }

    static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingPlayMode) Stop();
    }
}
