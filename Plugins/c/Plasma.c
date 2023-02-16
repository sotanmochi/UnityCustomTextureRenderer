// This code is a modified version of the plasma effect code by Keijiro Takahashi.
// https://github.com/keijiro/TextureUpdateExample/blob/master/Plugin/Plasma.c

#include <math.h>
#include <stdint.h>
#include <stdlib.h>
#include "Unity/IUnityInterface.h"

uint32_t Plasma(int x, int y, int width, int height, unsigned int frame)
{
    float px = (float)x / width;
    float py = (float)y / height;
    float time = frame / 60.0f;

    float l = sinf(px * sinf(time * 1.3f) + sinf(py * 4 + time) * sinf(time));

    uint32_t r = sinf(l *  6) * 127 + 127;
    uint32_t g = sinf(l *  7) * 127 + 127;
    uint32_t b = sinf(l * 10) * 127 + 127;

    return r + (g << 8) + (b << 16) + 0xff000000u;
}

void UNITY_INTERFACE_EXPORT UpdateRawTextureData(uint32_t *data, int width, int height, unsigned int frame)
{
    for (int y = 0; y < height; y++)
    {
        for (int x = 0; x < width; x++)
        {
            data[y * width + x] = Plasma(x, y, width, height, frame);
        }
    }
}
