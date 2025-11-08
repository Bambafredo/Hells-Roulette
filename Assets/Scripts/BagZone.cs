using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class BagZone : MonoBehaviour
{
    [Header("Zone")]
    public Collider2D zoneCollider;       // Área del slot
    public Transform contentRoot;         // Carpeta donde meter stickers

    [Header("State (solo AUTO)")]
    public bool occupied = false;         // true si tiene un sticker colocado automáticamente
    public BaseSticker autoSticker = null;

    private void Reset()
    {
        if (zoneCollider == null)
            zoneCollider = GetComponent<Collider2D>();

        if (contentRoot == null)
        {
            GameObject root = new GameObject("ContentRoot");
            root.transform.SetParent(transform, false);
            contentRoot = root.transform;
        }
    }

    private void Awake()
    {
        if (zoneCollider == null)
            zoneCollider = GetComponent<Collider2D>();

        if (contentRoot == null)
        {
            GameObject root = new GameObject("ContentRoot");
            root.transform.SetParent(transform, false);
            contentRoot = root.transform;
        }
    }
}
