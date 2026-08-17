using UnityEngine;
using SpaceStuff;
using System;

[RequireComponent(typeof(ScaledRigidbody))]
public class RelativisticObject : MonoBehaviour
{
    private ScaledRigidbody scaledRigidbody;
    private MaterialPropertyBlock block;

    private void Start()
    {
        scaledRigidbody = GetComponent<ScaledRigidbody>();
        block = new MaterialPropertyBlock();
    }

    private void FixedUpdate()
    {
        if (FloatingWorldOrigin.Instance == null || Camera.main == null || !scaledRigidbody.scaledTransform.visible)
            return;

        Vector3 relativeVelocity = (scaledRigidbody.velocity - FloatingWorldOrigin.Instance.scaledRigidbody.velocity).ToVector3();
        // relativeVelocity of 0 causes issues with shader
        if (relativeVelocity.sqrMagnitude < 0.000001f)
        {
            relativeVelocity = new Vector3(0f, 0f, 0.1f);
        }
        // float distance = (float)(scaledRigidbody.scaledTransform.realPosition - FloatingWorldOrigin.Instance.scaledRigidbody.scaledTransform.realPosition).magnitude;

        foreach (Renderer renderer in scaledRigidbody.scaledTransform.trackedRenderers)
        {
            renderer.GetPropertyBlock(block);
            block.SetVector(ScaledSpaceVisuals.RelativeVelocityID, relativeVelocity);
            renderer.SetPropertyBlock(block);
        }
    }
}