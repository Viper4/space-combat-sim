using System;
using SpaceStuff;
using UnityEngine;

[Serializable]
public struct ScaledObjectState
{
    public double posX;
    public double posY;
    public double posZ;

    public float rotX;
    public float rotY;
    public float rotZ;
    public float rotW;

    // Use floats wherever we can to save bytes
    public float velX;
    public float velY;
    public float velZ;

    public float angVelX;
    public float angVelY;
    public float angVelZ;

    public Vector3d Position => new Vector3d(posX, posY, posZ);
    public Quaternion Rotation => new Quaternion(rotX, rotY, rotZ, rotW);
    public Vector3d Velocity => new Vector3d(velX, velY, velZ);
    public Vector3 AngularVelocity => new Vector3(angVelX, angVelY, angVelZ);

    public static ScaledObjectState From(ScaledRigidbody scaledRigidbody)
    {
        return new ScaledObjectState
        {
            posX = scaledRigidbody.scaledTransform.realPosition.x,
            posY = scaledRigidbody.scaledTransform.realPosition.y,
            posZ = scaledRigidbody.scaledTransform.realPosition.z,
            rotX = scaledRigidbody.transform.rotation.x,
            rotY = scaledRigidbody.transform.rotation.y,
            rotZ = scaledRigidbody.transform.rotation.z,
            rotW = scaledRigidbody.transform.rotation.w,
            velX = (float)scaledRigidbody.velocity.x,
            velY = (float)scaledRigidbody.velocity.y,
            velZ = (float)scaledRigidbody.velocity.z,
            angVelX = scaledRigidbody.angularVelocity.x,
            angVelY = scaledRigidbody.angularVelocity.y,
            angVelZ = scaledRigidbody.angularVelocity.z,
        };
    }

    public void ApplyTo(ScaledRigidbody scaledRigidbody)
    {
        scaledRigidbody.scaledTransform.realPosition = Position;
        scaledRigidbody.transform.rotation = Rotation;
        scaledRigidbody.velocity = Velocity;
        scaledRigidbody.angularVelocity = AngularVelocity;
    }
}
