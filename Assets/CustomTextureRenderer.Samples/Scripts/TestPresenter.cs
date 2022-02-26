using UnityEngine;

namespace UnityCustomTextureRenderer.Samples
{
    public sealed class TestPresenter : MonoBehaviour
    {
        [SerializeField] Test _test;
        [SerializeField] TestUIView _uiView;

        FPSCounter _fpsCounter = new FPSCounter(10);

        void Awake()
        {
            _uiView.SetGraphicsAPI(SystemInfo.graphicsDeviceType.ToString());

            _test.OnInitialized += (values) =>
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