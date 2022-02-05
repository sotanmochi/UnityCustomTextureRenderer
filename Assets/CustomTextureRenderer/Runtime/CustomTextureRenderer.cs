using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace UnityCustomTextureRenderer
{
    public sealed class CustomTextureRenderer : IDisposable
    {
        public delegate void UpdateRawTextureDataFunction(IntPtr rawTextureData, int width, int height, uint userData);

        UpdateRawTextureDataFunction _updateRawTextureDataFunction;
        Texture _targetTexture;

        byte[] _buffer;
        GCHandle _bufferHandle;
        IntPtr _bufferPtr;

        bool _disposed;

        delegate void UnityRenderingEventAndData(int eventID, IntPtr data);
        readonly UnityRenderingEventAndData _callback;

        readonly CommandBuffer _commandBuffer = new CommandBuffer();

        /// <summary>
        /// The UpdateRawTextureDataFunction will be called in Render Thread.
        /// </summary>
        /// <param name="updateRawTextureDataFunction"></param>
        /// <param name="targetTexture"></param>
        /// <param name="bytesPerPixel"></param>
        /// <param name="autoDispose"></param>
        public CustomTextureRenderer(UpdateRawTextureDataFunction updateRawTextureDataFunction, 
                                        Texture targetTexture, int bytesPerPixel = 4, bool autoDispose = true)
        {
            _updateRawTextureDataFunction = updateRawTextureDataFunction;
            _targetTexture = targetTexture;

            _callback = new UnityRenderingEventAndData(TextureUpdateCallback);

            _buffer = new byte[_targetTexture.width * _targetTexture.height * bytesPerPixel];
            _bufferHandle = GCHandle.Alloc(_buffer, GCHandleType.Pinned);
            _bufferPtr = _bufferHandle.AddrOfPinnedObject();

            if (autoDispose){ Application.quitting += Dispose; }
        }

        public void Dispose()
        {
            _disposed = false;

            _bufferPtr = IntPtr.Zero;
            _bufferHandle.Free();
            _buffer = null;

            _updateRawTextureDataFunction = null;
            _targetTexture = null;

            DebugLog($"[{nameof(CustomTextureRenderer)}] Disposed");
        }

        public void Update(uint userData = 0)
        {
            if (_disposed) { return; }

            // Request texture update via the command buffer.
            _commandBuffer.IssuePluginCustomTextureUpdateV2(
                GetTextureUpdateCallback(), _targetTexture, userData
            );
            Graphics.ExecuteCommandBuffer(_commandBuffer);
            _commandBuffer.Clear();
        }

        IntPtr GetTextureUpdateCallback()
        {
            return Marshal.GetFunctionPointerForDelegate(_callback);
        }

        /// <summary>
        /// This function is called in Render Thread.
        /// </summary>
        /// <param name="eventID"></param>
        /// <param name="data"></param>
        unsafe void TextureUpdateCallback(int eventID, IntPtr data)
        {
            if (_bufferPtr == IntPtr.Zero) { return; }

            var updateParams = (UnityRenderingExtTextureUpdateParamsV2*)data.ToPointer();

            if (eventID == (int)UnityRenderingExtEventType.kUnityRenderingExtEventUpdateTextureBeginV2)
            {
                var width = (int)updateParams->width;
                var height = (int)updateParams->height;
                var userData = updateParams->userData;

                _updateRawTextureDataFunction(_bufferPtr, width, height, userData);

                updateParams->texData = _bufferPtr.ToPointer();
            }
            else if (eventID == (int)UnityRenderingExtEventType.kUnityRenderingExtEventUpdateTextureEndV2)
            {
                updateParams->texData = null;
            }
        }

        /// <summary>
        /// Logs a message to the Unity Console 
        /// only when DEVELOPMENT_BUILD or UNITY_EDITOR is defined.
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        [
            System.Diagnostics.Conditional("DEVELOPMENT_BUILD"), 
            System.Diagnostics.Conditional("UNITY_EDITOR"),
        ]
        void DebugLog(object message)
        {
            Debug.Log(message);
        }
    }
}