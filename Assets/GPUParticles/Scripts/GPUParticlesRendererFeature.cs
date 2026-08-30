using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections.Generic;

namespace GPUParticles
{
    public class GPUParticlesRendererFeature : ScriptableRendererFeature
    {
        class Pass : ScriptableRenderPass
        {
            static readonly ProfilingSampler s_ProfilingSim = new ProfilingSampler("GPUParticles.Simulate");
            static readonly ProfilingSampler s_ProfilingDraw = new ProfilingSampler("GPUParticles.Draw");

            // Cache camera attachments for restoring after MRT simulation.
            RTHandle m_CameraColor;
            RTHandle m_CameraDepth;

            public void SetRequiresDepthTexture(bool required)
            {
                ConfigureInput(required
                    ? ScriptableRenderPassInput.Depth
                    : ScriptableRenderPassInput.None);
            }

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                // URP 14: use RTHandles for camera targets
                m_CameraColor = renderingData.cameraData.renderer.cameraColorTargetHandle;
                m_CameraDepth = renderingData.cameraData.renderer.cameraDepthTargetHandle;

                // Inform URP which attachments this pass will render to by default
                // (we may temporarily bind MRTs during simulation).
                ConfigureTarget(m_CameraColor, m_CameraDepth);
                ConfigureClear(ClearFlag.None, Color.clear);
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (GPUParticleSystem.Active.Count == 0) return;

                var cmd = CommandBufferPool.Get("GPUParticles");
                using (new ProfilingScope(cmd, s_ProfilingSim))
                {
                    foreach (var sys in GPUParticleSystem.Active)
                    {
                        if (sys == null || !sys.isActiveAndEnabled) continue;
                        sys.Simulate(cmd, renderingData.cameraData.camera);
                    }
                }

                // Restore camera render target and viewport before issuing any draw calls.
                CoreUtils.SetRenderTarget(cmd, m_CameraColor, m_CameraDepth);
                cmd.SetViewport(renderingData.cameraData.camera.pixelRect);

                using (new ProfilingScope(cmd, s_ProfilingDraw))
                {
                    foreach (var sys in GPUParticleSystem.Active)
                    {
                        if (sys == null || !sys.isActiveAndEnabled) continue;
                        sys.Render(cmd, renderingData.cameraData.camera);
                    }
                }
                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }
        }

        Pass m_Pass;

        public override void Create()
        {
            m_Pass = new Pass();
            // Simulate early so particles are ready before transparents
            m_Pass.renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            bool requiresDepthTexture = false;
            foreach (var system in GPUParticleSystem.Active)
            {
                if (system != null &&
                    system.isActiveAndEnabled &&
                    system.materialSoftParticles)
                {
                    requiresDepthTexture = true;
                    break;
                }
            }
            m_Pass.SetRequiresDepthTexture(requiresDepthTexture);
            renderer.EnqueuePass(m_Pass);
        }
    }
}
