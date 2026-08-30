using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(
    typeof(MeshFilter),
    typeof(MeshRenderer)
)]
public class SegmentMesh : MonoBehaviour
{
    [Min(3)]
    public int resolution = 16;

    [Min(0.1f)]
    public float radius = 2.5f;

    [Range(1f, 360f)]
    public float angle = 30f;

    /*
     * Rotation of this Segment_X inside the complete wheel.
     *
     * This is used ONLY to generate wheel-space UVs.
     * The actual GameObject rotation is still handled by WheelGenerator.
     */
    public float wheelAngleOffset = 0f;

    public Color color =
        Color.white;


    // =========================================================
    // VISUAL RUNTIME STATE
    // =========================================================

    private Material visualMaterial;

    private Texture cosmeticPatternTexture;

    private Color cosmeticPatternColor =
        Color.black;

    private float cosmeticPatternOpacity =
        0f;

    private float cosmeticPatternScale =
        4f;

    private float cosmeticPatternRotation =
        0f;


    private bool blocked =
        false;

    private Color blockedBaseColor =
        Color.white;

    private float blockedBaseBlend =
        0.8f;

    private Color blockedStripeColor =
        Color.black;

    private float blockedStripeOpacity =
        0.35f;

    private float blockedStripeDensity =
        10f;

    private float blockedStripeWidth =
        0.12f;


    private MaterialPropertyBlock propertyBlock;


    // =========================================================
    // PUBLIC STATE
    // =========================================================

    public bool IsBlocked =>
        blocked;


    // =========================================================
    // SHADER PROPERTY IDS
    // =========================================================

    private static readonly int BaseColorId =
        Shader.PropertyToID(
            "_BaseColor"
        );

    /*
     * Compatibility fallback for Sprites/Default when the custom shader
     * has not been assigned yet.
     */
    private static readonly int LegacyColorId =
        Shader.PropertyToID(
            "_Color"
        );

    private static readonly int PatternTexId =
        Shader.PropertyToID(
            "_PatternTex"
        );

    private static readonly int PatternColorId =
        Shader.PropertyToID(
            "_PatternColor"
        );

    private static readonly int PatternOpacityId =
        Shader.PropertyToID(
            "_PatternOpacity"
        );

    private static readonly int PatternScaleId =
        Shader.PropertyToID(
            "_PatternScale"
        );

    private static readonly int PatternRotationId =
        Shader.PropertyToID(
            "_PatternRotation"
        );

    private static readonly int BlockedId =
        Shader.PropertyToID(
            "_Blocked"
        );

    private static readonly int BlockedBaseColorId =
        Shader.PropertyToID(
            "_BlockedBaseColor"
        );

    private static readonly int BlockedBaseBlendId =
        Shader.PropertyToID(
            "_BlockedBaseBlend"
        );

    private static readonly int BlockedStripeColorId =
        Shader.PropertyToID(
            "_BlockedStripeColor"
        );

    private static readonly int BlockedStripeOpacityId =
        Shader.PropertyToID(
            "_BlockedStripeOpacity"
        );

    private static readonly int BlockedStripeDensityId =
        Shader.PropertyToID(
            "_BlockedStripeDensity"
        );

    private static readonly int BlockedStripeWidthId =
        Shader.PropertyToID(
            "_BlockedStripeWidth"
        );


    // =========================================================
    // CONFIGURATION
    // =========================================================

    public void SetVisualMaterial(
        Material material)
    {
        visualMaterial =
            material;

        MeshRenderer renderer =
            GetComponent<MeshRenderer>();

        if (visualMaterial != null)
        {
            renderer.sharedMaterial =
                visualMaterial;
        }
        else if (renderer.sharedMaterial == null)
        {
            /*
             * Safe backwards-compatible fallback.
             *
             * The wheel still renders exactly as before even if the
             * SegmentVisual shader has not been assigned yet.
             */
            Shader fallbackShader =
                Shader.Find(
                    "Sprites/Default"
                );

            if (fallbackShader != null)
            {
                renderer.sharedMaterial =
                    new Material(
                        fallbackShader
                    );
            }
        }

        ApplyVisualState();
    }


    public void ConfigureCosmeticPattern(
        Texture texture,
        Color patternColor,
        float opacity,
        float scale,
        float rotation)
    {
        cosmeticPatternTexture =
            texture;

        cosmeticPatternColor =
            patternColor;

        cosmeticPatternOpacity =
            Mathf.Clamp01(
                opacity
            );

        cosmeticPatternScale =
            Mathf.Max(
                0.01f,
                scale
            );

        cosmeticPatternRotation =
            rotation;

        ApplyVisualState();
    }


    public void ConfigureBlockedVisual(
        Color baseColor,
        float baseBlend,
        Color stripeColor,
        float stripeOpacity,
        float stripeDensity,
        float stripeWidth)
    {
        blockedBaseColor =
            baseColor;

        blockedBaseBlend =
            Mathf.Clamp01(
                baseBlend
            );

        blockedStripeColor =
            stripeColor;

        blockedStripeOpacity =
            Mathf.Clamp01(
                stripeOpacity
            );

        blockedStripeDensity =
            Mathf.Max(
                0.01f,
                stripeDensity
            );

        blockedStripeWidth =
            Mathf.Clamp(
                stripeWidth,
                0.01f,
                0.45f
            );

        ApplyVisualState();
    }


