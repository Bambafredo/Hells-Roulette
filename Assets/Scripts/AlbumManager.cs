using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AlbumManager : MonoBehaviour
{
    public static AlbumManager Instance;

    [Header("References")]
    public AlbumZone albumZone;

    [Header("Presentation")]

    [Tooltip(
        "Optional root whose renderers are hidden when another UI element " +
        "temporarily covers the Album. Defaults to AlbumZone."
    )]
    public Transform presentationRoot;

    public bool IsInteractionEnabled =>
        !presentationHidden;

    private bool presentationHidden = false;

    private readonly Dictionary<Renderer, bool>
        rendererStatesBeforeHide =
            new Dictionary<Renderer, bool>();

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

        if (presentationRoot == null &&
            albumZone != null)
        {
            presentationRoot =
                albumZone.transform;
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
        if (!IsInteractionEnabled ||
            !HasAlbum())
        {
            return false;
        }

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
            !IsInteractionEnabled ||
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
    // PRESENTATION / INTERACTION
    // =========================================================

    public void SetPresentationHidden(
        bool hidden)
    {
        if (presentationHidden == hidden)
            return;

        presentationHidden = hidden;

        if (!presentationHidden)
        {
            RestoreHiddenRenderers();
            return;
        }

        HideAlbumRenderers();
    }


    private void LateUpdate()
    {
        /*
         * Stickers can change while the Album is hidden (for example a
         * Zombie transmutation). Catch newly-created renderers without
         * disabling any GameObject or sticker logic.
         */
        if (presentationHidden)
            HideAlbumRenderers();
    }


    private void HideAlbumRenderers()
    {
        Transform root =
            presentationRoot != null
                ? presentationRoot
                : albumZone != null
                    ? albumZone.transform
                    : null;

        if (root == null)
            return;

        Renderer[] renderers =
            root.GetComponentsInChildren<Renderer>(
                true
            );

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null)
                continue;

            if (!rendererStatesBeforeHide
                .ContainsKey(renderer))
            {
                rendererStatesBeforeHide.Add(
                    renderer,
                    renderer.enabled
                );
            }

            renderer.enabled = false;
        }
    }


    private void RestoreHiddenRenderers()
    {
        foreach (KeyValuePair<Renderer, bool> pair
                 in rendererStatesBeforeHide)
        {
            if (pair.Key != null)
                pair.Key.enabled = pair.Value;
        }

        rendererStatesBeforeHide.Clear();
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
