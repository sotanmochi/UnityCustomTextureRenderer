using System;

namespace UnityCustomTextureRenderer
{
    public delegate void UpdateRawTextureDataFunction(IntPtr rawTextureData, int width, int height, int bytesPerPixel);
}