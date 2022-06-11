# UnityCustomTextureRenderer

A high performance graphics utility to update textures from native plugins.  
The function for updating textures runs on another thread.  

<image src="./Docs/NonBlockingCustomTextureRenderer.gif">

## Tested Environment
- Unity 2020.3.27f1
- Windows 10

## How to install
```
// manifest.json
{
  "dependencies": {
    "jp.sotanmochi.unitycustomtexturerenderer": "https://github.com/sotanmochi/UnityCustomTextureRenderer.git?path=Assets/CustomTextureRenderer#v1.3.4",
    "jp.sotanmochi.unitycustomtexturerenderer.samples": "https://github.com/sotanmochi/UnityCustomTextureRenderer.git?path=Assets/CustomTextureRenderer.Samples#v1.3.4",
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
