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
        _BlockedPatternType ("Blocked Pattern Type", Float) = 0

        [Header(Telegraph Gameplay State)]
        _Telegraphed ("Telegraphed", Float) = 0
        _TelegraphColor ("Telegraph Color", Color) = (1,1,1,1)
        _TelegraphStrength ("Telegraph Strength", Range(0,1)) = 0.18
        _TelegraphPulseSpeed ("Telegraph Pulse Speed", Float) = 1.2

        [Header(Temporary Segment Mark)]
        _Marked ("Marked", Float) = 0
        _MarkColor ("Mark Color", Color) = (0.65,0.1,0.9,1)
        _MarkOpacity ("Mark Opacity", Range(0,1)) = 0.55
        _MarkDensity ("Mark Density", Float) = 8
        _MarkWidth ("Mark Width", Range(0.01,0.45)) = 0.12
        _MarkPatternType ("Mark Pattern Type", Float) = 0
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
                float _BlockedPatternType;

                float _Telegraphed;
                half4 _TelegraphColor;
                float _TelegraphStrength;
                float _TelegraphPulseSpeed;

                float _Marked;
                half4 _MarkColor;
                float _MarkOpacity;
                float _MarkDensity;
                float _MarkWidth;
                float _MarkPatternType;
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

            float ProceduralStripe(
                float stripeCoord,
                float stripeWidth)
            {
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

                return
                    1.0 -
                    smoothstep(
                        stripeWidth,
                        stripeWidth + aa,
                        distanceToStripe
                    );
            }

            float ProceduralDot(
                float2 uv,
                float density,
                float radius)
            {
                float2 cell =
                    frac(
                        uv * density
                    ) - 0.5;

                float distanceToCenter =
                    length(
                        cell
                    );

                float aa =
                    max(
                        fwidth(
                            distanceToCenter
                        ),
                        0.0001
                    );

                return
                    1.0 -
                    smoothstep(
                        radius,
                        radius + aa,
                        distanceToCenter
                    );
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
                // TELEGRAPH GAMEPLAY STATE
                // ---------------------------------------------------------
                //
                // A telegraph is a future-state hint, not a blocked state.
                // It softly pulses the segment towards the authored highlight
                // color while leaving geometry / gameplay completely untouched.
                //
                // Blocked presentation always wins if both flags are ever set.
                // ---------------------------------------------------------

                if (_Blocked < 0.5 &&
                    _Telegraphed >= 0.5)
                {
                    float speed =
                        max(
                            0.01,
                            _TelegraphPulseSpeed
                        );

                    float wave =
                        0.5 +
                        0.5 *
                        sin(
                            _Time.y *
                            speed *
                            6.2831853
                        );

                    /*
                     * Keep a faint floor instead of disappearing completely.
                     * The target remains readable, but the animation still has
                     * enough range to feel like a deliberate pulse.
                     */
                    float pulse =
                        0.2 +
                        0.8 *
                        wave;

                    float amount =
                        saturate(
                            _TelegraphStrength *
                            pulse
                        );

                    result.rgb =
                        lerp(
                            result.rgb,
                            _TelegraphColor.rgb,
                            amount
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
                     * All blocked patterns are procedural and use the same
                     * wheel-space UVs. WheelShifter can therefore resize wedges
                     * without stretching or reauthoring any texture.
                     *
                     * 0 = normal diagonal Segment Block
                     * 1 = crosshatch
                     * 2 = horizontal bars
                     * 3 = dots
                     */
                    float pattern =
                        0.0;

                    if (_BlockedPatternType < 0.5)
                    {
                        pattern =
                            ProceduralStripe(
                                (input.uv.x + input.uv.y) * density,
                                _BlockedStripeWidth
                            );
                    }
                    else if (_BlockedPatternType < 1.5)
                    {
                        float diagonalA =
                            ProceduralStripe(
                                (input.uv.x + input.uv.y) * density,
                                _BlockedStripeWidth
                            );

                        float diagonalB =
                            ProceduralStripe(
                                (input.uv.x - input.uv.y) * density,
                                _BlockedStripeWidth
                            );

                        pattern =
                            max(
                                diagonalA,
                                diagonalB
                            );
                    }
                    else if (_BlockedPatternType < 2.5)
                    {
                        pattern =
                            ProceduralStripe(
                                input.uv.y * density,
                                _BlockedStripeWidth
                            );
                    }
                    else
                    {
                        /*
                         * Reuse stripe width as a convenient authored control,
                         * remapped to a sensible dot radius inside each cell.
                         */
                        float dotRadius =
                            lerp(
                                0.12,
                                0.38,
                                saturate(
                                    _BlockedStripeWidth / 0.45
                                )
                            );

                        pattern =
                            ProceduralDot(
                                input.uv,
                                density * 0.5,
                                dotRadius
                            );
                    }

                    float stripeAmount =
                        saturate(
                            pattern *
                            _BlockedStripeOpacity
                        );

                    result.rgb =
                        lerp(
                            result.rgb,
                            _BlockedStripeColor.rgb,
                            stripeAmount
                        );
                }

                // ---------------------------------------------------------
                // TEMPORARY SEGMENT MARK
                // ---------------------------------------------------------
                //
                // This is an ADDITIONAL presentation layer.
                //
                // It is rendered after the existing blocked presentation so a
                // normal Segment Block / Limbo special block keeps its pattern
                // while effects such as Dominate can be superimposed on top.
                //
                // 0 = chevrons
                // 1 = checker
                // 2 = vertical bars
                // 3 = rings
                // ---------------------------------------------------------

                if (_Marked >= 0.5)
                {
                    float density =
                        max(
                            0.01,
                            _MarkDensity
                        );

                    float2 centered =
                        input.uv - 0.5;

                    float markPattern =
                        0.0;

                    if (_MarkPatternType < 0.5)
                    {
                        /*
                         * V-shaped repeating bands.
                         */
                        float chevronCoord =
                            (
                                abs(centered.x) +
                                centered.y
                            ) *
                            density;

                        markPattern =
                            ProceduralStripe(
                                chevronCoord,
                                _MarkWidth
                            );
                    }
                    else if (_MarkPatternType < 1.5)
                    {
                        float2 cell =
                            floor(
                                input.uv *
                                density
                            );

                        markPattern =
                            fmod(
                                cell.x +
                                cell.y,
                                2.0
                            );
                    }
                    else if (_MarkPatternType < 2.5)
                    {
                        markPattern =
                            ProceduralStripe(
                                input.uv.x *
                                density,
                                _MarkWidth
                            );
                    }
                    else
                    {
                        float radiusFromCenter =
                            length(
                                centered
                            );

                        markPattern =
                            ProceduralStripe(
                                radiusFromCenter *
                                density,
                                _MarkWidth
                            );
                    }

                    float markAmount =
                        saturate(
                            markPattern *
                            _MarkOpacity
                        );

                    result.rgb =
                        lerp(
                            result.rgb,
                            _MarkColor.rgb,
                            markAmount
                        );
                }


                return result;
            }

            ENDHLSL
        }
    }

    Fallback Off
}