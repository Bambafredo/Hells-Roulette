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

    // ------------------------------------------------------------
    // ROTACIÓN OPCIONAL
    // ------------------------------------------------------------
    [Header("Rotation Button (opcional)")]
    public Collider2D rotationButton;     // asigna aquí el botón con BoxCollider2D
    public float rotationSpeed = 40f;

    private bool rotating = false;
    private Camera cam;
    private RouletteController controller;

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
        cam = Camera.main;
        controller = FindObjectOfType<RouletteController>();

        if (zoneCollider == null)
            zoneCollider = GetComponent<Collider2D>();

        if (contentRoot == null)
        {
            GameObject root = new GameObject("ContentRoot");
            root.transform.SetParent(transform, false);
            contentRoot = root.transform;
        }
    }

    private void Update()
    {
        HandleRotationButton();
        ApplyRotation();
    }

    // ------------------------------------------------------------
    // DETECTAR PULSACIÓN EN EL BOTÓN
    // ------------------------------------------------------------
    private void HandleRotationButton()
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        if (rotationButton == null)
            return;

        Vector2 mouseWorld = cam.ScreenToWorldPoint(Input.mousePosition);

        // Mientras mantenemos pulsado
        if (Input.GetMouseButton(0))
        {
            if (rotationButton.OverlapPoint(mouseWorld))
            {
                if (!rotating)
                {
                    rotating = true;
                    // Bloquear ruleta mientras se rota
                    if (controller != null)
                        controller.SetInputBlocked(true);
                }
            }
        }

        // Cuando soltamos
        if (Input.GetMouseButtonUp(0))
        {
            if (rotating)
            {
                rotating = false;

                // Desbloquear ruleta
                if (controller != null)
                    controller.SetInputBlocked(false);
            }
        }
#endif
    }

    // ------------------------------------------------------------
    // APLICAR ROTACIÓN A TODOS LOS STICKERS DEL AREA
    // ------------------------------------------------------------
    private void ApplyRotation()
    {
        if (!rotating || contentRoot == null)
            return;

        float step = rotationSpeed * Time.deltaTime;

        foreach (Transform child in contentRoot)
            child.Rotate(0f, 0f, step);
    }
}
