using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshRenderer))]
public class Atmosphere : MonoBehaviour
{
    MeshRenderer meshRenderer;
    [SerializeField] Material sourceMaterial;
    Material material;

    [SerializeField] float radius;

    public Transform lightSource;

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
        material.SetVector("_LightDirection", (transform.position - lightSource.position).normalized);
        material.SetVector("_PlanetPosition", transform.position);
        material.SetFloat("_PlanetRadius", radius - 2);
        material.SetFloat("_AtmosphereRadius", radius);

        Vector3 worldScale = new Vector3(radius * 2, radius * 2, radius * 2);
        if (transform.parent != null)
        {
            transform.localScale = transform.parent.InverseTransformVector(worldScale);
        }
        else
        {
            transform.localScale = worldScale;
        }
    }
}
