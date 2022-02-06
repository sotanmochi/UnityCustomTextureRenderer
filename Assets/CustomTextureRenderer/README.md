# UnityCustomTextureRenderer

A graphics utility to update textures from native plugins.  
The function for updating textures runs on Unity's Render Thread or another thread.

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
このライブラリは、MIT Licenseでライセンスされています。  
This library is licensed under the MIT License.  
