using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class WheelSegmentData
{
    public int index;
    public PolygonCollider2D collider;
    public SegmentMesh meshComponent;
}

public class WheelGenerator : MonoBehaviour
{
    [Header("Segments")]
    [Range(2, 36)] public int segmentCount = 8;

    [Tooltip("Ángulos individuales de cada segmento (en grados).")]
    public List<float> segmentAngles = new List<float>();

    [Min(0.1f)] public float radius = 2.5f;
    [Min(3)] public int meshResolution = 20;

    [Header("Visual")]
    public bool regenerateOnPlay = true;
    public string sortingLayerName = "Default";
    public int sortingOrder = 0;

    [Header("Pins (ticks)")]
    public bool generatePins = true;
    public float pinRadiusOffset = -0.05f;
    public Vector2 pinSize = new Vector2(0.08f, 0.30f);
    public bool pinsAreTriggers = true;
    public string pinsParentName = "Pins";

    [Header("Runtime Stickers")]
    [Tooltip("Contenedor fuera de la rueda donde aparcamos stickers al regenerar.")]
    public Transform dynamicStickerContainer;

    [Tooltip("Referencia al FlagPin para NO destruirlo al regenerar.")]
    public FlagPin flagPin;

    [HideInInspector]
    public List<WheelSegmentData> segments = new List<WheelSegmentData>();

    // ------------------------------------------------------------
    // Info temporal para restaurar stickers tras regenerar
    // ------------------------------------------------------------
    private struct StickerRegenInfo
    {
        public Transform root;       // root del prefab de sticker (GO vacío de arriba del todo)
        public int segmentIndex;     // índice de segmento donde estaba
    }

    private readonly List<StickerRegenInfo> stickerRegenInfos = new List<StickerRegenInfo>();

    // =====================================================================
    // NORMALIZACIÓN DE ÁNGULOS = SIEMPRE 360º
    // =====================================================================
    void NormalizeAngles()
    {
        if (segmentAngles == null) segmentAngles = new List<float>();

        // Si el tamaño de la lista no coincide con el número de segmentos, la reseteamos
        if (segmentAngles.Count != segmentCount)
        {
            segmentAngles.Clear();
            float def = 360f / segmentCount;
            for (int i = 0; i < segmentCount; i++)
                segmentAngles.Add(def);
            return;
        }

        float sum = 0f;
        foreach (var a in segmentAngles) sum += a;

        // Si ya suman 360 (con tolerancia), no tocamos nada
        if (Mathf.Abs(sum - 360f) < 0.01f)
            return;

        // Reescalamos proporcionalmente
        float correction = 360f / sum;
        for (int i = 0; i < segmentAngles.Count; i++)
            segmentAngles[i] *= correction;
    }

    // =====================================================================
    // SCALE SEGMENT
    // =====================================================================
    public void ScaleSegmentAngle(int index, float factor)
    {
        if (index < 0 || index >= segmentAngles.Count) return;

        segmentAngles[index] *= factor;
        NormalizeAngles();
        GenerateWheel();
    }

    // =====================================================================
    // MAIN GENERATION
    // =====================================================================
    [ContextMenu("Generate Wheel")]
    public void GenerateWheel()
    {
        NormalizeAngles();

        // 1) Guardar stickers y flagpin ANTES de destruir la rueda actual
        SaveStickersBeforeGeneration();

        // 2) Destruir hijos actuales (segmentos, pins, etc), respetando el FlagPin
        ForceDestroyChildren();

        // 3) Limpiar y regenerar segmentos nuevos
        segments.Clear();

        Transform root = new GameObject("Segments").transform;
        root.SetParent(transform, false);

        int segmentLayer = LayerMask.NameToLayer("Segment");
        int pinLayer = LayerMask.NameToLayer("Pin");

        float angleStart = 0f; // ángulo acumulado (inicio del segmento actual)

        for (int i = 0; i < segmentCount; i++)
        {
            float angle = segmentAngles[i];

            GameObject segGO = new GameObject("Segment_" + i);
            segGO.transform.SetParent(root, false);

            // Rotamos en sentido positivo
            segGO.transform.localRotation = Quaternion.Euler(0, 0, angleStart);

            // ------------------------
            // MESH FILTER
            // ------------------------
            MeshFilter mf = segGO.GetComponent<MeshFilter>();
            if (mf == null)
                mf = segGO.AddComponent<MeshFilter>();

            // ------------------------
            // MESH RENDERER
            // ------------------------
            MeshRenderer mr = segGO.GetComponent<MeshRenderer>();
            if (mr == null)
                mr = segGO.AddComponent<MeshRenderer>();

            // ------------------------
            // SEGMENT MESH
            // ------------------------
            SegmentMesh sm = segGO.GetComponent<SegmentMesh>();
            if (sm == null)
                sm = segGO.AddComponent<SegmentMesh>();

            sm.angle = angle;
            sm.radius = radius;
            sm.resolution = meshResolution;
            sm.color = Color.HSVToRGB(i / (float)segmentCount, 0.85f, 1f);
            sm.GenerateMesh();

            mr.sortingLayerName = sortingLayerName;
            mr.sortingOrder = sortingOrder;

            // ------------------------
            // COLLIDER
            // ------------------------
            PolygonCollider2D poly = segGO.GetComponent<PolygonCollider2D>();
            if (poly == null)
                poly = segGO.AddComponent<PolygonCollider2D>();

            poly.isTrigger = true;

            List<Vector2> pts = new List<Vector2>();
            pts.Add(Vector2.zero);

            int res = Mathf.Max(3, meshResolution);
            for (int k = 0; k <= res; k++)
            {
                float t = k / (float)res;
                float a = t * angle * Mathf.Deg2Rad;
                pts.Add(new Vector2(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius));
            }

            poly.SetPath(0, pts.ToArray());

            if (segmentLayer != -1)
                segGO.layer = segmentLayer;

            segments.Add(new WheelSegmentData
            {
                index = i,
                collider = poly,
                meshComponent = sm
            });

            angleStart += angle;
        }

        // 4) Generar pins
        if (generatePins) GeneratePins(pinLayer);

        // 5) Restaurar stickers y flagpin sobre la nueva rueda
        RestoreStickersAfterGeneration();
    }

