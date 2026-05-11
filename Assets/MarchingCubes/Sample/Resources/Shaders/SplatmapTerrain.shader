Shader "MarchingSquares/SplatmapTerrain"
{
    Properties
    {
        _BaseTex      ("Base Texture",    2D)      = "white" {}
        _OverlayArray ("Overlay Atlases", 2DArray) = "" {}
        _Tiling       ("Base Tiling",     Float)   = 1

        // ── Per-tile，由 MaterialPropertyBlock 注入（不在 Inspector 显示）──────
        // _TileMsIdx : xyzw = layer 0/1/2/3 的 ms_idx (0~15)
        // _TileMsIdx4: layer 4 的 ms_idx
        // 直接 per-tile uniform，无纹理采样无解码 → 0 误差
        [HideInInspector] _TileMsIdx  ("Layer ms_idx 0-3", Vector) = (0, 0, 0, 0)
        [HideInInspector] _TileMsIdx4 ("Layer ms_idx 4",   Float)  = 0
    }

    // ── _OverlayArray 资产协议（重要变更，2026-05-10） ────────────────────────
    // 5 层 2DArray，layer t = type t 的 marching squares atlas（0 ≤ t ≤ 4）
    // 每层布局：4×4 atlas，存 16 个 MS case 的形状纹理（带 alpha）
    //   col = ms_idx % 4，row = ms_idx / 4，UV 原点左下
    //   ms_idx = bit_BL | bit_BR<<1 | bit_TR<<2 | bit_TL<<3（0~15，标准 MS 编码）
    // R 通道 mask byte 低 5 bit = 5 个 type 是否存在（bit t = type t 存在）
    // 渲染：遍历 type 0~4，高编号覆盖低；ms_idx=0 跳过；alpha 决定覆盖；底色 _BaseTex

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

            // 5 layer ms_idx 直接 per-tile uniform，无纹理采样
            float4 _TileMsIdx;   // x=layer0 idx, y=layer1, z=layer2, w=layer3
            float  _TileMsIdx4;  // layer4 idx
            float     _Tiling;

            struct appdata
            {
                float4 vertex  : POSITION;
                float3 normal  : NORMAL;
                float2 uv      : TEXCOORD0; // 格内 [0,1] UV（Blender 导出的平面 UV）
            };

            struct v2f
            {
                float4 pos            : SV_POSITION;
                float2 baseUV         : TEXCOORD0;  // uv * _Tiling，采样基础纹理
                float2 localUV        : TEXCOORD1;  // 原始 [0,1]，用于 overlay atlas 定位
                float3 worldNormal    : TEXCOORD2;
            };

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
                // ── 1. 直接读 per-tile uniform 拿 5 layer ms_idx，无采样无解码 ──
                int idx0 = (int)_TileMsIdx.x;
                int idx1 = (int)_TileMsIdx.y;
                int idx2 = (int)_TileMsIdx.z;
                int idx3 = (int)_TileMsIdx.w;
                int idx4 = (int)_TileMsIdx4;

                // ── 2. 底色 _BaseTex（所有 layer ms_idx=0 时露底）─────────────
                float2 lUV = i.localUV;
                float3 col = tex2D(_BaseTex, i.baseUV).rgb;

                // ── 3. 5 个 layer 按高编号覆盖低编号顺序，alpha 决定覆盖 ───────
                #define BLEND_LAYER(t, idx) \
                    if (idx > 0) { \
                        float2 atlasUV = float2(((idx & 3)  + lUV.x) * 0.25, \
                                                ((idx >> 2) + lUV.y) * 0.25); \
                        float4 ov = UNITY_SAMPLE_TEX2DARRAY(_OverlayArray, float3(atlasUV, t)); \
                        col = lerp(col, ov.rgb, ov.a); \
                    }
                BLEND_LAYER(0, idx0)
                BLEND_LAYER(1, idx1)
                BLEND_LAYER(2, idx2)
                BLEND_LAYER(3, idx3)
                BLEND_LAYER(4, idx4)
                #undef BLEND_LAYER

                // ── 4. Lambert 光照 ──────────────────────────────────────────
                float ndl = max(0.2, dot(i.worldNormal, _WorldSpaceLightPos0.xyz));
                col = col * ndl * _LightColor0.rgb;

                return fixed4(col, 1);
            }
            ENDCG
        }
    }

    Fallback "Diffuse"
}
