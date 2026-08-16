Shader "Custom/LEDOverlay"
{
    Properties
    {
        [HDR] _Color ("LED Color", Color) = (1, 1, 1, 1)

        _Intensity ("Intensity", Range(0, 20)) = 1

        // Gaussianのぼけ幅（σ）
        // Unity 1 unit = 1mなら、0.01 = 1cm
        _BlurSigma ("Blur Sigma", Range(0.001, 1.0)) = 0.02

        // 角丸半径
        _CornerRadius ("Corner Radius", Range(0, 1.0)) = 0.05
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Blend One One
            ZWrite Off
            Cull Back

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)

                half4 _Color;
                float _Intensity;
                float _BlurSigma;
                float _CornerRadius;

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
                // ==========================================
                // Quadのワールド空間上のサイズ
                // ==========================================

                float3 worldX =
                    mul(
                        (float3x3)unity_ObjectToWorld,
                        float3(1, 0, 0)
                    );

                float3 worldY =
                    mul(
                        (float3x3)unity_ObjectToWorld,
                        float3(0, 1, 0)
                    );

                float width = length(worldX);
                float height = length(worldY);

                float2 size =
                    float2(width, height);

                float2 halfSize =
                    size * 0.5;


                // ==========================================
                // UV → Quad中央基準の実距離
                // ==========================================

                float2 p =
                    (input.uv - float2(0.5, 0.5))
                    * size;


                // ==========================================
                // Corner Radius
                // ==========================================

                float maxRadius =
                    min(
                        halfSize.x,
                        halfSize.y
                    );

                float radius =
                    min(
                        _CornerRadius,
                        maxRadius
                    );


                // ==========================================
                // Rounded Rectangle SDF
                //
                // sdf < 0 : 内側
                // sdf = 0 : 境界
                // sdf > 0 : 外側
                // ==========================================

                float2 q =
                    abs(p)
                    - (halfSize - radius);

                float sdf =
                    length(max(q, 0.0))
                    + min(
                        max(q.x, q.y),
                        0.0
                    )
                    - radius;


                // ==========================================
                // 境界から内側への実距離
                // ==========================================

                float distanceInside =
                    max(-sdf, 0.0);


                // ==========================================
                // Gaussian系の減衰
                //
                // 境界     : 0
                // 内側1σ   : 約0.39
                // 内側2σ   : 約0.86
                // 内側3σ   : 約0.99
                // ==========================================

                float sigma =
                    max(
                        _BlurSigma,
                        0.0001
                    );

                float normalizedDistance =
                    distanceInside / sigma;

                float mask =
                    1.0
                    - exp(
                        -0.5
                        * normalizedDistance
                        * normalizedDistance
                    );


                // ==========================================
                // Rounded Rectangleの外側は完全に消す
                // ==========================================

                mask *= step(sdf, 0.0);


                // ==========================================
                // 発光
                // ==========================================

                half3 color =
                    _Color.rgb
                    * _Intensity
                    * mask;

                return half4(
                    color,
                    1.0
                );
            }

            ENDHLSL
        }
    }
}