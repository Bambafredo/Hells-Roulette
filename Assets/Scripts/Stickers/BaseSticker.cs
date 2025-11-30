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

        if (generator == null)
            generator = FindObjectOfType<WheelGenerator>();
    }

    protected virtual void Update()
    {
        HandleDragging();
    }

    // ===========================================================
    // DRAGGING
    // ===========================================================
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
                controller?.SetInputBlocked(true);
            }
        }

        if (isDragging)
        {
            root.position = (Vector3)mouseWorld + offset;

            if (Input.GetMouseButtonUp(0))
            {
                HandleDrop();
                isDragging = false;
                controller?.SetInputBlocked(false);
                SetAlpha(1f);
            }
        }
#endif
    }

    // ===========================================================
    // DROP LOGIC
    // ===========================================================
    private void HandleDrop()
    {
        if (BagManager.Instance == null)
        {
            TryPlaceSticker();
            return;
        }

        Vector2 p = root.position;

        // BAG PORTAL
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

        // GAMEPLAY PORTAL
        if (BagManager.Instance.IsPointOnGameplayPortal(p))
        {
            if (BagManager.Instance.PlaceStickerInNextEmptyGameplayArea_FromBag(this))
                return;

            ReturnToOrigin();
            return;
        }

        // BAG SCREEN
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

        // GAMEPLAY SCREEN
        var gSlot = BagManager.Instance.GetGameplaySlotAtPosition(p);
        if (gSlot != null)
        {
            if (BagManager.Instance.TryPlaceInSlotManual(this, gSlot, root.position))
                return;

            if (TryPlaceOnWheel(p))
                return;

            if (TryPlaceFreelyInGameplayArea(p))
                return;

            ReturnToOrigin();
            return;
        }

        // Try wheel
        if (TryPlaceOnWheel(p))
            return;

        // Free gameplay area
        if (BagManager.Instance.IsPointInsideAnyGameplayArea(p))
        {
            if (TryPlaceFreelyInGameplayArea(p))
                return;

            ReturnToOrigin();
            return;
        }

        TryPlaceSticker();
    }

    // ===========================================================
    // ROULETTE LOGIC
    // ===========================================================
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

    // Fallback
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

    // ===========================================================
    // SEGMENT VALIDATION
    // ===========================================================
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

    // ===========================================================
    // RESTORE AFTER WHEEL REGEN
    // ===========================================================
    public void OnRestoredAfterWheelRegen()
    {
        // seguridad
        if (currentSegment == null)
            return;

        isPlaced = true;

        // micro ajuste para actualizar físicas
        Vector3 p = transform.position;
        transform.position = p + new Vector3(0.001f, 0, 0);
        transform.position = p;
    }

    // ===========================================================
    // UTILITIES
    // ===========================================================
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

    // ===========================================================
    // GAMEPLAY FREE AREA LOGIC (sin cambios)
    // ===========================================================
    private bool TryPlaceFreelyInGameplayArea(Vector2 dropPos)
    {
        int idx = BagManager.Instance.GetGameplayAreaIndexAtPoint(dropPos);
        if (idx < 0) return false;

        var area = BagManager.Instance.gameplayAreas[idx];
        if (area == null || area.areaCollider == null)
            return false;

        float r = ApproxRadiusWorld();

        if (!PointInsideAreaWithMargin(area.areaCollider, dropPos, r))
            return false;

        var areaRoot = area.contentRoot != null ? area.contentRoot : root.parent;
        if (OverlapsAnyInArea(areaRoot, dropPos, r))
            return false;

        root.SetParent(areaRoot, true);

        Vector3 clamped = BagManager.Instance.ClampToGameplay(dropPos, idx);
        root.position = new Vector3(clamped.x, clamped.y, root.position.z);
        root.rotation = Quaternion.identity;
        isPlaced = false;
        currentSegment = null;

        return true;
    }

    private float ApproxRadiusWorld()
    {
        if (myCollider == null) myCollider = GetComponent<Collider2D>();
        var e = myCollider.bounds.extents;
        return Mathf.Max(e.x, e.y);
    }

    private bool PointInsideAreaWithMargin(Collider2D areaCol, Vector2 p, float margin)
    {
        if (!areaCol.OverlapPoint(p)) return false;
        Bounds b = areaCol.bounds;
        return (
            p.x >= b.min.x + margin &&
            p.x <= b.max.x - margin &&
            p.y >= b.min.y + margin &&
            p.y <= b.max.y - margin
        );
    }

    private bool OverlapsAnyInArea(Transform areaRoot, Vector2 candidate, float r)
    {
        if (areaRoot == null) return false;

        var others = areaRoot.GetComponentsInChildren<Collider2D>(true);
        Transform selfRoot = root;

        foreach (var o in others)
        {
            if (o.transform.IsChildOf(selfRoot))
                continue;

            BaseSticker other = o.GetComponentInParent<BaseSticker>();
            if (other == null) continue;

            float d = Vector2.Distance(candidate, o.bounds.center);
            float ro = Mathf.Max(o.bounds.extents.x, o.bounds.extents.y);

            if (d < (r + ro) * 0.98f)
                return true;
        }

        return false;
    }
}