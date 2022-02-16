// Copyright (c) 2022 Soichiro Sugimoto
// Licensed under the MIT License. See LICENSE in the project root for license information.

// #define DISABLE_ASYNC_GPU_UPLOAD // Run as a performance test without async GPU upload.

using System;
using System.Diagnostics;
using System.Threading;
using System.Runtime.InteropServices;
using UnityEngine;

#if DEVELOPMENT_BUILD || UNITY_EDITOR
using UnityEngine.Profiling;
#endif

namespace UnityCustomTextureRenderer
{
    /// <summary>
    /// A high performance graphics utility to update textures from native plugins. <br/>
    /// The function for updating textures runs on another thread.
    /// </summary>
    public sealed class NonBlockingCustomTextureRenderer : IDisposable
    {
        static readonly string RenderShaderName = "NonBlockingCustomTextureRenderer";
        static readonly string RawTextureDataPropertyName = "_RawTextureData";
        static readonly string TextureWidthPropertyName = "_TextureWidth";
        static readonly string TextureHeightPropertyName = "_TextureHeight";

        UpdateRawTextureDataFunction _updateRawTextureDataFunction;

        RenderTexture _targetTexture;
        readonly int _textureWidth;
        readonly int _textureHeight;
        readonly int _bytesPerPixel;

        bool _disposed;

        uint[] _currentBuffer;
        GCHandle _currentBufferHandle;
        IntPtr _currentBufferPtr;

        uint[] _nextBuffer;
        GCHandle _nextBufferHandle;
        IntPtr _nextBufferPtr;

        readonly Thread _pluginRenderThread;
        readonly CancellationTokenSource _cts;
        readonly int _targetFrameTimeMilliseconds;
        static readonly double TimestampsToTicks = TimeSpan.TicksPerSecond / (double)Stopwatch.Frequency;

        readonly Material _renderMaterial;

        readonly int _rawTextureDataPropertyId;
        readonly int _textureWidthPropertyId;
        readonly int _textureHeightPropertyId;

        ComputeBuffer _rawTextureDataComputeBuffer;

        readonly int _asyncGPUUploadCount;
        int _asyncGPUUploadFrame;

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        CustomSampler _updateRawTextureDataFunctionSampler;
        CustomSampler _asyncGPUUploadSampler;
#endif

        /// <summary>
        /// The UpdateRawTextureDataFunction runs on another thread. <br/>
        /// Asynchronous GPU upload (partial data copy) reduces the processing time per frame in the main thread for large size textures.
        /// </summary>
        /// <param name="updateRawTextureDataFunction"></param>
        /// <param name="targetTexture"></param>
        /// <param name="targetFrameRateOfPluginRenderThread"></param>
        /// <param name="asyncGPUUploadCount"></param>
        /// <param name="autoDispose"></param>
        public NonBlockingCustomTextureRenderer(UpdateRawTextureDataFunction updateRawTextureDataFunction, RenderTexture targetTexture, 
                                                int targetFrameRateOfPluginRenderThread = 60, int asyncGPUUploadCount = 1, bool autoDispose = true)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            _updateRawTextureDataFunctionSampler = CustomSampler.Create("UpdateRawTextureDataFunction");
            _asyncGPUUploadSampler = CustomSampler.Create("AsyncGPUUpload");
#endif
            if (!SystemInfo.supportsComputeShaders)
            {
                _disposed = true;
                DebugLogError($"[{nameof(NonBlockingCustomTextureRenderer)}] Current device does not support compute shaders.");
                return;
            }

            var renderShader = Resources.Load<Shader>(RenderShaderName);
            if (renderShader is null)
            {
                _disposed = true;
                DebugLogError($"[{nameof(NonBlockingCustomTextureRenderer)}] The shader '{RenderShaderName}' could not be found in 'Resources'.");
                return;
            }
            _renderMaterial = new Material(renderShader);

            if (targetTexture.format != RenderTextureFormat.ARGB32)
            {
                _disposed = true;
                DebugLogError($"[{nameof(NonBlockingCustomTextureRenderer)}] Unsupported texture format: {targetTexture.format}");
                return;
            }

            if (autoDispose){ Application.quitting += Dispose; }

            _updateRawTextureDataFunction = updateRawTextureDataFunction;
            DebugLog($"[{nameof(NonBlockingCustomTextureRenderer)}] The UpdateRawTextureDataFunction is \n'{_updateRawTextureDataFunction.Target}.{_updateRawTextureDataFunction.Method.Name}'.");

            _targetFrameTimeMilliseconds = (int)(1000.0f / targetFrameRateOfPluginRenderThread);
            DebugLog($"[{nameof(NonBlockingCustomTextureRenderer)}] Target frame time milliseconds: {_targetFrameTimeMilliseconds}");

            _targetTexture = targetTexture;
            _textureWidth = targetTexture.width;
            _textureHeight = targetTexture.height;
            _bytesPerPixel = 4; // RGBA32. 1 byte (8 bits) per channel.

            _currentBuffer = new uint[_targetTexture.width * _targetTexture.height];
            _currentBufferHandle = GCHandle.Alloc(_currentBuffer, GCHandleType.Pinned);
            _currentBufferPtr = _currentBufferHandle.AddrOfPinnedObject();

