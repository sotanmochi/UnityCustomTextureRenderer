using System;
using System.Threading;
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

        [System.Runtime.InteropServices.DllImport("Plasma3")]
        static extern IntPtr update_raw_texture_data(IntPtr data, int width, int height, uint frameCount);

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

        public event Action<(int TextureWidth, int TextureHeight)> OnUpdateTexture;

        MaterialPropertyBlock _prop;
        Renderer _renderer;

        SynchronizationContext _unityMainThreadContext;

        uint _frame;
        int _rendererId;
        int _currentTextureSize;
        PluginTextureRenderer _pluginTextureRenderer;

        void Start()
        {
            _prop = new MaterialPropertyBlock();
            _renderer = GetComponent<Renderer>();

            _unityMainThreadContext = SynchronizationContext.Current;

            _currentTextureSize = _textureSize switch
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

            if (_pluginType is PluginType.Type1)
            {
                var callback = Marshal.GetDelegateForFunctionPointer<IssuePluginCustomTextureUpdateCallback>(GetTextureUpdateCallback());
                _pluginTextureRenderer = new PluginTextureRenderer(callback, _currentTextureSize, _currentTextureSize);
                _rendererId = CustomTextureRenderSystem.Instance.AddRenderer(_pluginTextureRenderer);
            }
            else if (_pluginType is PluginType.Type2)
            {
                _pluginTextureRenderer = new PluginTextureRenderer(UpdateRawTextureDataCallback, _currentTextureSize, _currentTextureSize);
                _rendererId = CustomTextureRenderSystem.Instance.AddRenderer(_pluginTextureRenderer);
            }

            // Set the texture to the renderer with using a property block.
            _prop.SetTexture("_MainTex", _pluginTextureRenderer.TargetTexture);
            _renderer.SetPropertyBlock(_prop);

            OnUpdateTexture?.Invoke((_currentTextureSize, _currentTextureSize));
        }

        void Update()
        {
            _frame = (uint)(Time.time * 60);
            _pluginTextureRenderer.SetUserData(_frame);

            _currentTextureSize = _textureSize switch
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
            if (_pluginType is PluginType.Type2 
            && (_currentTextureSize != width || _currentTextureSize != height))
            {
                _unityMainThreadContext.Send(_ => 
                {
                    CustomTextureRenderSystem.Instance.RemoveRenderer((ushort)_rendererId);

                    data = _pluginTextureRenderer.CreateTextureBuffer(_currentTextureSize, _currentTextureSize);
                    width = _currentTextureSize;
                    height = _currentTextureSize;

                    _rendererId = CustomTextureRenderSystem.Instance.AddRenderer(_pluginTextureRenderer);

                    // Set the texture to the renderer with using a property block.
                    _prop.SetTexture("_MainTex", _pluginTextureRenderer.TargetTexture);
                    _renderer.SetPropertyBlock(_prop);

                    OnUpdateTexture?.Invoke((_currentTextureSize, _currentTextureSize));
                }, null);
            }

            UpdateRawTextureData(data, width, height, _frame);    // Plasma2.dll
            // update_raw_texture_data(data, width, height, _frame); // plasma3.dll
        }
    }
}
