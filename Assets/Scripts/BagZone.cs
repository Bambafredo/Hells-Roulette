using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class BagZone : MonoBehaviour
{
    [Header("Zone")]
    public Collider2D zoneCollider;      // El área válida (rectángulo, círculo, polígono…)
    public Transform contentRoot;        // Carpeta donde meter los stickers dentro del área

    private void Reset()
    {
        // Asignar automáticamente el collider
        if (zoneCollider == null)
            zoneCollider = GetComponent<Collider2D>();

        // Crear contentRoot si no existe
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
