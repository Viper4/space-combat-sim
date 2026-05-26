using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(CelestialBody))]
public class CelestialBodyEditor : Editor
{
    CelestialBody celestialBody;
    Editor generationEditor;
    Editor gravityEditor;

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        DrawSettingsEditor(celestialBody.generationSettings, ref celestialBody.generationSettingsFoldout, ref generationEditor);
        if(celestialBody.gravity)
            DrawSettingsEditor(celestialBody.gravitySettings, ref celestialBody.gravitySettingsFoldout, ref gravityEditor);
    }

    void DrawSettingsEditor(Object settings, ref bool foldout, ref Editor editor)
    {
        if (settings != null)
        {
            foldout = EditorGUILayout.InspectorTitlebar(foldout, settings);
            if (foldout)
            {
                CreateCachedEditor(settings, null, ref editor);
                editor.OnInspectorGUI();
            }
        }
    }

    private void OnEnable()
    {
        celestialBody = (CelestialBody)target;
    }
}
