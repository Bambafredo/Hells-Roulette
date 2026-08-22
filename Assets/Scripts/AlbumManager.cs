using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AlbumManager : MonoBehaviour
{
    public static AlbumManager Instance;

    [Header("References")]
    public AlbumZone albumZone;

    // =========================================================
    // UNITY
    // =========================================================

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        EnsureReferences();
    }

    private void OnValidate()
    {
        EnsureReferences();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // =========================================================
    // REFERENCES
    // =========================================================

    private void EnsureReferences()
    {
        if (albumZone == null)
        {
            albumZone =
                GetComponentInChildren<AlbumZone>(true);
        }
    }

    // =========================================================
    // QUERIES
    // =========================================================

    public bool HasAlbum()
    {
        return
            albumZone != null &&
            albumZone.areaCollider != null;
    }

    public bool IsPointInsideAlbum(Vector2 worldPoint)
    {
        if (!HasAlbum())
            return false;

        return albumZone.ContainsPoint(worldPoint);
    }

    public AlbumZone GetAlbumZoneAtPosition(
        Vector2 worldPoint)
    {
        if (!IsPointInsideAlbum(worldPoint))
            return null;

        return albumZone;
    }

    // =========================================================
    // PLACEMENT
    // =========================================================

    /// <summary>
    /// Intenta colocar físicamente un sticker en el álbum.
    ///
    /// Este método se ocupa solamente de:
    /// - comprobar límites
    /// - comprobar solapes
    /// - parentar al ContentRoot
    ///
    /// BaseSticker gestionará su estado lógico
    /// (currentAlbumZone, currentSegment, etc.)
    /// en el siguiente paso.
    /// </summary>
    public bool TryPlaceStickerInAlbum(
        BaseSticker sticker)
    {
        if (sticker == null ||
            !HasAlbum())
        {
            return false;
        }

        Transform root =
            sticker.stickerRoot != null
                ? sticker.stickerRoot
                : sticker.transform;

        if (root == null)
            return false;

        /*
         * Para considerar que el jugador intenta colocar
         * el sticker en Album, su posición central tiene
         * que estar dentro del área.
         *
         * Después AlbumPlacementUtility comprueba
         * EL COLLIDER ENTERO.
         */
        if (!albumZone.ContainsPoint(root.position))
            return false;

        Physics2D.SyncTransforms();

        if (!AlbumPlacementUtility.CanPlaceInAlbum(
                sticker,
                albumZone))
        {
            return false;
        }

        Transform contentRoot =
            albumZone.GetContentRoot();

        if (contentRoot == null)
            return false;

        root.SetParent(
            contentRoot,
            true
        );

        Physics2D.SyncTransforms();

        return true;
    }

    // =========================================================
    // HIERARCHY QUERY
    // =========================================================

    /// <summary>
    /// Comprueba físicamente si el sticker está actualmente
    /// dentro del ContentRoot del álbum.
    ///
    /// Nos sirve también para stickers que hayas colocado
    /// directamente desde Editor antes de darle a Play.
    /// </summary>
    public bool IsStickerInAlbum(
        BaseSticker sticker)
    {
        if (sticker == null ||
            !HasAlbum())
        {
            return false;
        }

        Transform root =
            sticker.stickerRoot != null
                ? sticker.stickerRoot
                : sticker.transform;

        Transform contentRoot =
            albumZone.GetContentRoot();

        if (root == null ||
            contentRoot == null)
        {
            return false;
        }

        return root.IsChildOf(contentRoot);
    }
}