            _nextBuffer = new uint[_targetTexture.width * _targetTexture.height];
            _nextBufferHandle = GCHandle.Alloc(_nextBuffer, GCHandleType.Pinned);
            _nextBufferPtr = _nextBufferHandle.AddrOfPinnedObject();

            DebugLog($"[{nameof(NonBlockingCustomTextureRenderer)}] Texture size: {_targetTexture.width}x{_targetTexture.height}");
            DebugLog($"[{nameof(NonBlockingCustomTextureRenderer)}] Texture buffer size: {_targetTexture.width * _targetTexture.height * _bytesPerPixel} [Bytes]");

            _rawTextureDataPropertyId = Shader.PropertyToID(RawTextureDataPropertyName);
            _textureWidthPropertyId = Shader.PropertyToID(TextureWidthPropertyName);
            _textureHeightPropertyId = Shader.PropertyToID(TextureHeightPropertyName);

            _rawTextureDataComputeBuffer = new ComputeBuffer(_targetTexture.width * _targetTexture.height, sizeof(uint));

            _asyncGPUUploadCount = (asyncGPUUploadCount < 1) ? 1 : asyncGPUUploadCount;

            DebugLog($"[{nameof(NonBlockingCustomTextureRenderer)}] Async GPU Upload Count: {_asyncGPUUploadCount}");

            _cts = new CancellationTokenSource();
            _pluginRenderThread = new Thread(PluginRenderThread);
            _pluginRenderThread.Start();
        }

        public void Dispose()
        {
            _cts.Cancel();
            _disposed = true;

            _currentBufferPtr = IntPtr.Zero;
            _currentBufferHandle.Free();
            _currentBuffer = null;

            _nextBufferPtr = IntPtr.Zero;
            _nextBufferHandle.Free();
            _nextBuffer = null;

            _updateRawTextureDataFunction = null;
            _targetTexture = null;

            _rawTextureDataComputeBuffer?.Dispose();
            _rawTextureDataComputeBuffer = null;

            DebugLog($"[{nameof(NonBlockingCustomTextureRenderer)}] Disposed");
        }

        public void Update()
        {
            if (_disposed) { return; }
            AsyncGPUUpload();
            Render();
        }

        void AsyncGPUUpload()
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            _asyncGPUUploadSampler.Begin();
#endif

#if DISABLE_ASYNC_GPU_UPLOAD
            // Run as a performance test without async GPU upload.
            _rawTextureDataComputeBuffer.SetData(_currentBuffer, 0, 0, _currentBuffer.Length);
#else
            if (_asyncGPUUploadFrame < _asyncGPUUploadCount)
            {
                var partialCopyLength = _currentBuffer.Length / _asyncGPUUploadCount;
                var startIndex = partialCopyLength * _asyncGPUUploadFrame;
                _rawTextureDataComputeBuffer.SetData(_currentBuffer, startIndex, startIndex, partialCopyLength);
            }
#endif
            _asyncGPUUploadFrame++;

#if DEVELOPMENT_BUILD || UNITY_EDITOR
            _asyncGPUUploadSampler.End();
#endif
        }

        void Render()
        {
#if !DISABLE_ASYNC_GPU_UPLOAD
            if (_asyncGPUUploadFrame == _asyncGPUUploadCount)
#endif
            {
                _renderMaterial.SetInt(_textureWidthPropertyId, _textureWidth);
                _renderMaterial.SetInt(_textureHeightPropertyId, _textureHeight);
                _renderMaterial.SetBuffer(_rawTextureDataPropertyId, _rawTextureDataComputeBuffer);
                Graphics.Blit(null, _targetTexture, _renderMaterial);
            }
        }

        /// <summary>
        /// This function runs on another thread.
        /// </summary>
        void PluginRenderThread()
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            Profiler.BeginThreadProfiling("CustomTextureRenderer", "PluginRenderThread");
#endif

            while (!_cts.IsCancellationRequested)
            {
                var begin = Stopwatch.GetTimestamp();

#if DEVELOPMENT_BUILD || UNITY_EDITOR
                _updateRawTextureDataFunctionSampler.Begin();
#endif

                // Main loop
                {
                    _updateRawTextureDataFunction?.Invoke(_nextBufferPtr, _textureWidth, _textureHeight, _bytesPerPixel);

                    // Swap the buffers.
                    _nextBufferPtr = Interlocked.Exchange(ref _currentBufferPtr, _nextBufferPtr);
                    _nextBuffer = Interlocked.Exchange(ref _currentBuffer, _nextBuffer);

                    // Reset to execute async gpu upload.
                    _asyncGPUUploadFrame = 0;
                }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
                _updateRawTextureDataFunctionSampler.End();
#endif

                var end = Stopwatch.GetTimestamp();
                var elapsedTicks = (end - begin) * TimestampsToTicks;
                var elapsedMilliseconds = (long)elapsedTicks / TimeSpan.TicksPerMillisecond;

                var waitForNextFrameMilliseconds = (int)(_targetFrameTimeMilliseconds - elapsedMilliseconds);
                if (waitForNextFrameMilliseconds > 0)
                {
                    Thread.Sleep(waitForNextFrameMilliseconds);
                }
            }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
            Profiler.EndThreadProfiling();
#endif
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
        static void DebugLog(object message)
        {
            UnityEngine.Debug.Log(message);
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