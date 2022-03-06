using UnityEngine;

namespace UnityCustomTextureRenderer.Samples
{
    public sealed class TestPresenter : MonoBehaviour
    {
        enum TargetFrameRate
        {
            None,
            FPS10,
            FPS30,
            FPS60,
        }

        [SerializeField] TargetFrameRate _targetFrameRate;
        [SerializeField] Test _test;
        [SerializeField] TestUIView _uiView;

        FPSCounter _fpsCounter = new FPSCounter(10);

        void Awake()
        {
            UnityEngine.QualitySettings.vSyncCount = 0;

            var fps = _targetFrameRate switch
            {
                TargetFrameRate.FPS10 => 10,
                TargetFrameRate.FPS30 => 30,
                TargetFrameRate.FPS60 => 60,
                _ => -1,
            };
            UnityEngine.Application.targetFrameRate = fps;

            _uiView.SetGraphicsAPI(SystemInfo.graphicsDeviceType.ToString());

            _test.OnUpdateTexture += (values) =>
            {
                var title = nameof(UnityCustomTextureRenderer.CustomTextureRenderSystem);
                _uiView.SetTitle(title);
                _uiView.SetTextureSize(values.TextureWidth, values.TextureHeight);
            };
        }

        void Update()
        {
            _fpsCounter.Update();
            _uiView.SetFrameRate(_fpsCounter.FPS);
        }
    }
}