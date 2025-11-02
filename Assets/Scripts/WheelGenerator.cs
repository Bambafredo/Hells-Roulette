using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WheelGenerator : MonoBehaviour
{
    [Header("Segments")]
    [Range(2, 36)] public int segmentCount = 8;
    [Min(0.1f)] public float radius = 2.5f;
    [Min(3)] public int meshResolution = 20;

    [Header("Visual")]
    public bool regenerateOnPlay = true;
    public int sortingOrder = 0;           // para que queden bajo la flecha
    public string sortingLayerName = "Default";

    private void Start()
    {
        if (regenerateOnPlay)
            GenerateWheel();
    }

    [ContextMenu("Generate Wheel")]
    public void GenerateWheel()
    {
        // limpia hijos anteriores
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);

        float step = 360f / segmentCount;

        for (int i = 0; i < segmentCount; i++)
        {
            var go = new GameObject($"Segment_{i}");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;

            // rota el segmento para que su arco cubra su sector
            float startAngle = i * step;
            go.transform.localRotation = Quaternion.Euler(0, 0, -startAngle);

            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            var sm = go.AddComponent<SegmentMesh>();

            // parámetros de la rebanada
            sm.radius = radius;
            sm.angle = step;
            sm.resolution = meshResolution;
            sm.color = Color.HSVToRGB((float)i / segmentCount, 0.85f, 1f);
            sm.GenerateMesh();

            // sorting (para que respeten capas 2D)
            mr.sortingLayerName = sortingLayerName;
            mr.sortingOrder = sortingOrder;
        }
    }
}