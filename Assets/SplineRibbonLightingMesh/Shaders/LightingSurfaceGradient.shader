Shader "SplineRibbonLighting/LightingSurfaceGradient"
{
    Properties
    {
        _LightColor ("Light Color", Color) = (1,1,1,1)
        _Intensity ("Intensity", Range(0,10)) = 1
        _FrontIntensity ("Front Intensity", Range(0,1)) = 0
        _FalloffPower ("Falloff Power", Range(0.05,8)) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode"="UniversalForward" }
            Cull Back
            ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; };
            CBUFFER_START(UnityPerMaterial)
                float4 _LightColor;
                float _Intensity;
                float _FrontIntensity;
                float _FalloffPower;
            CBUFFER_END
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                // UV.y = 1: Spline A / back / strong. UV.y = 0: Spline B / front / weak.
                float falloff = pow(saturate(input.uv.y), _FalloffPower);
                float attenuation = lerp(saturate(_FrontIntensity), 1.0, falloff);
                return half4(_LightColor.rgb * _Intensity * attenuation, _LightColor.a);
            }
            ENDHLSL
        }
    }
}
