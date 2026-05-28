using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ExtrudedText))]
public class ExtrudedTextEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("Generate a 3D extruded mesh from the current TMP text.", MessageType.Info);

        if (GUILayout.Button("Generate Extruded Mesh", GUILayout.Height(28f)))
        {
            ExtrudedText targetText = (ExtrudedText)target;
            Undo.RecordObject(targetText, "Generate Extruded Mesh");
            targetText.GetComponents();
            targetText.GenerateExtrudedMesh();
            EditorUtility.SetDirty(targetText);

            Mesh mesh = targetText.GetGeneratedMesh();
            if (mesh != null)
            {
                EditorUtility.SetDirty(mesh);
            }

            Debug.Log("Extruded text mesh regenerated.", targetText);
        }
    }
}