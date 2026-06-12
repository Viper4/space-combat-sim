using System;
using SpaceStuff;
using UnityEngine;

[Serializable]
public struct DoubleRigidbodyState
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

    public static DoubleRigidbodyState From(DoubleRigidbody doubleRigidbody)
    {
        return new DoubleRigidbodyState
        {
            posX = doubleRigidbody.scaledTransform.realPosition.x,
            posY = doubleRigidbody.scaledTransform.realPosition.y,
            posZ = doubleRigidbody.scaledTransform.realPosition.z,
            rotX = doubleRigidbody.transform.rotation.x,
            rotY = doubleRigidbody.transform.rotation.y,
            rotZ = doubleRigidbody.transform.rotation.z,
            rotW = doubleRigidbody.transform.rotation.w,
            velX = doubleRigidbody.velocity.x,
            velY = doubleRigidbody.velocity.y,
            velZ = doubleRigidbody.velocity.z,
            angVelX = doubleRigidbody.angularVelocity.x,
            angVelY = doubleRigidbody.angularVelocity.y,
            angVelZ = doubleRigidbody.angularVelocity.z,
        };
    }

    public void ApplyTo(DoubleRigidbody doubleRigidbody)
    {
        doubleRigidbody.scaledTransform.realPosition = Position;
        doubleRigidbody.transform.rotation = Rotation;
        doubleRigidbody.velocity = Velocity;
        doubleRigidbody.angularVelocity = AngularVelocity;
    }
}
