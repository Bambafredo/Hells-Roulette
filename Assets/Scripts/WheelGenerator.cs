using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class WheelSegmentData
{
    public int index;
    public PolygonCollider2D collider;
}

public class WheelGenerator : MonoBehaviour
{
    [Header("Segments")]
    [Range(2, 36)] public int segmentCount = 8;
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

    [HideInInspector]
    public List<WheelSegmentData> segments = new List<WheelSegmentData>();

    // --------------------------------------------------------------------
    // GENERATE WHEEL
    // --------------------------------------------------------------------
    [ContextMenu("Generate Wheel")]
    public void GenerateWheel()
    {
        ClearChildren();
        segments.Clear();

        float step = 360f / segmentCount;
        Transform segmentsRoot = new GameObject("Segments").transform;
        segmentsRoot.SetParent(transform, false);

        // Layers opcionales
        int segmentLayer = LayerMask.NameToLayer("Segment");
        int pinLayer = LayerMask.NameToLayer("Pin");

#if UNITY_EDITOR
        if (segmentLayer == -1)
            Debug.LogWarning("⚠️ Layer 'Segment' no existe. No pasa nada, pero deberías crearla.");
        if (pinLayer == -1)
            Debug.LogWarning("⚠️ Layer 'Pin' no existe. No pasa nada, pero deberías crearla.");
#endif

        // ------------------------------------------------------------
        // CREAR SEGMENTOS
        // ------------------------------------------------------------
        for (int i = 0; i < segmentCount; i++)
        {
            GameObject seg = new GameObject($"Segment_{i}");
            seg.transform.SetParent(segmentsRoot, false);
            seg.transform.localRotation = Quaternion.Euler(0, 0, -i * step);

            // Mesh
            var mf = seg.AddComponent<MeshFilter>();
            var mr = seg.AddComponent<MeshRenderer>();
            var sm = seg.AddComponent<SegmentMesh>();

            sm.radius = radius;
            sm.angle = step;
            sm.resolution = meshResolution;
            sm.color = Color.HSVToRGB((float)i / segmentCount, 0.85f, 1f);
            sm.GenerateMesh();

            mr.sortingLayerName = sortingLayerName;
            mr.sortingOrder = sortingOrder;

            // ------------------------------------------------------------
            // Collider del segmento
            // ------------------------------------------------------------
            PolygonCollider2D poly = seg.AddComponent<PolygonCollider2D>();
            poly.isTrigger = true;

            List<Vector2> pts = new List<Vector2>();
            pts.Add(Vector2.zero);

            int res = Mathf.Max(3, meshResolution);
            for (int k = 0; k <= res; k++)
            {
                float t = k / (float)res;
                float a = Mathf.Deg2Rad * (t * step);
                pts.Add(new Vector2(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius));
            }

            poly.SetPath(0, pts.ToArray());

            // Asignar capa segment
            if (segmentLayer != -1)
                seg.layer = segmentLayer;

            segments.Add(new WheelSegmentData
            {
                index = i,
                collider = poly
            });
        }

        // ------------------------------------------------------------
        // GENERAR PINS
        // ------------------------------------------------------------
        if (generatePins)
            GeneratePins(step, pinLayer);
    }

    // --------------------------------------------------------------------
    // GENERATE PINS
    // --------------------------------------------------------------------
    void GeneratePins(float step, int pinLayer)
    {
        Transform old = transform.Find(pinsParentName);
        if (old != null)
        {
#if UNITY_EDITOR
            DestroyImmediate(old.gameObject);
#else
            Destroy(old.gameObject);
#endif
        }

        Transform pinsRoot = new GameObject(pinsParentName).transform;
        pinsRoot.SetParent(transform, false);

        float useRadius = radius + pinRadiusOffset;

        for (int i = 0; i < segmentCount; i++)
        {
            float angleDeg = i * step;
            float rad = angleDeg * Mathf.Deg2Rad;

            Vector3 pos = new Vector3(
                Mathf.Cos(rad) * useRadius,
                Mathf.Sin(rad) * useRadius,
                0f
            );

            GameObject pin = new GameObject($"Pin_{i}");
            pin.transform.SetParent(pinsRoot, false);
            pin.transform.localPosition = pos;
            pin.transform.localRotation = Quaternion.Euler(0, 0, -angleDeg);

            BoxCollider2D bc = pin.AddComponent<BoxCollider2D>();
            bc.size = pinSize;
            bc.isTrigger = pinsAreTriggers;

            if (pinLayer != -1)
                pin.layer = pinLayer;
        }
    }

    // --------------------------------------------------------------------
    // HELPERS
    // --------------------------------------------------------------------
    public int GetSegmentUnderPoint(Vector2 worldPoint)
    {
        foreach (WheelSegmentData seg in segments)
        {
            if (seg.collider != null && seg.collider.OverlapPoint(worldPoint))
                return seg.index;
        }

        if (segments.Count == 0)
            return 0;

        return 0; // fallback
    }

    void Start()
    {
        if (regenerateOnPlay)
            GenerateWheel();
    }

    // --------------------------------------------------------------------
    // CLEANUP
    // --------------------------------------------------------------------
    void ClearChildren()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            while (transform.childCount > 0)
                DestroyImmediate(transform.GetChild(0).gameObject);
        }
        else
        {
            foreach (Transform t in transform)
                Destroy(t.gameObject);
        }
#else
        foreach (Transform t in transform)
            Destroy(t.gameObject);
#endif
    }
}