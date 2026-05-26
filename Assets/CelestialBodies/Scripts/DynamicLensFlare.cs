using SpaceStuff;
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(LensFlareComponentSRP))]
public class DynamicLensFlare : MonoBehaviour
{
    private LensFlareComponentSRP lensFlare;

    [SerializeField] private Light _light;
    [SerializeField] private ScaledTransform lightScaledTransform;
    [SerializeField] private LayerMask occlusionLayers;
    [SerializeField, Tooltip("Total number of linecasts to average over.")] private int numSamples = 4;
    [SerializeField, Tooltip("Linecasts per frame.")] private int samplesPerFrame = 1;
    [SerializeField, Tooltip("Splits intensity into this many buckets to smooth out noise.")] private int buckets = 4;
    [SerializeField] private Vector2 brightnessRange;
    [SerializeField] private Vector2 scaleRange;
    [SerializeField] private Vector2 distanceRange;
    [SerializeField] private float temperatureScale;

    private int[] samples;
    private int sampleIndex = 0;
    private int bucketSize;

    private void Start()
    {
        lensFlare = GetComponent<LensFlareComponentSRP>();
        samples = new int[numSamples];
        bucketSize = numSamples / buckets;
    }

    private void FixedUpdate()
    {
        Vector3d realCamPosition = FloatingWorldOrigin.Instance.worldOriginPosition + Camera.main.transform.position.ToVector3d();
        double sqrDistance = (lightScaledTransform.realPosition - realCamPosition).sqrMagnitude;
        if (sqrDistance > distanceRange.y * distanceRange.y || sqrDistance < distanceRange.x * distanceRange.x)
        {
            lensFlare.enabled = false;
            return;
        }

        for (int i = 0; i < samplesPerFrame; i++)
        {
            Vector3 randomEdge = lightScaledTransform.transform.position + lightScaledTransform.transform.localScale.x * 0.5f * Random.onUnitSphere;
            Vector3 direction = randomEdge - Camera.main.transform.position;
            Ray ray = new Ray(Camera.main.transform.position, direction);
            if (Physics.Raycast(ray, out RaycastHit hit, lightScaledTransform.scaledSpaceThreshold * 2f, occlusionLayers, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.gameObject.layer == lightScaledTransform.scaledSpaceLayer)
                {
                    double otherSqrDistance = (hit.transform.GetComponent<ScaledTransform>().realPosition - realCamPosition).sqrMagnitude;
                    if (otherSqrDistance < sqrDistance) // Object should be in front of this light relative to camera
                    {
                        samples[sampleIndex] = 1;
                    }
                    else
                    {
                        samples[sampleIndex] = 0;
                    }
                }
                else
                {
                    samples[sampleIndex] = 1;
                    Debug.DrawRay(ray.origin, ray.direction * 9999f, Color.red, Time.fixedDeltaTime);
                }
            }
            else
            {
                samples[sampleIndex] = 0;
                Debug.DrawRay(ray.origin, ray.direction * 9999f, Color.green, Time.fixedDeltaTime);
            }
            sampleIndex = (sampleIndex + 1) % numSamples;
        }

        int numOccluded = 0;
        for (int i = 0; i < numSamples; i++)
        {
            numOccluded += samples[i];
        }

        if (numOccluded >= numSamples)
        {
            lensFlare.enabled = false;
        }
        else
        {
            int bucket = 0;
            for (int i = 1; i < buckets; i++)
            {
                if (numOccluded < (i + 1) * bucketSize)
                {
                    bucket = i;
                    break;
                }
            }
            float fraction = 1f - ((float)bucket / (buckets - 1));

            lensFlare.enabled = true;
            // t is nonlinear but still smooth
            float t = (float)((sqrDistance - distanceRange.x * distanceRange.x) / (distanceRange.y * distanceRange.y - distanceRange.x * distanceRange.x));
            lensFlare.intensity = Mathf.Lerp(brightnessRange.x, brightnessRange.y, t) * _light.colorTemperature * temperatureScale * fraction;
            lensFlare.scale = Mathf.Lerp(scaleRange.x, scaleRange.y, t);
        }
    }
}
