# UnityCustomTextureRenderer

A graphics utility to update textures from native plugins.  
The function for updating textures runs on Unity's Render Thread or another thread.

IL2CPP is currently not supported.
```
NotSupportedException: IL2CPP does not support marshaling delegates that point to instance methods to native code.
The method we're attempting to marshal is: UnityCustomTextureRenderer.CustomTextureRenderer::TextureUpdateCallback
UnityCustomTextureRenderer.CustomTextureRenderer.Update () (at <00000000000000000000000000000000>:0)
UnityCustomTextureRenderer.Samples.Test.Update () (at <00000000000000000000000000000000>:0)
```
```
NotSupportedException: 
IL2CPP does not support marshaling delegates that point to instance methods to native code.
The method we're attempting to marshal is: UnityCustomTextureRenderer.NonBlockingCustomTextureRenderer::TextureUpdateCallback
UnityCustomTextureRenderer.NonBlockingCustomTextureRenderer.Update () (at <00000000000000000000000000000000>:0)
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
