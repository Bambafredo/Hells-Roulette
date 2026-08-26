using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class BaseFlagPin : MonoBehaviour
{
    [Header("Placement")]
    public Transform wheelCenter;
    public bool isPlaced = false;

    [Header("References")]
    public RoundManager round;
    public RouletteController controller;

    [Header("Safety")]
    public float selfMinInterval = 0.07f;

    protected Camera cam;
    protected bool isDragging = false;
    protected Vector3 offset;
    protected float _lastHitTime = -999f;

    protected virtual void Awake()
    {
        cam = Camera.main;

        if (wheelCenter == null)
        {
            var w = GameObject.Find("Wheel");
            if (w) wheelCenter = w.transform;
        }

        if (controller == null)
            controller = FindObjectOfType<RouletteController>();

        if (round == null)
            round = FindObjectOfType<RoundManager>();
    }

    protected virtual void Update()
    {
        HandleDragging();

        if (isPlaced && wheelCenter != null)
            UpdateOrientation();
    }

    protected virtual void HandleDragging()
    {
        if (StickerPlacementValidator.Instance != null &&
            StickerPlacementValidator.Instance.InputBlocked)
            return;
            
#if UNITY_EDITOR || UNITY_STANDALONE
        Vector2 mouseWorld = cam.ScreenToWorldPoint(Input.mousePosition);

        if (Input.GetMouseButtonDown(0))
        {
            if (GetComponent<Collider2D>().OverlapPoint(mouseWorld))
            {
                /*
                 * Subclasses can veto the START of a drag without
                 * duplicating the whole placement system.
                 */
                if (!CanBeginDrag())
                    return;

                isDragging = true;
                if (controller) controller.SetInputBlocked(true);

                if (isPlaced)
                {
                    isPlaced = false;
                    transform.SetParent(null, true);
                }

                offset = transform.position - (Vector3)mouseWorld;
            }
        }

        if (isDragging)
        {
            transform.position = (Vector3)mouseWorld + offset;

            if (wheelCenter != null)
                UpdateOrientation();

            Collider2D wheelCol = wheelCenter.GetComponent<CircleCollider2D>();
            if (wheelCol && wheelCol.OverlapPoint(mouseWorld))
            {
                PlaceOnWheel();
                return;
            }

            if (Input.GetMouseButtonUp(0))
            {
                isDragging = false;
                if (controller) controller.SetInputBlocked(false);
            }
        }
#endif
    }

    protected virtual void PlaceOnWheel()
    {
        if (isPlaced || wheelCenter == null) return;

        isPlaced = true;
        isDragging = false;

        if (controller) controller.SetInputBlocked(false);

        transform.SetParent(wheelCenter, true);

        Vector2 dir = (transform.position - wheelCenter.position).normalized;
        float radius = 1f;
        var wheelCol = wheelCenter.GetComponent<CircleCollider2D>();
        if (wheelCol)
            radius = wheelCol.radius * wheelCenter.localScale.x;

        transform.position = wheelCenter.position + (Vector3)dir * radius;

        UpdateOrientation();

        Debug.Log($"📍 {name} clavado correctamente en la rueda");
    }

    protected virtual void UpdateOrientation()
    {
        if (!wheelCenter) return;
        Vector2 dir = (wheelCenter.position - transform.position).normalized;
        float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, ang + 90f);
    }

    // =========================================================
    // DRAG POLICY
    // =========================================================

    protected virtual bool CanBeginDrag()
    {
        return true;
    }


    // =========================================================
    // HIT REGISTRATION
    // =========================================================

    /*
     * Returns true only when this hit passes the pin's own cooldown.
     * Subclasses can therefore award effects only for accepted hits.
     */
    protected bool TryRegisterHitInternal(
        FlagPin flagPin)
    {
        if (Time.time - _lastHitTime <
            selfMinInterval)
        {
            return false;
        }

        _lastHitTime =
            Time.time;

        if (round != null)
        {
            round.RegisterPinHit(
                flagPin
            );
        }

        return true;
    }


    public virtual void RegisterHit()
    {
        TryRegisterHitInternal(
            this as FlagPin
        );
    }
}
