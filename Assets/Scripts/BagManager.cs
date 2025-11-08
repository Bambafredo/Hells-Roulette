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
    public List<BagZone> gameplaySlots = new List<BagZone>();

    private void Awake()
    {
        Instance = this;

        // Si el diseñador ya asignó los slots en el editor, no hacemos nada.
        // Sólo autodescubrir si las listas están vacías (fallback).
        if (bagSlots.Count == 0 || gameplaySlots.Count == 0)
        {
            var allZones = FindObjectsOfType<BagZone>(true);
            foreach (var z in allZones)
            {
                if (bagScreen != null && z.transform.IsChildOf(bagScreen.transform))
                    if (!bagSlots.Contains(z)) bagSlots.Add(z);
                else if (gameplayScreen != null && z.transform.IsChildOf(gameplayScreen.transform))
                    if (!gameplaySlots.Contains(z)) gameplaySlots.Add(z);
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

    public bool IsPointInsideGameplay(Vector2 p)
        => gameplayAreaCollider != null && gameplayAreaCollider.OverlapPoint(p);

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

    public Vector3 ClampToGameplay(Vector3 p)
    {
        if (gameplayAreaCollider == null) return p;
        Bounds b = gameplayAreaCollider.bounds;
        return new Vector3(
            Mathf.Clamp(p.x, b.min.x, b.max.x),
            Mathf.Clamp(p.y, b.min.y, b.max.y),
            p.z
        );
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
        // Liberar slot anterior si existía
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

        s.isPlaced = false;  // no está en ruleta
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

    // ---------- HELPERS PARA COLOCACIÓN MANUAL EN SLOTS (sin solaparse) ----------

    public bool TryPlaceInSlotManual(BaseSticker s, BagZone slot, Vector3 dropPos, int maxTries = 80)
    {
        if (slot == null || slot.zoneCollider == null) return false;

        // Root del sticker según tu jerarquía (el "padre" contenedor)
        Transform selfRoot = s.transform.parent != null ? s.transform.parent : s.transform;

        // Radio de seguridad aproximado desde su collider
        float r = ApproxRadius(s);
        if (r <= 0f) r = 0.25f; // fallback

        // Centro inicial: clamp dentro del bounds del slot
        Bounds b = slot.zoneCollider.bounds;
        Vector3 seed = new Vector3(
            Mathf.Clamp(dropPos.x, b.min.x + r, b.max.x - r),
            Mathf.Clamp(dropPos.y, b.min.y + r, b.max.y - r),
            selfRoot.position.z
        );

        // Búsqueda espiral
        float stepR = Mathf.Max(r * 0.6f, 0.1f);
        float stepA = 18f; // grados
        float maxRadius = Mathf.Min(b.extents.x, b.extents.y) - r;

        // Colliders actuales dentro del slot (de otros stickers)
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
                selfRoot.rotation = Quaternion.identity; // sin giro en slot
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

    // *** ARREGLO CLAVE: sólo ignoramos el PROPIO sticker (su root), no a sus hermanos ***
    bool OverlapsAny(Vector3 candidate, float r, Collider2D[] others, BaseSticker self, Transform selfRoot)
    {
        foreach (var o in others)
        {
            if (o == null) continue;

            // Ignorar colliders que pertenezcan al mismo sticker (mismo root)
            if (o.transform.IsChildOf(selfRoot))
                continue;

            // Si el collider no pertenece a ningún sticker, lo ignoramos (por ejemplo decoraciones)
            var otherSticker = o.GetComponentInParent<BaseSticker>();
            if (otherSticker == null) continue;

            // Distancia centro a centro con radio aproximado del otro
            float d = Vector2.Distance(candidate, o.bounds.center);
            float ro = Mathf.Max(o.bounds.extents.x, o.bounds.extents.y);
            if (d < (r + ro) * 0.98f)
                return true;
        }
        return false;
    }
    public bool IsPointInsideAnyGameplayArea(Vector2 p)
    {
        if (gameplayAreaCollider != null && gameplayAreaCollider.OverlapPoint(p))
            return true;

        // Cualquier BagZone de gameplay cuenta como “área de gameplay”
        foreach (var slot in gameplaySlots)
            if (slot != null && slot.zoneCollider != null && slot.zoneCollider.OverlapPoint(p))
                return true;

        return false;
    }
}
