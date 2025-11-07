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

    [Header("Validation Masks")]
    public LayerMask segmentMask;
    public LayerMask stickerMask;

    [Header("Placement Tuning")]
    [Range(0f, 0.2f)] public float tolerance = 0.05f;
    [Range(0.5f, 1f)] public float coverageThreshold = 0.75f;

    // BAG SYSTEM
    public LayerMask bagMask;
    public LayerMask gameplayMask;

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

                if (controller) controller.SetInputBlocked(true);

                if (isPlaced)
                {
                    isPlaced = false;
                    currentSegment = null;
                    root.SetParent(null, true);
                }

                offset = root.position - (Vector3)mouseWorld;
                SetAlpha(0.6f);
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

    private void HandleDrop()
    {
        Vector2 p = root.position;

        // ✅ Gameplay → Bag portal
        if (BagManager.Instance.IsPointOnBagPortal(p))
        {
            Vector3 seed = BagManager.Instance.ClampToBag(p);

            root.SetParent(BagManager.Instance.bagContentRoot, true);

            BagManager.Instance.TryPlaceSticker(this, seed);
            return;
        }

        // ✅ Bag → Gameplay portal
        if (BagManager.Instance.IsPointOnGameplayPortal(p))
        {
            BagManager.Instance.RemoveSticker(this);

            // 1. Reparent al ContentRoot del Gameplay
            root.SetParent(BagManager.Instance.gameplayContentRoot, true);

            // 2. Intentar colocarlo en la ruleta
            if (TryPlaceOnWheel(p))
            {
                isPlaced = true;
                return;
            }

            // 3. Si no cabe en la ruleta, lo dejamos dentro del GameplayArea
            if (!BagManager.Instance.IsPointInsideGameplay(p))
                root.position = BagManager.Instance.gameplayAreaCollider.bounds.center;

            isPlaced = false;
            currentSegment = null;

            return;
        }

        // ✅ BAG ACTIVA
        if (BagManager.Instance.IsBagActive())
        {
            Vector3 seed = BagManager.Instance.ClampToBag(p);

            if (!BagManager.Instance.TryPlaceSticker(this, seed))
                ReturnToOrigin();

            return;
        }

        // ✅ GAMEPLAY ACTIVO
        if (BagManager.Instance.IsPointInsideGameplay(p))
        {
            // 1. Intentar colocarlo en la ruleta
            bool ok = TryPlaceOnWheel(p);

            if (ok)
            {
                BagManager.Instance.RemoveSticker(this);
                return;
            }

            // 2. Si NO cae en un segmento → se coloca libremente dentro del gameplay area
            //    (como hacemos con BagArea)
            root.SetParent(BagManager.Instance.gameplayContentRoot, true);
            isPlaced = false;
            currentSegment = null;

            // 3. Clamp opcional para no salir del GameplayArea
            Bounds g = BagManager.Instance.gameplayAreaCollider.bounds;
            float x = Mathf.Clamp(root.position.x, g.min.x, g.max.x);
            float y = Mathf.Clamp(root.position.y, g.min.y, g.max.y);
            root.position = new Vector3(x, y, root.position.z);

            return;
        }

        // ✅ Lógica original ruleta
        TryPlaceSticker();
    }

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
        Debug.Log($"✅ Sticker {name} colocado en {segCol.name}");
    }

    private bool IsMostlyInsideSegment(Collider2D sticker, Collider2D segment, float tolerance, float threshold)
    {
        Bounds b = sticker.bounds;
        Vector3 min = b.min - new Vector3(tolerance, tolerance, 0f);
        Vector3 max = b.max + new Vector3(tolerance, tolerance, 0f);

        int totalChecks = 9;
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

        float ratio = inside / (float)totalChecks;
        return ratio >= threshold;
    }

    protected virtual void ReturnToOrigin()
    {
        root.SetParent(originalParent);
        root.position = originalPosition;
        root.rotation = originalRotation;
        Debug.Log($"❌ Sticker {name} vuelve a su sitio");
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
        Debug.Log($"💵 Sticker '{effect.stickerName}' activado → {effect.dollarReward}$");
    }
}
