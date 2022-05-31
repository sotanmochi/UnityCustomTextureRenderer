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
    public sealed class PluginTextureRenderer : IDisposable
    {
        public Texture TargetTexture => _targetTexture;
        private Texture2D _targetTexture;

        public int TextureBufferSize => (_textureBuffer is null) ? -1 : _textureBuffer.Length;

        public bool Disposed => _disposed;
        private bool _disposed;

        private IntPtr _textureBufferPtr;
        private bool _updated;

        private int _textureWidth;
        private int _textureHeight;
        private TextureFormat _textureFormat = TextureFormat.RGBA32;
        private readonly int _bytesPerPixel = 4; // RGBA32. 1 byte (8 bits) per channel.
        private readonly ColorSpace _activeColorSpace;

        private readonly Thread _pluginRenderThread;
        private readonly Action _loopAction;
        private readonly CancellationTokenSource _cts;
        private readonly int _targetFrameTimeMilliseconds;
        private static readonly double TimestampsToTicks = TimeSpan.TicksPerSecond / (double)Stopwatch.Frequency;

        private RawTextureDataUpdateCallback _rawTextureDataUpdateCallback;
        private uint[] _textureBuffer;
        private GCHandle _textureBufferHandle;

        private IssuePluginCustomTextureUpdateCallback _customTextureUpdateCallback;
        private UnityRenderingExtTextureUpdateParamsV2 _textureUpdateParams;
        private IntPtr _textureUpdateParamsPtr;
        private uint _userData;

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        private CustomSampler _textureUpdateLoopSampler;
#endif

        public PluginTextureRenderer(RawTextureDataUpdateCallback callback, 
                                        int textureWidth, int textureHeight, int bufferSize = 0, TextureFormat format = TextureFormat.RGBA32, 
                                        int targetFrameRateOfPluginRenderThread = 60, bool autoDispose = true)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            _textureUpdateLoopSampler = CustomSampler.Create("RawTextureDataUpdateFunction");
#endif
            _activeColorSpace = UnityEngine.QualitySettings.activeColorSpace;

            _loopAction = RawTextureDataUpdate;
            _rawTextureDataUpdateCallback = callback;
            DebugLog($"[{nameof(PluginTextureRenderer)}] The RawTextureDataUpdateCallback is \n'{_rawTextureDataUpdateCallback.Target}.{_rawTextureDataUpdateCallback.Method.Name}'.");

            CreateTextureBuffer(textureWidth, textureHeight, bufferSize, format);

            if (autoDispose){ UnityEngine.Application.quitting += Dispose; }

            _targetFrameTimeMilliseconds = (int)(1000.0f / targetFrameRateOfPluginRenderThread);
            DebugLog($"[{nameof(PluginTextureRenderer)}] Target frame rate: {targetFrameRateOfPluginRenderThread}");
            DebugLog($"[{nameof(PluginTextureRenderer)}] Target frame time milliseconds: {_targetFrameTimeMilliseconds}");

            _cts = new CancellationTokenSource();
            _pluginRenderThread = new Thread(PluginRenderThread);
            _pluginRenderThread.Start();
        }

        public PluginTextureRenderer(IssuePluginCustomTextureUpdateCallback callback, 
                                        int textureWidth, int textureHeight, int bufferSize = 0, TextureFormat format = TextureFormat.RGBA32, 
                                        int targetFrameRateOfPluginRenderThread = 60, bool autoDispose = true)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            _textureUpdateLoopSampler = CustomSampler.Create("CustomTextureUpdateFunction");
