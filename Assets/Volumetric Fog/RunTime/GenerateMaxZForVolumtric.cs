using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Experimental.Rendering;
namespace URPVolumetricFog
{
    
    class GenerateMaxZForVolumtricPassData
    {
        public ComputeShader generateMaxZCS;
        public int maxZKernel;
        public int maxZDownsampleKernel;
        public int dilateMaxZKernel;
        public Vector2Int intermediateMaskSize;
        public Vector2Int finalMaskSize;
        public Vector2Int minDepthMipOffset;

        public float dilationWidth;
        public int viewCount;
    }
    class GenerateMaxZForVolumtric : ScriptableRenderPass
    {
        private GenerateMaxZForVolumtricPassData m_PassData;
        private RTHandle m_MaxZ8xBufferHandle;
        private RTHandle m_MaxZBufferHandle;
        private RTHandle m_DilatedMaxZBufferHandle;
        private VBufferParameters m_VBufferParameters;
        private ProfilingSampler m_ProfilingSampler;

        public GenerateMaxZForVolumtric()
        {
            renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
            m_PassData = new GenerateMaxZForVolumtricPassData();
        }

        public void SetUp(VolumetricConfig config, FogCamera fogCamera)
        {
            m_PassData.generateMaxZCS = config.generateMaxZCS;
            m_PassData.maxZKernel = m_PassData.generateMaxZCS.FindKernel("ComputeMaxZ");
            m_PassData.maxZDownsampleKernel = m_PassData.generateMaxZCS.FindKernel("ComputeFinalMask");
            m_PassData.dilateMaxZKernel = m_PassData.generateMaxZCS.FindKernel("DilateMask");
            int frameIndex = (int)VolumetricUtils.VolumetricFrameIndex(fogCamera);
            var currIdx = frameIndex & 1;
            m_VBufferParameters = fogCamera.vBufferParams[currIdx];
            m_ProfilingSampler = new ProfilingSampler("Generate MaxZ For Volumtric");
        }
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            var camera = renderingData.cameraData.camera;
            m_PassData.intermediateMaskSize.x = VolumetricUtils.DivRoundUp(camera.scaledPixelWidth,8);
            m_PassData.intermediateMaskSize.y = VolumetricUtils.DivRoundUp(camera.scaledPixelHeight,8);
            m_PassData.finalMaskSize.x = m_PassData.intermediateMaskSize.x / 2;
            m_PassData.finalMaskSize.y = m_PassData.intermediateMaskSize.y / 2;
            Vector2Int ViewportSize = new Vector2Int();
            ViewportSize.x = camera.pixelWidth;
            ViewportSize.y = camera.pixelHeight;
            PackedMipChainInfo depthMipInfo = new PackedMipChainInfo();
            depthMipInfo.Allocate();
            depthMipInfo.ComputePackedMipChainInfo(ViewportSize);
            m_PassData.minDepthMipOffset.x=depthMipInfo.mipLevelOffsets[4].x;
            m_PassData.minDepthMipOffset.y = depthMipInfo.mipLevelOffsets[4].y;
           
            var currentParams = m_VBufferParameters;
            float ratio = (float)currentParams.viewportSize.x / (float)camera.scaledPixelWidth;
            m_PassData.dilationWidth = ratio < 0.1f ? 2 : ratio < 0.5f ? 1 : 0;
            m_PassData.viewCount = 1;

            // var desc = new RenderTextureDescriptor(Mathf.CeilToInt(Screen.width / 8), Mathf.CeilToInt(Screen.height / 8),RenderTextureFormat.RFloat, 0);
            // desc.dimension = TextureDimension.Tex2D;
            // desc.useDynamicScale = true;
            // desc.enableRandomWrite = true;
            if (m_MaxZ8xBufferHandle==null)
            {
                m_MaxZ8xBufferHandle = RTHandles.Alloc(Mathf.CeilToInt(Screen.width / 8),
                    Mathf.CeilToInt(Screen.height / 8), colorFormat: GraphicsFormat.R32_SFloat, dimension: TextureDimension.Tex2D,enableRandomWrite: true,
                    useDynamicScale: true, name: "MaxZ mask 8x");
            }
            if (m_MaxZBufferHandle==null)
            {
                m_MaxZBufferHandle = RTHandles.Alloc(Mathf.CeilToInt(Screen.width / 8), Mathf.CeilToInt(Screen.height / 8),colorFormat: GraphicsFormat.R32_SFloat,
                    dimension: TextureDimension.Tex2D, enableRandomWrite: true, useDynamicScale: true, name: "MaxZ mask");
            }
            
