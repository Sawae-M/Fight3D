// キャラ用のトゥーン(アニメ調)シェーダー。URP 用。
// - 影はパキッと2段(明るい所 / 影)に分かれる
// - ふちに光(リムライト)、金属には小さなハイライト
// - 輪郭線は「裏返した少し大きい体」を黒く描く方法(インバーテッド・ハル)
// 値はすべて [仮]。マテリアルの Inspector で調整できる。
Shader "Fight3D/Toon"
{
    Properties
    {
        [MainColor] _BaseColor ("色", Color) = (1, 1, 1, 1)
        _ShadowColor ("影の色(色に掛ける)", Color) = (0.55, 0.5, 0.7, 1)
        _ShadowThreshold ("影の境目(0〜1)", Range(0, 1)) = 0.5
        _ShadowSoftness ("影の境目のぼかし", Range(0.001, 0.3)) = 0.02
        _Ambient ("まわりの明るさ", Range(0, 1)) = 0.35
        _RimColor ("ふちの光の色", Color) = (1, 1, 1, 1)
        _RimStrength ("ふちの光の強さ", Range(0, 1)) = 0.25
        _RimPower ("ふちの光の細さ", Range(1, 16)) = 5
        _SpecStrength ("ハイライトの強さ(金属用)", Range(0, 1)) = 0
        _SpecSize ("ハイライトの大きさ", Range(0, 1)) = 0.08
        _OutlineColor ("輪郭線の色", Color) = (0.05, 0.04, 0.06, 1)
        _OutlineWidth ("輪郭線の太さ(m)", Range(0, 0.02)) = 0.004
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        // マテリアルごとの値(SRP Batcher 用にまとめる)
        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half4 _ShadowColor;
            half _ShadowThreshold;
            half _ShadowSoftness;
            half _Ambient;
            half4 _RimColor;
            half _RimStrength;
            half _RimPower;
            half _SpecStrength;
            half _SpecSize;
            half4 _OutlineColor;
            float _OutlineWidth;
        CBUFFER_END
        ENDHLSL

        // ---- 本体: トゥーンの明るさ計算 ----
        Pass
        {
            Name "ToonForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                half fogFactor : TEXCOORD2;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs pos = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = pos.positionCS;
                o.positionWS = pos.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.fogFactor = ComputeFogFactor(pos.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                float3 viewDir = GetWorldSpaceNormalizeViewDir(i.positionWS);
                Light light = GetMainLight(TransformWorldToShadowCoord(i.positionWS));

                // 光の当たり具合を 0〜1 にして、境目でパキッと切り替える
                half halfLambert = dot(n, light.direction) * 0.5h + 0.5h;
                half lit = smoothstep(_ShadowThreshold - _ShadowSoftness, _ShadowThreshold + _ShadowSoftness, halfLambert);
                lit *= step(0.5h, light.shadowAttenuation);  // 他の物の影も2段にする

                half3 baseCol = _BaseColor.rgb;
                half3 col = lerp(baseCol * _ShadowColor.rgb, baseCol, lit) * light.color;
                col += baseCol * SampleSH(n) * _Ambient;  // まわりの明るさ(真っ黒にならないように)

                // ふちの光
                half rim = pow(1.0h - saturate(dot(n, viewDir)), _RimPower);
                col += _RimColor.rgb * rim * _RimStrength * (0.3h + 0.7h * lit);

                // 金属の小さなハイライト(境目くっきり)
                half3 h = normalize(light.direction + viewDir);
                half spec = step(1.0h - _SpecSize * 0.2h, saturate(dot(n, h)));
                col += light.color * spec * _SpecStrength * lit;

                col = MixFog(col, i.fogFactor);
                return half4(col, 1);
            }
            ENDHLSL
        }

        // ---- 輪郭線: 法線の方向に少しふくらませた裏面を、線の色で塗る ----
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half fogFactor : TEXCOORD0;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 grown = v.positionOS.xyz + normalize(v.normalOS) * _OutlineWidth;
                o.positionCS = TransformObjectToHClip(grown);
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                return half4(MixFog(_OutlineColor.rgb, i.fogFactor), 1);
            }
            ENDHLSL
        }

        // 影を落とす・深度を書く処理は URP の Lit シェーダーのものを借りる
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }

    FallBack "Universal Render Pipeline/Lit"
}
