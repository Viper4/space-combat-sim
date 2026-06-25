using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu()]
public class GravitySettings : ScriptableObject
{
    public bool applyGravity = false;
    public LayerMask affectedLayers;
    public bool autoOrient = true;
    public float autoOrientSpeed = 5;
    public float orbitAngle = 0;
}