#endif
            _activeColorSpace = UnityEngine.QualitySettings.activeColorSpace;

            _loopAction = IssuePluginCustomTextureUpdate;
            _customTextureUpdateCallback = callback;
            DebugLog($"[{nameof(PluginTextureRenderer)}] The CustomTextureUpdateCallback is \n'{_customTextureUpdateCallback.Target}.{_customTextureUpdateCallback.Method.Name}'.");

            _textureUpdateParamsPtr = Marshal.AllocHGlobal(Marshal.SizeOf(_textureUpdateParams));

            CreateTextureBuffer(textureWidth, textureHeight, bufferSize, format);

            if (autoDispose){ UnityEngine.Application.quitting += Dispose; }

            _targetFrameTimeMilliseconds = (int)(1000.0f / targetFrameRateOfPluginRenderThread);
            DebugLog($"[{nameof(PluginTextureRenderer)}] Target frame rate: {targetFrameRateOfPluginRenderThread}");
            DebugLog($"[{nameof(PluginTextureRenderer)}] Target frame time milliseconds: {_targetFrameTimeMilliseconds}");

            _cts = new CancellationTokenSource();
            _pluginRenderThread = new Thread(PluginRenderThread);
            _pluginRenderThread.Start();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                DebugLog($"[{nameof(PluginTextureRenderer)}] Already disposed");
                return;
            }

            _cts.Cancel();
            _disposed = true;

            _rawTextureDataUpdateCallback = null;
            _customTextureUpdateCallback = null;

            UnityEngine.Object.Destroy(_targetTexture);
            _textureBufferPtr = IntPtr.Zero;

            Marshal.FreeHGlobal(_textureUpdateParamsPtr);

            if (_textureBuffer != null)
            {
                _textureBuffer = null;
                _textureBufferHandle.Free();
            }

            DebugLog($"[{nameof(PluginTextureRenderer)}] Disposed");
        }

        /// <summary>
        /// Runs on Unity main thread.
        /// </summary>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <returns></returns>
        public IntPtr CreateTextureBuffer(int width, int height, int bufferSize = 0, TextureFormat format = TextureFormat.RGBA32)
        {
            _updated = false;

            if (_textureWidth != width || _textureHeight != height)
            {
                UnityEngine.Object.Destroy(_targetTexture);

                _targetTexture = new Texture2D(width, height, format, false);
                _textureWidth = width;
                _textureHeight = height;
                _textureFormat = format;
            }

            if (_rawTextureDataUpdateCallback != null)
            {
                if (_textureBuffer != null && _textureBuffer.Length < _textureWidth * _textureHeight)
                {
                    _textureBufferPtr = IntPtr.Zero;
                    _textureBuffer = null;
                    _textureBufferHandle.Free();
                }

                if (_textureBuffer is null)
                {
                    if (bufferSize > _targetTexture.width * _targetTexture.height)
                    {
                        _textureBuffer = new uint[bufferSize];
                    }
                    else
                    {
                        _textureBuffer = new uint[_targetTexture.width * _targetTexture.height];
                    }

                    _textureBufferHandle = GCHandle.Alloc(_textureBuffer, GCHandleType.Pinned);
                    _textureBufferPtr = _textureBufferHandle.AddrOfPinnedObject();
                }
            }

            DebugLog($"[{nameof(PluginTextureRenderer)}] Create texture: {_textureWidth}x{_textureHeight} [pixels]");

            return _textureBufferPtr;
        }

        public IntPtr GetTextureBufferPtr()
        {
            if (_updated)
            {
                _updated = false;
                return _textureBufferPtr;
            }
            else
            {
                return IntPtr.Zero;
            }
        }

        public void SetUserData(uint userData)
        {
            _userData = userData;
        }

        /// <summary>
        /// Runs on another thread.
        /// </summary>
        unsafe void RawTextureDataUpdate()
        {
            _rawTextureDataUpdateCallback.Invoke(_textureBufferPtr, _textureWidth, _textureHeight, _bytesPerPixel);
        }

        /// <summary>
        /// Runs on another thread.
        /// </summary>
        unsafe void IssuePluginCustomTextureUpdate()
        {
            _textureUpdateParams.width = (uint)_textureWidth;
            _textureUpdateParams.height = (uint)_textureHeight;
            _textureUpdateParams.bpp = (uint)_bytesPerPixel;
            _textureUpdateParams.userData = _userData;

            if (_activeColorSpace is ColorSpace.Linear)
            {
                _textureUpdateParams.format = _textureFormat switch 
                {
                    TextureFormat.RGBA32 => UnityRenderingExtTextureFormat.kUnityRenderingExtFormatR8G8B8A8_SRGB,
                    TextureFormat.BGRA32 => UnityRenderingExtTextureFormat.kUnityRenderingExtFormatB8G8R8A8_SRGB,
                    _ => UnityRenderingExtTextureFormat.kUnityRenderingExtFormatR8G8B8A8_SRGB,
                };
            }
            else
            {
                _textureUpdateParams.format = _textureFormat switch 
                {
                    TextureFormat.RGBA32 => UnityRenderingExtTextureFormat.kUnityRenderingExtFormatR8G8B8A8_UNorm,
                    TextureFormat.BGRA32 => UnityRenderingExtTextureFormat.kUnityRenderingExtFormatB8G8R8A8_UNorm,
                    _ => UnityRenderingExtTextureFormat.kUnityRenderingExtFormatR8G8B8A8_UNorm,
                };
            }

            var eventId = (int)UnityRenderingExtEventType.kUnityRenderingExtEventUpdateTextureBeginV2;
            Marshal.StructureToPtr(_textureUpdateParams, _textureUpdateParamsPtr, false);

            _customTextureUpdateCallback.Invoke(eventId, _textureUpdateParamsPtr);

            _textureUpdateParams = (UnityRenderingExtTextureUpdateParamsV2)
                                    Marshal.PtrToStructure(_textureUpdateParamsPtr, 
                                                            typeof(UnityRenderingExtTextureUpdateParamsV2));
            _textureBufferPtr = new IntPtr(_textureUpdateParams.texData);
        }

        /// <summary>
        /// Runs on another thread.
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
                _textureUpdateLoopSampler.Begin();
#endif

                // Main loop
                {
                    _loopAction.Invoke();
                    _updated = true;
                }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
                _textureUpdateLoopSampler.End();
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