    // =====================================================================
    // GUARDAR STICKERS ANTES DE REGENERAR
    // =====================================================================
    void SaveStickersBeforeGeneration()
    {
        stickerRegenInfos.Clear();

        // Recolectamos todos los stickers de la escena
        BaseSticker[] allStickers = FindObjectsOfType<BaseSticker>(true);

        foreach (BaseSticker s in allStickers)
        {
            if (s == null) continue;

            // Solo nos interesan los que están colocados en la ruleta
            if (!s.isPlaced || s.currentSegment == null)
                continue;

            // Nos aseguramos de que el segmento pertenece a ESTA rueda
            if (!s.currentSegment.IsChildOf(transform))
                continue;

            // 🔴 Nuevo: sacamos el índice del nombre "Segment_X"
            int segIdx = ExtractIndexFromName(s.currentSegment.name);
            if (segIdx < 0) continue;

            // Root del sticker = GO vacío superior (como en tu jerarquía)
            Transform stickerRoot = s.transform.parent != null ? s.transform.parent : s.transform;

            // Guardamos root + índice de segmento
            stickerRegenInfos.Add(new StickerRegenInfo
            {
                root = stickerRoot,
                segmentIndex = segIdx
            });

            // Lo aparcamos en el DynamicStickerContainer o suelto en la escena
            if (dynamicStickerContainer != null)
                stickerRoot.SetParent(dynamicStickerContainer, true);
            else
                stickerRoot.SetParent(null, true);
        }
    }

    // (la dejamos por si quieres usarla para debug en algún momento)
    int GetSegmentIndexFromSegmentTransform(Transform segTransform)
    {
        if (segTransform == null) return -1;

        for (int i = 0; i < segments.Count; i++)
        {
            if (segments[i].collider == null) continue;
            if (segments[i].collider.transform == segTransform)
                return segments[i].index;
        }

        return -1;
    }

    // =====================================================================
    // RESTAURAR STICKERS DESPUÉS DE REGENERAR
    // =====================================================================
    void RestoreStickersAfterGeneration()
    {
        foreach (var info in stickerRegenInfos)
        {
            Transform stickerRoot = info.root;
            if (stickerRoot == null) continue;

            BaseSticker s = stickerRoot.GetComponentInChildren<BaseSticker>();
            if (s == null) continue;

            if (info.segmentIndex < 0 || info.segmentIndex >= segments.Count)
            {
                Debug.LogWarning($"Sticker '{s.name}' lost its segment index after wheel regen.");
                continue;
            }

            WheelSegmentData segData = segments[info.segmentIndex];
            if (segData.collider == null)
            {
                Debug.LogWarning($"Sticker '{s.name}' has invalid segment collider after wheel regen.");
                continue;
            }

            Transform segTransform = segData.collider.transform;

            // Re-parent al nuevo segmento (manteniendo posición mundial)
            stickerRoot.SetParent(segTransform, true);

            // Actualizamos estado del sticker
            s.currentSegment = segTransform;
            s.isPlaced = true;

            // Validamos si sigue bien colocado
            bool inside = IsStickerMostlyInsideSegment(s, segData.collider);
            bool overlaps = StickerOverlapsOthersInSegment(s, segData.collider);

            if (!inside || overlaps)
            {
                Debug.LogWarning(
                    $"Sticker '{s.name}' is not correctly placed after wheel resize. " +
                    $"(inside={inside}, overlaps={overlaps})"
                );
            }

            // Micro-ajuste del propio sticker (tu método)
            s.OnRestoredAfterWheelRegen();
        }

        // Limpieza por si acaso
        stickerRegenInfos.Clear();
    }

