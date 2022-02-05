using System;
using UnityEngine;

namespace UnityCustomTextureRenderer.Samples
{
    public class Test : MonoBehaviour
    {
        [System.Runtime.InteropServices.DllImport("Plasma")]
        static extern IntPtr UpdateRawTextureData(IntPtr data, int width, int height, uint frameCount);

        Texture2D _texture;
        CustomTextureRenderer _customTextureRenderer;

        void Start()
        {
            _texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            _texture.wrapMode = TextureWrapMode.Clamp;

            _customTextureRenderer = new CustomTextureRenderer(UpdateRawTextureDataFunction, _texture);

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
            // Update texture
            _customTextureRenderer.Update((uint)(Time.time * 60));

            // Rotation
            transform.eulerAngles = new Vector3(10, 20, 30) * Time.time;
        }

        /// <summary>
        /// This function is called in Render Thread.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="frameCount"></param>
        void UpdateRawTextureDataFunction(IntPtr data, int width, int height, uint frameCount)
        {
            UpdateRawTextureData(data, width, height, frameCount);
        }
    }
}
