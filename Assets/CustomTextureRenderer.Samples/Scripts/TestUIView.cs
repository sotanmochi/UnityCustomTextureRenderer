using UnityEngine;
using UnityEngine.UI;

namespace UnityCustomTextureRenderer.Samples
{
    public sealed class TestUIView : MonoBehaviour
    {
        [SerializeField] Text _title;
        [SerializeField] Text _textureSize;
        [SerializeField] Text _frameRate;
        [SerializeField] Text _graphicsAPI;

        public void SetTitle(string value)
        {
            _title.text = value;
        }

        public void SetTextureSize(int width, int height)
        {
            _textureSize.text = $"{width}x{height}";
        }

        public void SetFrameRate(float value)
        {
            _frameRate.text = $"{value:F2}";
        }

        public void SetGraphicsAPI(string value)
        {
            _graphicsAPI.text = value;
        }
    }
}