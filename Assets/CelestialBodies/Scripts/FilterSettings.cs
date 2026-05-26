using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class FilterSettings
{
    public enum FilterType
    {
        Simplex,
        Ridge,
        Perlin,
        Crater,
        Valley
    }
    public FilterType filterType;

    [ConditionalHide("filterType", 0)] public SimplexNoiseSettings simplexNoiseSettings;
    [ConditionalHide("filterType", 1)] public RidgeNoiseSettings ridgeNoiseSettings;
    [ConditionalHide("filterType", 2)] public PerlinNoiseSettings perlinNoiseSettings;
    [ConditionalHide("filterType", 3)] public CraterSettings craterSettings;
    [ConditionalHide("filterType", 4)] public ValleyNoiseSettings valleyNoiseSettings;

    public class BaseSettings
    {
        public Vector3 scale;
        public Vector3 seed;
        public float strength = 1;
    }

    [System.Serializable]
    public class SimplexNoiseSettings : BaseSettings
    {
        [Range(1, 8)]
        public int layers = 1;
        public float baseRoughness = 1;
        public float roughness = 2;
        public float persistence = 0.5f;
        public float minValue;
    }

    [System.Serializable]
    public class RidgeNoiseSettings : SimplexNoiseSettings
    {
        public float weightMultiplier = 1;
    }

    [System.Serializable]
    public class PerlinNoiseSettings : SimplexNoiseSettings
    {
        
    }

    [System.Serializable]
    public class CraterSettings : BaseSettings
    {
        public int craters;
        public float minCraterRadius = 0.01f;
        public float maxCraterRadius = 0.1f;
        [Range(0, 1)]
        public float craterFloor = 0;
        [Range(1, 16)]
        public float sizeDistribution = 1;
        public float ridgeHeight = 0.1f;
        public float ridgeWidth = 0.1f;
    }

    [System.Serializable]
    public class ValleyNoiseSettings : SimplexNoiseSettings
    {
        public float weightMultiplier = 1;
    }
}
