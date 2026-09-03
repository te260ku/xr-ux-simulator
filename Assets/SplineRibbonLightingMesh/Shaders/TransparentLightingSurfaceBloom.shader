Shader "SplineRibbonLighting/TransparentLightingSurfaceBloom"
{
    Properties
    {
        // HDR picker. Intensity values above 1 are intentionally supported.
        [HDR] _LightColor ("Light Color", Color) = (1.0, 0.45, 0.12, 1.0)
        _Intensity ("HDR Intensity", Range(0, 20)) = 4.0

        [Header(Transparency)]
        _BackAlpha ("Back Alpha", Range(0, 1)) = 1.0
        _FrontAlpha ("Front Alpha", Range(0, 1)) = 0.0
        _FalloffPower ("Alpha Falloff Power", Range(0.05, 8)) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _LightColor;
                float _Intensity;
                float _BackAlpha;
                float _FrontAlpha;
                float _FalloffPower;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionHCS =
                    TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                /*
                 * Generated mesh UV contract:
                 *
                 * Spline A / back  : UV.y = 1
                 * Spline B / front : UV.y = 0
                 *
                 * Therefore alpha decreases toward the front.
                 */
                float backFactor =
                    pow(saturate(input.uv.y), _FalloffPower);

                float alpha =
                    lerp(
                        _FrontAlpha,
                        _BackAlpha,
                        backFactor);

                alpha *= _LightColor.a;

                /*
                 * Values above 1 are deliberately preserved.
                 * URP Bloom extracts these bright HDR pixels according
                 * to the Volume Bloom Threshold.
                 */
                float3 hdrColor =
                    _LightColor.rgb * max(_Intensity, 0.0);

                return half4(hdrColor, saturate(alpha));
            }

            ENDHLSL
        }
    }

    FallBack Off
}
