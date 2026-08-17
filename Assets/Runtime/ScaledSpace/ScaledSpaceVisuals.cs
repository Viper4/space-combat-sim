using UnityEngine;
using SpaceStuff;
using System;
using System.Collections.Generic;
using System.Collections;

public class ScaledSpaceVisuals : MonoBehaviour
{
    public static readonly string ObserverVelocityID = "_ObserverVelocity";
    public static readonly string SourceVelocityID = "_SourceVelocity";
    public static readonly string SpeedOfLightID = "_SpeedOfLight";
    public static readonly string BaseColorID = "_BaseColor";

    public static ScaledSpaceVisuals Instance;

    private List<ScaledTransform> scaledTransforms = new List<ScaledTransform>();
    private float lastUpdateTime;
    private bool updating = false;

    [SerializeField] private bool dynamicScaleFactor;
    [SerializeField, ConditionalHide("dynamicScaleFactor"), Tooltip("Minimum distance from camera in render space (transform).")]
    private double minRenderDistance = 0.1;
    [SerializeField, ConditionalHide("dynamicScaleFactor"), Tooltip("Maximum distance from camera in render space (transform).")]
    private double maxRenderDistance = 1500.0;
    [SerializeField] private float pollTime;
    [SerializeField] private bool nonlinearRemap = true;
    [SerializeField, ConditionalHide("nonlinearRemap"), Tooltip("k=1 logarithmic remapping, k>1 spreads nearby objects and bunches far ones, k<1 spreads far objects and bunches nearby ones")] private double power = 1.0;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        UpdateScaleFactors();
    }

    private void OnDestroy()
    {
        StopAllCoroutines();
    }

    private void Update()
    {
        if (Time.time - lastUpdateTime > pollTime)
        {
            UpdateScaleFactors();
        }
    }

    private void FixedUpdate()
    {
        if (FloatingWorldOrigin.Instance == null || RenderSettings.skybox == null)
            return;
        RenderSettings.skybox.SetVector(ObserverVelocityID, FloatingWorldOrigin.Instance.scaledRigidbody.velocity.ToVector3());
    }

    public void RegisterScaledTransform(ScaledTransform scaledTransform)
    {
        scaledTransform.index = scaledTransforms.Count;
        scaledTransforms.Add(scaledTransform);
    }

    public void UnregisterScaledTransform(ScaledTransform scaledTransform)
    {
        int index = scaledTransform.index;
        if (index < 0 || index > scaledTransforms.Count-1)
            return;
        int lastIndex = scaledTransforms.Count - 1;
        ScaledTransform last = scaledTransforms[lastIndex];

        // Replace with scaledTransform at end of list for fast removal and maintain indices
        scaledTransforms[index] = last;
        last.index = index;
        scaledTransforms.RemoveAt(lastIndex);
    }

    private IEnumerator UpdateScaleFactorsRoutine()
    {
        updating = true;
        while (FloatingWorldOrigin.Instance == null || Camera.main == null)
        {
            yield return new WaitForFixedUpdate();
        }

        lastUpdateTime = Time.time;

        // Find min and max real distance
        double minSqrDistance = double.MaxValue;
        double maxSqrDistance = -1.0;
        Vector3d renderCamPos = Camera.main.transform.position.ToVector3d();
        Vector3d realCamPos = FloatingWorldOrigin.Instance.scaledTransform.realPosition + renderCamPos;
        foreach(ScaledTransform scaledTransform in scaledTransforms)
        {
            if (!scaledTransform.visible || !scaledTransform.inScaledSpace)
                continue;

            Vector3d offset = scaledTransform.realPosition - realCamPos; // Unscaled offset from camera to object
            double sqrDistance = offset.sqrMagnitude;
            
            if (sqrDistance < minSqrDistance)
            {
                minSqrDistance = sqrDistance;
            }
            if (sqrDistance > maxSqrDistance)
            {
                maxSqrDistance = sqrDistance;
            }
        }

        if (maxSqrDistance < 0.0)
            yield break;

        // First check if all scaled transforms would fit within render bounds at one global scale factor
        double globalScaleFactor = maxSqrDistance / maxRenderDistance;
        double testMinRenderDistance = minSqrDistance / globalScaleFactor;
        if (testMinRenderDistance >= minRenderDistance) // We are within bounds
        {
            foreach(ScaledTransform scaledTransform in scaledTransforms)
            {
                scaledTransform.scaleFactor = globalScaleFactor;
            }
            yield break;
        }

        // Calculate scale factors individually with min/max remap to preserve visual order
        if (nonlinearRemap)
        {
            double logMin = Math.Log(Math.Sqrt(minSqrDistance));
            double logMax = Math.Log(Math.Sqrt(maxSqrDistance));
            foreach(ScaledTransform scaledTransform in scaledTransforms)
            {
                if (!scaledTransform.visible || !scaledTransform.inScaledSpace)
                    continue;
                double distance = (scaledTransform.realPosition - realCamPos).magnitude;
                // y = minRenderDistance+(max-min)((log(x)-log(minReal))/(log(maxReal)-log(minReal)))^k
                double t = Math.Pow((Math.Log(distance) - logMin) / (logMax - logMin), power);
                double renderDistance = minRenderDistance + (maxRenderDistance - minRenderDistance) * t;

                scaledTransform.scaleFactor = distance / renderDistance;
            }
        }
        else
        {
            foreach(ScaledTransform scaledTransform in scaledTransforms)
            {
                if (!scaledTransform.visible || !scaledTransform.inScaledSpace)
                    continue;
                double distance = (scaledTransform.realPosition - realCamPos).magnitude;
                double t = (distance - minSqrDistance) / (maxSqrDistance - minSqrDistance);
                double renderDistance = minRenderDistance + (maxRenderDistance - minRenderDistance) * t;
                scaledTransform.scaleFactor = distance / renderDistance;
            }
        }
        updating = false;
        Debug.Log(GameLog.ObjectLog(this, "Updated scale factors"));
    }

    public void UpdateScaleFactors()
    {
        if (updating)
            return;

        StartCoroutine(UpdateScaleFactorsRoutine());
    }
}
