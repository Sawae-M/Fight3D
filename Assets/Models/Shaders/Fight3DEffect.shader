// 攻撃の演出(火花・光・剣の軌跡)用のシェーダー。URP 用。
// 光を足し合わせる(加算)ので、暗い所ではほんのり光って見える。影や明るさの計算はしない。
// 色 = マテリアルの色 × 頂点の色(TrailRenderer やパーティクルの色)。透明度(a)で薄くできる。
Shader "Fight3D/Effect"
{
    Properties
    {
        [MainColor] _BaseColor ("色", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent" }

        Pass
        {
            Name "Effect"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha One   // 加算(元の画面に光を足す)
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.color = v.color * _BaseColor;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                return i.color;
            }
            ENDHLSL
        }
    }
}
