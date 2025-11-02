using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WheelGenerator : MonoBehaviour
{
    [Header("Segments")]
    [Range(2, 36)] public int segmentCount = 8;
    [Min(0.1f)] public float radius = 2.5f;
    [Min(3)] public int meshResolution = 20;

    [Header("Visual / Sorting")]
    public bool regenerateOnPlay = true;
    public string sortingLayerName = "Default";
    public int sortingOrder = 0; // por debajo de la flecha

    [Header("Pins (ticks)")]
    public bool generatePins = true;
    [Tooltip("Offset radial respecto a 'radius'. Negativo = un pelín hacia dentro")]
    public float pinRadiusOffset = -0.05f;
    [Tooltip("Ancho x Alto del collider del pin")]
    public Vector2 pinSize = new Vector2(0.08f, 0.30f);
    public bool pinsAreTriggers = true;
    public string pinsParentName = "Pins";

    [ContextMenu("Generate Wheel")]
    public void GenerateWheel()
    {
        ClearChildren();

        float step = 360f / segmentCount;

        // Contenedor de segmentos (para mantener orden limpio)
        Transform segmentsRoot = new GameObject("Segments").transform;
        segmentsRoot.SetParent(transform, false);

        // Crear segmentos
        for (int i = 0; i < segmentCount; i++)
        {
            var go = new GameObject($"Segment_{i}");
            go.transform.SetParent(segmentsRoot, false);
            go.transform.localPosition = Vector3.zero;

            float startAngle = i * step;
            go.transform.localRotation = Quaternion.Euler(0, 0, -startAngle);

            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            var sm = go.AddComponent<SegmentMesh>();

            sm.radius = radius;
            sm.angle = step;
            sm.resolution = meshResolution;
            sm.color = Color.HSVToRGB((float)i / segmentCount, 0.85f, 1f);
            sm.GenerateMesh();

            mr.sortingLayerName = sortingLayerName;
            mr.sortingOrder = sortingOrder;
        }

        if (generatePins)
            GeneratePins(step);
    }

    void GeneratePins(float step)
    {
        // Limpia contenedor previo si existe
        Transform old = transform.Find(pinsParentName);
        if (old != null)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) DestroyImmediate(old.gameObject);
            else Destroy(old.gameObject);
#else
            Destroy(old.gameObject);
#endif
        }

        Transform pinsRoot = new GameObject(pinsParentName).transform;
        pinsRoot.SetParent(transform, false);

        float useRadius = radius + pinRadiusOffset;

        // Un pin en cada borde de segmento (en ángulos 0, step, 2*step, ...)
        for (int i = 0; i < segmentCount; i++)
        {
            float angleDeg = i * step;
            float rad = angleDeg * Mathf.Deg2Rad;

            Vector3 pos = new Vector3(Mathf.Cos(rad) * useRadius, Mathf.Sin(rad) * useRadius, 0f);

            GameObject pin = new GameObject($"Pin_{i}");
            pin.transform.SetParent(pinsRoot, false);
            pin.transform.localPosition = pos;
            pin.transform.localRotation = Quaternion.Euler(0, 0, -angleDeg);

            var bc = pin.AddComponent<BoxCollider2D>();
            bc.size = pinSize;
            bc.isTrigger = pinsAreTriggers;

            // Si quieres verlos en escena, puedes añadirles un SpriteRenderer opcional con un píxel
            // o dejarlos invisibles (recomendado).
        }
    }

    void Start()
    {
        if (regenerateOnPlay) GenerateWheel();
    }

    void ClearChildren()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i);
#if UNITY_EDITOR
            if (!Application.isPlaying) DestroyImmediate(child.gameObject);
            else Destroy(child.gameObject);
#else
            Destroy(child.gameObject);
#endif
        }
    }
}