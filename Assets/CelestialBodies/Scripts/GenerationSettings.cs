using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu()]
public class GenerationSettings : ScriptableObject
{
    public bool simple;
    public bool autoGenerate = true;
    public bool sphere = true;
    public bool random = true;
    [ConditionalHide("random")] public Vector3[] scaleRange = new Vector3[2];
    public bool calculateMass = true;
    [ConditionalHide("calculateMass")] public float density = 50; // In kg / m^3
    public Vector3[] initialAngularVelocityRange = new Vector3[2];
}
