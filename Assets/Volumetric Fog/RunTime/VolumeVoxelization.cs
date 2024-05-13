using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace URPVolumetricFog
{

    class VolumeVoxelizationPassData
    {
        public ComputeShader voxelizationCS;
        public int voxelizationKernel;
        public Vector4 resolution;
        public ShaderVariablesVolumetric volumetricCB;
        public ShaderVariablesFog m_FogCB ;
        public int viewCount;
        // public ComputeBuffer visibleVolumeBoundsBuffer;
        // public ComputeBuffer visibleVolumeDataBuffer;
    }
    class VolumeVoxelization : ScriptableRenderPass
    {
        private RTHandle m_densityBuffer;
        private VolumeVoxelizationPassData m_PassData;
        private VolumetricConfig m_Config;
        private ProfilingSampler m_ProfilingSampler;
        private VBufferParameters m_VBufferParameters;
        private Vector2[] m_xySeq;
        private Matrix4x4[] m_VBufferCoordToViewDirWS;
        private Matrix4x4 m_PixelCoordToViewDirWS;
        private int m_FrameIndex;
        private bool m_VBufferHistoryIsValid;
        public VolumeVoxelization()
        {
            renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
            m_PassData = new VolumeVoxelizationPassData();
            m_VBufferCoordToViewDirWS = new Matrix4x4[1];
            m_xySeq = new Vector2[7];
        }
        
        public void Setup(VolumetricConfig config, FogCamera fogCamera)
        {
            m_Config = config;
            m_PassData.voxelizationCS = m_Config.volumeVoxelizationCS;
            m_PassData.voxelizationKernel = m_Config.volumeVoxelizationCS.FindKernel("VolumeVoxelization");
            int frameIndex = (int)VolumetricUtils.VolumetricFrameIndex(fogCamera);
            m_FrameIndex = frameIndex;
            var currIdx = frameIndex & 1;
            var prevIdx = (frameIndex + 1) & 1;
            m_VBufferParameters = fogCamera.vBufferParams[currIdx];
            m_PassData.viewCount = 1;
            var cvp = m_VBufferParameters.viewportSize;
            m_PassData.resolution = new Vector4(cvp.x, cvp.y, 1.0f / cvp.x, 1.0f / cvp.y);
            m_ProfilingSampler = new ProfilingSampler("Volumetric Lighting");
        }
        
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            var vBufferViewportSize = m_VBufferParameters.viewportSize;
            if (m_densityBuffer == null)
            {
                m_densityBuffer=RTHandles.Alloc(vBufferViewportSize.x,
                    vBufferViewportSize.y,slices:vBufferViewportSize.z, colorFormat: GraphicsFormat.R16G16B16A16_SFloat, dimension: TextureDimension.Tex3D,enableRandomWrite: true,
                    useDynamicScale: true, name: "_VBufferDensity");
            }
           
            UpdateShaderVariableslVolumetrics(ref m_PassData.volumetricCB,renderingData.cameraData);
             ConstantBuffer.PushGlobal(cmd, m_PassData.volumetricCB, IDs.ShaderVariablesVolumetric);
             cmd.SetGlobalMatrix(IDs._PixelCoordToViewDirWS, m_PixelCoordToViewDirWS);
            UpdateFogShaderVariables(ref m_PassData.m_FogCB);
            ConstantBuffer.PushGlobal(cmd, m_PassData.m_FogCB, IDs._ShaderVariablesFog);
        }
        
        private void UpdateShaderVariableslVolumetrics(ref ShaderVariablesVolumetric cb, CameraData cameraData)
        {
            var camera = cameraData.camera;
            var vBufferViewportSize = m_VBufferParameters.viewportSize;
            var vFoV = camera.GetGateFittedFieldOfView() * Mathf.Deg2Rad;
            var unitDepthTexelSpacing = VolumetricUtils.ComputZPlaneTexelSpacing(1.0f, vFoV, vBufferViewportSize.y);

            VolumetricUtils.GetHexagonalClosePackedSpheres7(m_xySeq);
            int sampleIndex = m_FrameIndex % 7;
            var xySeqOffset = new Vector4();
            xySeqOffset.Set(m_xySeq[sampleIndex].x * m_Config.sampleOffsetWeight, m_xySeq[sampleIndex].y * m_Config.sampleOffsetWeight, VolumetricUtils.zSeq[sampleIndex], m_FrameIndex);

            VolumetricUtils.GetPixelCoordToViewDirWS(cameraData, new Vector4(Screen.width, Screen.height, 1f / Screen.width, 1f / Screen.height), ref m_PixelCoordToViewDirWS);
            var viewportSize = new Vector4(vBufferViewportSize.x, vBufferViewportSize.y, 1.0f / vBufferViewportSize.x, 1.0f / vBufferViewportSize.y);
            VolumetricUtils.GetPixelCoordToViewDirWS(cameraData, viewportSize, ref m_VBufferCoordToViewDirWS); 
            
            cb._VolumetricFilteringEnabled = m_Config.filterVolume ? 1u : 0u;
            cb._VBufferHistoryIsValid = (m_Config.enableReprojection && m_VBufferHistoryIsValid) ? 1u : 0u;
            cb._VBufferSliceCount = (uint)vBufferViewportSize.z;
            cb._VBufferAnisotropy = m_Config.anisotropy;
            cb._CornetteShanksConstant = VolumetricUtils.CornetteShanksPhasePartConstant(m_Config.anisotropy);
            cb._VBufferVoxelSize = m_VBufferParameters.voxelSize;
            cb._VBufferRcpSliceCount = 1f / vBufferViewportSize.z;
            cb._VBufferUnitDepthTexelSpacing = unitDepthTexelSpacing;
            cb._VBufferScatteringIntensity = m_Config.directionalScatteringIntensity;
            cb._VBufferLocalScatteringIntensity = m_Config.localScatteringIntensity;
            cb._VBufferLastSliceDist = m_VBufferParameters.ComputeLastSliceDistance((uint)vBufferViewportSize.z);
            cb._VBufferViewportSize = viewportSize;
            cb._VBufferLightingViewportScale = m_VBufferParameters.ComputeViewportScale(vBufferViewportSize);
            cb._VBufferLightingViewportLimit = m_VBufferParameters.ComputeViewportLimit(vBufferViewportSize);
            cb._VBufferDistanceEncodingParams = m_VBufferParameters.depthEncodingParams;
            cb._VBufferDistanceDecodingParams = m_VBufferParameters.depthDecodingParams;
            cb._VBufferSampleOffset = xySeqOffset;
            cb._VLightingRTHandleScale = Vector4.one;
            cb._VBufferCoordToViewDirWS = m_VBufferCoordToViewDirWS[0];
            
        }
        
        private void UpdateFogShaderVariables(ref ShaderVariablesFog cb)
        {
            float extinction = 1.0f / m_Config.fogAttenuationDistance;
            Vector3 scattering = extinction * (Vector3)(Vector4)m_Config.albedo;
            float layerDepth = Mathf.Max(0.01f, m_Config.maximumHeight - m_Config.baseHeight);
            float H = VolumetricUtils.ScaleHeightFromLayerDepth(layerDepth);
            Vector2 heightFogExponents = new Vector2(1.0f / H, H);

            bool useSkyColor = m_Config.colorMode == FogColorMode.SkyColor;
            
            cb._FogEnabled = m_Config.enabled ? 1u : 0u;
            cb._EnableVolumetricFog = m_Config.volumetricLighting ? 1u : 0u;
            cb._FogColorMode = useSkyColor ? 1u : 0u;
            cb._MaxEnvCubemapMip = (uint)VolumetricUtils.CalculateMaxEnvCubemapMip();
            cb._FogColor = useSkyColor ? m_Config.tint : m_Config.color;
            cb._MipFogParameters = new Vector4(m_Config.mipFogNear, m_Config.mipFogFar, m_Config.mipFogMaxMip, 0);
            cb._HeightFogParams = new Vector4(m_Config.baseHeight, extinction, heightFogExponents.x, heightFogExponents.y);
            cb._HeightFogBaseScattering = m_Config.volumetricLighting ? scattering : Vector4.one * extinction;
        }

        
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            
            var camera = renderingData.cameraData.camera;
            var cs = m_PassData.voxelizationCS;
            var cmd = CommandBufferPool.Get();
            var kernel = m_PassData.voxelizationKernel;
            using (new ProfilingScope(cmd, m_ProfilingSampler))
            {
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                var voxelSize = m_VBufferParameters.voxelSize;
                var vBufferViewportSize = m_VBufferParameters.viewportSize;
                
                // The shader defines GROUP_SIZE_1D = 8.
                int width = ((int)vBufferViewportSize.x + 7) / 8;
                int height = ((int)vBufferViewportSize.y + 7) / 8;

                
                cmd.SetComputeTextureParam(cs, kernel, IDs._VBufferDensity, m_densityBuffer);
                cmd.DispatchCompute(cs, kernel, width, height, 1);
                
                
            }
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
        
        public void Dispose()
        {
            
            m_densityBuffer?.Release();
        }

        // Cleanup any allocated resources that were created during the execution of this render pass.
        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            
            //m_densityBuffer?.Release();
        }
    }

}


