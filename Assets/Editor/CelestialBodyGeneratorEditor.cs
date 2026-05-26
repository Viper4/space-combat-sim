using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(CelestialBodyGenerator))]
public class CelestialBodyGeneratorEditor : Editor
{
    CelestialBodyGenerator celestialBody;
    Editor shapeEditor;
    Editor colorEditor;

    public override void OnInspectorGUI()
    {
        using (var check = new EditorGUI.ChangeCheckScope())
        {
            base.OnInspectorGUI();

            if (celestialBody.autoUpdate && check.changed)
            {
                celestialBody.GenerateCelestialBody();
            }
        }

        if(GUILayout.Button("Generate"))
        {
            celestialBody.GenerateCelestialBody();
        }

        if (GUILayout.Button("Generate Random"))
        {
            celestialBody.GenerateRandomCelestialBody();
        }

        if (GUILayout.Button("Generate Colors")) 
        {
            celestialBody.GenerateColors();
        }

        DrawSettingsEditor(celestialBody.originalShapeSettings, celestialBody.OnShapeSettingsUpdated, ref celestialBody.shapeSettingsFoldout, ref shapeEditor);
        DrawSettingsEditor(celestialBody.originalColorSettings, celestialBody.OnColorSettingsUpdated, ref celestialBody.colorSettingsFoldout, ref colorEditor);
    }

    void DrawSettingsEditor(Object settings, System.Action onSettingsUpdated, ref bool foldout, ref Editor editor)
    {
        if(settings != null)
        {
            foldout = EditorGUILayout.InspectorTitlebar(foldout, settings);
            using (var check = new EditorGUI.ChangeCheckScope())
            {
                if (foldout)
                {
                    CreateCachedEditor(settings, null, ref editor);
                    editor.OnInspectorGUI();

                    if (check.changed)
                    {
                        if (onSettingsUpdated != null)
                        {
                            onSettingsUpdated();
                        }
                    }
                }
            }
        }
    }

    private void OnEnable()
    {
        celestialBody = (CelestialBodyGenerator)target;
    }
}
