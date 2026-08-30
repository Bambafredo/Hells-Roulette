Shader "HellRoulette/SegmentVisual"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1,1,1,1)

        [Header(Cosmetic Pattern)]
        _PatternTex ("Pattern Texture", 2D) = "white" {}
        _PatternColor ("Pattern Color", Color) = (0,0,0,1)
        _PatternOpacity ("Pattern Opacity", Range(0,1)) = 0
        _PatternScale ("Pattern Scale", Float) = 4
        _PatternRotation ("Pattern Rotation", Float) = 0

        [Header(Blocked Gameplay State)]
        _Blocked ("Blocked", Float) = 0
        _BlockedBaseColor ("Blocked Base Color", Color) = (1,1,1,1)
        _BlockedBaseBlend ("Blocked Base Blend", Range(0,1)) = 0.8
        _BlockedStripeColor ("Blocked Stripe Color", Color) = (0,0,0,1)
        _BlockedStripeOpacity ("Blocked Stripe Opacity", Range(0,1)) = 0.35
        _BlockedStripeDensity ("Blocked Stripe Density", Float) = 10
        _BlockedStripeWidth ("Blocked Stripe Width", Range(0.01,0.45)) = 0.12
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "SegmentVisual"

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 2.0

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

            TEXTURE2D(_PatternTex);
            SAMPLER(sampler_PatternTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;

                half4 _PatternColor;
                float _PatternOpacity;
                float _PatternScale;
                float _PatternRotation;

                float _Blocked;
                half4 _BlockedBaseColor;
                float _BlockedBaseBlend;
                half4 _BlockedStripeColor;
                float _BlockedStripeOpacity;
                float _BlockedStripeDensity;
                float _BlockedStripeWidth;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;

                VertexPositionInputs positionInputs =
                    GetVertexPositionInputs(input.positionOS.xyz);

                output.positionHCS =
                    positionInputs.positionCS;

                output.uv =
                    input.uv;

                return output;
            }

            float2 RotateAroundCenter(
                float2 uv,
                float degrees)
            {
                float radians =
                    degrees * 0.017453292519943295;

                float s =
                    sin(radians);

                float c =
                    cos(radians);

                float2 p =
                    uv - 0.5;

                float2 rotated =
                    float2(
                        p.x * c - p.y * s,
                        p.x * s + p.y * c
                    );

                return
                    rotated + 0.5;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 result =
                    _BaseColor;

                // ---------------------------------------------------------
                // COSMETIC PATTERN
                // ---------------------------------------------------------
                //
                // This layer is intentionally subtle and completely
                // independent from gameplay states.
                //
                // SegmentMesh supplies wheel-space UVs, so resizing a wedge
                // reveals more / less of the pattern instead of stretching it.
                // ---------------------------------------------------------

                if (_Blocked < 0.5)
                {
                    float safeScale =
                        max(
                            0.01,
                            _PatternScale
                        );

                    float2 patternUV =
                        RotateAroundCenter(
                            input.uv,
                            _PatternRotation
                        );

                    patternUV =
                        (patternUV - 0.5) *
                        safeScale +
                        0.5;

                    half4 patternSample =
                        SAMPLE_TEXTURE2D(
                            _PatternTex,
                            sampler_PatternTex,
                            patternUV
                        );

                    float patternMask =
                        saturate(
                            patternSample.a *
                            _PatternOpacity
                        );

                    result.rgb =
                        lerp(
                            result.rgb,
                            _PatternColor.rgb,
                            patternMask
                        );
                }

                // ---------------------------------------------------------
                // BLOCKED GAMEPLAY STATE
                // ---------------------------------------------------------
                //
                // The blocked presentation deliberately replaces the cosmetic
                // pattern while active. Gameplay state must always read more
                // strongly than customization.
                // ---------------------------------------------------------

                if (_Blocked >= 0.5)
                {
                    result.rgb =
                        lerp(
                            result.rgb,
                            _BlockedBaseColor.rgb,
                            saturate(
                                _BlockedBaseBlend
                            )
                        );

                    float density =
                        max(
                            0.01,
                            _BlockedStripeDensity
                        );

                    /*
                     * uv.x + uv.y creates diagonal hatching.
                     *
                     * UVs are generated in wheel-space, therefore:
                     * - every segment uses the same stripe direction;
                     * - WheelShifter can resize wedges freely;
                     * - the stripes remain glued to the wheel.
                     */
                    float stripeCoord =
                        (input.uv.x +
                         input.uv.y) *
                        density;

                    float stripePhase =
                        frac(
                            stripeCoord
                        );

                    float distanceToStripe =
                        min(
                            stripePhase,
                            1.0 - stripePhase
                        );

                    float aa =
                        max(
                            fwidth(
                                stripeCoord
                            ),
                            0.0001
                        );

                    float stripe =
                        1.0 -
                        smoothstep(
                            _BlockedStripeWidth,
                            _BlockedStripeWidth + aa,
                            distanceToStripe
                        );

                    float stripeAmount =
                        saturate(
                            stripe *
                            _BlockedStripeOpacity
                        );

                    result.rgb =
                        lerp(
                            result.rgb,
                            _BlockedStripeColor.rgb,
                            stripeAmount
                        );
                }

                return result;
            }

            ENDHLSL
        }
    }

    Fallback Off
}