    // =====================================================================
    // VALIDACIÓN DE STICKER EN SEGMENTO (similar a BaseSticker)
    // =====================================================================
    bool IsStickerMostlyInsideSegment(BaseSticker sticker, PolygonCollider2D segment)
    {
        if (sticker == null || segment == null) return false;

        Collider2D col = sticker.GetComponent<Collider2D>();
        if (col == null) return false;

        float tolerance = sticker.tolerance;
        float threshold = sticker.coverageThreshold;

        Bounds b = col.bounds;
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

    bool StickerOverlapsOthersInSegment(BaseSticker sticker, PolygonCollider2D segment)
    {
        if (sticker == null || segment == null) return false;

        Collider2D myCol = sticker.GetComponent<Collider2D>();
        if (myCol == null) return false;

        // Radio aproximado para comprobar solapes
        Vector2 center = myCol.bounds.center;
        float r = Mathf.Max(myCol.bounds.extents.x, myCol.bounds.extents.y) * 0.9f;

        // Usamos la misma máscara de stickers que usa el propio sticker
        LayerMask mask = sticker.stickerMask;
        Collider2D[] overlaps = Physics2D.OverlapCircleAll(center, r, mask);

        foreach (var o in overlaps)
        {
            if (o == null) continue;
            if (o == myCol) continue;

            // Debe pertenecer al mismo segmento
            if (!o.transform.IsChildOf(segment.transform))
                continue;

            BaseSticker other = o.GetComponentInParent<BaseSticker>();
            if (other == null || other == sticker) continue;

            // Encontramos otro sticker en este mismo segmento → solape lógico
            return true;
        }

        return false;
    }

    // =====================================================================
    // GENERAR PINS AL CENTRO DE CADA SEGMENTO
    // =====================================================================
    void GeneratePins(int pinLayer)
    {
        Transform old = transform.Find(pinsParentName);
        if (old != null)
#if UNITY_EDITOR
            DestroyImmediate(old.gameObject);
#else
            Destroy(old.gameObject);
#endif

        Transform pinsRoot = new GameObject(pinsParentName).transform;
        pinsRoot.SetParent(transform, false);

        float angleAccum = 0f;
        float r = radius + pinRadiusOffset;

        for (int i = 0; i < segmentCount; i++)
        {
            float mid = angleAccum;
            float rad = mid * Mathf.Deg2Rad;

            Vector3 pos = new Vector3(Mathf.Cos(rad) * r, Mathf.Sin(rad) * r, 0f);

            GameObject pin = new GameObject("Pin_" + i);
            pin.transform.SetParent(pinsRoot, false);
            pin.transform.localPosition = pos;

            pin.transform.localRotation = Quaternion.Euler(0, 0, mid);

            BoxCollider2D bc = pin.AddComponent<BoxCollider2D>();
            bc.isTrigger = pinsAreTriggers;
            bc.size = pinSize;

            if (pinLayer != -1)
                pin.layer = pinLayer;

            angleAccum += segmentAngles[i];
        }
    }

    // =====================================================================
    // DESTROY CHILDREN (EDIT Y PLAY) SIN CARGARSE EL FLAGPIN
    // =====================================================================
    void ForceDestroyChildren()
    {
        List<Transform> toDestroy = new List<Transform>();

        foreach (Transform t in transform)
        {
            // No destruir nada que contenga un FlagPin en su jerarquía
            if (t.GetComponentInChildren<FlagPin>(true) != null)
                continue;

            toDestroy.Add(t);
        }

        foreach (var t in toDestroy)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                DestroyImmediate(t.gameObject);
            else
                Destroy(t.gameObject);
#else
            Destroy(t.gameObject);
#endif
        }
    }

    void Start()
    {
        if (regenerateOnPlay)
            GenerateWheel();
    }

    // =====================================================================
    // NUEVO: SACAR ÍNDICE DEL NOMBRE "Segment_X"
    // =====================================================================
    int ExtractIndexFromName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return -1;

        if (!name.StartsWith("Segment_"))
            return -1;

        string number = name.Substring("Segment_".Length);
        if (int.TryParse(number, out int idx))
            return idx;

        return -1;
    }
}