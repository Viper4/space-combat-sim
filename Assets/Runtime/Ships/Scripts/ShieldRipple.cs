using System.Collections;
using UnityEngine;

public class ShieldRipple : MonoBehaviour
{
    private Material rippleMaterial;

    private Vector3 localPoint;
    private float duration = 0.5f;
    private float maxAlpha = 1.0f;
    private float minAlpha = 0.1f;
    private float maxRadius = 1.0f;
    private float minRadius = 0.1f;
    private float magnitude = 1.0f;

    float timer = 0;

    private void Start()
    {
        rippleMaterial = GetComponent<MeshRenderer>().material;
    }

    private void Update()
    {
        float t = timer / duration;
        rippleMaterial.SetVector("_Center", transform.TransformPoint(localPoint));
        rippleMaterial.SetFloat("_Alpha", Mathf.Lerp(maxAlpha, minAlpha, t));
        rippleMaterial.SetFloat("_Radius", Mathf.Lerp(maxRadius, minRadius, t) * magnitude);
        timer += Time.deltaTime;

        if (timer > duration)
        {
            Destroy(gameObject);
        }
    }

    public void Init(Vector3 point, float duration, float maxAlpha, float minAlpha, float maxRadius, float minRadius, float magnitude)
    {
        localPoint = transform.InverseTransformPoint(point);
        this.duration = duration;
        this.maxAlpha = maxAlpha;
        this.minAlpha = minAlpha;
        this.maxRadius = maxRadius;
        this.minRadius = minRadius;
        this.magnitude = magnitude;
    }
}
