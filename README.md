# UnityCustomTextureRenderer

A graphics utility to update textures from native plugins.

## NonBlockingCustomTextureRenderer
`NonBlockingCustomTextureRenderer` is a high performance graphics utility to update textures from native plugins.

<image src="./Docs/NonBlockingCustomTextureRenderer.gif" width="80%">

The function for updating textures runs on another thread.  
Asynchronous GPU upload (partial data copy) reduces the processing time per frame in the main thread for large size textures.

`NonBlockingCustomTextureRenderer` is available on IL2CPP.

## CustomTextureRenderer
`CustomTextureRenderer` is an example to update textures from native plugins 
using [CommandBuffer.IssuePluginCustomTextureUpdateV2](https://docs.unity3d.com/ScriptReference/Rendering.CommandBuffer.IssuePluginCustomTextureUpdateV2.html).

<image src="./Docs/CustomTextureRenderer.gif" width="80%">

The function for updating textures runs on Unity's Render Thread.

`CustomTextureRenderer` does not work on IL2CPP.
```
NotSupportedException: 
IL2CPP does not support marshaling delegates that point to instance methods to native code.
The method we're attempting to marshal is: UnityCustomTextureRenderer.CustomTextureRenderer::TextureUpdateCallback
UnityCustomTextureRenderer.CustomTextureRenderer.Update () (at <00000000000000000000000000000000>:0)
UnityCustomTextureRenderer.Samples.Test.Update () (at <00000000000000000000000000000000>:0)
```

## Tested Environment
- Unity 2020.3.27f1
- Windows 10

## How to install
```
// manifest.json
{
  "dependencies": {
    "jp.sotanmochi.unitycustomtexturerenderer": "https://github.com/sotanmochi/UnityCustomTextureRenderer.git?path=Assets/CustomTextureRenderer",
    "jp.sotanmochi.unitycustomtexturerenderer.samples": "https://github.com/sotanmochi/UnityCustomTextureRenderer.git?path=Assets/CustomTextureRenderer.Samples",
    ...
  }
}
```

## References
- [UnityグラフィックスAPI総点検！〜最近こんなの増えてました〜 - Unityステーション](https://youtu.be/7tjycAEMJNg?t=3197)
- https://github.com/keijiro/TextureUpdateExample

## License
このプロジェクトは、サードパーティのアセットを除き、MIT Licenseでライセンスされています。  
This project is licensed under the MIT License, except for third party assets.  
