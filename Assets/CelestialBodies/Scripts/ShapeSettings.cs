using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu()]
public class ShapeSettings : ScriptableObject
{
    public const int numSupportedLODS = 24;
    public static int simpleMeshColliderSize = 6;

    public float radius = 1;
    [Range(2, 256)]
    public int meshFilterResolution = 8;
    [Range(1, numSupportedLODS)]
    public int levelOfDetail = 1;
    public bool complexMeshCollider = true;
    [ConditionalHide("complexMeshCollider"), Range(2, 256)]
    public int meshColliderResolution = 8;
    //public float radius = 1;
    public FilterLayer[] filterLayers;

    [System.Serializable]
    public class FilterLayer
    {
        public bool enabled = true;
        public bool useFirstLayerAsMask;
        public bool applyScale;
        public FilterSettings filterSettings;
    }
}
