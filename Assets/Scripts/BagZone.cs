using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class BagZone : MonoBehaviour
{
    [Header("Zone")]
    public Collider2D zoneCollider;       // área del slot
    public Transform contentRoot;         // dónde se meten los stickers

    [Header("State")]
    public bool occupied = false;         // si tiene sticker AUTO colocado
    public BaseSticker autoSticker = null; // el sticker asignado automáticamente

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
