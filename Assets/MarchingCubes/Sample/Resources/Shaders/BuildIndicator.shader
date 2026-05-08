Shader "MarchingCubes/BuildIndicator"
{
    Properties
    {
        _Color  ("Border Color", Color) = (1, 0.8, 0, 1)
        _Width  ("Border Width", Range(0.01, 0.5)) = 0.06
        _Fill   ("Fill Alpha", Range(0, 1)) = 0.08
    }

    SubShader
    {
        // 在 opaque 之后、transparent 之前绘制，ZTest LessEqual 保证不穿墙，
        // ZWrite Off 不污染深度缓冲，Cull Off 正背面均绘制（quad 从下方也可见）
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
                // 距离任意一条边的最小值
                float d = min(min(uv.x, 1.0 - uv.x), min(uv.y, 1.0 - uv.y));
                bool onBorder = d < _Width;

                fixed4 col = _Color;
                col.a = onBorder ? _Color.a : _Fill;

                // 内部填充 alpha 为 0 时直接丢弃，减少 overdraw
                if (!onBorder && _Fill <= 0.001) discard;

                return col;
            }
            ENDCG
        }
    }
}
