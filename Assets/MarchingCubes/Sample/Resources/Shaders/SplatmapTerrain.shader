Shader "MarchingSquares/SplatmapTerrain"
{
    Properties
    {
        _BaseArray    ("Base Textures",   2DArray) = "" {}
        _OverlayArray ("Overlay Atlases", 2DArray) = "" {}
        _Tiling       ("Base Tiling",     Float)   = 1

        // ── Per-tile，由 MaterialPropertyBlock 注入（不在 Inspector 显示）──────
        // _TerrainPointTex   : 点阵纹理（每像素 = 一个格点的 terrainType，R8）
        // _TerrainPointTexST : xy = BL 角点像素中心 UV，zw = 单步间距（1/texW, 1/texH）
        [HideInInspector] _TerrainPointTex ("Point Tex",   2D)     = "black" {}
        [HideInInspector] _TerrainPointTexST ("Point ST",  Vector) = (0,0,1,1)
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

            sampler2D _TerrainPointTex;
            float4    _TerrainPointTexST;   // xy=BL像素中心UV, zw=单步间距
            float     _Tiling;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv     : TEXCOORD0;  // 格内 [0,1] UV（Blender 导出的平面 UV）
            };

            struct v2f
            {
                float4 pos         : SV_POSITION;
                float2 baseUV      : TEXCOORD0;  // uv * _Tiling，采样基础纹理
                float2 localUV     : TEXCOORD1;  // 原始 [0,1]，用于 overlay atlas 定位
                float3 worldNormal : TEXCOORD2;
            };

            // 从点阵纹理采样整数地形类型（0-4）
            float SampleType(float2 uv)
            {
                return round(tex2D(_TerrainPointTex, uv).r * 4.0);
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.pos         = UnityObjectToClipPos(v.vertex);
                o.baseUV      = v.uv * _Tiling;
                o.localUV     = v.uv;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // ── 1. 采样 4 个角点的 terrainType ──────────────────────────────
                float2 c = _TerrainPointTexST.xy;   // BL 像素中心 UV
                float2 d = _TerrainPointTexST.zw;   // 一格步长

                float t0 = SampleType(c);                    // V0 BL
                float t1 = SampleType(c + float2(d.x, 0));  // V1 BR
                float t2 = SampleType(c + d);                // V2 TR
                float t3 = SampleType(c + float2(0, d.y));  // V3 TL

                // ── 2. 基础层 = 四角最小类型 ────────────────────────────────────
                float baseType = min(min(t0, t1), min(t2, t3));
                fixed4 col = UNITY_SAMPLE_TEX2DARRAY(_BaseArray, float3(i.baseUV, baseType));

                // ── 3. 叠加层（最多 3 层，手动展开避免 GPU 动态分支开销）───────
                // Overlay Atlas：4×4 grid，case N 占第 (N%4, N/4) 格，格内 UV = localUV
                // atlasUV = ((N%4 + localUV.x) * 0.25, (N/4 + localUV.y) * 0.25)

                float2 lUV = i.localUV;

                // 叠加层 1
                {
                    float ot = baseType + 1;
                    if (ot <= 4 && (t0 >= ot || t1 >= ot || t2 >= ot || t3 >= ot))
                    {
                        int mask = (t0 >= ot ? 1 : 0) | (t1 >= ot ? 2 : 0)
                                 | (t2 >= ot ? 4 : 0) | (t3 >= ot ? 8 : 0);
                        float2 aUV = float2((mask % 4 + lUV.x) * 0.25,
                                            (mask / 4 + lUV.y) * 0.25);
                        fixed4 ov = UNITY_SAMPLE_TEX2DARRAY(_OverlayArray, float3(aUV, ot));
                        col.rgb = lerp(col.rgb, ov.rgb, ov.a);
                    }
                }

                // 叠加层 2
                {
                    float ot = baseType + 2;
                    if (ot <= 4 && (t0 >= ot || t1 >= ot || t2 >= ot || t3 >= ot))
                    {
                        int mask = (t0 >= ot ? 1 : 0) | (t1 >= ot ? 2 : 0)
                                 | (t2 >= ot ? 4 : 0) | (t3 >= ot ? 8 : 0);
                        float2 aUV = float2((mask % 4 + lUV.x) * 0.25,
                                            (mask / 4 + lUV.y) * 0.25);
                        fixed4 ov = UNITY_SAMPLE_TEX2DARRAY(_OverlayArray, float3(aUV, ot));
                        col.rgb = lerp(col.rgb, ov.rgb, ov.a);
                    }
                }

                // 叠加层 3
                {
                    float ot = baseType + 3;
                    if (ot <= 4 && (t0 >= ot || t1 >= ot || t2 >= ot || t3 >= ot))
                    {
                        int mask = (t0 >= ot ? 1 : 0) | (t1 >= ot ? 2 : 0)
                                 | (t2 >= ot ? 4 : 0) | (t3 >= ot ? 8 : 0);
                        float2 aUV = float2((mask % 4 + lUV.x) * 0.25,
                                            (mask / 4 + lUV.y) * 0.25);
                        fixed4 ov = UNITY_SAMPLE_TEX2DARRAY(_OverlayArray, float3(aUV, ot));
                        col.rgb = lerp(col.rgb, ov.rgb, ov.a);
                    }
                }

                // ── 4. Lambert 光照 ──────────────────────────────────────────────
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
