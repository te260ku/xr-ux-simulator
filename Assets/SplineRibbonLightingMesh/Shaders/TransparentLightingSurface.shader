Shader "SplineRibbonLighting/TransparentLightingSurface"
{
    Properties
    {
        [HDR] _LightColor ("Light Color", Color) = (1, 1, 1, 1)
        _Intensity ("Intensity", Range(0, 10)) = 1

        [Header(Transparency)]
        _BackAlpha ("Back Alpha", Range(0, 1)) = 1
        _FrontAlpha ("Front Alpha", Range(0, 1)) = 0
        _FalloffPower ("Falloff Power", Range(0.05, 8)) = 1
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
            Tags
            {
                "LightMode" = "UniversalForward"
            }

            // The generated ribbon can be viewed from either side.
            Cull Off

            // Standard alpha blending.
            Blend SrcAlpha OneMinusSrcAlpha

            // Transparent surfaces normally should not write depth.
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
                 * Mesh UV contract:
                 *
                 *   Spline A / back
                 *   UV.y = 1
                 *
                 *       ↓ becomes more transparent
                 *
                 *   Spline B / front
                 *   UV.y = 0
                 */

                float backFactor =
                    pow(saturate(input.uv.y), _FalloffPower);

                float alpha =
                    lerp(
                        _FrontAlpha,
                        _BackAlpha,
                        backFactor);

                alpha *= _LightColor.a;

                // HDR color/intensity can drive Bloom when post-processing
                // is enabled.
                float3 color =
                    _LightColor.rgb * _Intensity;

                return half4(color, saturate(alpha));
            }

            ENDHLSL
        }
    }

    FallBack Off
}
