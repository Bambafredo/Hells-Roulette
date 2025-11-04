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

    private Camera cam;
    private bool isDragging = false;
    private Vector3 offset;
    private Vector3 originalPosition;
    private Quaternion originalRotation;
    private Transform originalParent;
    private Collider2D myCollider;
    private Transform root; // moveremos este GO

    protected virtual void Awake()
    {
        cam = Camera.main;
        myCollider = GetComponent<Collider2D>();
        root = transform.parent; // 👈 moveremos el root, no este GO

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

        // 🔹 Si está colocado, que gire junto con la ruleta
        if (isPlaced && currentSegment != null)
            root.rotation = currentSegment.rotation;
    }

    protected virtual void HandleDragging()
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        Vector2 mouseWorld = cam.ScreenToWorldPoint(Input.mousePosition);

        if (Input.GetMouseButtonDown(0))
        {
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

        // 1️⃣ Comprobar si está dentro del collider de un segmento
        Collider2D segCol = Physics2D.OverlapPoint(worldPos, segmentMask);
        if (segCol == null)
        {
            ReturnToOrigin();
            return;
        }

        // 2️⃣ Comprobar que todo el collider esté dentro del segmento (no solo el centro)
        if (!IsFullyInsideSegment(myCollider, segCol))
        {
            ReturnToOrigin();
            return;
        }

        // 3️⃣ Comprobar si toca otros stickers
        Collider2D[] overlaps = Physics2D.OverlapCircleAll(worldPos, myCollider.bounds.extents.x * 0.9f, stickerMask);
        foreach (var o in overlaps)
        {
            if (o != myCollider)
            {
                ReturnToOrigin();
                return;
            }
        }

        // ✅ Colocar correctamente
        root.SetParent(segCol.transform, true);
        currentSegment = segCol.transform;
        isPlaced = true;
        Debug.Log($"✅ Sticker {name} colocado en {segCol.name}");
    }

    private bool IsFullyInsideSegment(Collider2D sticker, Collider2D segment)
    {
        Vector3[] corners = new Vector3[4];
        Bounds b = sticker.bounds;
        corners[0] = new Vector3(b.min.x, b.min.y);
        corners[1] = new Vector3(b.min.x, b.max.y);
        corners[2] = new Vector3(b.max.x, b.min.y);
        corners[3] = new Vector3(b.max.x, b.max.y);

        foreach (var c in corners)
        {
            if (!segment.OverlapPoint(c))
                return false;
        }
        return true;
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
