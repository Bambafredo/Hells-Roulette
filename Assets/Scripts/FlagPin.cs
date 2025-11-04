using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class FlagPin : MonoBehaviour
{
    [Header("Placement")]
    public Transform wheelCenter;
    public bool isPlaced = false;

    [Header("References")]
    public Round1Manager round;
    public RouletteController controller;

    [Header("Safety")]
    public float selfMinInterval = 0.07f; // micro-seguro local

    private Camera cam;
    private bool isDragging = false;
    private Vector3 offset;
    private float _lastHitTime = -999f;

    void Awake()
    {
        cam = Camera.main;
        if (wheelCenter == null)
        {
            var w = GameObject.Find("Wheel");
            if (w) wheelCenter = w.transform;
        }
    }

    void Update()
    {
        HandleDragging();

        if (isPlaced && wheelCenter != null)
            UpdateOrientation();
    }

    void HandleDragging()
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        Vector2 mouseWorld = cam.ScreenToWorldPoint(Input.mousePosition);

        // Iniciar drag
        if (Input.GetMouseButtonDown(0))
        {
            if (GetComponent<Collider2D>().OverlapPoint(mouseWorld))
            {
                isDragging = true;
                if (controller) controller.SetInputBlocked(true);

                // Si estaba clavado, lo "desclavamos"
                if (isPlaced)
                {
                    isPlaced = false;
                    transform.SetParent(null, true);
                }

                offset = transform.position - (Vector3)mouseWorld;
            }
        }

        // Mientras arrastro
        if (isDragging)
        {
            transform.position = (Vector3)mouseWorld + offset;

            // Orientarlo hacia el centro mientras se arrastra
            if (wheelCenter != null)
                UpdateOrientation();

            // Detectar si toca la rueda mientras arrastramos
            if (wheelCenter != null)
            {
                Collider2D wheelCol = wheelCenter.GetComponent<Collider2D>();
                if (wheelCol && wheelCol.OverlapPoint(mouseWorld))
                {
                    PlaceOnWheel();
                    return; // deja de arrastrar
                }
            }

            if (Input.GetMouseButtonUp(0))
            {
                isDragging = false;
                if (controller) controller.SetInputBlocked(false);
            }
        }
#endif
    }

    void PlaceOnWheel()
    {
        if (isPlaced || wheelCenter == null) return;

        isPlaced = true;
        isDragging = false;

        // Se vuelve a permitir el input de la ruleta
        if (controller) controller.SetInputBlocked(false);

        // Parentarlo a la rueda para que gire con ella
        transform.SetParent(wheelCenter, true);

        // Calcular posición justo en el borde
        Vector2 dir = (transform.position - wheelCenter.position).normalized;
        float radius = 1f;
        var wheelCol = wheelCenter.GetComponent<CircleCollider2D>();
        if (wheelCol)
            radius = wheelCol.radius * wheelCenter.localScale.x;

        transform.position = wheelCenter.position + (Vector3)dir * radius;

        UpdateOrientation();
        Debug.Log("📍 FlagPin clavado correctamente en la rueda");
    }

    void UpdateOrientation()
    {
        if (!wheelCenter) return;
        Vector2 dir = (wheelCenter.position - transform.position).normalized;
        float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, ang + 90f);
    }

    public void RegisterHit()
    {
        // Micro-debounce: descarta hits pegados
        if (Time.time - _lastHitTime < selfMinInterval) return;
        _lastHitTime = Time.time;

        if (round != null)
            round.RegisterPinHit(this);
    }
}