using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu()]
public class GravitySettings : ScriptableObject
{
    public Vector2 surfaceGravityRange = new Vector2(9.807f, 9.807f);
    public LayerMask affectedLayers;
    public bool autoOrient = true;
    public float autoOrientSpeed = 5;
    public float orbitAngle = 0;
}
