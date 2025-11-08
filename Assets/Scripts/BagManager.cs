using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BagManager : MonoBehaviour
{
    public static BagManager Instance;

    [Header("Screens")]
    public GameObject bagScreen;
    public GameObject gameplayScreen;

    [Header("Areas")]
    public Collider2D bagAreaCollider;
    public Collider2D gameplayAreaCollider;

    [Header("Portals")]
    public Collider2D bagPortalCollider;        // Gameplay → Bag
    public Collider2D gameplayPortalCollider;   // Bag → Gameplay

    [Header("Roots")]
    public Transform bagContentRoot;
    public Transform gameplayContentRoot;

    [Header("Slots")]
    public List<BagZone> bagSlots = new List<BagZone>();
    public BagZone currentBagZone;

    private void Awake()
    {
        Instance = this;

        // Detectar todos los BagZone de la escena
        bagSlots.AddRange(GetComponentsInChildren<BagZone>(true));
    }

    private void Update()
    {
        HandlePortalClicks();
    }

    // ------------------ PORTAL INPUT ---------------------

    private void HandlePortalClicks()
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

    // ------------------ SCREEN SWITCH ---------------------

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

    // ------------------ HELPERS ---------------------

    public bool IsPointInsideBag(Vector2 p)
        => bagAreaCollider != null && bagAreaCollider.OverlapPoint(p);

    public bool IsPointInsideGameplay(Vector2 p)
        => gameplayAreaCollider != null && gameplayAreaCollider.OverlapPoint(p);

    public bool IsPointOnBagPortal(Vector2 p)
        => bagPortalCollider != null && bagPortalCollider.OverlapPoint(p);

    public bool IsPointOnGameplayPortal(Vector2 p)
        => gameplayPortalCollider != null && gameplayPortalCollider.OverlapPoint(p);

    public Vector3 ClampToBag(Vector3 p)
    {
        Bounds b = bagAreaCollider.bounds;
        return new Vector3(
            Mathf.Clamp(p.x, b.min.x, b.max.x),
            Mathf.Clamp(p.y, b.min.y, b.max.y),
            p.z
        );
    }

    // ------------------ SLOT LOGIC ---------------------

    public BagZone FindFirstFreeSlot()
    {
        foreach (var slot in bagSlots)
        {
            if (!slot.occupied)
                return slot;
        }
        return null;
    }

    public BagZone GetSlotAtPosition(Vector2 p)
    {
        foreach (var slot in bagSlots)
        {
            if (slot.zoneCollider.OverlapPoint(p))
                return slot;
        }
        return null;
    }

    public void PlaceStickerInSlot_Auto(BaseSticker s, BagZone slot)
    {
        // liberar slot anterior si tenía
        if (s.currentBagZone != null)
        {
            s.currentBagZone.occupied = false;
            s.currentBagZone.autoSticker = null;
        }

        slot.occupied = true;
        slot.autoSticker = s;
        s.currentBagZone = slot;
        s.isPlaced = true;

        Transform root = s.transform.parent;
        root.SetParent(slot.contentRoot, false);
        root.localPosition = Vector3.zero;
        root.localRotation = Quaternion.identity;
    }

    public void FreeSlot(BaseSticker s)
    {
        if (s.currentBagZone != null)
        {
            s.currentBagZone.occupied = false;
            s.currentBagZone.autoSticker = null;
            s.currentBagZone = null;
        }
    }
}
