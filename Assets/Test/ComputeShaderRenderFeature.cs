using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class ComputeShaderRenderFeature : ScriptableRendererFeature
{
    class ComputeShaderPass : ScriptableRenderPass
    {
        private ComputeShader computeShader;
        private RenderTexture resultTexture;
        private int kernelHandle;

        public ComputeShaderPass(ComputeShader shader)
        {
            computeShader = shader;
            renderPassEvent = RenderPassEvent.AfterRenderingTransparents; // Choose when to inject this pass
            kernelHandle = computeShader.FindKernel("CSMain");
            
            resultTexture = new RenderTexture(256, 256, 0);
            resultTexture.enableRandomWrite = true;
            resultTexture.Create();
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            computeShader.SetTexture(kernelHandle, "Result", resultTexture);
            computeShader.SetFloat("resolution",resultTexture.width);
            computeShader.Dispatch(kernelHandle, resultTexture.width / 8, resultTexture.height / 8, 1);

            CommandBuffer cmd = CommandBufferPool.Get("ComputeShaderPass");
            cmd.Blit(resultTexture, renderingData.cameraData.renderer.cameraColorTarget);
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }

    [SerializeField]
    public ComputeShader shader;

    private ComputeShaderPass computeShaderPass;

    public override void Create()
    {
        computeShaderPass = new ComputeShaderPass(shader);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(computeShaderPass);
    }
}


