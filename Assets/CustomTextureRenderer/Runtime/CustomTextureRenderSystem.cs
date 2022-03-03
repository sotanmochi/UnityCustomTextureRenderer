using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

#if DEVELOPMENT_BUILD || UNITY_EDITOR
using UnityEngine.Profiling;
#endif

namespace UnityCustomTextureRenderer
{
    public delegate void RawTextureDataUpdateCallback(IntPtr data, int width, int height, int bytesPerPixel); 
    public delegate void IssuePluginCustomTextureUpdateCallback(int eventID, IntPtr data);

    /// <summary>
    /// A high performance graphics utility to update textures from native plugins. <br/>
    /// The function for updating textures runs on another thread.
    /// </summary>
    public sealed class CustomTextureRenderSystem : MonoBehaviour, IDisposable
    {

#region Singleton class handling

        public static CustomTextureRenderSystem Instance
        {
            get 
            {
                if (_instance is null)
                {
                    var previous = FindObjectOfType<CustomTextureRenderSystem>();
                    if (previous)
                    {
                        _instance = previous;
                        DebugLogWarning($"[{nameof(CustomTextureRenderSystem)}] The instance attached on \"{previous.gameObject.name}\" is used.");
                    }
                    else
                    {
                        var go = new GameObject("__CustomTextureRenderSystem");
                        _instance = go.AddComponent<CustomTextureRenderSystem>();
                        DontDestroyOnLoad(go);
                        // go.hideFlags = HideFlags.HideInHierarchy;
                    }
                }
                return _instance;
            }
        }

        private static CustomTextureRenderSystem _instance;

#endregion

        private bool _initialized;
        private bool _disposed;

        private CommandBuffer _commandBuffer;
        private ComputeBuffer _rendererIdComputeBuffer;
        private uint[] _rendererIdBuffer = new uint[1];

        private ushort _rendererRegisterationCount;
        private static readonly ConcurrentDictionary<ushort, PluginTextureRenderer> s_TextureRenderers = new ConcurrentDictionary<ushort, PluginTextureRenderer>();
        private static readonly ConcurrentDictionary<ushort, IntPtr> s_TextureBufferPtrs = new ConcurrentDictionary<ushort, IntPtr>();

        class TextureRenderingStatus
        {
            public bool Executing;
        }

        private static readonly Dictionary<ushort, TextureRenderingStatus> s_TextureRenderingStatus = new Dictionary<ushort, TextureRenderingStatus>();

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        private static readonly CustomSampler _textureUpdateCallbackSampler = CustomSampler.Create("TextureUpdateCallback");
#endif

#region MonoBehaviour functions

        private void Awake()
        {
            Initialize();
        }

        private void LateUpdate()
        {
            SystemUpdate();
        }

#endregion

        public void Initialize()
        {
            if (_initialized)
            {
                DebugLog($"[{nameof(CustomTextureRenderSystem)}] Already initialized");
                return;
            }

            UnityEngine.Application.quitting += Dispose;

            _commandBuffer = new CommandBuffer();
            _commandBuffer.name = "CustomTextureRenderer.IssuePluginCustomTextureUpdateV2";

            _rendererIdComputeBuffer = new ComputeBuffer(1, sizeof(uint));

            _initialized = true;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                DebugLog($"[{nameof(CustomTextureRenderSystem)}] Already disposed");
                return;
            }

            _disposed = true;

            foreach (var renderer in s_TextureRenderers.Values)
            {
                renderer.Dispose();
            }

            s_TextureRenderers.Clear();
            s_TextureBufferPtrs.Clear();

            _commandBuffer.Dispose();
            _commandBuffer = null;

            _rendererIdComputeBuffer.Dispose();
            _rendererIdComputeBuffer = null;

            DebugLog($"[{nameof(CustomTextureRenderSystem)}] Disposed");
        }

        public int AddRenderer(PluginTextureRenderer renderer)
        {
            var rendererId = _rendererRegisterationCount;
            if (s_TextureRenderers.TryAdd(rendererId, renderer))
            {
                s_TextureRenderingStatus[rendererId] = new TextureRenderingStatus();
                _rendererRegisterationCount++;
                return rendererId;
            }
            else
            {
                return -1;
            }
        }

        public void RemoveRenderer(ushort rendererId)
        {
            s_TextureRenderers.TryRemove(rendererId, out PluginTextureRenderer renderer);
            s_TextureBufferPtrs.TryRemove(rendererId, out IntPtr bufferPtr);
            s_TextureRenderingStatus.Remove(rendererId);
        }

        /// <summary>
        /// Runs on main thread
        /// </summary>
        private void SystemUpdate()
        {
            if (_disposed) { return; }

            foreach (var keyValue in s_TextureRenderers)
            {
                var rendererId = keyValue.Key;
                var renderer = keyValue.Value;

                s_TextureBufferPtrs[rendererId] = renderer.GetTextureBufferPtr();

                if (s_TextureBufferPtrs[rendererId] != IntPtr.Zero
                && !s_TextureRenderingStatus[rendererId].Executing)
                {
                    _rendererIdBuffer[0] = rendererId;
                    _rendererIdComputeBuffer.SetData(_rendererIdBuffer);

                    s_TextureRenderingStatus[rendererId].Executing = true;

                    _commandBuffer.IssuePluginCustomTextureUpdateV2(GetTextureUpdateCallback(), renderer.TargetTexture, rendererId);
                    _commandBuffer.RequestAsyncReadback(_rendererIdComputeBuffer, request =>
                    {
                        var rendererId = (ushort)request.GetData<uint>()[0];
                        s_TextureRenderingStatus[rendererId].Executing = false;
                    });
                }
            }

            Graphics.ExecuteCommandBuffer(_commandBuffer);
            _commandBuffer.Clear();
        }

#region TextureUpdateCallback

        private IntPtr GetTextureUpdateCallback()
        {
            return Marshal.GetFunctionPointerForDelegate(_callback);
        }

        private delegate void UnityRenderingEventAndData(int eventID, IntPtr data);
        private static readonly UnityRenderingEventAndData _callback = new UnityRenderingEventAndData(TextureUpdateCallback);

        /// <summary>
        /// This function runs on Unity's Render Thread.
        /// </summary>
        /// <param name="eventID"></param>
        /// <param name="data"></param>
        [AOT.MonoPInvokeCallback(typeof(IssuePluginCustomTextureUpdateCallback))]
        private static unsafe void TextureUpdateCallback(int eventID, IntPtr data)
        {
            var updateParams = (UnityRenderingExtTextureUpdateParamsV2*)data.ToPointer();

            if (eventID == (int)UnityRenderingExtEventType.kUnityRenderingExtEventUpdateTextureBeginV2)
            {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                _textureUpdateCallbackSampler.Begin();
#endif

                var rendererId = updateParams->userData;
                if (s_TextureBufferPtrs.TryGetValue((ushort)rendererId, out var textureBufferPtr))
                {
                    updateParams->texData = textureBufferPtr.ToPointer();
                }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
                _textureUpdateCallbackSampler.End();
#endif
            }
            else if (eventID == (int)UnityRenderingExtEventType.kUnityRenderingExtEventUpdateTextureEndV2)
            {
                updateParams->texData = null;
            }
        }

#endregion

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
        private static void DebugLog(object message)
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
        private static void DebugLogWarning(object message)
        {
            UnityEngine.Debug.LogWarning(message);
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
        private static void DebugLogError(object message)
        {
            UnityEngine.Debug.LogError(message);
        }
    }
}