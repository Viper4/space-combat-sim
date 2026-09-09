using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SpaceStuff;
using System;

[RequireComponent(typeof(MeshRenderer), typeof(ScaledTransform))]
public class SpaceLight : MonoBehaviour
{
    public ScaledTransform scaledTransform {get; private set;}
    private ScaledRigidbody scaledRigidbody;
    private Material materialClone;

    [SerializeField] private Light worldLight;
    [SerializeField] private float intensityMultiplier = 2f;
    [SerializeField] private Gradient temperatureGradient;
    [SerializeField] private float gradientMinTemperature = 1000f;
    [SerializeField] private float gradientMaxTemperature = 20000f;
    [SerializeField] private float cellTemperatureOffset = 1000f;
    [SerializeField] private bool relativisticEffects;

    public float intensity;
    public float temperature;
    public Color baseColor {get; private set;}
    private Color cellColor;
    private double radius;
    private double luminosity;
    private bool initialized = false;
    private float redStd;
    private float greenStd;
    private float blueStd;
    private float violetStd;
    private float beamingFactor;

    private void Awake()
    {
        Init();
    }

    public void Init()
    {
        if (initialized)
            return;
        initialized = true;
        scaledTransform = GetComponent<ScaledTransform>();
        TryGetComponent(out scaledRigidbody);
        materialClone = GetComponent<MeshRenderer>().material;
        if (relativisticEffects)
        {
            redStd = materialClone.GetFloat("_RedStd");
            greenStd = materialClone.GetFloat("_GreenStd");
            blueStd = materialClone.GetFloat("_BlueStd");
            violetStd = materialClone.GetFloat("_VioletStd");
            beamingFactor = materialClone.GetFloat("_BeamingFactor");
        }
    }

    public void SetTemperature(float temperature, Color tint)
    {
        Init();
        this.temperature = temperature;
        float clampedTemp = Mathf.Clamp(temperature, gradientMinTemperature, gradientMaxTemperature);
        float t = Mathf.InverseLerp(gradientMinTemperature, gradientMaxTemperature, clampedTemp);
        baseColor = temperatureGradient.Evaluate(t) * tint;
        float cellTemperature = Mathf.Clamp(cellTemperatureOffset + temperature, gradientMinTemperature, gradientMaxTemperature);
        cellColor = temperatureGradient.Evaluate(Mathf.InverseLerp(gradientMinTemperature, gradientMaxTemperature, cellTemperature));
        worldLight.color = baseColor;
        radius = Math.Max(Math.Max(scaledTransform.realScale.x, scaledTransform.realScale.y), scaledTransform.realScale.z);
        luminosity = 4.0 * Math.PI * radius * radius * SpaceMath.stefanBoltzmann * temperature * temperature * temperature * temperature;

        radius = Math.Max(Math.Max(scaledTransform.realScale.x, scaledTransform.realScale.y), scaledTransform.realScale.z);

        double relativeLuminosity = Math.Pow(radius, 2.0) * Math.Pow(temperature / 5778.0, 4.0);

        float colorIntensity = (float)Math.Log10(relativeLuminosity + 1.0) * intensityMultiplier;

        materialClone.SetColor("_BaseColor", baseColor * colorIntensity);
        materialClone.SetColor("_CellColor", cellColor * colorIntensity);
        UpdateLight();
    }

    private void OnDestroy()
    {
        Destroy(materialClone);
    }

    private void FixedUpdate()
    {
        UpdateLight();
    }

    private void UpdateLight()
    {
        if (Camera.main == null || FloatingWorldOrigin.Instance == null)
            return;
        
        Vector3d realCamPosition = FloatingWorldOrigin.Instance.GetRealCameraPosition();
        Vector3d delta = realCamPosition - scaledTransform.realPosition;
        Vector3 direction = delta.normalized.ToVector3();
        if (direction.sqrMagnitude > 0.0001)
        {
            worldLight.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
        }

        // Inverse square law of light: f = L / (4piD^2)
        double sqrDistance = delta.sqrMagnitude;
        double irradiance = luminosity / (4 * Math.PI * sqrDistance);
        intensity = (float)Math.Log10(irradiance + 1);
        if (intensity < float.Epsilon)
        {
            if (worldLight.enabled)
                worldLight.enabled = false;
        }
        else
        {
            if (!worldLight.enabled)
                worldLight.enabled = true;
            worldLight.intensity = intensity;
        }
        if (relativisticEffects)
        {
            Vector3d relativeVelocity = scaledRigidbody.velocity - FloatingWorldOrigin.Instance.scaledRigidbody.velocity;
            Vector3d toStar = FloatingWorldOrigin.Instance.scaledTransform.realPosition - scaledTransform.realPosition;
            worldLight.color = GetDopplerColor(baseColor, relativeVelocity, toStar.normalized);
        }
    }

