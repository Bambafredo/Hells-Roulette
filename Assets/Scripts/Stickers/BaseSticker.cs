using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class BaseSticker : MonoBehaviour
{
    [Header("Sticker Config")]
    public StickerEffect effect;
    public Transform wheelCenter;
    public WheelGenerator generator;
    public RouletteController controller;

    [Header("Placement")]
    public bool isPlaced = false;          // true si está en un segmento de la ruleta
    public Transform currentSegment;

    // Seguimiento de slots (AUTO)
    [HideInInspector] public BagZone currentBagZone;        // slot auto de bag
    [HideInInspector] public BagZone currentGameplayZone;   // slot auto de gameplay

    [Header("Validation Masks")]
    public LayerMask segmentMask;
    public LayerMask stickerMask;

    [Header("Placement Tuning")]
    [Range(0f, 0.2f)] public float tolerance = 0.05f;
    [Range(0.5f, 1f)] public float coverageThreshold = 0.75f;

    private Camera cam;
    private bool isDragging = false;
    private Vector3 offset;
    private Vector3 originalPosition;
    private Quaternion originalRotation;
    private Transform originalParent;
    private Collider2D myCollider;
    private Transform root;

    protected virtual void Awake()
    {
        cam = Camera.main;
        myCollider = GetComponent<Collider2D>();
        root = transform.parent != null ? transform.parent : transform;   // ← fallback seguro

        if (wheelCenter == null)
        {
            var w = GameObject.Find("Wheel");
            if (w) wheelCenter = w.transform;
        }

        if (controller == null)
            controller = FindObjectOfType<RouletteController>();
    }

    protected virtual void Update()
    {
        HandleDragging();
    }

    // ---------------- DRAG -----------------------

    protected virtual void HandleDragging()
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        Vector2 mouseWorld = cam.ScreenToWorldPoint(Input.mousePosition);

        if (Input.GetMouseButtonDown(0))
        {
            if (controller != null && controller.SpinInProgress)
                return;

            if (myCollider.OverlapPoint(mouseWorld))
            {
                isDragging = true;

                originalPosition = root.position;
                originalRotation = root.rotation;
                originalParent = root.parent;

                // Liberar cualquier slot AUTO que tuviera
                if (BagManager.Instance != null)
                {
                    BagManager.Instance.FreeBagSlot(this);
                    BagManager.Instance.FreeGameplaySlot(this);
                }

                // Si estaba en ruleta, salir de ella
                if (isPlaced)
                {
                    isPlaced = false;
                    currentSegment = null;
                    root.SetParent(null, true);
                }

                offset = root.position - (Vector3)mouseWorld;
                SetAlpha(0.6f);

                if (controller) controller.SetInputBlocked(true);
            }
        }

        if (isDragging)
        {
            root.position = (Vector3)mouseWorld + offset;

            if (Input.GetMouseButtonUp(0))
            {
                HandleDrop();
                isDragging = false;

                if (controller) controller.SetInputBlocked(false);
                SetAlpha(1f);
            }
        }
#endif
    }

    // ---------------- DROP -----------------------

    private void HandleDrop()
    {
        if (BagManager.Instance == null)
        {
            TryPlaceSticker();
            return;
        }

        Vector2 p = root.position;

        // PORTAL → BAG (auto-slot único)
        if (BagManager.Instance.IsPointOnBagPortal(p))
        {
            var free = BagManager.Instance.FindFirstFreeBagSlot();
            if (free != null)
            {
                BagManager.Instance.PlaceStickerInBagSlot_Auto(this, free);
                return;
            }
            ReturnToOrigin();
            return;
        }

        // PORTAL → GAMEPLAY (auto-slot único)
        if (BagManager.Instance.IsPointOnGameplayPortal(p))
        {
            int targetArea = BagManager.Instance.FindFirstEmptyGameplayArea();

            if (targetArea >= 0)
            {
                BagManager.Instance.PlaceStickerInGameplayArea_Auto(this, targetArea);
                return;
            }

            // Si todas las áreas están ocupadas, movimiento inválido
            ReturnToOrigin();
            return;
        }

        // -------- BAG SCREEN: slots manuales (permiten varios)
        if (BagManager.Instance.IsBagActive())
        {
            var slot = BagManager.Instance.GetBagSlotAtPosition(p);
            if (slot != null)
            {
                if (BagManager.Instance.TryPlaceInSlotManual(this, slot, root.position))
                    return;

                // No hay sitio sin solapar en ese slot → volver
                ReturnToOrigin();
                return;
            }

            // No ha caído en slot → volver
            ReturnToOrigin();
            return;
        }

        // -------- GAMEPLAY SCREEN
        var gSlot = BagManager.Instance.GetGameplaySlotAtPosition(p);
        if (gSlot != null)
        {
            if (BagManager.Instance.TryPlaceInSlotManual(this, gSlot, root.position))
                return;

            // Si el slot está lleno (no encuentra hueco), probamos ruleta como fallback
            if (TryPlaceOnWheel(p))
                return;

            // Y si tampoco cabe en la ruleta, intentamos colocación libre en el área correcta con anti-overlap
            if (TryPlaceFreelyInGameplayArea(p))
                return;

            // Si no fue posible, volver
            ReturnToOrigin();
            return;
        }

        // 2) Si NO está sobre slot, probamos primero ruleta
        if (TryPlaceOnWheel(p))
            return;

        // 3) Y finalmente, si cae en cualquier área de gameplay, intentamos colocación libre con anti-overlap
        if (BagManager.Instance.IsPointInsideAnyGameplayArea(p))
        {
            if (TryPlaceFreelyInGameplayArea(p))
                return;

            ReturnToOrigin();
            return;
        }

        // Fallback
        TryPlaceSticker();
    }

    // ---------------- ROULETTE LOGIC -----------------------

    private bool TryPlaceOnWheel(Vector3 dropPos)
    {
        Collider2D segCol = Physics2D.OverlapPoint(dropPos, segmentMask);
        if (segCol == null)
            return false;

        if (!IsMostlyInsideSegment(myCollider, segCol, tolerance, coverageThreshold))
            return false;

        // Filtrar solapes: solo nos importa si hay stickers en ESTE MISMO SEGMENTO
        Collider2D[] overlaps =
            Physics2D.OverlapCircleAll(dropPos, myCollider.bounds.extents.x * 0.9f, stickerMask);

        foreach (var o in overlaps)
        {
            if (o == myCollider) continue;

            // ignorar stickers que no estén bajo el mismo segmento
            if (!o.transform.IsChildOf(segCol.transform))
                continue;

            // hay otro sticker en este segmento
            return false;
        }

        root.SetParent(segCol.transform, true);
        currentSegment = segCol.transform;
        isPlaced = true;
        return true;
    }

    // Fallback “ruleta-only” que ya usabas desde TryPlaceSticker()
    protected virtual void TryPlaceSticker()
    {
        Vector2 worldPos = root.position;
        Collider2D segCol = Physics2D.OverlapPoint(worldPos, segmentMask);

        if (segCol == null)
        {
            ReturnToOrigin();
            return;
        }

        if (!IsMostlyInsideSegment(myCollider, segCol, tolerance, coverageThreshold))
        {
            ReturnToOrigin();
            return;
        }

        Collider2D[] overlaps =
            Physics2D.OverlapCircleAll(worldPos, myCollider.bounds.extents.x * 0.9f, stickerMask);

        foreach (var o in overlaps)
        {
            if (o == myCollider) continue;

            // mismo filtro de segmento para evitar falsos positivos
            if (!o.transform.IsChildOf(segCol.transform))
                continue;

            ReturnToOrigin();
            return;
        }

        root.SetParent(segCol.transform, true);
        currentSegment = segCol.transform;
        isPlaced = true;
    }

    private bool IsMostlyInsideSegment(Collider2D sticker, Collider2D segment, float tolerance, float threshold)
    {
        Bounds b = sticker.bounds;
        Vector3 min = b.min - new Vector3(tolerance, tolerance, 0f);
        Vector3 max = b.max + new Vector3(tolerance, tolerance, 0f);

        int total = 9;
        int inside = 0;

        for (int ix = 0; ix < 3; ix++)
        {
            for (int iy = 0; iy < 3; iy++)
            {
                float x = Mathf.Lerp(min.x, max.x, ix / 2f);
                float y = Mathf.Lerp(min.y, max.y, iy / 2f);
                if (segment.OverlapPoint(new Vector2(x, y)))
                    inside++;
            }
        }

        return inside / (float)total >= threshold;
    }

    protected virtual void ReturnToOrigin()
    {
        root.SetParent(originalParent);
        root.position = originalPosition;
        root.rotation = originalRotation;
    }

    private void SetAlpha(float a)
    {
        var sr = root.GetComponentInChildren<SpriteRenderer>();
        if (sr)
        {
            Color c = sr.color;
            c.a = a;
            sr.color = c;
        }
    }

    public virtual void OnSegmentWin()
    {
        if (effect == null) return;
        effect.ApplyEffect();
    }

    // --------------------------------------------------------------------
    //                ★★  ANTI-OVERLAP EN GAMEPLAY LIBRE  ★★
    // --------------------------------------------------------------------

    // Intenta colocar libremente en el área correcta (sin slot), evitando solapes.
    private bool TryPlaceFreelyInGameplayArea(Vector2 dropPos)
    {
        // ¿En qué área estamos?
        int idx = BagManager.Instance.GetGameplayAreaIndexAtPoint(dropPos);
        if (idx < 0)
            return false;

        var area = BagManager.Instance.gameplayAreas[idx];
        if (area == null || area.areaCollider == null)
            return false;

        // Radio aproximado del sticker
        float r = ApproxRadiusWorld();

        // Validar que el centro quede dentro con margen (para no atravesar borde)
        if (!PointInsideAreaWithMargin(area.areaCollider, dropPos, r))
            return false;

        // Antes de parentar, comprobamos solape con otros stickers en ese contentRoot
        var areaRoot = area.contentRoot != null ? area.contentRoot : root.parent;
        if (OverlapsAnyInArea(areaRoot, dropPos, r))
            return false;

        // OK → parent + clamp final por seguridad
        root.SetParent(areaRoot, true);

        Vector3 clamped = BagManager.Instance.ClampToGameplay(dropPos, idx);
        root.position = new Vector3(clamped.x, clamped.y, root.position.z);
        root.rotation = Quaternion.identity;
        isPlaced = false;
        currentSegment = null;

        return true;
    }

    // Aproxima el radio usando el collider propio en mundo
    private float ApproxRadiusWorld()
    {
        if (myCollider == null) myCollider = GetComponent<Collider2D>();
        if (myCollider == null) return 0.2f;
        var e = myCollider.bounds.extents;
        return Mathf.Max(e.x, e.y);
    }

    // Comprueba que el punto esté dentro del área con un margen = radio
    private bool PointInsideAreaWithMargin(Collider2D areaCol, Vector2 p, float margin)
    {
        if (!areaCol.OverlapPoint(p)) return false;
        Bounds b = areaCol.bounds;
        return (p.x >= b.min.x + margin && p.x <= b.max.x - margin &&
                p.y >= b.min.y + margin && p.y <= b.max.y - margin);
    }

    // ¿Solapa con algún otro collider de sticker dentro de este areaRoot?
    private bool OverlapsAnyInArea(Transform areaRoot, Vector2 candidate, float r)
    {
        if (areaRoot == null) return false;

        // Recolectamos colliders del área
        var others = areaRoot.GetComponentsInChildren<Collider2D>(true);

        // Root de este sticker (para ignorar sus propios colliders/hijos)
        Transform selfRoot = (transform.parent != null) ? transform.parent : transform;

        foreach (var o in others)
        {
            if (o == null) continue;

            // Ignorar mis propios colliders
            if (o.transform.IsChildOf(selfRoot))
                continue;

            // Solo cuenta si pertenece a otro sticker
            var otherSticker = o.GetComponentInParent<BaseSticker>();
            if (otherSticker == null) continue;

            // Distancia centro a centro con radio aprox del otro
            float d = Vector2.Distance(candidate, o.bounds.center);
            float ro = Mathf.Max(o.bounds.extents.x, o.bounds.extents.y);

            if (d < (r + ro) * 0.98f)
                return true;
        }

        return false;
    }
}