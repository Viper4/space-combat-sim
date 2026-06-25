using System;
using SpaceStuff;
using UnityEngine;

[Serializable]
public struct ScaledRigidbodyState
{
    public double posX;
    public double posY;
    public double posZ;

    public float rotX;
    public float rotY;
    public float rotZ;
    public float rotW;

    public double velX;
    public double velY;
    public double velZ;

    public double angVelX;
    public double angVelY;
    public double angVelZ;

    public Vector3d Position => new Vector3d(posX, posY, posZ);
    public Quaternion Rotation => new Quaternion(rotX, rotY, rotZ, rotW);
    public Vector3d Velocity => new Vector3d(velX, velY, velZ);
    public Vector3d AngularVelocity => new Vector3d(angVelX, angVelY, angVelZ);

    public static ScaledRigidbodyState From(ScaledRigidbody scaledRigidbody)
    {
        return new ScaledRigidbodyState
        {
            posX = scaledRigidbody.scaledTransform.realPosition.x,
            posY = scaledRigidbody.scaledTransform.realPosition.y,
            posZ = scaledRigidbody.scaledTransform.realPosition.z,
            rotX = scaledRigidbody.transform.rotation.x,
            rotY = scaledRigidbody.transform.rotation.y,
            rotZ = scaledRigidbody.transform.rotation.z,
            rotW = scaledRigidbody.transform.rotation.w,
            velX = scaledRigidbody.velocity.x,
            velY = scaledRigidbody.velocity.y,
            velZ = scaledRigidbody.velocity.z,
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
