using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System;

namespace URPVolumetricFog
{
    public class VolumetricFog : ScriptableRendererFeature
    {

        public VolumetricConfig config;
        private GenerateMaxZForVolumtric m_GenerateMaxZForVolumtric;
        private VolumeVoxelization m_VolumeVoxelization;
        public FogCamera m_fogCamera;
        private static Vector3Int s_CurrentVolumetricBufferSize;
        
       
         
        public override void Create()
        {
            m_GenerateMaxZForVolumtric = new GenerateMaxZForVolumtric();
            m_VolumeVoxelization = new VolumeVoxelization();
            m_fogCamera = new FogCamera(config);
        } 

        // Here you can inject one or multiple render passes in the renderer.
        // This method is called when setting up the renderer once per-camera.
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            m_fogCamera.camera = renderingData.cameraData.camera;
            m_fogCamera.Update();
            m_GenerateMaxZForVolumtric.SetUp(config,m_fogCamera);
            m_VolumeVoxelization.Setup(config,m_fogCamera);
            
            renderer.EnqueuePass(m_GenerateMaxZForVolumtric);
            renderer.EnqueuePass(m_VolumeVoxelization);
        }
        
        protected override void Dispose(bool disposing)
        {
            m_GenerateMaxZForVolumtric.Dispose();
            m_VolumeVoxelization.Dispose();
        }
        
        
        public static void UpdateVolumetricBufferParams(FogCamera fogCamera)
        {
            var currentParams = VolumetricUtils.ComputeVolumetricBufferParameters(fogCamera.config,fogCamera.camera);
            int frameIndex = (int)VolumetricUtils.VolumetricFrameIndex(fogCamera);
            var currIdx = (frameIndex + 0) & 1;
            var prevIdx = (frameIndex + 1) & 1;
            fogCamera.vBufferParams[currIdx] = currentParams;

            // Handle case of first frame. When we are on the first frame, we reuse the value of original frame.
            if (fogCamera.vBufferParams[prevIdx].viewportSize.x == 0.0f && fogCamera.vBufferParams[prevIdx].viewportSize.y == 0.0f)
            {
                fogCamera.vBufferParams[prevIdx] = currentParams;
            }

            // Update size used to create volumetric buffers.
            s_CurrentVolumetricBufferSize = new Vector3Int(Math.Max(s_CurrentVolumetricBufferSize.x, currentParams.viewportSize.x),
                Math.Max(s_CurrentVolumetricBufferSize.y, currentParams.viewportSize.y),
                Math.Max(s_CurrentVolumetricBufferSize.z, currentParams.viewportSize.z));
            
        }
    }
}