    public void SetBlocked(
        bool value)
    {
        blocked =
            value;

        ApplyVisualState();
    }


    // =========================================================
    // MESH
    // =========================================================

    public void GenerateMesh()
    {
        Mesh mesh =
            new Mesh
            {
                name = "SegmentMesh"
            };

        GetComponent<MeshFilter>()
            .sharedMesh =
                mesh;


        int safeResolution =
            Mathf.Max(
                3,
                resolution
            );

        int vertexCount =
            safeResolution + 2;

        Vector3[] vertices =
            new Vector3[
                vertexCount
            ];

        Vector2[] uvs =
            new Vector2[
                vertexCount
            ];

        int[] triangles =
            new int[
                safeResolution * 3
            ];


        vertices[0] =
            Vector3.zero;

        uvs[0] =
            WheelSpaceToUV(
                Vector2.zero
            );


        for (int i = 0;
             i <= safeResolution;
             i++)
        {
            float t =
                i /
                (float)safeResolution;

            float a =
                Mathf.Deg2Rad *
                (
                    t *
                    angle
                );

            float x =
                Mathf.Cos(a) *
                radius;

            float y =
                Mathf.Sin(a) *
                radius;

            Vector2 localPoint =
                new Vector2(
                    x,
                    y
                );

            vertices[i + 1] =
                new Vector3(
                    x,
                    y,
                    0f
                );

            /*
             * IMPORTANT FOR WHEEL SHIFTER / FUTURE PATTERNS:
             *
             * UVs use the point's position in COMPLETE WHEEL space,
             * not "0..1 across this wedge".
             *
             * Therefore a segment that changes angle does not stretch
             * the texture or hatching. It simply reveals a larger/smaller
             * part of the same wheel-space pattern.
             */
            uvs[i + 1] =
                WheelSpaceToUV(
                    localPoint
                );
        }


        for (int i = 0;
             i < safeResolution;
             i++)
        {
            int tri =
                i * 3;

            triangles[tri] =
                0;

            triangles[tri + 1] =
                i + 1;

            triangles[tri + 2] =
                i + 2;
        }


        mesh.vertices =
            vertices;

        mesh.uv =
            uvs;

        mesh.triangles =
            triangles;

        mesh.RecalculateBounds();


        /*
         * Preserve the current material assignment and push colors /
         * gameplay state through a MaterialPropertyBlock.
         *
         * This lets every segment share ONE material while still having
         * different base colors and blocked state.
         */
        SetVisualMaterial(
            visualMaterial
        );

        ApplyVisualState();
    }


    private Vector2 WheelSpaceToUV(
        Vector2 localPoint)
    {
        float radians =
            wheelAngleOffset *
            Mathf.Deg2Rad;

        float cos =
            Mathf.Cos(
                radians
            );

        float sin =
            Mathf.Sin(
                radians
            );

        Vector2 wheelPoint =
            new Vector2(
                localPoint.x * cos -
                localPoint.y * sin,

                localPoint.x * sin +
                localPoint.y * cos
            );


        float diameter =
            Mathf.Max(
                0.0001f,
                radius * 2f
            );


        return
            new Vector2(
                wheelPoint.x /
                    diameter +
                    0.5f,

                wheelPoint.y /
                    diameter +
                    0.5f
            );
    }


    // =========================================================
    // VISUAL APPLY
    // =========================================================

    public void ApplyVisualState()
    {
        MeshRenderer renderer =
            GetComponent<MeshRenderer>();

        if (renderer == null)
            return;


        if (propertyBlock == null)
        {
            propertyBlock =
                new MaterialPropertyBlock();
        }


        renderer.GetPropertyBlock(
            propertyBlock
        );


        propertyBlock.SetColor(
            BaseColorId,
            color
        );

        propertyBlock.SetColor(
            LegacyColorId,
            color
        );


        if (cosmeticPatternTexture != null)
        {
            propertyBlock.SetTexture(
                PatternTexId,
                cosmeticPatternTexture
            );
        }


        propertyBlock.SetColor(
            PatternColorId,
            cosmeticPatternColor
        );

        propertyBlock.SetFloat(
            PatternOpacityId,
            cosmeticPatternOpacity
        );

        propertyBlock.SetFloat(
            PatternScaleId,
            cosmeticPatternScale
        );

        propertyBlock.SetFloat(
            PatternRotationId,
            cosmeticPatternRotation
        );


        propertyBlock.SetFloat(
            BlockedId,
            blocked
                ? 1f
                : 0f
        );

        propertyBlock.SetColor(
            BlockedBaseColorId,
            blockedBaseColor
        );

        propertyBlock.SetFloat(
            BlockedBaseBlendId,
            blockedBaseBlend
        );

        propertyBlock.SetColor(
            BlockedStripeColorId,
            blockedStripeColor
        );

        propertyBlock.SetFloat(
            BlockedStripeOpacityId,
            blockedStripeOpacity
        );

        propertyBlock.SetFloat(
            BlockedStripeDensityId,
            blockedStripeDensity
        );

        propertyBlock.SetFloat(
            BlockedStripeWidthId,
            blockedStripeWidth
        );


        renderer.SetPropertyBlock(
            propertyBlock
        );
    }
}