using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu()]
public class GenerationSettings : ScriptableObject
{
    public bool simple;
    public bool autoGenerate = true;
    public bool randomShapeGeneration = false;
    public bool sphere = true;
    public Vector3[] initialAngularVelocityRange = new Vector3[2];
    public enum BodyType
    {
        Planet,
        Star,
        BlackHole
    }
    public BodyType bodyType;
    [ConditionalHide("bodyType", 0)] public Vector3[] scaleRange = new Vector3[2];
    [ConditionalHide("bodyType", 0), Tooltip("In kg/m^3")] public Vector2 densityRange;

    public enum StarType
    {
        // Sub-stellar
        BrownDwarf,         // Never ignites hydrogen

        // Main sequence (luminosity class V) — ordered cool to hot
        RedDwarf,           // M-V
        OrangeDwarf,        // K-V
        YellowDwarf,        // G-V
        WhiteYellowDwarf,   // F-V
        WhiteDwarfMainSeq,  // A-V  (rename to avoid confusion with stellar remnant)
        BlueWhiteDwarf,     // B-V
        BlueDwarf,          // O-V — rarest, shortest-lived

        // Evolved giants
        RedGiant,           // Low/mid mass star leaving main sequence, swells and cools
        BlueGiant,          // High mass evolved star, still hot
        RedSupergiant,      // Most massive stars, expanded and cooled
        BlueSupergiant,     // Massive stars, hot and luminous

        // Exotic main sequence / near-main-sequence
        WolfRayet,          // Stripped-envelope, extremely hot, strong stellar winds
        CarbonStar,         // Cool giant with carbon-dominated atmosphere

        // Stellar remnants
        WhiteDwarf,         // Collapsed core of low/mid-mass star
        NeutronStar,        // Collapsed core of massive star
        Magnetar,           // Neutron star with extreme magnetic field
    }
    [System.Serializable]
    public struct StarTypeRule
    {
        public StarType starType;
        [Tooltip("Relative spawn weight — higher = more common")]
        public float weight;
        [Tooltip("Min mass in solar masses")]
        public float minMass;
        [Tooltip("Max mass in solar masses")]
        public float maxMass;
        [Tooltip("Min temperature in kelvin")]
        public float minTemperature;
        [Tooltip("Max temperature in kelvin")]
        public float maxTemperature;
        [Tooltip("If false, radius is computed from mass-luminosity + Stefan-Boltzmann (main sequence). If true, radius is sampled from min/maxRadius.")]
        public bool setRadius;
        [ConditionalHide("setRadius"), Tooltip("Min radius in solar radii")]
        public float minRadius;
        [ConditionalHide("setRadius"), Tooltip("Max radius in solar radii")]
        public float maxRadius;
    }
    [ConditionalHide("bodyType", 1)] public StarTypeRule[] starDistributions;
}