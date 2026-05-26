using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class RadarIcon : MonoBehaviour
{
    [SerializeField] private LineRenderer lineRenderer;
    [SerializeField] private float newLinePosThreshold;

    public Transform model;
    private MeshRenderer modelMeshRenderer;
    [SerializeField] private TextMeshPro text3D;

    [SerializeField] private float killTime = 0.5f;
    private float killTimer;

    private Color baseColor;

    void LateUpdate()
    {
        killTimer -= Time.deltaTime;
        if (killTimer <= 0)
        {
            Destroy(gameObject);
        }
        text3D.transform.rotation = Quaternion.LookRotation(text3D.transform.position - Camera.main.transform.position, Camera.main.transform.up);
    }

    public void Init(Vector3 position, Quaternion rotation, Color baseColor, Color emission, string text)
    {
        this.baseColor = baseColor;
        modelMeshRenderer = model.GetComponent<MeshRenderer>();
        Material clonedMaterial = Instantiate(modelMeshRenderer.sharedMaterial);
        clonedMaterial.color = baseColor;
        clonedMaterial.SetColor("_EmissionColor", emission);
        modelMeshRenderer.sharedMaterial = clonedMaterial;
        if (lineRenderer != null)
        {
            lineRenderer.sharedMaterial = clonedMaterial;
        }

        killTimer = killTime;
        lineRenderer.positionCount = 2;
        UpdateIcon(position, rotation, text);
    }

    public void UpdateIcon(Vector3 position, Quaternion rotation, string text)
    {
        killTimer = killTime;

        transform.position = position;
        model.rotation = rotation;
        if (lineRenderer != null)
        {
            lineRenderer.SetPosition(0, new Vector3(0, -transform.localPosition.y, 0));
            lineRenderer.SetPosition(1, Vector3.zero);
            /*float sqrMagnitude = linePositions.Count > 0 ? (linePositions[^1] - transform.localPosition).sqrMagnitude : 999f;
            if (sqrMagnitude > sqrNewLinePosThreshold)
            {
                linePositions.Add(transform.localPosition);
                if (linePositions.Count > maxLinePositions)
                {
                    linePositions.RemoveAt(0);
                }
                lineRenderer.positionCount = linePositions.Count + 1;
            }
            for (int i = 0; i < linePositions.Count; i++)
            {
                lineRenderer.SetPosition(i, linePositions[i] - transform.localPosition);
            }
            lineRenderer.SetPosition(linePositions.Count, Vector3.zero);*/
        }

        if (text3D != null)
        {
            text3D.text = text;
            text3D.color = baseColor;
        }
    }

    public void UpdateIcon(Vector3 position, string text)
    {
        killTimer = killTime;

        transform.position = position;
        if (lineRenderer != null)
        {
            lineRenderer.SetPosition(0, new Vector3(0, -transform.localPosition.y, 0));
            lineRenderer.SetPosition(1, Vector3.zero);
            /*
            // Move vertices back by displacement
            for (int i = 0; i < linePositions.Count; i++)
            {
                linePositions[i] += displacement;
            }
            float sqrMagnitude = linePositions.Count > 0 ? (linePositions[^1] - transform.localPosition).sqrMagnitude : 999f;
            if (sqrMagnitude > sqrNewLinePosThreshold)
            {
                linePositions.Add(transform.localPosition);
                if (linePositions.Count > maxLinePositions)
                {
                    linePositions.RemoveAt(0);
                }
                lineRenderer.positionCount = linePositions.Count + 1;
            }
            for (int i = 0; i < linePositions.Count; i++)
            {
                lineRenderer.SetPosition(i, linePositions[i] - transform.localPosition);
            }
            lineRenderer.SetPosition(linePositions.Count, Vector3.zero);*/
        }

        if (text3D != null)
        {
            text3D.text = text;
            text3D.color = baseColor;
        }
    }

    public Color GetColor()
    {
        return modelMeshRenderer.sharedMaterial.color;
    }

    public Color GetEmission()
    {
        return modelMeshRenderer.sharedMaterial.GetColor("_EmissionColor");
    }

    public void SetColor(Color mainColor, Color emissionColor)
    {
        modelMeshRenderer.sharedMaterial.color = mainColor;
        modelMeshRenderer.sharedMaterial.SetColor("_EmissionColor", emissionColor);
        if (lineRenderer != null)
        {
            lineRenderer.sharedMaterial.color = mainColor;
            lineRenderer.sharedMaterial.SetColor("_EmissionColor", emissionColor);
        }
    }
}
