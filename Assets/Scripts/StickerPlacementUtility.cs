using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Única fuente de verdad para validar stickers colocados sobre la ruleta.
///
/// Regla:
/// 1) La geometría real del collider del sticker debe quedar dentro del segmento.
/// 2) El collider real del sticker no puede solaparse con otro sticker colocado en la ruleta.
///
/// La tolerancia es una pequeña distancia en unidades de mundo que permitimos fuera del
/// borde del segmento para evitar falsos negativos por precisión de floating point.
/// </summary>
public static class StickerPlacementUtility
{
    private const int CircleSamples = 32;
    private const int PolygonEdgeSubdivisions = 4;

    /// <summary>
    /// Busca entre los segmentos que están bajo el área del sticker y devuelve el primero
    /// en el que la colocación completa sea válida.
    /// </summary>
    public static Collider2D FindValidSegment(
        BaseSticker sticker,
        LayerMask segmentMask,
        float boundaryTolerance)
    {
        if (sticker == null)
            return null;

        Collider2D stickerCollider = sticker.StickerCollider;

        if (stickerCollider == null || !stickerCollider.enabled)
            return null;

        Bounds b = stickerCollider.bounds;

        Collider2D[] candidates = Physics2D.OverlapBoxAll(
            b.center,
            b.size,
            0f,
            segmentMask
        );

        foreach (Collider2D candidate in candidates)
        {
            if (candidate == null || !candidate.enabled)
                continue;

            if (CanPlaceOnSegment(sticker, candidate, boundaryTolerance))
                return candidate;
        }

        return null;
    }

    /// <summary>
    /// Valida un sticker contra un segmento concreto.
    /// </summary>
    public static bool CanPlaceOnSegment(
        BaseSticker sticker,
        Collider2D segment,
        float boundaryTolerance)
    {
        if (sticker == null || segment == null)
            return false;

        Collider2D stickerCollider = sticker.StickerCollider;

        if (stickerCollider == null ||
            !stickerCollider.enabled ||
            !segment.enabled)
        {
            return false;
        }

        if (!IsColliderInsideSegment(
                stickerCollider,
                segment,
                boundaryTolerance))
        {
            return false;
        }

        if (OverlapsAnotherWheelSticker(sticker, stickerCollider))
            return false;

        return true;
    }

