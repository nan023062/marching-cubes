Shader "MarchingSquares/SplatmapTerrain"
{
    Properties
    {
        _BaseArray ("Base Textures", 2DArray) = "" {}
        _OverlayArray ("Overlay Atlases", 2DArray) = "" {}
        _Tiling ("Base Tiling", Float) = 1
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
            #pragma require 2darray
            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            UNITY_DECLARE_TEX2DARRAY(_BaseArray);
            UNITY_DECLARE_TEX2DARRAY(_OverlayArray);
            float _Tiling;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv0 : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
                float2 uv2 : TEXCOORD2;
                float2 uv3 : TEXCOORD3;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv0 : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
                float2 uv2 : TEXCOORD2;
                float2 uv3 : TEXCOORD3;
                float4 color : TEXCOORD4;
                float3 worldNormal : TEXCOORD5;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv0 = v.uv0 * _Tiling;
                o.uv1 = v.uv1;
                o.uv2 = v.uv2;
                o.uv3 = v.uv3;
                o.color = v.color;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Decode type indices from vertex color (byte * 51 → 0-4)
                float baseIdx = round(i.color.r * 5.0);
                float over1Idx = round(i.color.g * 5.0);
                float over2Idx = round(i.color.b * 5.0);
                float over3Idx = round(i.color.a * 5.0);

                // Base: seamless tiling, always opaque
                fixed4 col = UNITY_SAMPLE_TEX2DARRAY(_BaseArray, float3(i.uv0, baseIdx));

                // Overlay 1: MS tile atlas with alpha
                fixed4 o1 = UNITY_SAMPLE_TEX2DARRAY(_OverlayArray, float3(i.uv1, over1Idx));
                col.rgb = lerp(col.rgb, o1.rgb, o1.a);

                // Overlay 2
                fixed4 o2 = UNITY_SAMPLE_TEX2DARRAY(_OverlayArray, float3(i.uv2, over2Idx));
                col.rgb = lerp(col.rgb, o2.rgb, o2.a);

                // Overlay 3
                fixed4 o3 = UNITY_SAMPLE_TEX2DARRAY(_OverlayArray, float3(i.uv3, over3Idx));
                col.rgb = lerp(col.rgb, o3.rgb, o3.a);

                // Lambert lighting
                float ndl = max(0.2, dot(i.worldNormal, _WorldSpaceLightPos0.xyz));
                col.rgb *= ndl * _LightColor0.rgb;
                col.a = 1;

                return col;
            }
            ENDCG
        }
    }

    Fallback "Diffuse"
}
