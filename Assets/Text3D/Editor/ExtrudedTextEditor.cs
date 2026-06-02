using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ExtrudedText))]
public class ExtrudedTextEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        GUILayout.Space(10f);

        if (GUILayout.Button("Generate Extruded Mesh"))
        {
            ExtrudedText extruded =
                (ExtrudedText)target;

            extruded.Generate();

            EditorUtility.SetDirty(extruded);
        }
    }
}