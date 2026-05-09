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

            // 一次采样拿到格子 4 角的 terrainType（RGBA = BL BR TR TL）
            void SampleCornerTypes(float2 cellUV,
                out float t0, out float t1, out float t2, out float t3)
            {
                fixed4 c = tex2D(_TerrainPointTex, cellUV);
                t0 = round(c.r * 4.0);  // BL
                t1 = round(c.g * 4.0);  // BR
                t2 = round(c.b * 4.0);  // TR
                t3 = round(c.a * 4.0);  // TL
            }

            // 计算一层 overlay 并叠加到 col（全无分支）
            fixed4 ApplyOverlay(fixed4 col, float ot,
                                float t0, float t1, float t2, float t3,
                                float2 lUV)
            {
                // step(a,b)=1 when b>=a，替代所有 >=? 1:0 分支
                float b0 = step(ot, t0), b1 = step(ot, t1);
                float b2 = step(ot, t2), b3 = step(ot, t3);

                // mask%4 = b0 + b1*2（Atlas 列），mask/4 = b2 + b3*2（Atlas 行）
                // 直接用浮点加法算 Atlas UV，省去 int/% / 运算
                float2 aUV = float2((b0 + b1 * 2 + lUV.x) * 0.25,
                                    (b2 + b3 * 2 + lUV.y) * 0.25);

                // min 防越界；b 全 0 时采样 case 0（Atlas 设计保证 alpha=0），lerp 自然不变色
                fixed4 ov = UNITY_SAMPLE_TEX2DARRAY(_OverlayArray, float3(aUV, min(ot, 4.0)));
                col.rgb = lerp(col.rgb, ov.rgb, ov.a);
                return col;
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
                // ── 1. 一次采样取 4 角 terrainType（RGBA = BL BR TR TL）────────
                float t0, t1, t2, t3;
                SampleCornerTypes(_TerrainPointTexST.xy, t0, t1, t2, t3);

                // ── 2. 基础层 = 四角最小类型 ────────────────────────────────────
                float baseType = min(min(t0, t1), min(t2, t3));
                fixed4 col = UNITY_SAMPLE_TEX2DARRAY(_BaseArray, float3(i.baseUV, baseType));

                // ── 3. 叠加层（最多 3 层）───────────────────────────────────────
                float2 lUV = i.localUV;
                col = ApplyOverlay(col, baseType + 1, t0, t1, t2, t3, lUV);
                col = ApplyOverlay(col, baseType + 2, t0, t1, t2, t3, lUV);
                col = ApplyOverlay(col, baseType + 3, t0, t1, t2, t3, lUV);

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
