using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/* TODO:
 * Add option to apply coloring based on random noise instead of elevation and/or biome
 */
[CreateAssetMenu()]
public class ColorSettings : ScriptableObject
{
    public Material material;
    //public Gradient randomColorGradient;
    //public FilterSettings randomColorFilter;
    public BiomeColorSettings biomeColorSettings;

    [System.Serializable]
    public class BiomeColorSettings
    {
        public Biome[] biomes;
        public FilterSettings filter;
        public float offset;
        public float strength;
        [Range(0, 1)] public float blend;

        [System.Serializable]
        public class Biome
        {
            public Gradient gradient;
            public Color tint;
            public bool colorWithMinMax = true; 
            [ConditionalHide("colorWithMinMax"), Range(0, 1)] public float startHeight;
            [Range(0, 1)] public float tintPercent;
            public float weightMultiplier = 1;
        }
    }
}
