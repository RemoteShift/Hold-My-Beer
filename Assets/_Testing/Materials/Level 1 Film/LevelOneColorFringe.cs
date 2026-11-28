using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace HoldMyBeer.Rendering
{
    public sealed class LevelOneColorFringe : FullScreenPassRendererFeature
    {
        private const string LevelPath = "Assets/_Testing/Scenes/Level 1.unity";

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var cameraData = renderingData.cameraData;
            if (!cameraData.postProcessEnabled
                || cameraData.cameraType != CameraType.Game
                || cameraData.renderType != CameraRenderType.Base
                || (!cameraData.camera.CompareTag("MainCamera")
                    && !cameraData.camera.GetComponentInParent<BottleResultsScreen>()
                    && !cameraData.camera.GetComponentInParent<PauseCardMenu>()
                    && !cameraData.camera.GetComponentInParent<FilmCursor>())
                || SceneManager.GetActiveScene().path != LevelPath)
            {
                return;
            }

            base.AddRenderPasses(renderer, ref renderingData);
        }
    }
}



