using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BagManager : MonoBehaviour
{
    public static BagManager Instance;

    [Header("Screen Objects")]
    public GameObject bagScreen;
    public GameObject gameplayScreen;

    [Header("Area Colliders")]
    public Collider2D bagAreaCollider;
    public Collider2D gameplayAreaCollider;

    [Header("Portals")]
    public Collider2D bagPortalCollider;        // gameplay → bag
    public Collider2D gameplayPortalCollider;   // bag → gameplay

    [Header("Roots")]
    public Transform bagContentRoot;
    public Transform gameplayContentRoot;

    [Header("State")]
    public List<BaseSticker> placedStickers = new List<BaseSticker>();

    [Header("Buttons (UI)")]
    public UnityEngine.UI.Button goToBagButton;
    public UnityEngine.UI.Button goToGameplayButton;

    private void Awake()
    {
        Instance = this;

        if (goToBagButton != null)
            goToBagButton.onClick.AddListener(SwitchToBag);

        if (goToGameplayButton != null)
            goToGameplayButton.onClick.AddListener(SwitchToGameplay);
    }

    private void Update()
    {
        HandlePortalClicks();
    }

    // ---------------------------------------------------------------------
    // ✅ Detectar clic en portales (USANDO COLLIDERS COMO BOTONES)
    void HandlePortalClicks()
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        if (Input.GetMouseButtonDown(0))
        {
            Vector2 pos = Camera.main.ScreenToWorldPoint(Input.mousePosition);

            if (bagPortalCollider != null && bagPortalCollider.OverlapPoint(pos))
            {
                SwitchToBag();
                return;
            }

            if (gameplayPortalCollider != null && gameplayPortalCollider.OverlapPoint(pos))
            {
                SwitchToGameplay();
                return;
            }
        }
#endif
    }

    // ---------------------------------------------------------------------
    public bool IsPointInsideBag(Vector2 point)
    {
        return bagAreaCollider != null && bagAreaCollider.OverlapPoint(point);
    }

    public bool IsPointInsideGameplay(Vector2 point)
    {
        return gameplayAreaCollider != null && gameplayAreaCollider.OverlapPoint(point);
    }

    public bool IsPointOnBagPortal(Vector2 point)
    {
        return bagPortalCollider != null && bagPortalCollider.OverlapPoint(point);
    }

    public bool IsPointOnGameplayPortal(Vector2 point)
    {
        return gameplayPortalCollider != null && gameplayPortalCollider.OverlapPoint(point);
    }

    // ---------------------------------------------------------------------
    public void SwitchToBag()
    {
        gameplayScreen.SetActive(false);
        bagScreen.SetActive(true);
    }

    public void SwitchToGameplay()
    {
        bagScreen.SetActive(false);
        gameplayScreen.SetActive(true);
    }

    public bool IsBagActive() => bagScreen.activeSelf;

    // ---------------------------------------------------------------------
    public Vector3 ClampToBag(Vector3 p)
    {
        Bounds b = bagAreaCollider.bounds;

        float x = Mathf.Clamp(p.x, b.min.x, b.max.x);
        float y = Mathf.Clamp(p.y, b.min.y, b.max.y);

        return new Vector3(x, y, p.z);
    }

    public bool TryPlaceSticker(BaseSticker s, Vector3 dropPos)
    {
        Vector2 center = dropPos;
        float step = 0.25f;

        float safeRadius = s.GetComponent<Collider2D>().bounds.extents.x * 1.1f;

        float maxRadius = Mathf.Max(
            bagAreaCollider.bounds.extents.x,
            bagAreaCollider.bounds.extents.y
        );

        for (float r = 0; r < maxRadius; r += step)
        {
            for (float a = 0; a < 360; a += 12)
            {
                Vector2 candidate = center + new Vector2(
                    Mathf.Cos(a * Mathf.Deg2Rad) * r,
                    Mathf.Sin(a * Mathf.Deg2Rad) * r
                );

                if (!bagAreaCollider.OverlapPoint(candidate))
                    continue;

                if (Collides(candidate, safeRadius, s))
                    continue;

                Transform root = s.transform.parent;
                root.position = candidate;
                root.rotation = Quaternion.identity;

                root.SetParent(bagContentRoot, true);

                if (!placedStickers.Contains(s))
                    placedStickers.Add(s);

                s.currentSegment = null;
                s.isPlaced = true;

                return true;
            }
        }

        return false;
    }

    private bool Collides(Vector2 candidate, float radius, BaseSticker ignore)
    {
        foreach (var other in placedStickers)
        {
            if (other == ignore) continue;

            float d = Vector2.Distance(candidate, other.transform.position);
            if (d < radius * 2f)
                return true;
        }

        return false;
    }

    public void RemoveSticker(BaseSticker s)
    {
        placedStickers.Remove(s);
    }
}
