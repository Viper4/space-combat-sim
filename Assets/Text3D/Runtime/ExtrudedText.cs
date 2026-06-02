using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class ExtrudedText : MonoBehaviour
{
    [SerializeField] private TMP_Text tmpText;
    [SerializeField] private Font font;

    [SerializeField] private float depth = 0.1f;

    [SerializeField] private Material sideMaterial;

    [Header("Save")]
    [SerializeField] private string saveFolder = "Assets/GeneratedTextMeshes";
    [SerializeField] private string meshFileName = "ExtrudedText";

    [SerializeField] private Mesh generatedMesh;

    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;

    private void OnEnable()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
    }

    public void Generate()
    {
        if (tmpText == null || font == null)
        {
            Debug.LogWarning("Missing TMP_Text or Font.");
            return;
        }
        tmpText.ForceMeshUpdate(true, true);

        Debug.Log(tmpText.text);
        Debug.Log(tmpText.textInfo.characterCount);

        string fontPath = AssetDatabase.GetAssetPath(font);
        Debug.Log("FONT PATH: " + fontPath);
        Debug.Log("FONT EXISTS: " + File.Exists(fontPath));
        if (string.IsNullOrEmpty(fontPath))
        {
            Debug.LogError("Could not find font asset path.");
            return;
        }
        byte[] fontBytes = File.ReadAllBytes(fontPath);
        Debug.Log("FONT BYTES: " + fontBytes.Length);
        FontOutlineExtractor extractor = new FontOutlineExtractor(fontBytes);

        Mesh mesh = ExtrudedMeshBuilder.Build(
            tmpText,
            depth,
            extractor
        );

        if (mesh == null)
        {
            Debug.LogError("Mesh generation failed.");
            return;
        }

        mesh.name = meshFileName;

        SaveMeshAsset(mesh);

        generatedMesh = mesh;

        meshFilter.sharedMesh = generatedMesh;

        meshRenderer.sharedMaterials = new Material[]
        {
            tmpText.fontSharedMaterial,
            sideMaterial
        };

        EditorUtility.SetDirty(this);

        Debug.Log("Generated extruded text mesh.");
    }

    private void SaveMeshAsset(Mesh mesh)
    {
        if (!AssetDatabase.IsValidFolder(saveFolder))
        {
            string[] folders = saveFolder.Split('/');

            string current = folders[0];

            for (int i = 1; i < folders.Length; i++)
            {
                string next = current + "/" + folders[i];

                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, folders[i]);
                }

                current = next;
            }
        }

        string assetPath = $"{saveFolder}/{meshFileName}.asset";

        Mesh existingMesh = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);

        if (existingMesh != null)
        {
            EditorUtility.CopySerialized(mesh, existingMesh);
            AssetDatabase.SaveAssets();

            generatedMesh = existingMesh;
        }
        else
        {
            AssetDatabase.CreateAsset(mesh, assetPath);
            AssetDatabase.SaveAssets();

            generatedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
        }

        AssetDatabase.Refresh();
    }
}