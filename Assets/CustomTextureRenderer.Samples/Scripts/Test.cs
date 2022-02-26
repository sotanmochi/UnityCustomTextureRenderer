using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace UnityCustomTextureRenderer.Samples
{
    public class Test : MonoBehaviour
    {
#if PLATFORM_IOS
        [System.Runtime.InteropServices.DllImport("__Internal")]
#else
        [System.Runtime.InteropServices.DllImport("Plasma")]
#endif
        static extern IntPtr GetTextureUpdateCallback();

        [System.Runtime.InteropServices.DllImport("Plasma2")]
        static extern IntPtr UpdateRawTextureData(IntPtr data, int width, int height, uint frameCount);

        enum TextureSize
        {
            _64x64,
            _128x128,
            _256x256,
            _512x512,
            _1024x1024,
            _2048x2048,
            _4096x4096,
        }

        enum PluginType
        {
            Type1,
            Type2
        }

        [SerializeField] TextureSize _textureSize;
        [SerializeField] PluginType _pluginType;

        public event Action<(int TextureWidth, int TextureHeight)> OnInitialized;

        uint _frame;

        Texture2D _texture;
        PluginTextureRenderer _pluginTextureRenderer;

        void Start()
        {
            var size = _textureSize switch
            {
                TextureSize._64x64     => 64,
                TextureSize._128x128   => 128,
                TextureSize._256x256   => 256,
                TextureSize._512x512   => 512,
                TextureSize._1024x1024 => 1024,
                TextureSize._2048x2048 => 2048,
                TextureSize._4096x4096 => 4096,
                _ => 64,
            };

            _texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            _texture.wrapMode = TextureWrapMode.Clamp;

            if (_pluginType is PluginType.Type1)
            {
                var callback = Marshal.GetDelegateForFunctionPointer<IssuePluginCustomTextureUpdateCallback>(GetTextureUpdateCallback());
                _pluginTextureRenderer = new PluginTextureRenderer(callback, _texture);
                CustomTextureRenderSystem.Instance.AddRenderer(_pluginTextureRenderer);
            }
            else if (_pluginType is PluginType.Type2)
            {
                _pluginTextureRenderer = new PluginTextureRenderer(UpdateRawTextureDataCallback, _texture);
                CustomTextureRenderSystem.Instance.AddRenderer(_pluginTextureRenderer);
            }

            // Set the texture to the renderer with using a property block.
            var prop = new MaterialPropertyBlock();
            prop.SetTexture("_MainTex", _texture);
            GetComponent<Renderer>().SetPropertyBlock(prop);

            OnInitialized?.Invoke((size, size));
        }

        void OnDestroy()
        {
            Destroy(_texture);
        }

        void Update()
        {
            _frame = (uint)(Time.time * 60);
            _pluginTextureRenderer.SetUserData(_frame);

            // Rotation
            transform.eulerAngles = new Vector3(10, 20, 30) * Time.time;
        }

        /// <summary>
        /// This function runs on Unity's Render Thread or another thread.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="frameCount"></param>
        void UpdateRawTextureDataCallback(IntPtr data, int width, int height, int bytesPerPixel)
        {
            UpdateRawTextureData(data, width, height, _frame);
        }
    }
}
