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
            // 🚫 No permitir arrastrar stickers mientras la ruleta gira
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
                    root.SetParent(null, true);
                    currentSegment = null;
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
                TryPlaceSticker();
                isDragging = false;
                if (controller) controller.SetInputBlocked(false);
                SetAlpha(1f);
            }
        }
#endif
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

        Collider2D[] overlaps = Physics2D.OverlapCircleAll(worldPos, myCollider.bounds.extents.x * 0.9f, stickerMask);
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
        Debug.Log($"❌ Sticker {name} fuera de los límites o solapado");
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
