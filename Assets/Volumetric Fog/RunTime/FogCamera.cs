using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace URPVolumetricFog
{
    public class FogCamera
    {
        internal uint cameraFrameCount ;
        internal VBufferParameters[] vBufferParams;
        public VolumetricConfig config;
        public Camera camera;
        
        public FogCamera(VolumetricConfig config)
        {
            this.cameraFrameCount = 0;
            this.config = config;
            this.camera = new Camera();
            this.vBufferParams = new VBufferParameters[2]; 
        }
        internal void Update()
        {
            cameraFrameCount++;
            VolumetricFog.UpdateVolumetricBufferParams(this);
        }
        
        // internal bool IsVolumetricReprojectionEnabled()
        // {
        //     bool a = Fog.IsVolumetricFogEnabled(this);
        //     // We only enable volumetric re projection if we are processing the game view or a scene view with animated materials on
        //     bool b = camera.cameraType == CameraType.Game || (camera.cameraType == CameraType.SceneView && CoreUtils.AreAnimatedMaterialsEnabled(camera));
        //     bool c = frameSettings.IsEnabled(FrameSettingsField.ReprojectionForVolumetrics);
        //
        //     return a && b && c;
        // }
    }
}
