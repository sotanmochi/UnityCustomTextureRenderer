// This code is modified version of FPSCounter implemented by baba_s.
// The original source code is available in the following article.
// https://baba-s.hatenablog.com/entry/2019/05/04/220500

using UnityEngine;

namespace UnityCustomTextureRenderer.Samples
{
    public sealed class FPSCounter
    {
        public float FPS => _fps;
        private float _fps;

        private int   _frameCount;
        private float _deltaTime;

        private readonly int _updateRate;

        public FPSCounter(int updateRate = 4)
        {
            _updateRate = updateRate;
        }

        public void Update()
        {
            _deltaTime += Time.unscaledDeltaTime;

            _frameCount++;

            if ( !( _deltaTime > 1f / _updateRate ) ) return;

            _fps = _frameCount / _deltaTime;

            _deltaTime  = 0;
            _frameCount = 0;
        }
    }
}