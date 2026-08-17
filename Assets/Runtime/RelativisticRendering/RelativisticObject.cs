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

        foreach (Renderer renderer in scaledRigidbody.scaledTransform.trackedRenderers)
        {
            renderer.GetPropertyBlock(block);
            block.SetVector(ScaledSpaceVisuals.SourceVelocityID, scaledRigidbody.velocity.ToVector3());
            block.SetVector(ScaledSpaceVisuals.ObserverVelocityID, FloatingWorldOrigin.Instance.scaledRigidbody.velocity.ToVector3());
            renderer.SetPropertyBlock(block);
        }
    }
}