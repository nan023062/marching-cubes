Shader "MarchingSquares/SplatmapTerrain"
{
    Properties
    {
        _BaseTex      ("Base Texture",    2D)      = "white" {}
        _OverlayArray ("Overlay Atlases", 2DArray) = "" {}
        _Tiling       ("Base Tiling",     Float)   = 1

        // ── Per-tile，由 MaterialPropertyBlock 注入（不在 Inspector 显示）──────
        // _TerrainPointTex   : 点阵纹理（每像素 = 一个格点的 terrainMask byte，R8）
        //   编码：R 通道 = mask byte（Color32 整数往返）；shader 端 round(*255) 反解
        //   bit i = 1 表示 type i 存在；最多 8 type 同点叠加
        // _TerrainPointTexST : xy = BL 角点像素中心 UV，zw = 单步间距（1/texW, 1/texH）
        //   注意：必须用 zw 显式传步长，不能用 _TerrainPointTex_TexelSize.xy ——
        //   MPB 注入的纹理不会同步更新 _TexelSize 内置变量（恒为默认 1×1 = (1,1)）
        [HideInInspector] _TerrainPointTex ("Point Tex",   2D)     = "black" {}
        [HideInInspector] _TerrainPointTexST ("Point ST",  Vector) = (0,0,0,0)

        // _NormalMap : 切线空间法线贴图（Blender 端 tileable noise 烘焙；缺省 "bump" = 平面无扰动）
        _NormalMap ("Normal Map", 2D) = "bump" {}
    }

    // ── _OverlayArray 资产协议（重要变更，2026-05-10） ────────────────────────
    // 旧协议：packed atlas 4 列 × N 行，由 ApplyOverlay 内部 b0+b1*2 / b2+b3*2 计算 UV 取子格
    // 新协议：1 type / 层，array index = type i（0~7）；每张是完整的 tileable overlay
    // 美术资产侧需重新烘焙/导入 8 张 type overlay，array layer 顺序 = type id
    // 未配置的 type 在采样时返回 array 越界默认值（黑色或空），DecodeCorner 中遇 mask=0 fallback _BaseTex

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
            float4    _TerrainPointTexST;       // xy=BL角点像素中心UV，zw=步长(1/texW,1/texH)
            sampler2D _NormalMap;
            float     _Tiling;

            struct appdata
            {
                float4 vertex  : POSITION;
                float3 normal  : NORMAL;
                float4 tangent : TANGENT;   // MikkTSpace 切线（ImportTangents=CalculateMikk 保证）
                float2 uv      : TEXCOORD0; // 格内 [0,1] UV（Blender 导出的平面 UV）
            };

            struct v2f
            {
                float4 pos            : SV_POSITION;
                float2 baseUV         : TEXCOORD0;  // uv * _Tiling，采样基础纹理
                float2 localUV        : TEXCOORD1;  // 原始 [0,1]，用于 overlay atlas + normal map 定位
                float3 worldNormal    : TEXCOORD2;
                float3 worldTangent   : TEXCOORD3;
                float3 worldBitangent : TEXCOORD4;
            };

            // 单角解码：8-bit mask → 加权混合 8 张 overlay → 角颜色
            // 权重：weight(i) = i + 1（线性，bit 位高的 type 视觉更强）
            // mask = 0 时 totalW = 0 → fallback 到 _BaseTex 颜色（baseRGB 由调用方传入）
            float3 DecodeCorner(int mask, float2 lUV, float3 baseRGB)
            {
                float3 acc = 0;
                float  totalW = 0;
                [unroll]
                for (int t = 0; t < 8; t++)
                {
                    if ((mask >> t) & 1)
                    {
                        float w = (float)(t + 1);
                        float3 ov = UNITY_SAMPLE_TEX2DARRAY(_OverlayArray, float3(lUV, t)).rgb;
                        acc    += w * ov;
                        totalW += w;
                    }
                }
                return totalW > 0 ? acc / totalW : baseRGB;
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.pos            = UnityObjectToClipPos(v.vertex);
                o.baseUV         = v.uv * _Tiling;
                o.localUV        = v.uv;
                o.worldNormal    = UnityObjectToWorldNormal(v.normal);
                o.worldTangent   = UnityObjectToWorldDir(v.tangent.xyz);
                // bitangent.w 是 MikkTSpace 手性符号（±1），保证 TBN 与烘焙方向一致
                o.worldBitangent = cross(o.worldNormal, o.worldTangent) * v.tangent.w * unity_WorldTransformParams.w;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // ── 1. 4 角各采样 1 次，反解 mask byte ────────────────────────
                float2 uvBL = _TerrainPointTexST.xy;
                float2 stp  = _TerrainPointTexST.zw; // 1/texW, 1/texH（C# 端显式传入）
                int m0 = (int)round(tex2D(_TerrainPointTex, uvBL                          ).r * 255.0); // BL
                int m1 = (int)round(tex2D(_TerrainPointTex, uvBL + float2(stp.x, 0      ) ).r * 255.0); // BR
                int m2 = (int)round(tex2D(_TerrainPointTex, uvBL + float2(stp.x, stp.y) ).r * 255.0); // TR
                int m3 = (int)round(tex2D(_TerrainPointTex, uvBL + float2(0,     stp.y) ).r * 255.0); // TL

                // ── 2. 基础层颜色（mask=0 角的 fallback） ─────────────────────
                fixed4 baseCol = tex2D(_BaseTex, i.baseUV);

                // ── 3. 每角解码 + 加权混合（8 type，weight = bitIndex + 1） ───
                float2 lUV = i.localUV;
                float3 c0 = DecodeCorner(m0, lUV, baseCol.rgb); // BL
                float3 c1 = DecodeCorner(m1, lUV, baseCol.rgb); // BR
                float3 c2 = DecodeCorner(m2, lUV, baseCol.rgb); // TR
                float3 c3 = DecodeCorner(m3, lUV, baseCol.rgb); // TL

                // ── 4. 4 角颜色 quad 双线性插值 ──────────────────────────────
                float2 f = lUV;
                float3 cBottom = lerp(c0, c1, f.x); // BL→BR
                float3 cTop    = lerp(c3, c2, f.x); // TL→TR
                float3 col     = lerp(cBottom, cTop, f.y);

                // ── 5. 切线空间法线扰动 + Lambert 光照 ───────────────────────
                // 缺省 _NormalMap = "bump" → UnpackNormal 返回 (0,0,1)，nWorld ≡ i.worldNormal，无扰动
                float3 nT     = UnpackNormal(tex2D(_NormalMap, lUV));
                float3 nWorld = normalize(nT.x * i.worldTangent
                                        + nT.y * i.worldBitangent
                                        + nT.z * i.worldNormal);
                float ndl = max(0.2, dot(nWorld, _WorldSpaceLightPos0.xyz));
                col = col * ndl * _LightColor0.rgb;

                return fixed4(col, 1);
            }
            ENDCG
        }
    }

    Fallback "Diffuse"
}
