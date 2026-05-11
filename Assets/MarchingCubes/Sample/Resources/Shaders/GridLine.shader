Shader "MarchingCubes/GridLine"
{
    Properties
    {
        _Color ("Color", Color) = (0, 1, 0.2, 1)
    }

    SubShader
    {
        // grid 始终显示在最上层：Overlay 队列最后绘制 + ZTest Always 忽略深度测试 + ZWrite Off 不影响后续物体
        Tags { "RenderType"="Overlay" "Queue"="Overlay" }
        ZTest   Always
        ZWrite  Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;

            struct appdata { float4 vertex : POSITION; };
            struct v2f    { float4 pos    : SV_POSITION; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target { return _Color; }
            ENDCG
        }
    }
}
