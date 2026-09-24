using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace VJPractice.Stage.Motion
{
    /// <summary>Captures the normal URP camera, including its native Canvas and particles, without Camera.Render().</summary>
    public sealed class StageCompositor : IDisposable
    {
        readonly Camera camera;
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
            RenderPipelineManager.endCameraRendering += EndCamera;
        }

        public void Prepare(bool enabled, int width, int height)
        {
            if (!enabled)
            {
                if (active && camera) { camera.targetTexture = originalTarget; camera.rect = originalRect; }
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
            camera.targetTexture = live; camera.rect = new Rect(0, 0, 1, 1);
        }

        void EndCamera(ScriptableRenderContext context, Camera renderedCamera)
        {
            if (!active || renderedCamera != camera || !live || !presented || (frozen() && hasFrame)) return;
            var command = CommandBufferPool.Get("Nightflight final output");
            try { command.Blit(live, presented); context.ExecuteCommandBuffer(command); hasFrame = true; }
            finally { CommandBufferPool.Release(command); }
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
            RenderPipelineManager.endCameraRendering -= EndCamera;
            if (camera) { camera.targetTexture = originalTarget; camera.rect = originalRect; }
            Release(); active = false;
        }
    }
}
