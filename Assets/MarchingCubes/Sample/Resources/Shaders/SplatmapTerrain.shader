Shader "MarchingSquares/SplatmapTerrain"
{
    Properties
    {
        _BaseTex      ("Base Texture",    2D)      = "white" {}
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

            sampler2D _BaseTex;
            UNITY_DECLARE_TEX2DARRAY(_OverlayArray);

            sampler2D _TerrainPointTex;
            float4    _TerrainPointTexST;       // xy=BL角点像素中心UV
            float4    _TerrainPointTex_TexelSize; // Unity自动提供：xy=(1/w,1/h)
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

                // b 全 0 时采样 case 0（Atlas alpha=0），lerp 自然不变色；ot 调用方保证 1-4
                fixed4 ov = UNITY_SAMPLE_TEX2DARRAY(_OverlayArray, float3(aUV, ot));
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
                // ── 1. 分 4 次采样各角格点像素，R 通道 = terrainType / 4 ──────
                float2 uvBL = _TerrainPointTexST.xy;
                float2 step = _TerrainPointTex_TexelSize.xy; // 1/texW, 1/texH（Unity自动）
                float t0 = round(tex2D(_TerrainPointTex, uvBL                          ).r * 4.0); // BL
                float t1 = round(tex2D(_TerrainPointTex, uvBL + float2(step.x, 0      )).r * 4.0); // BR
                float t2 = round(tex2D(_TerrainPointTex, uvBL + float2(step.x, step.y )).r * 4.0); // TR
                float t3 = round(tex2D(_TerrainPointTex, uvBL + float2(0,      step.y )).r * 4.0); // TL

                // ── 2. 基础层：全图平铺底色，保证无缝，overlay alpha 覆盖在上 ────
                fixed4 col = tex2D(_BaseTex, i.baseUV);

                // ── 3. 叠加层 type 1~4（共 4 层，覆盖全部 5 种地形类型）────────
                float2 lUV = i.localUV;
                col = ApplyOverlay(col, 1, t0, t1, t2, t3, lUV);
                col = ApplyOverlay(col, 2, t0, t1, t2, t3, lUV);
                col = ApplyOverlay(col, 3, t0, t1, t2, t3, lUV);
                col = ApplyOverlay(col, 4, t0, t1, t2, t3, lUV);

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
