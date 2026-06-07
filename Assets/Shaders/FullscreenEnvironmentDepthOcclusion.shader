Shader "Custom/MR/FullscreenEnvironmentDepthOcclusion"
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
            Name "FullscreenEnvironmentDepthOcclusion"

            ZWrite Off
            ZTest Always
            Cull Off

            // 窓領域だけ処理する。
            // 事前に WindowMask が Stencil = 1 を書いている前提。
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

            // Depth APIのHard/Soft Occlusion切り替え用。
            // EnvironmentDepthManager.OcclusionShadersMode と連動する。
            #pragma multi_compile _ HARD_OCCLUSION SOFT_OCCLUSION

            // XR / Multiview / Single Pass Instanced 対応
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            // Meta Depth API
            #include "Packages/com.meta.xr.sdk.core/Shaders/EnvironmentDepth/URP/EnvironmentOcclusionURP.hlsl"

            float _EnvironmentDepthBias;

            float3 ReconstructWorldPosition(float2 uv)
            {
                float rawDepth = SampleSceneDepth(uv);

                // UnityのComputeWorldSpacePositionは、プラットフォームごとのDepth範囲を合わせる必要がある。
                #if !UNITY_REVERSED_Z
                    rawDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, rawDepth);
                #endif

                return ComputeWorldSpacePosition(uv, rawDepth, UNITY_MATRIX_I_VP);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord;

                // Full Screen Pass Renderer Feature の Requirements で Color を有効にすると、
                // 現在のカメラカラーが _BlitTexture に入る。
                half4 col = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

                // すでに透明なら何もしない。
                // 窓外や未描画領域を無駄に処理しないため。
                if (col.a <= 0.001)
                {
                    return col;
                }

                // VirtualExterior描画後のCamera Depthから、
                // このピクセルに描かれている仮想物体のワールド座標を復元する。
                float3 virtualWorldPos = ReconstructWorldPosition(uv);

                // Meta Depth API:
                // 現実DepthがこのvirtualWorldPosより手前にある場合、
                // col.rgb / col.a がオクルージョン量に応じて減衰する。
                // 完全に隠れる場合は alpha = 0 になり、Passthrough Underlayが見える。
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