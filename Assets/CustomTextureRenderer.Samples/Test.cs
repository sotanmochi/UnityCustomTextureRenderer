using System;
using UnityEngine;

namespace UnityCustomTextureRenderer.Samples
{
    public class Test : MonoBehaviour
    {
        [System.Runtime.InteropServices.DllImport("Plasma")]
        static extern IntPtr UpdateRawTextureData(IntPtr data, int width, int height, uint frameCount);

        enum TextureSize
        {
            _64x64,
            _128x128,
            _256x256,
            _512x512,
            _1024x1024,
        }

        [SerializeField] TextureSize _textureSize;
        [SerializeField] bool _useAnotherThread;

        uint _frame;

        Texture2D _texture;
        CustomTextureRenderer _customTextureRenderer;
        NonBlockingCustomTextureRenderer _nonBlockingCustomTextureRenderer;

        void Start()
        {
            var size = _textureSize switch
            {
                TextureSize._64x64     => 64,
                TextureSize._128x128   => 128,
                TextureSize._256x256   => 256,
                TextureSize._512x512   => 512,
                TextureSize._1024x1024 => 1024,
                _ => 64,
            };

            _texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            _texture.wrapMode = TextureWrapMode.Clamp;

            if (_useAnotherThread)
            {
                _nonBlockingCustomTextureRenderer = new NonBlockingCustomTextureRenderer(UpdateRawTextureDataFunction, _texture);
            }
            else
            {
                _customTextureRenderer = new CustomTextureRenderer(UpdateRawTextureDataFunction, _texture);
            }

            // Set the texture to the renderer with using a property block.
            var prop = new MaterialPropertyBlock();
            prop.SetTexture("_MainTex", _texture);
            GetComponent<Renderer>().SetPropertyBlock(prop);
        }

        void OnDestroy()
        {
            Destroy(_texture);
        }

        void Update()
        {
            _frame = (uint)(Time.time * 60);

            // Update texture
            if (_useAnotherThread)
            {
                _nonBlockingCustomTextureRenderer.Update();
            }
            else
            {
                _customTextureRenderer.Update();
            }

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
        void UpdateRawTextureDataFunction(IntPtr data, int width, int height, int bytesPerPixel)
        {
            UpdateRawTextureData(data, width, height, _frame);
        }
    }
}
