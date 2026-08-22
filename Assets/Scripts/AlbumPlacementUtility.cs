using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AlbumPlacementUtility : MonoBehaviour
{
    // =========================================================
    // MAIN VALIDATION
    // =========================================================

    public static bool CanPlaceInAlbum(
        BaseSticker sticker,
        AlbumZone albumZone)
    {
        if (sticker == null ||
            albumZone == null ||
            albumZone.areaCollider == null)
        {
            return false;
        }

        Collider2D stickerCollider =
            sticker.GetComponent<Collider2D>();

        if (stickerCollider == null ||
            !stickerCollider.enabled)
        {
            return false;
        }

        Physics2D.SyncTransforms();

        if (!IsColliderInsideAlbum(
                stickerCollider,
                albumZone.areaCollider,
                albumZone.boundaryTolerance))
        {
            return false;
        }

        if (OverlapsAnotherAlbumSticker(
                sticker,
                stickerCollider,
                albumZone))
        {
            return false;
        }

        return true;
    }

    // =========================================================
    // INSIDE ALBUM
    // =========================================================

    private static bool IsColliderInsideAlbum(
        Collider2D stickerCollider,
        Collider2D albumCollider,
        float tolerance)
    {
        List<Vector2> points =
            GetBoundaryPoints(stickerCollider);

        if (points.Count == 0)
            return false;

        foreach (Vector2 point in points)
        {
            if (!IsPointInsideWithTolerance(
                    albumCollider,
                    point,
                    tolerance))
            {
                return false;
            }
        }

        return true;
    }

    // =========================================================
    // POINT VALIDATION
    // =========================================================

    private static bool IsPointInsideWithTolerance(
        Collider2D area,
        Vector2 point,
        float tolerance)
    {
        if (area.OverlapPoint(point))
            return true;

        Vector2 closest =
            area.ClosestPoint(point);

        float distance =
            Vector2.Distance(
                point,
                closest
            );

        return distance <= tolerance;
    }

    // =========================================================
    // OVERLAP
    // =========================================================

    private static bool OverlapsAnotherAlbumSticker(
        BaseSticker sticker,
        Collider2D stickerCollider,
        AlbumZone albumZone)
    {
        BaseSticker[] allStickers =
            Object.FindObjectsOfType<BaseSticker>(true);

        foreach (BaseSticker other in allStickers)
        {
            if (other == null ||
                other == sticker)
            {
                continue;
            }

            /*
             * Por ahora identificamos un sticker del álbum
             * comprobando si su root está parentado dentro
             * del ContentRoot del AlbumZone.
             *
             * En el siguiente paso añadiremos currentAlbumZone
             * a BaseSticker y esto quedará todavía más limpio.
             */
            Transform otherRoot =
                other.stickerRoot != null
                    ? other.stickerRoot
                    : other.transform;

            Transform contentRoot =
                albumZone.GetContentRoot();

            if (otherRoot == null ||
                contentRoot == null)
            {
                continue;
            }

            if (!otherRoot.IsChildOf(contentRoot))
                continue;

            Collider2D otherCollider =
                other.GetComponent<Collider2D>();

            if (otherCollider == null ||
                !otherCollider.enabled)
            {
                continue;
            }

            ColliderDistance2D distance =
                stickerCollider.Distance(
                    otherCollider
                );

            if (distance.isOverlapped)
                return true;
        }

        return false;
    }

    // =========================================================
    // COLLIDER BOUNDARY SAMPLING
    // =========================================================

    private static List<Vector2> GetBoundaryPoints(
        Collider2D collider)
    {
        List<Vector2> points =
            new List<Vector2>();

        // -----------------------------------------------------
        // POLYGON
        // -----------------------------------------------------

        PolygonCollider2D polygon =
            collider as PolygonCollider2D;

        if (polygon != null)
        {
            for (int pathIndex = 0;
                 pathIndex < polygon.pathCount;
                 pathIndex++)
            {
                Vector2[] path =
                    polygon.GetPath(pathIndex);

                AddPolygonPathPoints(
                    polygon,
                    path,
                    points
                );
            }

            return points;
        }

        // -----------------------------------------------------
        // BOX
        // -----------------------------------------------------

        BoxCollider2D box =
            collider as BoxCollider2D;

        if (box != null)
        {
            AddBoxPoints(
                box,
                points
            );

            return points;
        }

        // -----------------------------------------------------
        // CIRCLE
        // -----------------------------------------------------

        CircleCollider2D circle =
            collider as CircleCollider2D;

        if (circle != null)
        {
            AddCirclePoints(
                circle,
                points
            );

            return points;
        }

        // -----------------------------------------------------
        // FALLBACK
        // -----------------------------------------------------

        AddBoundsPoints(
            collider.bounds,
            points
        );

        return points;
    }

    // =========================================================
    // POLYGON
    // =========================================================

    private static void AddPolygonPathPoints(
        PolygonCollider2D polygon,
        Vector2[] path,
        List<Vector2> output)
    {
        if (path == null ||
            path.Length == 0)
        {
            return;
        }

        const int edgeSubdivisions = 4;

        for (int i = 0;
             i < path.Length;
             i++)
        {
            Vector2 localA =
                path[i] +
                polygon.offset;

            Vector2 localB =
                path[(i + 1) % path.Length] +
                polygon.offset;

            Vector2 worldA =
                polygon.transform.TransformPoint(
                    localA
                );

            Vector2 worldB =
                polygon.transform.TransformPoint(
                    localB
                );

            for (int s = 0;
                 s <= edgeSubdivisions;
                 s++)
            {
                float t =
                    s /
                    (float)edgeSubdivisions;

                output.Add(
                    Vector2.Lerp(
                        worldA,
                        worldB,
                        t
                    )
                );
            }
        }
    }

    // =========================================================
    // BOX
    // =========================================================

    private static void AddBoxPoints(
        BoxCollider2D box,
        List<Vector2> output)
    {
        Vector2 half =
            box.size * 0.5f;

        Vector2[] localCorners =
        {
            box.offset + new Vector2(-half.x, -half.y),
            box.offset + new Vector2(-half.x,  half.y),
            box.offset + new Vector2( half.x,  half.y),
            box.offset + new Vector2( half.x, -half.y)
        };

        const int edgeSubdivisions = 4;

        for (int i = 0;
             i < localCorners.Length;
             i++)
        {
            Vector2 worldA =
                box.transform.TransformPoint(
                    localCorners[i]
                );

            Vector2 worldB =
                box.transform.TransformPoint(
                    localCorners[
                        (i + 1) %
                        localCorners.Length
                    ]
                );

            for (int s = 0;
                 s <= edgeSubdivisions;
                 s++)
            {
                float t =
                    s /
                    (float)edgeSubdivisions;

                output.Add(
                    Vector2.Lerp(
                        worldA,
                        worldB,
                        t
                    )
                );
            }
        }
    }

    // =========================================================
    // CIRCLE
    // =========================================================

    private static void AddCirclePoints(
        CircleCollider2D circle,
        List<Vector2> output)
    {
        const int samples = 32;

        for (int i = 0;
             i < samples;
             i++)
        {
            float angle =
                i /
                (float)samples *
                Mathf.PI *
                2f;

            Vector2 localPoint =
                circle.offset +
                new Vector2(
                    Mathf.Cos(angle),
                    Mathf.Sin(angle)
                ) *
                circle.radius;

            Vector2 worldPoint =
                circle.transform.TransformPoint(
                    localPoint
                );

            output.Add(worldPoint);
        }
    }

    // =========================================================
    // FALLBACK BOUNDS
    // =========================================================

    private static void AddBoundsPoints(
        Bounds bounds,
        List<Vector2> output)
    {
        Vector2 min =
            bounds.min;

        Vector2 max =
            bounds.max;

        Vector2 bottomLeft =
            new Vector2(min.x, min.y);

        Vector2 topLeft =
            new Vector2(min.x, max.y);

        Vector2 topRight =
            new Vector2(max.x, max.y);

        Vector2 bottomRight =
            new Vector2(max.x, min.y);

        output.Add(bottomLeft);
        output.Add(topLeft);
        output.Add(topRight);
        output.Add(bottomRight);

        output.Add(
            (bottomLeft + topLeft) * 0.5f
        );

        output.Add(
            (topLeft + topRight) * 0.5f
        );

        output.Add(
            (topRight + bottomRight) * 0.5f
        );

        output.Add(
            (bottomRight + bottomLeft) * 0.5f
        );
    }
}
