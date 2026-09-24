using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace VJPractice.Stage.Motion
{
    /// <summary>Captures the normal URP camera, including its native Canvas and particles, without Camera.Render().</summary>
    public sealed class StageCompositor : IDisposable
    {
        readonly Camera camera;
        readonly Camera displayCamera;
        readonly Func<bool> frozen, blackout;
        readonly RenderTexture originalTarget;
        readonly Rect originalRect;
        RenderTexture live, presented, black;
        bool active, hasFrame;
        public RenderTexture Output => active ? (blackout() ? black : presented) : null;
        public bool HasFrame => hasFrame;

        public StageCompositor(Camera camera, Func<bool> frozen, Func<bool> blackout)
        {
            this.camera = camera ? camera : throw new ArgumentNullException(nameof(camera));
            this.frozen = frozen; this.blackout = blackout;
            originalTarget = camera.targetTexture; originalRect = camera.rect;
            // The stage camera renders only to a texture in kinetic mode. Keep a
            // display camera alive so the Editor and Player have a real backbuffer
            // behind the IMGUI operator console instead of "No cameras rendering".
            var display = new GameObject("Kinetic display clear", typeof(Camera));
            display.hideFlags = HideFlags.HideInHierarchy;
            displayCamera = display.GetComponent<Camera>();
            displayCamera.enabled = false;
            displayCamera.cullingMask = 0;
            displayCamera.clearFlags = CameraClearFlags.SolidColor;
            displayCamera.backgroundColor = Color.black;
            displayCamera.depth = camera.depth - 1;
            displayCamera.targetDisplay = camera.targetDisplay;
            RenderPipelineManager.endContextRendering += EndContext;
        }

        public void Prepare(bool enabled, int width, int height)
        {
            if (!enabled)
            {
                if (active && camera) { camera.targetTexture = originalTarget; camera.rect = originalRect; }
                if (displayCamera) displayCamera.enabled = false;
                active = false; return;
            }
            width = Mathf.Clamp(width, 320, 1920); height = Mathf.Clamp(height, 180, 1080);
            if (!live || live.width != width || live.height != height)
            {
                if (camera) camera.targetTexture = originalTarget;
                Release();
                live = Make("Kinetic live", width, height, 24);
                presented = Make("Kinetic final", width, height, 0);
                black = Make("Kinetic blackout", width, height, 0);
                hasFrame = false;
            }
            active = true;
            if (displayCamera) displayCamera.enabled = true;
            camera.targetTexture = live; camera.rect = new Rect(0, 0, 1, 1);
        }

        void EndContext(ScriptableRenderContext context, List<Camera> cameras)
        {
            if (!active || !live || !presented || (frozen() && hasFrame)) return;
            // URP has submitted its camera commands by this point. Scheduling another
            // command on that ScriptableRenderContext leaves the presented RT black.
            var previous = RenderTexture.active;
            try { Graphics.Blit(live, presented); }
            finally { RenderTexture.active = previous; }
            hasFrame = true;
        }

        static RenderTexture Make(string name, int width, int height, int depth)
        {
            var rt = new RenderTexture(width, height, depth, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default)
            { name = name, filterMode = FilterMode.Bilinear, useMipMap = false, autoGenerateMips = false };
            if (!rt.Create()) { UnityEngine.Object.Destroy(rt); throw new InvalidOperationException("Cannot allocate " + name); }
            var previous = RenderTexture.active;
            try { RenderTexture.active = rt; GL.Clear(true, true, Color.black); }
            finally { RenderTexture.active = previous; }
            return rt;
        }

        void Release()
        {
            foreach (RenderTexture rt in new[] { live, presented, black })
                if (rt) { rt.Release(); UnityEngine.Object.Destroy(rt); }
            live = presented = black = null;
        }

        public void Dispose()
        {
            RenderPipelineManager.endContextRendering -= EndContext;
            if (camera) { camera.targetTexture = originalTarget; camera.rect = originalRect; }
            if (displayCamera) UnityEngine.Object.Destroy(displayCamera.gameObject);
            Release(); active = false;
        }
    }
}
