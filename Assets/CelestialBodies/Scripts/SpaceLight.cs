using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SpaceStuff;
using System;

[RequireComponent(typeof(MeshRenderer), typeof(ScaledTransform))]
public class SpaceLight : MonoBehaviour
{
    public ScaledTransform scaledTransform {get; private set;}
    private Material materialClone;

    [SerializeField] private Light worldLight;
    [SerializeField] private float intensityMultiplier = 2f;
    [SerializeField] private Gradient temperatureGradient;
    [SerializeField] private float gradientMinTemperature = 1000f;
    [SerializeField] private float gradientMaxTemperature = 20000f;
    [SerializeField] private float cellTemperatureOffset = 1000f;

    public Color mainColor {get; private set;}
    private Color cellColor;
    private double radius;
    private double luminosity;

    private bool initialized = false;

    public float intensity;

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
        materialClone = GetComponent<MeshRenderer>().material;
    }

    public void SetTemperature(float temperature, Color tint)
    {
        Init();
        float clampedTemp = Mathf.Clamp(temperature, gradientMinTemperature, gradientMaxTemperature);
        float t = Mathf.InverseLerp(gradientMinTemperature, gradientMaxTemperature, clampedTemp);
        mainColor = temperatureGradient.Evaluate(t) * tint;
        float cellTemperature = Mathf.Clamp(cellTemperatureOffset + temperature, gradientMinTemperature, gradientMaxTemperature);
        cellColor = temperatureGradient.Evaluate(Mathf.InverseLerp(gradientMinTemperature, gradientMaxTemperature, cellTemperature));
        worldLight.color = mainColor;
        radius = Math.Max(Math.Max(scaledTransform.realScale.x, scaledTransform.realScale.y), scaledTransform.realScale.z);
        luminosity = 4.0 * Math.PI * radius * radius * SpaceMath.stefanBoltzmann * temperature * temperature * temperature * temperature;

        radius = Math.Max(Math.Max(scaledTransform.realScale.x, scaledTransform.realScale.y), scaledTransform.realScale.z);

        double relativeLuminosity = Math.Pow(radius, 2.0) * Math.Pow(temperature / 5778.0, 4.0);

        float colorIntensity = (float)Math.Log10(relativeLuminosity + 1.0) * intensityMultiplier;

        materialClone.SetColor("_MainColor", mainColor * colorIntensity);
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
            
        Vector3 direction = Camera.main.transform.position - transform.position;
        worldLight.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);

        // Inverse square law of light: f = L / (4piD^2)
        Vector3d realCameraPos = FloatingWorldOrigin.Instance.GetRealCameraPosition();
        double sqrDistance = (realCameraPos - scaledTransform.realPosition).sqrMagnitude;
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
    }
}
