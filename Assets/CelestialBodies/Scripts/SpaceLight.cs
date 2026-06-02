using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SpaceStuff;
using System;

[RequireComponent(typeof(MeshRenderer), typeof(ScaledTransform), typeof(TransformChange))]
public class SpaceLight : MonoBehaviour
{
    private const double sigma = 5.67e-8;

    private ScaledTransform scaledTransform;
    private TransformChange transformChange;
    private Material materialClone;

    [SerializeField] private Light scaledLight;
    [SerializeField] private Light worldLight;
    [SerializeField] private float intensityMultiplier = 2f;
    [SerializeField] private Gradient temperatureGradient;
    [SerializeField] private float minTemperature = 2000f;
    [SerializeField] private float maxTemperature = 50000f;
    [SerializeField] private float cellTemperatureOffset = 1000f;

    private float temperature;
    private Color mainColor;
    private Color cellColor;
    private double luminosity;

    private void Awake()
    {
        scaledTransform = GetComponent<ScaledTransform>();
        transformChange = GetComponent<TransformChange>();
        materialClone = GetComponent<MeshRenderer>().material;

        temperature = UnityEngine.Random.Range(minTemperature, maxTemperature);
        float t = SpaceMath.Normalize(temperature, minTemperature, maxTemperature);
        mainColor = temperatureGradient.Evaluate(t);
        float cellTemperature = Mathf.Clamp(cellTemperatureOffset + temperature, minTemperature, maxTemperature);
        cellColor = temperatureGradient.Evaluate(SpaceMath.Normalize(cellTemperature, minTemperature, maxTemperature));
        scaledLight.color = mainColor;
        worldLight.color = mainColor;

        float intensity = t * intensityMultiplier;
        scaledLight.intensity = intensity;

        materialClone.SetColor("_MainColor", mainColor * intensity);
        materialClone.SetColor("_CellColor", cellColor * intensity);
        UpdateLight();
    }

    private void OnEnable()
    {
        transformChange.OnPositionChange += UpdateLight;
    }

    private void OnDisable()
    {
        transformChange.OnPositionChange -= UpdateLight;
    }

    private void OnDestroy()
    {
        Destroy(materialClone);
    }

    public void Init(float radius)
    {
        // L = 4piR^2sigmaT^4
        // f = L / (4piD^2)
        luminosity = 4 * Math.PI * radius * radius * sigma * temperature * temperature * temperature * temperature;
    }

    private void UpdateLight()
    {
        Vector3 direction = Camera.main.transform.position - transform.position;
        worldLight.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);

        // Inverse square law of light: f = L / (4piD^2)
        double sqrDistance = (Camera.main.transform.position.ToVector3d() - scaledTransform.realPosition).sqrMagnitude;
        double irradiance = luminosity / (4 * Math.PI * sqrDistance);
        worldLight.intensity = (float)Math.Log10(irradiance + 1);
        if (scaledTransform.inScaledSpace)
            worldLight.shadows = LightShadows.Soft;
        else
            worldLight.shadows = LightShadows.None;
    }
}