            if (m_DilatedMaxZBufferHandle==null)
            {
                m_DilatedMaxZBufferHandle = RTHandles.Alloc(Mathf.CeilToInt(Screen.width / 16),
                    Mathf.CeilToInt(Screen.height / 16), colorFormat: GraphicsFormat.R32_SFloat,dimension: TextureDimension.Tex2D, enableRandomWrite: true,
                    useDynamicScale: true, name: "Dilated MaxZ mask");
            }
           
        }

        public static bool ReAllocateIfNeeded(ref RTHandle handle, int width, int height, int slices,
            GraphicsFormat colorFormat, FilterMode filterMode, TextureWrapMode wrapMode, TextureDimension dimension,
            bool enableRandomWrite, bool useDynamicScale)
        {
            return false;
        }
        public void Dispose()
        {
            Debug.Log("11");
            m_MaxZ8xBufferHandle?.Release();
            m_MaxZBufferHandle?.Release();
            m_DilatedMaxZBufferHandle?.Release();
        }
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            var data = m_PassData;
            var cs = data.generateMaxZCS;
            if (cs == null)
                return;
            var cmd=CommandBufferPool.Get();

            using (new UnityEngine.Rendering.ProfilingScope(cmd, m_ProfilingSampler))
            {
                var kernel = data.maxZKernel;
                int maskW = data.intermediateMaskSize.x;
                int maskH = data.intermediateMaskSize.y;

                int dispatchX = maskW;
                int dispatchY = maskH;
                cmd.SetComputeTextureParam(cs, kernel, IDs._OutputTexture, m_MaxZ8xBufferHandle);
                cmd.DispatchCompute(cs,kernel,dispatchX,dispatchY,data.viewCount);

                kernel = data.maxZDownsampleKernel;
                cmd.SetComputeTextureParam(cs, kernel, IDs._InputTexture, m_MaxZ8xBufferHandle);
                cmd.SetComputeTextureParam(cs, kernel, IDs._OutputTexture, m_MaxZBufferHandle);
                
                Vector4 srcLimitAndDepthOffset = new Vector4(
                    maskW,
                    maskH,
                    data.minDepthMipOffset.x,
                    data.minDepthMipOffset.y
                );
                
                cmd.SetComputeVectorParam(cs, IDs._SrcOffsetAndLimit, srcLimitAndDepthOffset);
                cmd.SetComputeFloatParam(cs, IDs._DilationWidth, data.dilationWidth);
                
                int finalMaskW = Mathf.CeilToInt(maskW / 2.0f);
                int finalMaskH = Mathf.CeilToInt(maskH / 2.0f);

                dispatchX = VolumetricUtils.DivRoundUp(finalMaskW, 8);
                dispatchY = VolumetricUtils.DivRoundUp(finalMaskH, 8);
                cmd.DispatchCompute(cs,kernel,dispatchX,dispatchY,data.viewCount);
                
                kernel = data.dilateMaxZKernel;
                cmd.SetComputeTextureParam(cs, kernel, IDs._InputTexture, m_MaxZBufferHandle);
                cmd.SetComputeTextureParam(cs, kernel, IDs._OutputTexture, m_DilatedMaxZBufferHandle);
                srcLimitAndDepthOffset.x = finalMaskW;
                srcLimitAndDepthOffset.y = finalMaskH;
                cmd.SetComputeVectorParam(cs, IDs._SrcOffsetAndLimit, srcLimitAndDepthOffset);
                cmd.DispatchCompute(cs,kernel,dispatchX,dispatchY,data.viewCount);
                
                //cmd.Blit(m_DilatedMaxZBufferHandle, renderingData.cameraData.renderer.cameraColorTarget);
                cmd.SetGlobalTexture(IDs._MaxZMaskTexture, m_DilatedMaxZBufferHandle);
            }
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
        
        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            // m_MaxZ8xBufferHandle?.Release();
            // m_MaxZBufferHandle?.Release();
            // m_DilatedMaxZBufferHandle?.Release();
            
        }
    }
}
    



