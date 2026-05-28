using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class ExtrudedText : MonoBehaviour
{
    [Header("Text")]
    [SerializeField] private TextMeshPro tmpText;

    [Header("Mesh")]
    [SerializeField] private float depth = 0.08f;
    [SerializeField] private bool generateCollider = true;

    [Header("Materials")]
    [SerializeField] private Material sideMaterial;

    [Header("Output")]
    [SerializeField] private Mesh generatedMesh;

    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private MeshCollider meshCollider;

    private void OnEnable()
    {
        GetComponents();
        ApplyMaterials();
    }

    private void OnValidate()
    {
        GetComponents();
        ApplyMaterials();
    }

    public void GetComponents()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
        meshCollider = GetComponent<MeshCollider>();
    }

    private void ApplyMaterials()
    {
        if (meshRenderer == null)
            return;

        if (tmpText != null && tmpText.fontSharedMaterial != null)
        {
            if (sideMaterial != null)
                meshRenderer.sharedMaterials = new[] { tmpText.fontSharedMaterial, sideMaterial };
            else
                meshRenderer.sharedMaterial = tmpText.fontSharedMaterial;
        }
    }

    [ContextMenu("Generate Extruded Mesh")]
    public void GenerateExtrudedMesh()
    {
        if (tmpText == null)
        {
            Debug.LogWarning($"{name} TMP Text is null, cannot generate an extruded mesh.");
            return;
        }

        tmpText.ForceMeshUpdate(true, true);

        TMP_TextInfo textInfo = tmpText.textInfo;
        if (textInfo == null || textInfo.meshInfo == null || textInfo.meshInfo.Length == 0)
        {
            Debug.LogWarning($"{name} TMP Text has no mesh data to extrude.");
            return;
        }

        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<Vector3> normals = new List<Vector3>();
        List<Color32> colors = new List<Color32>();
        List<int> frontBackTriangles = new List<int>();
        List<int> sideTriangles = new List<int>();

        for (int m = 0; m < textInfo.meshInfo.Length; m++)
        {
            TMP_MeshInfo meshInfo = textInfo.meshInfo[m];
            if (meshInfo.vertices == null || meshInfo.vertices.Length == 0)
                continue;

            int frontStart = vertices.Count;
            int backStart = frontStart + meshInfo.vertices.Length;

            for (int i = 0; i < meshInfo.vertices.Length; i++)
            {
                vertices.Add(meshInfo.vertices[i]);
                uvs.Add(meshInfo.uvs0.Length > i ? meshInfo.uvs0[i] : Vector2.zero);
                normals.Add(Vector3.forward);
                colors.Add(meshInfo.colors32.Length > i ? meshInfo.colors32[i] : tmpText.color);
            }

            for (int i = 0; i < meshInfo.vertices.Length; i++)
            {
                vertices.Add(meshInfo.vertices[i] + new Vector3(0f, 0f, -depth));
                uvs.Add(meshInfo.uvs0.Length > i ? meshInfo.uvs0[i] : Vector2.zero);
                normals.Add(Vector3.back);
                colors.Add(meshInfo.colors32.Length > i ? meshInfo.colors32[i] : tmpText.color);
            }

            for (int i = 0; i < meshInfo.triangles.Length; i += 3)
            {
                int a = frontStart + meshInfo.triangles[i];
                int b = frontStart + meshInfo.triangles[i + 1];
                int c = frontStart + meshInfo.triangles[i + 2];

                frontBackTriangles.Add(a);
                frontBackTriangles.Add(b);
                frontBackTriangles.Add(c);

                int ba = backStart + meshInfo.triangles[i];
                int bb = backStart + meshInfo.triangles[i + 2];
                int bc = backStart + meshInfo.triangles[i + 1];

                frontBackTriangles.Add(ba);
                frontBackTriangles.Add(bb);
                frontBackTriangles.Add(bc);
            }
        }

        int[] frontStarts = new int[textInfo.meshInfo.Length];
        for (int m = 1; m < textInfo.meshInfo.Length; m++)
            frontStarts[m] = frontStarts[m - 1] + textInfo.meshInfo[m - 1].vertices.Length * 2;

        Dictionary<long, int> edgeUseCount = new Dictionary<long, int>();
        for (int m = 0; m < textInfo.meshInfo.Length; m++)
        {
            TMP_MeshInfo meshInfo = textInfo.meshInfo[m];
            for (int i = 0; i < meshInfo.triangles.Length; i += 3)
            {
                AddEdge(edgeUseCount, m, meshInfo.triangles[i], meshInfo.triangles[i + 1]);
                AddEdge(edgeUseCount, m, meshInfo.triangles[i + 1], meshInfo.triangles[i + 2]);
                AddEdge(edgeUseCount, m, meshInfo.triangles[i + 2], meshInfo.triangles[i]);
            }
        }

        foreach (KeyValuePair<long, int> edge in edgeUseCount)
        {
            if (edge.Value != 1)
                continue;

            int meshIndex = (int)(edge.Key >> 56);
            int a = (int)((edge.Key >> 32) & 0xFFFFFFFF);
            int b = (int)(edge.Key & 0xFFFFFFFF);
            int frontStart = frontStarts[meshIndex];
            int backStart = frontStart + textInfo.meshInfo[meshIndex].vertices.Length;

            int frontA = frontStart + a;
            int frontB = frontStart + b;
            int backA = backStart + a;
            int backB = backStart + b;

            Vector3 va = vertices[frontA];
            Vector3 vb = vertices[frontB];
            Vector3 vba = vertices[backA];
            Vector3 vbb = vertices[backB];

            Vector3 edgeDir = (vb - va).normalized;
            Vector3 sideNormal = Vector3.Cross(edgeDir, Vector3.forward).normalized;
            if (sideNormal.sqrMagnitude < 0.0001f)
                sideNormal = Vector3.up;

            int sideStart = vertices.Count;
            vertices.Add(va);
            vertices.Add(vb);
            vertices.Add(vbb);
            vertices.Add(vba);
            uvs.Add(new Vector2(0f, 0f));
            uvs.Add(new Vector2(1f, 0f));
            uvs.Add(new Vector2(1f, 1f));
            uvs.Add(new Vector2(0f, 1f));
            normals.Add(sideNormal);
            normals.Add(sideNormal);
            normals.Add(sideNormal);
            normals.Add(sideNormal);
            colors.Add(tmpText.color);
            colors.Add(tmpText.color);
            colors.Add(tmpText.color);
            colors.Add(tmpText.color);

            sideTriangles.Add(sideStart + 0);
            sideTriangles.Add(sideStart + 1);
            sideTriangles.Add(sideStart + 2);
            sideTriangles.Add(sideStart + 0);
            sideTriangles.Add(sideStart + 2);
            sideTriangles.Add(sideStart + 3);
        }

        if (generatedMesh == null)
            generatedMesh = new Mesh { name = $"{tmpText.text}_ExtrudedMesh" };

        generatedMesh.indexFormat = IndexFormat.UInt32;
        generatedMesh.Clear();
        generatedMesh.SetVertices(vertices);
        generatedMesh.SetUVs(0, uvs);
        generatedMesh.SetColors(colors);
        generatedMesh.SetNormals(normals);
        generatedMesh.subMeshCount = 2;
        generatedMesh.SetTriangles(frontBackTriangles, 0);
        generatedMesh.SetTriangles(sideTriangles, 1);
        generatedMesh.RecalculateBounds();
        generatedMesh.RecalculateTangents();

        if (meshFilter != null)
            meshFilter.sharedMesh = generatedMesh;

        ApplyMaterials();

        if (generateCollider && meshCollider != null)
            meshCollider.sharedMesh = generatedMesh;
        else if (generateCollider)
        {
            meshCollider = GetComponent<MeshCollider>();
            if (meshCollider == null)
                meshCollider = gameObject.AddComponent<MeshCollider>();
            meshCollider.sharedMesh = generatedMesh;
        }
    }

    private static void AddEdge(Dictionary<long, int> edgeUseCount, int meshIndex, int a, int b)
    {
        int first = Mathf.Min(a, b);
        int second = Mathf.Max(a, b);
        long key = ((long)meshIndex << 56) | ((long)first << 32) | (uint)second;
        if (!edgeUseCount.ContainsKey(key))
            edgeUseCount[key] = 0;
        edgeUseCount[key]++;
    }

    public Mesh GetGeneratedMesh() => generatedMesh;
}