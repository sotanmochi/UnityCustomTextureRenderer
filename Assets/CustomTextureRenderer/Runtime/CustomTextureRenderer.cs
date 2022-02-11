using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

#if DEVELOPMENT_BUILD || UNITY_EDITOR
using UnityEngine.Profiling;
#endif

namespace UnityCustomTextureRenderer
{
    /// <summary>
    /// A graphics utility to update textures from native plugins.
    /// The function for updating textures runs on Unity's Render Thread.
    /// </summary>
    public sealed class CustomTextureRenderer : IDisposable
    {
        UpdateRawTextureDataFunction _updateRawTextureDataFunction;

        Texture2D _targetTexture;
        int _textureWidth;
        int _textureHeight;
        int _bytesPerPixel;

        bool _disposed;

        uint[] _buffer;
        GCHandle _bufferHandle;
        IntPtr _bufferPtr;

        delegate void UnityRenderingEventAndData(int eventID, IntPtr data);
        readonly UnityRenderingEventAndData _callback;
        readonly CommandBuffer _commandBuffer = new CommandBuffer()
        {
            name = "CustomTextureRenderer.IssuePluginCustomTextureUpdateV2"
        };

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        CustomSampler _updateRawTextureDataFunctionSampler;
#endif

        /// <summary>
        /// The UpdateRawTextureDataFunction runs on Unity's Render Thread.
        /// </summary>
        /// <param name="updateRawTextureDataFunction"></param>
        /// <param name="targetTexture"></param>
        /// <param name="bytesPerPixel"></param>
        /// <param name="autoDispose"></param>
        public CustomTextureRenderer(UpdateRawTextureDataFunction updateRawTextureDataFunction, 
                                        Texture2D targetTexture, int bytesPerPixel = 4, bool autoDispose = true)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            _updateRawTextureDataFunctionSampler = CustomSampler.Create("UpdateRawTextureDataFunction");
#endif

            if (targetTexture.format != TextureFormat.RGBA32)
            {
                _disposed = true;
                DebugLogError($"[{nameof(NonBlockingCustomTextureRenderer)}] Unsupported texture format: {targetTexture.format}");
                return;
            }

            if (autoDispose){ Application.quitting += Dispose; }

            _updateRawTextureDataFunction = updateRawTextureDataFunction;
            _callback = new UnityRenderingEventAndData(TextureUpdateCallback);

            _targetTexture = targetTexture;
            _textureWidth = targetTexture.width;
            _textureHeight = targetTexture.height;
            _bytesPerPixel = bytesPerPixel;

            _buffer = new uint[_targetTexture.width * _targetTexture.height];
            _bufferHandle = GCHandle.Alloc(_buffer, GCHandleType.Pinned);
            _bufferPtr = _bufferHandle.AddrOfPinnedObject();
        }

        public void Dispose()
        {
            _disposed = true;

            _bufferPtr = IntPtr.Zero;
            _bufferHandle.Free();
            _buffer = null;

            _updateRawTextureDataFunction = null;
            _targetTexture = null;

            DebugLog($"[{nameof(CustomTextureRenderer)}] Disposed");
        }

        public void Update()
        {
            if (_disposed) { return; }

            // Request texture update via the command buffer.
            _commandBuffer.IssuePluginCustomTextureUpdateV2(GetTextureUpdateCallback(), _targetTexture, 0);
            Graphics.ExecuteCommandBuffer(_commandBuffer);
            _commandBuffer.Clear();
        }

        /// <summary>
        /// This function runs on Unity's Render Thread.
        /// </summary>
        /// <param name="eventID"></param>
        /// <param name="data"></param>
        unsafe void TextureUpdateCallback(int eventID, IntPtr data)
        {
            if (_bufferPtr == IntPtr.Zero) { return; }

            var updateParams = (UnityRenderingExtTextureUpdateParamsV2*)data.ToPointer();

            if (eventID == (int)UnityRenderingExtEventType.kUnityRenderingExtEventUpdateTextureBeginV2)
            {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                _updateRawTextureDataFunctionSampler.Begin();
#endif

                _updateRawTextureDataFunction?.Invoke(_bufferPtr, _textureWidth, _textureHeight, _bytesPerPixel);

#if DEVELOPMENT_BUILD || UNITY_EDITOR
                _updateRawTextureDataFunctionSampler.End();
#endif

                updateParams->texData = _bufferPtr.ToPointer();
            }
            else if (eventID == (int)UnityRenderingExtEventType.kUnityRenderingExtEventUpdateTextureEndV2)
            {
                updateParams->texData = null;
            }
        }

        IntPtr GetTextureUpdateCallback()
        {
            return Marshal.GetFunctionPointerForDelegate(_callback);
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
        static void DebugLogError(object message)
        {
            UnityEngine.Debug.LogError(message);
        }
    }
}