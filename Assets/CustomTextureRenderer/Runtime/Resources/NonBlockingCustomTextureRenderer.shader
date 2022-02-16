Shader "UnityCustomTextureRenderer/NonBlockingCustomTextureRenderer"
{
    SubShader
    {
        Pass
        {
            CGPROGRAM

            #pragma target   5.0
            #pragma vertex   vert_img
            #pragma fragment frag

            #include "UnityCG.cginc"

            int _TextureWidth;
            int _TextureHeight;
            StructuredBuffer<uint> _RawTextureData;

            fixed4 frag (v2f_img i) : SV_Target
            {
                int width  = _TextureWidth;
                int height = _TextureHeight;
                int x = (int)(width  * i.uv.x);
                int y = (int)(height * i.uv.y);

                int index = y * width + x;
                int pixel = _RawTextureData[index];

                float red   =  (pixel & 0x000000ff) / 255.0;
                float green = ((pixel & 0x0000ff00) >> 8)  / 255.0;
                float blue  = ((pixel & 0x00ff0000) >> 16) / 255.0;
                float alpha = ((pixel & 0xff000000) >> 24) / 255.0;

                return float4(red, green, blue, alpha);
            }

            ENDCG
        }
    }
}