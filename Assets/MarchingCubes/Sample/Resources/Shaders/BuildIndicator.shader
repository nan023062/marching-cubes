Shader "MarchingCubes/BuildIndicator"
{
    Properties
    {
        _Color  ("Border Color", Color)           = (1, 0.8, 0, 1)
        _Width  ("Border Width", Range(0.01, 0.5)) = 0.06
        _Fill   ("Fill Alpha",   Range(0, 1))     = 0.08
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        ZWrite Off
        ZTest LEqual
        Cull Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
            };

            fixed4 _Color;
            float  _Width;
            float  _Fill;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv  = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float dx = min(uv.x, 1.0 - uv.x);  // 距左/右边距离
                float dy = min(uv.y, 1.0 - uv.y);  // 距上/下边距离
                float d  = min(dx, dy);

                bool onBorder = d < _Width;

                if (onBorder)
                {
                    // 每条边均分 5 段，偶数段（0,2,4）实线，奇数段（1,3）虚线
                    float along = (dy <= dx) ? uv.x : uv.y;
                    int   seg   = (int)(along * 5.0);
                    if (seg % 2 != 0) discard;

                    return _Color;
                }

                if (_Fill <= 0.001) discard;

                fixed4 col = _Color;
                col.a = _Fill;
                return col;
            }
            ENDCG
        }
    }
}
