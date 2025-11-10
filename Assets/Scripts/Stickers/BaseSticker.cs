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
    public bool isPlaced = false;          
    public Transform currentSegment;

    // Seguimiento de slots (AUTO)
    [HideInInspector] public BagZone currentBagZone;        
    [HideInInspector] public BagZone currentGameplayZone;   

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
        
        root = transform.parent != null ? transform.parent : transform;

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

        // Seguir rotación del segmento si está en la ruleta
        if (isPlaced && currentSegment != null)
            root.rotation = currentSegment.rotation;
    }

    // ----------------------------------------------------------
    // DRAG
    // ----------------------------------------------------------
    protected virtual void HandleDragging()
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        Vector2 mouseWorld = cam.ScreenToWorldPoint(Input.mousePosition);

        // START DRAG
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

                // Liberar slots AUTO
                if (BagManager.Instance != null)
                {
                    BagManager.Instance.FreeBagSlot(this);
                    BagManager.Instance.FreeGameplaySlot(this);
                }

                // Salir de la ruleta
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

        // WHILE DRAGGING
        if (isDragging)
        {
            root.position = (Vector3)mouseWorld + offset;

            // DROP
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

    // ----------------------------------------------------------
    // DROP HANDLER
    // ----------------------------------------------------------
    private void HandleDrop()
    {
        if (BagManager.Instance == null)
        {
            TryPlaceSticker();
            return;
        }

        Vector2 p = root.position;

        // PORTAL → BAG (AUTO)
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

        // PORTAL → GAMEPLAY (AUTO)
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

        // BAG SCREEN → slots manuales
        if (BagManager.Instance.IsBagActive())
        {
            var slot = BagManager.Instance.GetBagSlotAtPosition(p);
            if (slot != null)
            {
                if (BagManager.Instance.TryPlaceInSlotManual(this, slot, root.position))
                    return;

                ReturnToOrigin();
                return;
            }

            ReturnToOrigin();
            return;
        }

        // GAMEPLAY SCREEN → slots manuales
        var gSlot = BagManager.Instance.GetGameplaySlotAtPosition(p);
        if (gSlot != null)
        {
            if (BagManager.Instance.TryPlaceInSlotManual(this, gSlot, root.position))
                return;

            // fallback: ruleta
            if (TryPlaceOnWheel(p))
                return;

            // fallback final: área gameplay con clamp
            int idxG = BagManager.Instance.GetGameplayAreaIndexAtPoint(p);
            if (idxG < 0) idxG = 0;
            var areaRoot = BagManager.Instance.gameplayAreas[idxG].contentRoot;

            root.SetParent(areaRoot, true);
            Vector3 cg = BagManager.Instance.ClampToGameplay(root.position, idxG);
            root.position = new Vector3(cg.x, cg.y, root.position.z);
            return;
        }

        // 2) ruleta
        if (TryPlaceOnWheel(p))
            return;

        // 3) clamp al área gameplay si cae en ella
        if (BagManager.Instance.IsPointInsideAnyGameplayArea(p))
        {
            int idx = BagManager.Instance.GetGameplayAreaIndexAtPoint(p);
            if (idx < 0) idx = 0;

            var areaRoot = BagManager.Instance.gameplayAreas[idx].contentRoot;

            root.SetParent(areaRoot, true);
            Vector3 cg = BagManager.Instance.ClampToGameplay(root.position, idx);
            root.position = new Vector3(cg.x, cg.y, root.position.z);
            return;
        }

        // fallback genérico
        TryPlaceSticker();
    }

    // ----------------------------------------------------------
    // ROULETTE / SEGMENT LOGIC
    // ----------------------------------------------------------
    private bool TryPlaceOnWheel(Vector3 dropPos)
    {
        Collider2D segCol = Physics2D.OverlapPoint(dropPos, segmentMask);
        if (segCol == null) return false;

        if (!IsMostlyInsideSegment(myCollider, segCol, tolerance, coverageThreshold))
            return false;

        // Evitar solapes SOLO con stickers dentro del MISMO segmento
        Collider2D[] overlaps =
            Physics2D.OverlapCircleAll(dropPos, myCollider.bounds.extents.x * 0.9f, stickerMask);

        foreach (var o in overlaps)
        {
            if (o == myCollider) continue;

            if (!o.transform.IsChildOf(segCol.transform))
                continue;

            return false;
        }

        root.SetParent(segCol.transform, true);
        currentSegment = segCol.transform;
        isPlaced = true;
        return true;
    }

    // fallback ruleta básico
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
}