    private float VisibilityWindow(float wavelength)
    {
        // Visible spectrum is from 380nm to 740nm
        const float MIN_WAVELENGTH = 380.0f;
        const float MAX_WAVELENGTH = 740.0f;
        const float VISIBLE_SPECTRUM_EXTRA = 20.0f; // Go a little past the boundaries for a better fade to black+alpha fade
        const float BUFFER = 50.0f;

        float lowRamp = Mathf.SmoothStep(
            MIN_WAVELENGTH - VISIBLE_SPECTRUM_EXTRA,
            MIN_WAVELENGTH + BUFFER,
            wavelength);

        float highRamp = 1f - Mathf.SmoothStep(
            MAX_WAVELENGTH - BUFFER,
            MAX_WAVELENGTH + VISIBLE_SPECTRUM_EXTRA,
            wavelength);

        return Mathf.Clamp01(lowRamp * highRamp);
    }

    private static float Gaussian(float wavelength, float center, float std)
    {
        float x = (wavelength - center) / Mathf.Max(std, 0.001f);
        return Mathf.Exp(-0.5f * x * x);
    }

    private Color GetDopplerColor(Color color, Vector3d relativeVelocity, Vector3d viewDir)
    {
        const float RED_WAVELENGTH   = 650.0f;
        const float GREEN_WAVELENGTH = 540.0f;
        const float BLUE_WAVELENGTH  = 475.0f;
        const float VIOLET_WAVELENGTH = 380.0f;

        double relativeSpeedSquared = relativeVelocity.sqrMagnitude;

        double relativeBetaSquared = Math.Min(relativeSpeedSquared / (ScaledSpacePhysics.speedOfLight * ScaledSpacePhysics.speedOfLight), 1.0 - 1e-6);

        double relativeBeta = Math.Sqrt(relativeBetaSquared);
        double relativeSpeed = Math.Sqrt(relativeSpeedSquared);

        Vector3d relativeVelocityDir = relativeVelocity / Math.Max(relativeSpeed, 1e-6);

        double cosTheta = Vector3d.Dot(relativeVelocityDir, -viewDir);

        // Same formula as the HLSL.
        double invDopplerFactor = Math.Abs(
            Math.Sqrt(1.0 - relativeBetaSquared) /
            (1.0 - relativeBeta * cosTheta));

        float brightness = Mathf.Pow(
            (float)(1.0 / invDopplerFactor),
            beamingFactor);

        Vector3 rgb = new Vector3(
            Mathf.Max(color.r, 0f),
            Mathf.Max(color.g, 0f),
            Mathf.Max(color.b, 0f));

        float shiftedR = RED_WAVELENGTH * (float)invDopplerFactor;
        float shiftedG = GREEN_WAVELENGTH * (float)invDopplerFactor;
        float shiftedB = BLUE_WAVELENGTH * (float)invDopplerFactor;

        float visR = VisibilityWindow(shiftedR);
        float visG = VisibilityWindow(shiftedG);
        float visB = VisibilityWindow(shiftedB);

        float visibility = Mathf.Max(visR, Mathf.Max(visG, visB));

        float r =
            Gaussian(shiftedR, RED_WAVELENGTH, redStd) * rgb.x +
            Gaussian(shiftedG, RED_WAVELENGTH, redStd) * rgb.y +
            Gaussian(shiftedB, RED_WAVELENGTH, redStd) * rgb.z;

        float g =
            Gaussian(shiftedR, GREEN_WAVELENGTH, greenStd) * rgb.x +
            Gaussian(shiftedG, GREEN_WAVELENGTH, greenStd) * rgb.y +
            Gaussian(shiftedB, GREEN_WAVELENGTH, greenStd) * rgb.z;

        float b =
            Gaussian(shiftedR, BLUE_WAVELENGTH, blueStd) * rgb.x +
            Gaussian(shiftedG, BLUE_WAVELENGTH, blueStd) * rgb.y +
            Gaussian(shiftedB, BLUE_WAVELENGTH, blueStd) * rgb.z;

        // Violet contribution.
        float violet =
            Gaussian(shiftedR, VIOLET_WAVELENGTH, violetStd) * rgb.x;

        r += violet * 0.2f;
        b += violet * 1.0f;

        return new Color(
            Mathf.Max(r, 0f) * brightness,
            Mathf.Max(g, 0f) * brightness,
            Mathf.Max(b, 0f) * brightness,
            color.a * visibility
        );
    }
}
