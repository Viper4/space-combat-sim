using System.Collections;
using System.Collections.Generic;
using SpaceStuff;
using UnityEngine;

[RequireComponent(typeof(MeshRenderer))]
public class Atmosphere : MonoBehaviour
{
    [SerializeField] private ScaledTransform planet;
    [SerializeField] private SpaceLight lightSource;
    private MeshRenderer meshRenderer;
    [SerializeField] private Material sourceMaterial;
    private Material material;

    private void OnValidate()
    {
        if(lightSource != null)
        {
            Init();
            UpdateMaterial();
        }
    }

    void Start()
    {
        Init();
    }

    void Update()
    {
        UpdateMaterial();
    }

    private void Init()
    {
        if (meshRenderer == null)
            meshRenderer = GetComponent<MeshRenderer>();
        if (material == null)
            material = Instantiate(sourceMaterial);
        meshRenderer.sharedMaterial = material;
    }

    private void UpdateMaterial()
    {
        if (planet == null || lightSource == null || lightSource.scaledTransform == null)
            return;
        float radius = Mathf.Max(planet.transform.localScale.x, planet.transform.localScale.y, planet.transform.localScale.z);
        material.SetVector("_LightDirection", (planet.realPosition - lightSource.scaledTransform.realPosition).normalized.ToVector3());
        material.SetColor("_LightColor", lightSource.mainColor);
        material.SetVector("_PlanetPosition", planet.transform.position);
        material.SetFloat("_PlanetRadius", radius);
        material.SetFloat("_AtmosphereRadius", radius + 2);
    }
}
