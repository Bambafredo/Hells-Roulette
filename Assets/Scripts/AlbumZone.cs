using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class AlbumZone : MonoBehaviour
{
    [Header("Album Area")]

    [Tooltip("Collider que define los límites físicos del álbum.")]
    public Collider2D areaCollider;

    [Tooltip("Root donde se parentarán todos los stickers guardados en el álbum.")]
    public Transform contentRoot;

    [Header("Placement")]

    [Tooltip(
        "Pequeña tolerancia permitida respecto al borde. " +
        "Misma filosofía que la colocación en la ruleta."
    )]
    [Range(0f, 0.2f)]
    public float boundaryTolerance = 0.01f;

    // =========================================================
    // UNITY
    // =========================================================

    private void Awake()
    {
        EnsureReferences();
    }

    private void OnValidate()
    {
        EnsureReferences();
    }

    // =========================================================
    // REFERENCES
    // =========================================================

    private void EnsureReferences()
    {
        if (areaCollider == null)
            areaCollider = GetComponent<Collider2D>();

        /*
         * Si no has asignado ContentRoot manualmente,
         * intentamos encontrar un hijo llamado exactamente
         * "ContentRoot".
         */
        if (contentRoot == null)
        {
            Transform found =
                transform.Find("ContentRoot");

            if (found != null)
                contentRoot = found;
        }
    }

    // =========================================================
    // BASIC QUERIES
    // =========================================================

    /// <summary>
    /// ¿Este punto del mundo está dentro del área del álbum?
    ///
    /// Esto solo sirve para saber si el jugador está intentando
    /// hacer drop en el álbum. La validación REAL del sticker
    /// comprobará después todo su collider.
    /// </summary>
    public bool ContainsPoint(Vector2 worldPoint)
    {
        if (areaCollider == null)
            return false;

        return areaCollider.OverlapPoint(worldPoint);
    }

    /// <summary>
    /// Root utilizado para guardar stickers.
    /// Si por algún motivo no existe ContentRoot,
    /// usamos el propio AlbumZone como fallback.
    /// </summary>
    public Transform GetContentRoot()
    {
        if (contentRoot != null)
            return contentRoot;

        return transform;
    }
}