    /// <summary>
    /// Comprueba la forma real del collider, no su Bounds rectangular.
    /// </summary>
    public static bool IsColliderInsideSegment(
        Collider2D stickerCollider,
        Collider2D segment,
        float boundaryTolerance)
    {
        if (stickerCollider == null || segment == null)
            return false;

        List<Vector2> samplePoints = new List<Vector2>(64);

        CollectWorldBoundaryPoints(
            stickerCollider,
            samplePoints
        );

        if (samplePoints.Count == 0)
            return false;

        float tolerance = Mathf.Max(
            0f,
            boundaryTolerance
        );

        foreach (Vector2 point in samplePoints)
        {
            if (!IsPointInsideOrNearSegment(
                    segment,
                    point,
                    tolerance))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Comprueba solape REAL entre colliders.
    /// Solo compara contra otros stickers ya colocados en la ruleta.
    /// </summary>
    public static bool OverlapsAnotherWheelSticker(
        BaseSticker sticker,
        Collider2D stickerCollider)
    {
        if (sticker == null || stickerCollider == null)
            return false;

        BaseSticker[] allStickers =
            Object.FindObjectsOfType<BaseSticker>(true);

        foreach (BaseSticker other in allStickers)
        {
            if (other == null || other == sticker)
                continue;

            // Solo nos importan stickers realmente colocados en la ruleta.
            if (!other.isPlaced || other.currentSegment == null)
                continue;

            Collider2D otherCollider =
                other.StickerCollider;

            if (otherCollider == null ||
                !otherCollider.enabled)
            {
                continue;
            }

            ColliderDistance2D distance =
                stickerCollider.Distance(otherCollider);

            if (distance.isOverlapped)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Consideramos válido un punto si está dentro del segmento,
    /// o si está solo una distancia minúscula fuera según tolerance.
    /// </summary>
    private static bool IsPointInsideOrNearSegment(
        Collider2D segment,
        Vector2 point,
        float tolerance)
    {
        if (segment.OverlapPoint(point))
            return true;

        if (tolerance <= 0f)
            return false;

        Vector2 closest =
            segment.ClosestPoint(point);

        return Vector2.Distance(
            point,
            closest
        ) <= tolerance;
    }

    /// <summary>
    /// Obtiene puntos de la geometría real del collider.
    /// PolygonCollider2D es el caso principal de Hell's Roulette.
    /// </summary>
    private static void CollectWorldBoundaryPoints(
        Collider2D col,
        List<Vector2> points)
    {
        if (col is PolygonCollider2D polygon)
        {
            CollectPolygonPoints(
                polygon,
                points
            );

            return;
        }

        if (col is BoxCollider2D box)
        {
            CollectBoxPoints(
                box,
                points
            );

            return;
        }

        if (col is CircleCollider2D circle)
        {
            CollectCirclePoints(
                circle,
                points
            );

            return;
        }

        // Fallback para otros tipos de collider.
        CollectBoundsPoints(
            col.bounds,
            points
        );
    }

    /// <summary>
    /// PolygonCollider2D:
    /// comprobamos los vértices reales y varios puntos de cada arista.
    /// Así no dependemos del rectángulo Bounds.
    /// </summary>
    private static void CollectPolygonPoints(
        PolygonCollider2D polygon,
        List<Vector2> points)
    {
        for (int pathIndex = 0;
             pathIndex < polygon.pathCount;
             pathIndex++)
        {
            Vector2[] path =
                polygon.GetPath(pathIndex);

            if (path == null || path.Length == 0)
                continue;

            for (int i = 0; i < path.Length; i++)
            {
                Vector2 localA =
                    path[i] + polygon.offset;

                Vector2 localB =
                    path[(i + 1) % path.Length] +
                    polygon.offset;

                for (int step = 0;
                     step < PolygonEdgeSubdivisions;
                     step++)
                {
                    float t =
                        step /
                        (float)PolygonEdgeSubdivisions;

                    Vector2 localPoint =
                        Vector2.Lerp(
                            localA,
                            localB,
                            t
                        );

                    points.Add(
                        polygon.transform.TransformPoint(
                            localPoint
                        )
                    );
                }
            }
        }
    }

    /// <summary>
    /// Soporte para BoxCollider2D por si algún sticker lo utiliza.
    /// </summary>
    private static void CollectBoxPoints(
        BoxCollider2D box,
        List<Vector2> points)
    {
        Vector2 half =
            box.size * 0.5f;

        Vector2 c =
            box.offset;

        Vector2[] corners =
        {
            c + new Vector2(-half.x, -half.y),
            c + new Vector2(-half.x,  half.y),
            c + new Vector2( half.x,  half.y),
            c + new Vector2( half.x, -half.y)
        };

        for (int i = 0;
             i < corners.Length;
             i++)
        {
            Vector2 a =
                corners[i];

            Vector2 b =
                corners[(i + 1) % corners.Length];

            for (int step = 0;
                 step < PolygonEdgeSubdivisions;
                 step++)
            {
                float t =
                    step /
                    (float)PolygonEdgeSubdivisions;

                points.Add(
                    box.transform.TransformPoint(
                        Vector2.Lerp(
                            a,
                            b,
                            t
                        )
                    )
                );
            }
        }
    }

    /// <summary>
    /// Soporte para CircleCollider2D.
    /// Muestreamos el perímetro real.
    /// </summary>
    private static void CollectCirclePoints(
        CircleCollider2D circle,
        List<Vector2> points)
    {
        for (int i = 0;
             i < CircleSamples;
             i++)
        {
            float angle =
                (i / (float)CircleSamples) *
                Mathf.PI *
                2f;

            Vector2 localPoint =
                circle.offset +
                new Vector2(
                    Mathf.Cos(angle) *
                    circle.radius,

                    Mathf.Sin(angle) *
                    circle.radius
                );

            points.Add(
                circle.transform.TransformPoint(
                    localPoint
                )
            );
        }
    }

    /// <summary>
    /// Fallback conservador para colliders no contemplados.
    /// </summary>
    private static void CollectBoundsPoints(
        Bounds bounds,
        List<Vector2> points)
    {
        Vector2 min =
            bounds.min;

        Vector2 max =
            bounds.max;

        Vector2 center =
            bounds.center;

        points.Add(
            new Vector2(min.x, min.y)
        );

        points.Add(
            new Vector2(min.x, max.y)
        );

        points.Add(
            new Vector2(max.x, max.y)
        );

        points.Add(
            new Vector2(max.x, min.y)
        );

        points.Add(
            new Vector2(center.x, min.y)
        );

        points.Add(
            new Vector2(center.x, max.y)
        );

        points.Add(
            new Vector2(min.x, center.y)
        );

        points.Add(
            new Vector2(max.x, center.y)
        );
    }
}
