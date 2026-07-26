Shader "Custom/URP/BlackCeramicPortalGui"
{
    Properties
    {
        [NoScaleOffset]
        _GuiTex("GUI Render Texture", 2D) = "black" {}

        _BaseColor(
            "Black Ceramic Color",
            Color
        ) = (0.005, 0.005, 0.005, 1)

        _GuiTint(
            "GUI Tint",
            Color
        ) = (1, 1, 1, 1)

        _GuiIntensity(
            "GUI Intensity",
            Range(0, 5)
        ) = 1

        [Toggle]
        _FlipX("Flip X", Float) = 0

        [Toggle]
        _FlipY("Flip Y", Float) = 0

        [Enum(Front Face, 1, Back Face, 0)]
        _InteriorIsFrontFace(
            "Interior Face",
            Float
        ) = 0

        _PortalEpsilon(
            "Portal Depth Epsilon",
            Float
        ) = 0.0005
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "BlackCeramicPortal"

            Tags
            {
                "LightMode" = "UniversalForward"
            }

            // 黒セラは両面とも黒く描画する
            Cull Off

            // 黒セラ自体の位置で深度を書き込む
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM

            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS  : TEXCOORD0;

                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_GuiTex);
            SAMPLER(sampler_GuiTex);

            CBUFFER_START(UnityPerMaterial)

                half4 _BaseColor;
                half4 _GuiTint;

                float4 _PlaneOriginWS;
                float4 _PlaneRightWS;
                float4 _PlaneUpWS;
                float4 _PlaneNormalWS;

                // x = 幅[m]
                // y = 高さ[m]
                float4 _PlaneSize;

                float _GuiIntensity;
                float _FlipX;
                float _FlipY;

                float _InteriorIsFrontFace;
                float _PortalEpsilon;

            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output =
                    (Varyings)0;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionWS =
                    TransformObjectToWorld(
                        input.positionOS.xyz);

                output.positionHCS =
                    TransformWorldToHClip(
                        output.positionWS);

                return output;
            }

            half4 Frag(
                Varyings input,
                FRONT_FACE_TYPE faceData
                    : FRONT_FACE_SEMANTIC
            ) : SV_Target
            {
                /*
                 * ここで現在描画中の左眼または右眼を
                 * シェーダーへ反映する。
                 */
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(
                    input);

                half3 baseColor =
                    _BaseColor.rgb;

                /*
                 * 三角形の表裏を判定する。
                 * 法線方向ではなく、頂点順序に基づく面判定。
                 */
                float isFrontFace =
                    IS_FRONT_VFACE(
                        faceData,
                        1.0,
                        0.0);

                bool isInteriorFace =
                    _InteriorIsFrontFace > 0.5
                        ? isFrontFace > 0.5
                        : isFrontFace < 0.5;

                /*
                 * 車外側から見ている場合は、
                 * 黒セラだけを表示する。
                 */
                if (!isInteriorFace)
                {
                    return half4(
                        baseColor,
                        1.0);
                }

                /*
                 * 現在レンダリング中の眼位置。
                 * 固定ReferenceEyeは使用しない。
                 */
                float3 eyePositionWS =
                    GetCameraPositionWS();

                /*
                 * 眼から黒セラ上の現在のピクセルへ向かうRay。
                 */
                float3 eyeToSurface =
                    input.positionWS -
                    eyePositionWS;

                float surfaceDistance =
                    length(eyeToSurface);

                if (surfaceDistance <= 0.00001)
                {
                    return half4(
                        baseColor,
                        1.0);
                }

                float3 rayDirection =
                    eyeToSurface /
                    surfaceDistance;

                float3 planeNormal =
                    normalize(
                        _PlaneNormalWS.xyz);

                /*
                 * Rayと平面の交差判定。
                 */
                float denominator =
                    dot(
                        rayDirection,
                        planeNormal);

                if (abs(denominator) <= 0.00001)
                {
                    return half4(
                        baseColor,
                        1.0);
                }

                float intersectionDistance =
                    dot(
                        _PlaneOriginWS.xyz -
                        eyePositionWS,
                        planeNormal
                    ) /
                    denominator;

                /*
                 * 仮想平面が黒セラより手前にある場合は
                 * GUIを描画しない。
                 */
                if (
                    intersectionDistance <=
                    surfaceDistance +
                    _PortalEpsilon
                )
                {
                    return half4(
                        baseColor,
                        1.0);
                }

                float3 hitPositionWS =
                    eyePositionWS +
                    rayDirection *
                    intersectionDistance;

                float3 planeOffsetWS =
                    hitPositionWS -
                    _PlaneOriginWS.xyz;

                float planeWidth =
                    max(
                        _PlaneSize.x,
                        0.00001);

                float planeHeight =
                    max(
                        _PlaneSize.y,
                        0.00001);

                float3 planeRight =
                    normalize(
                        _PlaneRightWS.xyz);

                float3 planeUp =
                    normalize(
                        _PlaneUpWS.xyz);

                /*
                 * 仮想平面上の位置をUVへ変換する。
                 */
                float2 guiUv;

                guiUv.x =
                    dot(
                        planeOffsetWS,
                        planeRight
                    ) /
                    planeWidth +
                    0.5;

                guiUv.y =
                    dot(
                        planeOffsetWS,
                        planeUp
                    ) /
                    planeHeight +
                    0.5;

                /*
                 * 仮想GUI平面の外側では黒セラだけ。
                 */
                if (
                    guiUv.x < 0.0 ||
                    guiUv.x > 1.0 ||
                    guiUv.y < 0.0 ||
                    guiUv.y > 1.0
                )
                {
                    return half4(
                        baseColor,
                        1.0);
                }

                if (_FlipX > 0.5)
                {
                    guiUv.x =
                        1.0 - guiUv.x;
                }

                if (_FlipY > 0.5)
                {
                    guiUv.y =
                        1.0 - guiUv.y;
                }

                half4 guiColor =
                    SAMPLE_TEXTURE2D(
                        _GuiTex,
                        sampler_GuiTex,
                        guiUv);

                /*
                 * GUIを黒セラの奥にある発光平面として合成。
                 * RenderTextureの透明領域では黒だけが残る。
                 */
                half guiAlpha =
                    saturate(
                        guiColor.a *
                        _GuiTint.a);

                half3 guiEmission =
                    guiColor.rgb *
                    _GuiTint.rgb *
                    _GuiIntensity;

                half3 finalColor =
                    saturate(
                        baseColor +
                        guiEmission *
                        guiAlpha);

                return half4(
                    finalColor,
                    1.0);
            }

            ENDHLSL
        }
    }
}