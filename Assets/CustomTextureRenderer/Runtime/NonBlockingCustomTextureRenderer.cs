using System;
using System.Diagnostics;
using System.Threading;
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
    /// The function for updating textures runs on another thread.
    /// </summary>
    public sealed class NonBlockingCustomTextureRenderer : IDisposable
    {
        UpdateRawTextureDataFunction _updateRawTextureDataFunction;

        Texture _targetTexture;
        int _textureWidth;
        int _textureHeight;
        int _bytesPerPixel;

        bool _disposed;

        byte[] _currentBuffer;
        GCHandle _currentBufferHandle;
        IntPtr _currentBufferPtr;

        byte[] _nextBuffer;
        GCHandle _nextBufferHandle;
        IntPtr _nextBufferPtr;

        static readonly double TimestampsToTicks = TimeSpan.TicksPerSecond / (double)Stopwatch.Frequency;
        readonly int _targetFrameTimeMilliseconds;
        readonly Thread _pluginRenderThread;
        readonly CancellationTokenSource _cts;

        delegate void UnityRenderingEventAndData(int eventID, IntPtr data);
        readonly UnityRenderingEventAndData _callback;
        readonly CommandBuffer _commandBuffer = new CommandBuffer();

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        CustomSampler _updateRawTextureDataFunctionSampler;
#endif

        /// <summary>
        /// The UpdateRawTextureDataFunction runs on another thread.
        /// </summary>
        /// <param name="updateRawTextureDataFunction"></param>
        /// <param name="targetTexture"></param>
        /// <param name="bytesPerPixel"></param>
        /// <param name="Dispose"></param>
        /// <param name="targetFrameTimeMilliseconds"></param>
        public NonBlockingCustomTextureRenderer(UpdateRawTextureDataFunction updateRawTextureDataFunction, Texture targetTexture, 
                                                    int bytesPerPixel = 4, bool autoDispose = true, int targetFrameTimeMilliseconds = 20)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            _updateRawTextureDataFunctionSampler = CustomSampler.Create("UpdateRawTextureDataFunction");
#endif
            if (autoDispose){ Application.quitting += Dispose; }

            _targetFrameTimeMilliseconds = targetFrameTimeMilliseconds;

            _updateRawTextureDataFunction = updateRawTextureDataFunction;
            _callback = new UnityRenderingEventAndData(TextureUpdateCallback);

            _targetTexture = targetTexture;
            _textureWidth = targetTexture.width;
            _textureHeight = targetTexture.height;
            _bytesPerPixel = bytesPerPixel;

            _currentBuffer = new byte[_targetTexture.width * _targetTexture.height * bytesPerPixel];
            _currentBufferHandle = GCHandle.Alloc(_currentBuffer, GCHandleType.Pinned);
            _currentBufferPtr = _currentBufferHandle.AddrOfPinnedObject();

            _nextBuffer = new byte[_targetTexture.width * _targetTexture.height * bytesPerPixel];
            _nextBufferHandle = GCHandle.Alloc(_nextBuffer, GCHandleType.Pinned);
            _nextBufferPtr = _nextBufferHandle.AddrOfPinnedObject();

            _cts = new CancellationTokenSource();
            _pluginRenderThread = new Thread(PluginRenderThread);
            _pluginRenderThread.Start();
        }

        public void Dispose()
        {
            _cts.Cancel();
            _disposed = false;

            _currentBufferPtr = IntPtr.Zero;
            _currentBufferHandle.Free();
            _currentBuffer = null;

            _nextBufferPtr = IntPtr.Zero;
            _nextBufferHandle.Free();
            _nextBuffer = null;

            _updateRawTextureDataFunction = null;
            _targetTexture = null;

            DebugLog($"[{nameof(NonBlockingCustomTextureRenderer)}] Disposed");
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
                    _nextBufferPtr = Interlocked.Exchange(ref _currentBufferPtr, _nextBufferPtr);
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
        /// This function runs on Unity's Render Thread.
        /// </summary>
        /// <param name="eventID"></param>
        /// <param name="data"></param>
        unsafe void TextureUpdateCallback(int eventID, IntPtr data)
        {
            if (_currentBufferPtr == IntPtr.Zero) { return; }

            var updateParams = (UnityRenderingExtTextureUpdateParamsV2*)data.ToPointer();

            if (eventID == (int)UnityRenderingExtEventType.kUnityRenderingExtEventUpdateTextureBeginV2)
            {
                updateParams->texData = _currentBufferPtr.ToPointer();
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
        static void DebugLog(object message)
        {
            UnityEngine.Debug.Log(message);
        }
    }
}