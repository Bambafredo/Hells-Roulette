using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class SegmentMesh : MonoBehaviour
{
    [Min(3)] public int resolution = 16; // suavidad del arco
    [Min(0.1f)] public float radius = 2.5f;
    [Range(1f, 360f)] public float angle = 30f; // grados
    public Color color = Color.white;

    public void GenerateMesh()
    {
        var mesh = new Mesh { name = "SegmentMesh" };
        GetComponent<MeshFilter>().sharedMesh = mesh;

        int vertexCount = resolution + 2; // centro + (res+1) arco
        var vertices = new Vector3[vertexCount];
        var triangles = new int[resolution * 3];

        // centro
        vertices[0] = Vector3.zero;

        // vértices del arco (sentido antihorario)
        for (int i = 0; i <= resolution; i++)
        {
            float t = i / (float)resolution;
            float a = Mathf.Deg2Rad * (t * angle);
            float x = Mathf.Cos(a) * radius;
            float y = Mathf.Sin(a) * radius;
            vertices[i + 1] = new Vector3(x, y, 0f);
        }

        // triángulos en abanico
        for (int i = 0; i < resolution; i++)
        {
            int tri = i * 3;
            triangles[tri + 0] = 0;
            triangles[tri + 1] = i + 1;
            triangles[tri + 2] = i + 2;
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();

        // material tipo sprite (respeta sorting layer / order)
        var mr = GetComponent<MeshRenderer>();
        if (mr.sharedMaterial == null)
        {
            var mat = new Material(Shader.Find("Sprites/Default"));
            mr.sharedMaterial = mat;
        }

        mr.sharedMaterial.color = color;
    }
}