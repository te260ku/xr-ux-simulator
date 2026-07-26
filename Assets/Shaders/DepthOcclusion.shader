Shader "Custom/MR/DepthOcclusion"
{
    Properties
    {
        _EnvironmentDepthBias ("Environment Depth Bias", Range(0.0, 0.3)) = 0.06
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Overlay"
        }

        Pass
        {
            Name "DepthOcclusion"

            ZWrite Off
            ZTest Always
            Cull Off

            Stencil
            {
                Ref 1
                ReadMask 255
                WriteMask 0
                Comp Equal
                Pass Keep
                Fail Keep
                ZFail Keep
            }

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0

            #pragma multi_compile _ HARD_OCCLUSION SOFT_OCCLUSION
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            #include "Packages/com.meta.xr.sdk.core/Shaders/EnvironmentDepth/URP/EnvironmentOcclusionURP.hlsl"

            float _EnvironmentDepthBias;

            float3 ReconstructWorldPosition(float2 uv)
            {
                float rawDepth = SampleSceneDepth(uv);

                #if !UNITY_REVERSED_Z
                    rawDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, rawDepth);
                #endif

                return ComputeWorldSpacePosition(uv, rawDepth, UNITY_MATRIX_I_VP);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord;

                half4 col = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

                if (col.a <= 0.001h)
                {
                    return col;
                }

                float3 virtualWorldPos = ReconstructWorldPosition(uv);

                META_DEPTH_OCCLUDE_OUTPUT_PREMULTIPLY_WORLDPOS(
                    virtualWorldPos,
                    col,
                    _EnvironmentDepthBias
                );

                return col;
            }

            ENDHLSL
        }
    }

    Fallback Off
}