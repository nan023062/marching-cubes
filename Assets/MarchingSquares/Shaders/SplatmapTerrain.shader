Shader "MarchingSquares/SplatmapTerrain"
{
    Properties
    {
        _TexBase ("Base (Dirt)", 2D) = "white" {}
        _TexR ("Layer R (Grass)", 2D) = "white" {}
        _TexG ("Layer G (Rock)", 2D) = "white" {}
        _TexB ("Layer B (Snow)", 2D) = "white" {}
        _TexA ("Layer A (Corrupted)", 2D) = "white" {}
        _Tiling ("Tiling", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        Pass
        {
            Tags { "LightMode"="ForwardBase" }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            sampler2D _TexBase, _TexR, _TexG, _TexB, _TexA;
            float _Tiling;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                float3 worldNormal : TEXCOORD1;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv * _Tiling;
                o.color = v.color;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 cBase = tex2D(_TexBase, i.uv);
                fixed4 cR = tex2D(_TexR, i.uv);
                fixed4 cG = tex2D(_TexG, i.uv);
                fixed4 cB = tex2D(_TexB, i.uv);
                fixed4 cA = tex2D(_TexA, i.uv);

                float r = i.color.r;
                float g = i.color.g;
                float b = i.color.b;
                float a = i.color.a;
                float base_w = saturate(1.0 - r - g - b - a);

                fixed4 col = cBase * base_w + cR * r + cG * g + cB * b + cA * a;

                float ndl = max(0.2, dot(i.worldNormal, _WorldSpaceLightPos0.xyz));
                col.rgb *= ndl * _LightColor0.rgb;

                return col;
            }
            ENDCG
        }
    }

    Fallback "Diffuse"
}
