using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BagManager : MonoBehaviour
{
    public static BagManager Instance;

    [Header("Screens")]
    public GameObject bagScreen;
    public GameObject gameplayScreen;

    // ---------------------------------------------------------
    //          NUEVO SISTEMA MULTI-ÁREA DE GAMEPLAY
    // ---------------------------------------------------------

    [System.Serializable]
    public class GameplayArea
    {
        public string name;
        public Collider2D areaCollider;
        public Transform contentRoot;
    }

    [Header("Gameplay Areas (MULTIPLE)")]
    public List<GameplayArea> gameplayAreas = new List<GameplayArea>();     // NEW

    // ---------------------------------------------------------
    //                     SISTEMA BAG
    // ---------------------------------------------------------

    [Header("Bag Area")]
    public Collider2D bagAreaCollider;

    [Header("Portals")]
    public Collider2D bagPortalCollider;        // Gameplay → Bag
    public Collider2D gameplayPortalCollider;   // Bag → Gameplay

    [Header("Roots")]
    public Transform bagContentRoot;

    // ¡OJO! gameplayContentRoot ya NO se usa como único root
    // pero lo dejo para compatibilidad (por si algo aún lo referencia).
    public Transform gameplayContentRoot;

    [Header("Slots")]
    public List<BagZone> bagSlots = new List<BagZone>();
    public List<BagZone> gameplaySlots = new List<BagZone>();

    private void Awake()
    {
        Instance = this;

        // Auto-descubrir slots si las listas están vacías
        if (bagSlots.Count == 0 || gameplaySlots.Count == 0)
        {
            var allZones = FindObjectsOfType<BagZone>(true);
            foreach (var z in allZones)
            {
                if (bagScreen != null && z.transform.IsChildOf(bagScreen.transform))
                {
                    if (!bagSlots.Contains(z)) bagSlots.Add(z);
                }
                else if (gameplayScreen != null && z.transform.IsChildOf(gameplayScreen.transform))
                {
                    if (!gameplaySlots.Contains(z)) gameplaySlots.Add(z);
                }
            }
        }
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

    public bool IsBagActive() => bagScreen != null && bagScreen.activeSelf;

    // ------------------ HELPERS ---------------------

    public bool IsPointInsideBag(Vector2 p)
        => bagAreaCollider != null && bagAreaCollider.OverlapPoint(p);

    // ya no usamos gameplayAreaCollider único
    // pero lo mantengo por compatibilidad
    public bool IsPointInsideGameplay(Vector2 p)
        => IsPointInsideAnyGameplayArea(p);

    public bool IsPointOnBagPortal(Vector2 p)
        => bagPortalCollider != null && bagPortalCollider.OverlapPoint(p);

    public bool IsPointOnGameplayPortal(Vector2 p)
        => gameplayPortalCollider != null && gameplayPortalCollider.OverlapPoint(p);

    public Vector3 ClampToBag(Vector3 p)
    {
        if (bagAreaCollider == null) return p;
        Bounds b = bagAreaCollider.bounds;
        return new Vector3(
            Mathf.Clamp(p.x, b.min.x, b.max.x),
            Mathf.Clamp(p.y, b.min.y, b.max.y),
            p.z
        );
    }

    // ---------- MULTI-ÁREA: CLAMP REAL AL ÁREA CORRECTA ----------

    public Vector3 ClampToGameplay(Vector3 p)
    {
        int idx = GetGameplayAreaIndexAtPoint(p);
        if (idx >= 0) return ClampToGameplay(p, idx);

        // fallback: usa área 0 si existe
        if (gameplayAreas.Count > 0)
            return ClampToGameplay(p, 0);

        return p;
    }

    public Vector3 ClampToGameplay(Vector3 p, int areaIndex)
    {
        if (areaIndex < 0 || areaIndex >= gameplayAreas.Count)
            return p;

        var area = gameplayAreas[areaIndex];
        if (area == null || area.areaCollider == null)
            return p;

        Bounds b = area.areaCollider.bounds;
        return new Vector3(
            Mathf.Clamp(p.x, b.min.x, b.max.x),
            Mathf.Clamp(p.y, b.min.y, b.max.y),
            p.z
        );
    }

    // ---------- MULTI-ÁREA: DETECTAR QUÉ ÁREA ESTÁ BAJO UN PUNTO ----------

    public int GetGameplayAreaIndexAtPoint(Vector2 p)
    {
        for (int i = 0; i < gameplayAreas.Count; i++)
        {
            var area = gameplayAreas[i];
            if (area != null && area.areaCollider != null && area.areaCollider.OverlapPoint(p))
                return i;
        }
        return -1;
    }

    // ---------- MULTI-ÁREA: ¿EL PUNTO ESTÁ EN ALGUNA ÁREA? ----------

    public bool IsPointInsideAnyGameplayArea(Vector2 p)
    {
        foreach (var area in gameplayAreas)
        {
            if (area == null || area.areaCollider == null) continue;
            if (area.areaCollider.OverlapPoint(p))
                return true;
        }

        // Los slots también cuentan como "área válida"
        foreach (var slot in gameplaySlots)
            if (slot != null && slot.zoneCollider != null && slot.zoneCollider.OverlapPoint(p))
                return true;

        return false;
    }

    // ------------------ SLOT QUERIES ---------------------

    public BagZone FindFirstFreeBagSlot()
    {
        foreach (var slot in bagSlots)
            if (!slot.occupied) return slot;
        return null;
    }

    public BagZone FindFirstFreeGameplaySlot()
    {
        foreach (var slot in gameplaySlots)
            if (!slot.occupied) return slot;
        return null;
    }

    public BagZone GetBagSlotAtPosition(Vector2 p)
    {
        foreach (var slot in bagSlots)
            if (slot.zoneCollider != null && slot.zoneCollider.OverlapPoint(p))
                return slot;
        return null;
    }

    public BagZone GetGameplaySlotAtPosition(Vector2 p)
    {
        foreach (var slot in gameplaySlots)
            if (slot.zoneCollider != null && slot.zoneCollider.OverlapPoint(p))
                return slot;
        return null;
    }

    // ------------------ AUTO PLACE (portales) ---------------------

    public void PlaceStickerInBagSlot_Auto(BaseSticker s, BagZone slot)
    {
        if (s.currentBagZone != null)
        {
            s.currentBagZone.occupied = false;
            s.currentBagZone.autoSticker = null;
        }

        slot.occupied = true;
        slot.autoSticker = s;
        s.currentBagZone = slot;

        Transform root = s.transform.parent != null ? s.transform.parent : s.transform;
        root.SetParent(slot.contentRoot, false);
        root.localPosition = Vector3.zero;
        root.localRotation = Quaternion.identity;
        s.isPlaced = true;
    }

    public void PlaceStickerInGameplaySlot_Auto(BaseSticker s, BagZone slot)
    {
        if (s.currentGameplayZone != null)
        {
            s.currentGameplayZone.occupied = false;
            s.currentGameplayZone.autoSticker = null;
        }

        slot.occupied = true;
        slot.autoSticker = s;
        s.currentGameplayZone = slot;

        Transform root = s.transform.parent != null ? s.transform.parent : s.transform;
        root.SetParent(slot.contentRoot, false);
        root.localPosition = Vector3.zero;
        root.localRotation = Quaternion.identity;

        s.isPlaced = false;
        s.currentSegment = null;
    }

    // ------------------ FREE (al empezar drag) ---------------------

    public void FreeBagSlot(BaseSticker s)
    {
        if (s.currentBagZone != null)
        {
            s.currentBagZone.occupied = false;
            s.currentBagZone.autoSticker = null;
            s.currentBagZone = null;
        }
    }

    public void FreeGameplaySlot(BaseSticker s)
    {
        if (s.currentGameplayZone != null)
        {
            s.currentGameplayZone.occupied = false;
            s.currentGameplayZone.autoSticker = null;
            s.currentGameplayZone = null;
        }
    }

    // ---------- HELPERS PARA COLOCACIÓN MANUAL EN SLOTS ----------

    public bool TryPlaceInSlotManual(BaseSticker s, BagZone slot, Vector3 dropPos, int maxTries = 80)
    {
        if (slot == null || slot.zoneCollider == null) return false;

        Transform selfRoot = s.transform.parent != null ? s.transform.parent : s.transform;

        float r = ApproxRadius(s);
        if (r <= 0f) r = 0.25f;

        Bounds b = slot.zoneCollider.bounds;
        Vector3 seed = new Vector3(
            Mathf.Clamp(dropPos.x, b.min.x + r, b.max.x - r),
            Mathf.Clamp(dropPos.y, b.min.y + r, b.max.y - r),
            selfRoot.position.z
        );

        float stepR = Mathf.Max(r * 0.6f, 0.1f);
        float stepA = 18f;
        float maxRadius = Mathf.Min(b.extents.x, b.extents.y) - r;

        var others = slot.contentRoot.GetComponentsInChildren<Collider2D>(true);

        int tries = 0;
        for (float rad = 0f; rad <= maxRadius + 0.0001f; rad += stepR)
        {
            for (float a = 0; a < 360f; a += stepA)
            {
                if (tries++ > maxTries) break;

                Vector3 candidate = new Vector3(
                    seed.x + Mathf.Cos(a * Mathf.Deg2Rad) * rad,
                    seed.y + Mathf.Sin(a * Mathf.Deg2Rad) * rad,
                    selfRoot.position.z
                );

                if (!PointInsideWithMargin(slot.zoneCollider, candidate, r))
                    continue;

                if (OverlapsAny(candidate, r, others, s, selfRoot))
                    continue;

                selfRoot.SetParent(slot.contentRoot, true);
                selfRoot.position = candidate;
                selfRoot.rotation = Quaternion.identity;
                return true;
            }
            if (tries > maxTries) break;
        }

        return false;
    }

    float ApproxRadius(BaseSticker s)
    {
        var c = s.GetComponent<Collider2D>();
        if (c == null) return 0.2f;
        var e = c.bounds.extents;
        return Mathf.Max(e.x, e.y);
    }

    bool PointInsideWithMargin(Collider2D zone, Vector3 p, float margin)
    {
        if (!zone.OverlapPoint(p)) return false;
        Bounds b = zone.bounds;
        return (p.x >= b.min.x + margin && p.x <= b.max.x - margin &&
                p.y >= b.min.y + margin && p.y <= b.max.y - margin);
    }

    bool OverlapsAny(Vector3 candidate, float r, Collider2D[] others, BaseSticker self, Transform selfRoot)
    {
        foreach (var o in others)
        {
            if (o == null) continue;

            if (o.transform.IsChildOf(selfRoot))
                continue;

            var otherSticker = o.GetComponentInParent<BaseSticker>();
            if (otherSticker == null) continue;

            float d = Vector2.Distance(candidate, o.bounds.center);
            float ro = Mathf.Max(o.bounds.extents.x, o.bounds.extents.y);
            if (d < (r + ro) * 0.98f)
                return true;
        }
        return false;
    }
}
