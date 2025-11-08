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
        root = transform.parent;

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

        if (isPlaced && currentSegment != null)
            root.rotation = currentSegment.rotation;
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
            var free = BagManager.Instance.FindFirstFreeGameplaySlot();
            if (free != null)
            {
                BagManager.Instance.PlaceStickerInGameplaySlot_Auto(this, free);
                return;
            }
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
        if (BagManager.Instance.IsPointInsideGameplay(p))
        {
            // 1) Si suelta sobre un slot de gameplay, intentamos colocarlo ahí primero (prioridad al slot)
            var gSlot = BagManager.Instance.GetGameplaySlotAtPosition(p);
            if (gSlot != null)
            {
                if (BagManager.Instance.TryPlaceInSlotManual(this, gSlot, root.position))
                    return;

                // Si no hay sitio libre en ese slot, probamos ruleta
                if (TryPlaceOnWheel(p))
                    return;

                // Nada → clamp a gameplay area
                root.SetParent(BagManager.Instance.gameplayContentRoot, true);
                Bounds b = BagManager.Instance.gameplayAreaCollider.bounds;
                float x = Mathf.Clamp(root.position.x, b.min.x, b.max.x);
                float y = Mathf.Clamp(root.position.y, b.min.y, b.max.y);
                root.position = new Vector3(x, y, root.position.z);
                return;
            }

            // 2) Si NO está sobre un slot, intentamos la ruleta
            if (TryPlaceOnWheel(p))
                return;

            // 3) Área libre de gameplay (clamp)
            root.SetParent(BagManager.Instance.gameplayContentRoot, true);
            Bounds b2 = BagManager.Instance.gameplayAreaCollider.bounds;
            float x2 = Mathf.Clamp(root.position.x, b2.min.x, b2.max.x);
            float y2 = Mathf.Clamp(root.position.y, b2.min.y, b2.max.y);
            root.position = new Vector3(x2, y2, root.position.z);
            return;
        }

        // Fallback original: intentar ruleta
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

        Collider2D[] overlaps =
            Physics2D.OverlapCircleAll(dropPos, myCollider.bounds.extents.x * 0.9f, stickerMask);

        foreach (var o in overlaps)
        {
            if (o != myCollider)
                return false;
        }

        root.SetParent(segCol.transform, true);
        currentSegment = segCol.transform;
        isPlaced = true;
        return true;
    }

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
            if (o != myCollider)
            {
                ReturnToOrigin();
                return;
            }
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
}