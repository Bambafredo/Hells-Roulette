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

    [HideInInspector]
    public List<WheelSegmentData> segments = new List<WheelSegmentData>();

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

        ForceDestroyChildren();
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

            // ❗ AQUÍ EL CAMBIO IMPORTANTE: ROTAMOS EN POSITIVO
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

        if (generatePins) GeneratePins(pinLayer);
    }

    // =====================================================================
    // GENERAR PINS AL CENTRO DE CADA SEGMENTO
    // =====================================================================
    void GeneratePins(int pinLayer)
    {
        Transform old = transform.Find(pinsParentName);
        if (old != null)
            DestroyImmediate(old.gameObject);

        Transform pinsRoot = new GameObject(pinsParentName).transform;
        pinsRoot.SetParent(transform, false);

        float angleAccum = 0f;
        float r = radius + pinRadiusOffset;

        for (int i = 0; i < segmentCount; i++)
        {
            float mid = angleAccum + segmentAngles[i] * 0.5f;
            float rad = mid * Mathf.Deg2Rad;

            Vector3 pos = new Vector3(Mathf.Cos(rad) * r, Mathf.Sin(rad) * r, 0f);

            GameObject pin = new GameObject("Pin_" + i);
            pin.transform.SetParent(pinsRoot, false);
            pin.transform.localPosition = pos;

            // También en positivo para que “mire” hacia afuera coherentemente
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
    // DESTROY CHILDREN (EDIT Y PLAY)
    // =====================================================================
    void ForceDestroyChildren()
    {
        List<Transform> toDestroy = new List<Transform>();
        foreach (Transform t in transform)
            toDestroy.Add(t);

        foreach (var t in toDestroy)
        {
#if UNITY_EDITOR
            DestroyImmediate(t.gameObject);
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
}