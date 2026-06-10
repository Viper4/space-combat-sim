using UnityEngine;
using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using UnityEngine.Internal;
using System.Runtime.InteropServices;

namespace SpaceStuff
{
    //
    // Summary:
    //     Representation of 3D vectors and points in double precision.
    [Serializable]
    public struct Vector3d : IEquatable<Vector3d>, IFormattable
    {
        //public const double kEpsilon = 1E-05;

        //public const double kEpsilonNormalSqrt = 1E-15;

        //
        // Summary:
        //     X component of the vector.
        public double x;

        //
        // Summary:
        //     Y component of the vector.
        public double y;

        //
        // Summary:
        //     Z component of the vector.
        public double z;

        private static readonly Vector3d zeroVector = new Vector3d(0, 0, 0);

        private static readonly Vector3d oneVector = new Vector3d(1, 1, 1);

        private static readonly Vector3d upVector = new Vector3d(0, 1, 0);

        private static readonly Vector3d downVector = new Vector3d(0, -1, 0);

        private static readonly Vector3d leftVector = new Vector3d(-1, 0, 0);

        private static readonly Vector3d rightVector = new Vector3d(1, 0, 0);

        private static readonly Vector3d forwardVector = new Vector3d(0, 0, 1);

        private static readonly Vector3d backVector = new Vector3d(0, 0, -1);

        private static readonly Vector3d positiveInfinityVector = new Vector3d(double.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);

        private static readonly Vector3d negativeInfinityVector = new Vector3d(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

        public double this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return index switch
                {
                    0 => x,
                    1 => y,
                    2 => z,
                    _ => throw new IndexOutOfRangeException("Invalid Vector3d index!"),
                };
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                switch (index)
                {
                    case 0:
                        x = value;
                        break;
                    case 1:
                        y = value;
                        break;
                    case 2:
                        z = value;
                        break;
                    default:
                        throw new IndexOutOfRangeException("Invalid Vector3d index!");
                }
            }
        }

        //
        // Summary:
        //     Returns this vector with a magnitude of 1 (Read Only).
        public Vector3d normalized
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return Normalize(this);
            }
        }

        //
        // Summary:
        //     Returns the length of this vector (Read Only).
        public double magnitude
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return Math.Sqrt(x * x + y * y + z * z);
            }
        }

        //
        // Summary:
        //     Returns the squared length of this vector (Read Only).
        public double sqrMagnitude
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return x * x + y * y + z * z;
            }
        }

        //
        // Summary:
        //     Shorthand for writing Vector3d(0, 0, 0).
        public static Vector3d zero
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return zeroVector;
            }
        }

        //
        // Summary:
        //     Shorthand for writing Vector3d(1, 1, 1).
        public static Vector3d one
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return oneVector;
            }
        }

        //
        // Summary:
        //     Shorthand for writing Vector3d(0, 0, 1).
        public static Vector3d forward
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return forwardVector;
            }
        }

        //
        // Summary:
        //     Shorthand for writing Vector3d(0, 0, -1).
        public static Vector3d back
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return backVector;
            }
        }

        //
        // Summary:
        //     Shorthand for writing Vector3d(0, 1, 0).
        public static Vector3d up
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return upVector;
            }
        }

        //
        // Summary:
        //     Shorthand for writing Vector3d(0, -1, 0).
        public static Vector3d down
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return downVector;
            }
        }

        //
        // Summary:
        //     Shorthand for writing Vector3d(-1, 0, 0).
        public static Vector3d left
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return leftVector;
            }
        }

        //
        // Summary:
        //     Shorthand for writing Vector3d(1, 0, 0).
        public static Vector3d right
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return rightVector;
            }
        }

        //
        // Summary:
        //     Shorthand for writing Vector3d(float.PositiveInfinity, float.PositiveInfinity,
        //     float.PositiveInfinity).
        public static Vector3d positiveInfinity
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return positiveInfinityVector;
            }
        }

        //
        // Summary:
        //     Shorthand for writing Vector3d(float.NegativeInfinity, float.NegativeInfinity,
        //     float.NegativeInfinity).
        public static Vector3d negativeInfinity
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return negativeInfinityVector;
            }
        }

        [MethodImpl(MethodImplOptions.InternalCall)]
        private static extern void OrthoNormalize2(ref Vector3d a, ref Vector3d b);

        public static void OrthoNormalize(ref Vector3d normal, ref Vector3d tangent)
        {
            OrthoNormalize2(ref normal, ref tangent);
        }

        [MethodImpl(MethodImplOptions.InternalCall)]
        private static extern void OrthoNormalize3(ref Vector3d a, ref Vector3d b, ref Vector3d c);

        public static void OrthoNormalize(ref Vector3d normal, ref Vector3d tangent, ref Vector3d binormal)
        {
            OrthoNormalize3(ref normal, ref tangent, ref binormal);
        }

        //
        // Summary:
        //     Linearly interpolates between two points.
        //
        // Parameters:
        //   a:
        //     Start value, returned when t = 0.
        //
        //   b:
        //     End value, returned when t = 1.
        //
        //   t:
        //     Value used to interpolate between a and b.
        //
        // Returns:
        //     Interpolated value, equals to a + (b - a) * t.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3d Lerp(Vector3d a, Vector3d b, double t)
        {
            t = Math.Clamp(t, 0, 1);
            return new Vector3d(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t, a.z + (b.z - a.z) * t);
        }

        //
        // Summary:
        //     Linearly interpolates between two vectors.
        //
        // Parameters:
        //   a:
        //
        //   b:
        //
        //   t:
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3d LerpUnclamped(Vector3d a, Vector3d b, double t)
        {
            return new Vector3d(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t, a.z + (b.z - a.z) * t);
        }

        //
        // Summary:
        //     Calculate a position between the points specified by current and target, moving
        //     no farther than the distance specified by maxDistanceDelta.
        //
        // Parameters:
        //   current:
        //     The position to move from.
        //
        //   target:
        //     The position to move towards.
        //
        //   maxDistanceDelta:
        //     Distance to move current per call.
        //
        // Returns:
        //     The new position.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3d MoveTowards(Vector3d current, Vector3d target, double maxDistanceDelta)
        {
            double num = target.x - current.x;
            double num2 = target.y - current.y;
            double num3 = target.z - current.z;
            double num4 = num * num + num2 * num2 + num3 * num3;
            if (num4 == 0f || (maxDistanceDelta >= 0f && num4 <= maxDistanceDelta * maxDistanceDelta))
            {
                return target;
            }

            double num5 = Math.Sqrt(num4);
            return new Vector3d(current.x + num / num5 * maxDistanceDelta, current.y + num2 / num5 * maxDistanceDelta, current.z + num3 / num5 * maxDistanceDelta);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [ExcludeFromDocs]
        public static Vector3d SmoothDamp(Vector3d current, Vector3d target, ref Vector3d currentVelocity, double smoothTime, double maxSpeed)
        {
            double deltaTime = Time.deltaTime;
            return SmoothDamp(current, target, ref currentVelocity, smoothTime, maxSpeed, deltaTime);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [ExcludeFromDocs]
        public static Vector3d SmoothDamp(Vector3d current, Vector3d target, ref Vector3d currentVelocity, double smoothTime)
        {
            double deltaTime = Time.deltaTime;
            double maxSpeed = double.PositiveInfinity;
            return SmoothDamp(current, target, ref currentVelocity, smoothTime, maxSpeed, deltaTime);
        }

        public static Vector3d SmoothDamp(Vector3d current, Vector3d target, ref Vector3d currentVelocity, double smoothTime, [DefaultValue("Mathf.Infinity")] double maxSpeed, [DefaultValue("Time.deltaTime")] double deltaTime)
        {
            double num = 0;
            double num2 = 0;
            double num3 = 0;
            smoothTime = Math.Max(0.0001, smoothTime);
            double num4 = 2f / smoothTime;
            double num5 = num4 * deltaTime;
            double num6 = 1 / (1 + num5 + 0.48 * num5 * num5 + 0.235 * num5 * num5 * num5);
            double num7 = current.x - target.x;
            double num8 = current.y - target.y;
            double num9 = current.z - target.z;
            Vector3d vector = target;
            double num10 = maxSpeed * smoothTime;
            double num11 = num10 * num10;
            double num12 = num7 * num7 + num8 * num8 + num9 * num9;
            if (num12 > num11)
            {
                double num13 = Math.Sqrt(num12);
                num7 = num7 / num13 * num10;
                num8 = num8 / num13 * num10;
                num9 = num9 / num13 * num10;
            }

            target.x = current.x - num7;
            target.y = current.y - num8;
            target.z = current.z - num9;
            double num14 = (currentVelocity.x + num4 * num7) * deltaTime;
            double num15 = (currentVelocity.y + num4 * num8) * deltaTime;
            double num16 = (currentVelocity.z + num4 * num9) * deltaTime;
            currentVelocity.x = (currentVelocity.x - num4 * num14) * num6;
            currentVelocity.y = (currentVelocity.y - num4 * num15) * num6;
            currentVelocity.z = (currentVelocity.z - num4 * num16) * num6;
            num = target.x + (num7 + num14) * num6;
            num2 = target.y + (num8 + num15) * num6;
            num3 = target.z + (num9 + num16) * num6;
            double num17 = vector.x - current.x;
            double num18 = vector.y - current.y;
            double num19 = vector.z - current.z;
            double num20 = num - vector.x;
            double num21 = num2 - vector.y;
            double num22 = num3 - vector.z;
            if (num17 * num20 + num18 * num21 + num19 * num22 > 0f)
            {
                num = vector.x;
                num2 = vector.y;
                num3 = vector.z;
                currentVelocity.x = (num - vector.x) / deltaTime;
                currentVelocity.y = (num2 - vector.y) / deltaTime;
                currentVelocity.z = (num3 - vector.z) / deltaTime;
            }

            return new Vector3d(num, num2, num3);
        }

        //
        // Summary:
        //     Creates a new vector with given x, y, z components.
        //
        // Parameters:
        //   x:
        //
        //   y:
        //
        //   z:
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector3d(double x, double y, double z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        //
        // Summary:
        //     Creates a new vector with given x, y components and sets z to zero.
        //
        // Parameters:
        //   x:
        //
        //   y:
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector3d(double x, double y)
        {
            this.x = x;
            this.y = y;
            z = 0f;
        }

        //
        // Summary:
        //     Set x, y and z components of an existing Vector3d.
        //
        // Parameters:
        //   newX:
        //
        //   newY:
        //
        //   newZ:
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Set(float newX, float newY, float newZ)
        {
            x = newX;
            y = newY;
            z = newZ;
        }

        //
        // Summary:
        //     Multiplies two vectors component-wise.
        //
        // Parameters:
        //   a:
        //
        //   b:
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3d Scale(Vector3d a, Vector3d b)
        {
            return new Vector3d(a.x * b.x, a.y * b.y, a.z * b.z);
        }

        //
        // Summary:
        //     Multiplies every component of this vector by the same component of scale.
        //
        // Parameters:
        //   scale:
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Scale(Vector3d scale)
        {
            x *= scale.x;
            y *= scale.y;
            z *= scale.z;
        }

        //
        // Summary:
        //     Cross Product of two vectors.
        //
        // Parameters:
        //   lhs:
        //
        //   rhs:
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3d Cross(Vector3d lhs, Vector3d rhs)
        {
            return new Vector3d(lhs.y * rhs.z - lhs.z * rhs.y, lhs.z * rhs.x - lhs.x * rhs.z, lhs.x * rhs.y - lhs.y * rhs.x);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
        {
            return x.GetHashCode() ^ (y.GetHashCode() << 2) ^ (z.GetHashCode() >> 2);
        }

        //
        // Summary:
        //     Returns true if the given vector is exactly equal to this vector.
        //
        // Parameters:
        //   other:
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool Equals(object other)
        {
            if (!(other is Vector3d))
            {
                return false;
            }

            return Equals((Vector3d)other);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(Vector3d other)
        {
            return x == other.x && y == other.y && z == other.z;
        }

        //
        // Summary:
        //     Reflects a vector off the plane defined by a normal.
        //
        // Parameters:
        //   inDirection:
        //
        //   inNormal:
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3d Reflect(Vector3d inDirection, Vector3d inNormal)
        {
            double num = -2 * Dot(inNormal, inDirection);
            return new Vector3d(num * inNormal.x + inDirection.x, num * inNormal.y + inDirection.y, num * inNormal.z + inDirection.z);
        }

        //
        // Summary:
        //     Makes this vector have a magnitude of 1.
        //
        // Parameters:
        //   value:
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3d Normalize(Vector3d value)
        {
            double num = Magnitude(value);
            if (num > 1E-05)
            {
                return value / num;
            }

            return zero;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Normalize()
        {
            double num = Magnitude(this);
            if (num > 1E-05)
            {
                this /= num;
            }
            else
            {
                this = zero;
            }
        }

        //
        // Summary:
        //     Dot Product of two vectors.
        //
        // Parameters:
        //   lhs:
        //
        //   rhs:
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Dot(Vector3d lhs, Vector3d rhs)
        {
            return lhs.x * rhs.x + lhs.y * rhs.y + lhs.z * rhs.z;
        }

        //
        // Summary:
        //     Projects a vector onto another vector.
        //
        // Parameters:
        //   vector:
        //
        //   onNormal:
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3d Project(Vector3d vector, Vector3d onNormal)
        {
            double num = Dot(onNormal, onNormal);
            if (num < double.Epsilon)
            {
                return zero;
            }

            double num2 = Dot(vector, onNormal);
            return new Vector3d(onNormal.x * num2 / num, onNormal.y * num2 / num, onNormal.z * num2 / num);
        }

        //
        // Summary:
        //     Projects a vector onto a plane defined by a normal orthogonal to the plane.
        //
        // Parameters:
        //   planeNormal:
        //     The direction from the vector towards the plane.
        //
        //   vector:
        //     The location of the vector above the plane.
        //
        // Returns:
        //     The location of the vector on the plane.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3d ProjectOnPlane(Vector3d vector, Vector3d planeNormal)
        {
            double num = Dot(planeNormal, planeNormal);
            if (num < double.Epsilon)
            {
                return vector;
            }

            double num2 = Dot(vector, planeNormal);
            return new Vector3d(vector.x - planeNormal.x * num2 / num, vector.y - planeNormal.y * num2 / num, vector.z - planeNormal.z * num2 / num);
        }

        //
        // Summary:
        //     Calculates the angle between vectors from and.
        //
        // Parameters:
        //   from:
        //     The vector from which the angular difference is measured.
        //
        //   to:
        //     The vector to which the angular difference is measured.
        //
        // Returns:
        //     The angle in degrees between the two vectors.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Angle(Vector3d from, Vector3d to)
        {
            double num = Math.Sqrt(from.sqrMagnitude * to.sqrMagnitude);
            if (num < 1E-15f)
            {
                return 0f;
            }

            double num2 = Math.Clamp(Dot(from, to) / num, -1, 1);
            return Math.Acos(num2) * 57.29578f;
        }

        //
        // Summary:
        //     Calculates the signed angle between vectors from and to in relation to axis.
        //
        // Parameters:
        //   from:
        //     The vector from which the angular difference is measured.
        //
        //   to:
        //     The vector to which the angular difference is measured.
        //
        //   axis:
        //     A vector around which the other vectors are rotated.
        //
        // Returns:
        //     Returns the signed angle between from and to in degrees.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double SignedAngle(Vector3d from, Vector3d to, Vector3d axis)
        {
            double num = Angle(from, to);
            double num2 = from.y * to.z - from.z * to.y;
            double num3 = from.z * to.x - from.x * to.z;
            double num4 = from.x * to.y - from.y * to.x;
            double num5 = Math.Sign(axis.x * num2 + axis.y * num3 + axis.z * num4);
            return num * num5;
        }

        //
        // Summary:
        //     Returns the distance between a and b.
        //
        // Parameters:
        //   a:
        //
        //   b:
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Distance(Vector3d a, Vector3d b)
        {
            double num = a.x - b.x;
            double num2 = a.y - b.y;
            double num3 = a.z - b.z;
            return Math.Sqrt(num * num + num2 * num2 + num3 * num3);
        }

        //
        // Summary:
        //     Returns a copy of vector with its magnitude clamped to maxLength.
        //
        // Parameters:
        //   vector:
        //
        //   maxLength:
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3d ClampMagnitude(Vector3d vector, double maxLength)
        {
            double num = vector.sqrMagnitude;
            if (num > maxLength * maxLength)
            {
                double num2 = Math.Sqrt(num);
                double num3 = vector.x / num2;
                double num4 = vector.y / num2;
                double num5 = vector.z / num2;
                return new Vector3d(num3 * maxLength, num4 * maxLength, num5 * maxLength);
            }

            return vector;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Magnitude(Vector3d vector)
        {
            return Math.Sqrt(vector.x * vector.x + vector.y * vector.y + vector.z * vector.z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double SqrMagnitude(Vector3d vector)
        {
            return vector.x * vector.x + vector.y * vector.y + vector.z * vector.z;
        }

        //
        // Summary:
        //     Returns a vector that is made from the smallest components of two vectors.
        //
        // Parameters:
        //   lhs:
        //
        //   rhs:
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3d Min(Vector3d lhs, Vector3d rhs)
        {
            return new Vector3d(Math.Min(lhs.x, rhs.x), Math.Min(lhs.y, rhs.y), Math.Min(lhs.z, rhs.z));
        }

        //
        // Summary:
        //     Returns a vector that is made from the largest components of two vectors.
        //
        // Parameters:
        //   lhs:
        //
        //   rhs:
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3d Max(Vector3d lhs, Vector3d rhs)
        {
            return new Vector3d(Math.Max(lhs.x, rhs.x), Math.Max(lhs.y, rhs.y), Math.Max(lhs.z, rhs.z));
        }

        // Operations with double vectors
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3d operator +(Vector3d a, Vector3d b)
        {
            return new Vector3d(a.x + b.x, a.y + b.y, a.z + b.z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3d operator -(Vector3d a, Vector3d b)
        {
            return new Vector3d(a.x - b.x, a.y - b.y, a.z - b.z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3d operator -(Vector3d a)
        {
            return new Vector3d(0 - a.x, 0 - a.y, 0 - a.z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3d operator *(Vector3d a, double d)
        {
            return new Vector3d(a.x * d, a.y * d, a.z * d);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3d operator *(double d, Vector3d a)
        {
            return new Vector3d(a.x * d, a.y * d, a.z * d);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3d operator /(Vector3d a, double d)
        {
            return new Vector3d(a.x / d, a.y / d, a.z / d);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(Vector3d lhs, Vector3d rhs)
        {
            double num = lhs.x - rhs.x;
            double num2 = lhs.y - rhs.y;
            double num3 = lhs.z - rhs.z;
            double num4 = num * num + num2 * num2 + num3 * num3;
            return num4 < 9.99999944E-11;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(Vector3d lhs, Vector3d rhs)
        {
            return !(lhs == rhs);
        }

        //
        // Summary:
        //     Returns a formatted string for this vector.
        //
        // Parameters:
        //   format:
        //     A numeric format string.
        //
        //   formatProvider:
        //     An object that specifies culture-specific formatting.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override string ToString()
        {
            return ToString(null, null);
        }

        //
        // Summary:
        //     Returns a formatted string for this vector.
        //
        // Parameters:
        //   format:
        //     A numeric format string.
        //
        //   formatProvider:
        //     An object that specifies culture-specific formatting.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string ToString(string format)
        {
            return ToString(format, null);
        }

        //
        // Summary:
        //     Returns a formatted string for this vector.
        //
        // Parameters:
        //   format:
        //     A numeric format string.
        //
        //   formatProvider:
        //     An object that specifies culture-specific formatting.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string ToString(string format, IFormatProvider formatProvider)
        {
            if (string.IsNullOrEmpty(format))
            {
                format = "F2";
            }

            if (formatProvider == null)
            {
                formatProvider = CultureInfo.InvariantCulture.NumberFormat;
            }

            return "(" + x.ToString(format, formatProvider) + ", " + y.ToString(format, formatProvider) + ", " + z.ToString(format, formatProvider) + ")";
        }
    }

    public static class SpaceMath
    {
        public const double kilo = 1e3;
        public const double mega = 1e6;
        public const double giga = 1e9;
        public const double astronomicalUnit = 1.495978707e11;
        public const double lightYear = 9.46073e15;
        public const double c = 2.99792458e8;

        private const int minute = 60;
        private const int hour = 3600;
        private const int day = 86400;
        private const int year = 31536000;

        private const float epsilon = 0.0001f;

        public static Vector3d Rotate(this Vector3d vector, Quaternion rotation)
        {
            float num = rotation.x * 2f;
            float num2 = rotation.y * 2f;
            float num3 = rotation.z * 2f;
            float num4 = rotation.x * num;
            float num5 = rotation.y * num2;
            float num6 = rotation.z * num3;
            float num7 = rotation.x * num2;
            float num8 = rotation.x * num3;
            float num9 = rotation.y * num3;
            float num10 = rotation.w * num;
            float num11 = rotation.w * num2;
            float num12 = rotation.w * num3;
            Vector3d result = default;
            result.x = (1f - (num5 + num6)) * vector.x + (num7 - num12) * vector.y + (num8 + num11) * vector.z;
            result.y = (num7 + num12) * vector.x + (1f - (num4 + num6)) * vector.y + (num9 - num10) * vector.z;
            result.z = (num8 - num11) * vector.x + (num9 + num10) * vector.y + (1f - (num4 + num5)) * vector.z;
            return result;
        }

        /// <summary>
        /// Normalizes an angle in degrees to the range [-180, 180].
        /// </summary>
        /// <param name="angle">Angle in degrees</param>
        /// <returns>Normalized angle in degrees</returns>
        public static float NormalizeAngle(float angle)
        {
            // Convert from (-infinity, infinity) to [-180, 180]
            angle %= 360f;

            if (angle > 180f)
                angle -= 360f;

            if (angle < -180f)
                angle += 360f;

            return angle;
        }

        /// <summary>
        /// Normalizes Euler angles to the range [-180, 180] for each axis.
        /// </summary>
        /// <param name="eulerAngles">Euler angles in degrees</param>
        /// <returns>Normalized Euler angles in degrees</returns>
        public static Vector3 NormalizeEulerAngles(this Vector3 eulerAngles)
        {
            float x = NormalizeAngle(eulerAngles.x);
            float y = NormalizeAngle(eulerAngles.y);
            float z = NormalizeAngle(eulerAngles.z);
            return new Vector3(x, y, z);
        }

        /// <summary>
        /// Clamps each component of a vector between the corresponding components of min and max.
        /// </summary>
        /// <param name="vector">The vector to clamp</param>
        /// <param name="min">The minimum values for each component</param>
        /// <param name="max">The maximum values for each component</param>
        /// <returns>The clamped vector</returns>
        public static Vector3 Clamp(Vector3 vector, Vector3 min, Vector3 max)
        {
            return new Vector3(Mathf.Clamp(vector.x, min.x, max.x), Mathf.Clamp(vector.y, min.y, max.y), Mathf.Clamp(vector.z, min.z, max.z));
        }

        /// <summary>
        /// Clamps each component of a vector between min and max.
        /// </summary>
        /// <param name="vector">The vector to clamp</param>
        /// <param name="min">The minimum value for each component</param>
        /// <param name="max">The maximum value for each component</param>
        /// <returns>The clamped vector</returns>
        public static Vector3 Clamp(Vector3 vector, float min, float max)
        {
            return new Vector3(Mathf.Clamp(vector.x, min, max), Mathf.Clamp(vector.y, min, max), Mathf.Clamp(vector.z, min, max));
        }

        /// <summary>
        /// Wraps each component of a vector between the corresponding components of min and max.
        /// </summary>
        /// <param name="vector">The vector to wrap</param>
        /// <param name="min">The minimum values for each component</param>
        /// <param name="max">The maximum values for each component</param>
        /// <returns>The wrapped vector</returns>
        public static Vector3 WrapClamp(Vector3 vector, Vector3 min, Vector3 max)
        {
            Vector3 returnVector = vector;
            if (vector.x < min.x)
            {
                returnVector.x = max.x;
            }
            else if (vector.x > max.x)
            {
                returnVector.x = min.x;
            }
            if (vector.y < min.y)
            {
                returnVector.y = max.y;
            }
            else if (vector.y > max.y)
            {
                returnVector.y = min.y;
            }
            if (vector.z < min.z)
            {
                returnVector.z = max.z;
            }
            else if (vector.z > max.z)
            {
                returnVector.z = min.z;
            }
            return returnVector;
        }

        /// <summary>
        /// Wraps each component of a vector between min and max.
        /// </summary>
        /// <param name="vector">The vector to wrap</param>
        /// <param name="min">The minimum value for each component</param>
        /// <param name="max">The maximum value for each component</param>
        /// <returns>The wrapped vector</returns>
        public static Vector3 WrapClamp(Vector3 vector, float min, float max)
        {
            Vector3 returnVector = vector;
            if (vector.x < min)
            {
                returnVector.x = max;
            }
            else if (vector.x > max)
            {
                returnVector.x = min;
            }
            if (vector.y < min)
            {
                returnVector.y = max;
            }
            else if (vector.y > max)
            {
                returnVector.y = min;
            }
            if (vector.z < min)
            {
                returnVector.z = max;
            }
            else if (vector.z > max)
            {
                returnVector.z = min;
            }
            return returnVector;
        }

        /// <summary>
        /// Normalizes a value to the range [0, 1] based on the provided minimum and maximum values.
        /// </summary>
        /// <param name="value">The value to normalize</param>
        /// <param name="min">The minimum value</param>
        /// <param name="max">The maximum value</param>
        /// <returns>The normalized value</returns>
        public static float Normalize(float value, float min, float max)
        {
            return (value - min) / (max - min);
        }

        /// <summary>
        /// Formats a distance in meters to the largest appropriate unit (m, km, Mm, Gm, AU, or ly) with the specified number of decimal places.
        /// </summary>
        /// <param name="distance">Distance in meters</param>
        /// <param name="decimals">Number of decimal places to display</param>
        /// <returns>Formatted distance string</returns>
        public static string DistanceToFormattedString(double distance, string format)
        {
            if (distance < kilo)
            {
                return distance.ToString(format) + "m";
            }
            else if (distance < mega)
            {
                return (distance / kilo).ToString(format) + "km";
            }
            else if (distance < c * 100.0)
            {
                return (distance / c).ToString(format) + "ls";
            }
            else if (distance < astronomicalUnit * 100.0)
            {
                return (distance / astronomicalUnit).ToString(format) + "au";
            }

            return (distance / lightYear).ToString(format) + "ly";
        }

        /// <summary>
        /// Formats a speed in meters per second to the largest appropriate unit (m/s, km/s, Mm/s, or c) with the specified number of decimal places.
        /// </summary>
        /// <param name="speed">Speed in meters per second</param>
        /// <param name="decimals">Number of decimal places to display</param>
        /// <returns>Formatted speed string</returns>
        public static string SpeedToFormattedString(double speed, string format)
        {
            double absSpeed = Math.Abs(speed);

            if (absSpeed < kilo)
            {
                return speed.ToString(format) + "m/s";
            }
            else if (absSpeed < 0.01 * c)
            {
                return (speed / kilo).ToString(format) + "km/s";
            }

            return (speed / c).ToString(format) + "c";
        }

        /// <summary>
        /// Formats a time in seconds to the largest appropriate unit (seconds, minutes, hours, days, or years) with the specified number of decimal places.
        /// </summary>
        /// <param name="seconds"></param>
        /// <param name="decimals">Number of decimal places to display</param>
        /// <returns>Formatted time string</returns>
        public static string SecondsToFormattedString(double seconds, string format)
        {
            if(seconds >= year)
            {
                return (seconds / year).ToString(format) + "years";
            }
            else if(seconds >= day)
            {
                return (seconds / day).ToString(format) + "days";
            }
            else if(seconds >= hour)
            {
                return (seconds / hour).ToString(format) + "hours";
            }
            else if(seconds >= minute)
            {
                return (seconds / minute).ToString(format) + "minutes";
            }
            return seconds.ToString(format) + "seconds";
        }

        public static Vector3d ToVector3d(this Vector3 vector)
        {
            return new Vector3d(vector.x, vector.y, vector.z);
        }

        public static Vector3 ToVector3(this Vector3d vector)
        {
            return new Vector3((float)vector.x, (float)vector.y, (float)vector.z);
        }

        public static GameObject GenerateModel(GameObject GO, int modelLayer, Material modelMaterial, int childDepth)
        {
            GameObject model = new GameObject
            {
                layer = modelLayer,
                name = GO.name + " Model"
            };
            if (GO.TryGetComponent<MeshFilter>(out var meshFilter))
            {
                MeshFilter modelMesh = model.AddComponent<MeshFilter>();
                MeshRenderer modelRenderer = model.AddComponent<MeshRenderer>();
                modelMesh.sharedMesh = meshFilter.sharedMesh;
                int materialsLength = meshFilter.GetComponent<MeshRenderer>().sharedMaterials.Length;
                Material[] modelMaterials = new Material[materialsLength];
                for (int i = 0; i < materialsLength; i++)
                {
                    modelMaterials[i] = modelMaterial;
                }
                modelRenderer.sharedMaterials = modelMaterials;
            }
            if(childDepth > 0)
            {
                foreach (Transform child in GO.transform)
                {
                    GameObject childModel = GenerateModel(child.gameObject, modelLayer, modelMaterial, childDepth - 1);
                    childModel.transform.SetLocalPositionAndRotation(child.localPosition, child.localRotation);
                    childModel.transform.SetParent(model.transform);
                }
            }

            return model;
        }

        public static Vector3d Round(this Vector3d vector, int digits)
        {
            Vector3d result = new Vector3d
            {
                x = Math.Round(vector.x, digits),
                y = Math.Round(vector.y, digits),
                z = Math.Round(vector.z, digits)
            };
            return result;
        }

        public static Vector3 Round(this Vector3 vector, int digits)
        {
            Vector3 result = new Vector3
            {
                x = (float)Math.Round(vector.x, digits),
                y = (float)Math.Round(vector.y, digits),
                z = (float)Math.Round(vector.z, digits)
            };
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void TryRoot(double t, ref double best)
        {
            if (t > 0f)
                best = Math.Min(best, t);
        }

        /// <summary>
        /// Solves the quadratic equation: ax^2+bx+c=0 and returns the smallest positive real root.
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <param name="c"></param>
        /// <param name="best"></param>
        /// <returns>Smallest positive real root, or -1 if imaginary or negative.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double SolveQuadratic(double a, double b, double c)
        {
            if (Math.Abs(a) < epsilon)
            {
                if (Math.Abs(b) < epsilon)
                    return -1.0;
                double x = -c / b;
                return x < 0 ? -1.0 : x;
            }
            // x = (-b +/- sqrt(b^2-4ac)) / 2a
            double discriminant = b * b - 4.0 * a * c;
            if (discriminant < 0.0)
                return -1.0;
            double sqrtD = Math.Sqrt(discriminant);
            double inv2a = 0.5 / a;
            double best = double.PositiveInfinity;
            TryRoot((-b + sqrtD) * inv2a, ref best);
            TryRoot((-b - sqrtD) * inv2a, ref best);
            return double.IsPositiveInfinity(best) ? -1.0 : best;
        }

        /// <summary>
        /// Calculates the time it will take for object A to arrive at object B given the closingAcceleration, closingVelocity, and distance from A to B
        /// </summary>
        /// <param name="closingAcceleration">m/s^2. + => accelerating towards B, - => accelerating away from B</param>
        /// <param name="closingVelocity">m/s. + => moving towards B, - => moving away from B</param>
        /// <param name="distance">Distance in meters between A and B</param>
        /// <returns>The time in seconds it will take for the object to arrive at the target, or -1 if it can never arrive.</returns>
        public static double CalculateArrivalTime(double distance, double closingVelocity, double closingAcceleration)
        {
            // dst = (1/2)At^2 + Vt
            // a = (1/2)A, b = V, c = -dst
            if (distance < epsilon)
                return 0f;
            
            return SolveQuadratic(0.5 * closingAcceleration, closingVelocity, -distance);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SolveBiquadratic(double p, double r, double shift, ref double best)
        {
            // x⁴ + p·x² + r = 0  →  u = x²,  u² + p·u + r = 0
            double disc = p * p - 4.0 * r;
            if (disc < 0.0)
                return;
            double sq = Math.Sqrt(disc);
            foreach (double u in new[] { (-p + sq) * 0.5, (-p - sq) * 0.5 })
            {
                if (u < 0.0) continue;
                double xr = Math.Sqrt(u);
                TryRoot( xr + shift, ref best);
                TryRoot(-xr + shift, ref best);
            }
        }

        /// <summary>
        /// Solves   m³ + B·m² + C·m + D = 0.
        /// </summary>
        /// <param name="B"></param>
        /// <param name="C"></param>
        /// <param name="D"></param>
        /// <returns>1 or 3 real roots</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double[] SolveCubic(double B, double C, double D)
        {
            const double third = 1.0 / 3.0;
            const double twoPiThird = 2.0 * Math.PI * third;

            // Depress to  u³ + pu + q = 0  via  m = u − B/3
            double B2     = B * B;
            double p      = C - B2 * third;
            double q      = D + B * (2.0 * B2 / 27.0 - C * third);
            double mShift = -B * third;

            // Δ > 0  ↔  three distinct real roots
            double delta = -(4.0 * p * p * p + 27.0 * q * q);

            if (delta > 0.0)
            {
                // Trigonometric method (p < 0 is guaranteed when Δ > 0)
                double sqrtMp3 = Math.Sqrt(-p * third);
                double cosArg  = Math.Max(-1.0, Math.Min(1.0, 1.5 * q / (p * sqrtMp3)));
                double theta   = Math.Acos(cosArg) * third;
                double amp     = 2.0 * sqrtMp3;
                return new[]
                {
                    amp * Math.Cos(theta)               + mShift,
                    amp * Math.Cos(theta - twoPiThird)  + mShift,
                    amp * Math.Cos(theta + twoPiThird)  + mShift,
                };
            }
            else
            {
                // Cardano — one real root
                double hq        = q * 0.5;
                double inner     = Math.Max(0.0, hq * hq + p * p * p / 27.0);
                double sqrtInner = Math.Sqrt(inner);
                return new[]
                {
                    Math.Cbrt(-hq + sqrtInner) + Math.Cbrt(-hq - sqrtInner) + mShift
                };
            }
        }

        public static double CalculateProjectileTime(Vector3d relativePosition, Vector3d relativeVelocity, Vector3d relativeAcceleration, double projectileSpeed)
        {
            // Equate projectile's displacement to the target's displacement
            // y = time
            // v_b = (x_i-x_b) + vy + (1/2)ay^2
            // 0 = -v_b + (x_i-x_b) + vy + (1/2)ay^2
            // Square both sides to remove vector component
            // ay^4 + by^3 + cy^2 + dy + e = 0
            // a = (1/4)|a|^2
            // b = dot(v, a)
            // c = |v|^2 + dot(x_i-x_b, a) - bulletSpeed^2
            // d = 2(dot(x_i-x_b, v)
            // e = |(x_i-x_b)|^2

            double a = 0.25 * relativeAcceleration.sqrMagnitude;
            double b = Vector3d.Dot(relativeVelocity, relativeAcceleration);
            double c = relativeVelocity.sqrMagnitude + Vector3d.Dot(relativePosition, relativeAcceleration) - projectileSpeed * projectileSpeed;
            double d = 2.0 * Vector3d.Dot(relativePosition, relativeVelocity);
            double e = relativePosition.sqrMagnitude;

            if (a < epsilon)
            {
                // No relative acceleration, use quadratic instead
                return SolveQuadratic(c, d, e);
            }

            // Depress quartic to: y^4 + (b/a)y^3 + (c/a)y^2 + (d/a)y + e/a = 0
            // Let y = x-b/(4a)
            // x^4 + px^2 + qx + r = 0
            // p = (8ac - 3b^2) / (8a^2)
            // q = (b^3 - 4abc + 8(a^2)d) / (8a^3)
            // r = (16a(b^2)c - 64(a^2)bd - 3b^4 + 256(a^3)e) / 256a^4
            double inv_a = 1.0 / a;
            double inv_a2 = inv_a * inv_a;
            double b2 = b * b;
            //double p = (8.0*a*c - 3.0*b2) / (8.0*a*a);
            double p = inv_a*c - 0.375*b2*inv_a2;
            //double q = (b2*b - 4.0*a*b*c + 8.0*a*a*d) / 8*a*a*a;
            double q = 0.125*b2*b*inv_a2*inv_a - 0.5*inv_a2*b*c + d*inv_a;
            //double r = (16.0*a*b2*c - 64.0*a*a*b*d - 3.0*b2*b2 + 256.0*a*a*a*e) / 256*a*a*a*a;
            double r = 0.0625*inv_a2*inv_a*b2*c - 0.25*inv_a2*b*d - 0.01171875*inv_a2*inv_a2*b2*b2 + inv_a*e;
            
            double shift = -b * 0.25 * inv_a; // t = x + shift
            double t = double.PositiveInfinity;

            if (Math.Abs(q) < epsilon)
            {
                // Biquadratic shortcut
                SolveBiquadratic(p, r, shift, ref t);
                return double.IsPositiveInfinity(t) ? -1.0 : t;
            }

            // Now use Ferrari method to solve the 4 solutions
            // x^4 + px^2 + qx + r = (x^2 + sx + t)(x^2 + ux + v)
            // (s+u) = 0
            // (t+v+su) = p
            // (sv+tu) = q
            // tv = r
            // v = (2sr) / (s^3 + ps - q)
            // rs^6 + 2prs^4 + (rp^2 - 4r^2)s^2 = (q^2)r = 0
            // Find s²=m via resolvent cubic m³ + 2p·m² + (p²−4r)·m − q² = 0
            // Factor the quartic as (x² + sx + tv)(x² − sx + vv)
            // Any real root of the resolvent cubic works; the largest is most stable.
            double[] mRoots = SolveCubic(2.0 * p, p * p - 4.0 * r, -(q * q));
            double m = double.NegativeInfinity;
            foreach (double root in mRoots)
                if (!double.IsNaN(root) && root > m) m = root;

            if (m < 0.0)
                return -1.0; // quartic has no real roots

            m = Math.Max(0.0, m); // numerical guard against tiny negative
            double s = Math.Sqrt(m);

            if (s < epsilon) // s ≈ 0: degenerate back to biquadratic
            {
                SolveBiquadratic(p, r, shift, ref t);
                return double.IsPositiveInfinity(t) ? -1.0 : t;
            }

            // tv = (s² + p − q/s) / 2,   vv = (s² + p + q/s) / 2
            double sp  = m + p;
            double qos = q / s;
            double tv  = (sp - qos) * 0.5;
            double vv  = (sp + qos) * 0.5;

            // Quadratic 1: x² + sx + tv = 0,  roots x = (−s ± √(s²−4tv)) / 2
            double disc1 = m - 4.0 * tv;
            if (disc1 >= 0.0)
            {
                double sq1 = Math.Sqrt(disc1);
                TryRoot(0.5 * (-s + sq1) + shift, ref t);
                TryRoot(0.5 * (-s - sq1) + shift, ref t);
            }

            // Quadratic 2: x² − sx + vv = 0,  roots x = (s ± √(s²−4vv)) / 2
            double disc2 = m - 4.0 * vv;
            if (disc2 >= 0.0)
            {
                double sq2 = Math.Sqrt(disc2);
                TryRoot(0.5 * ( s + sq2) + shift, ref t);
                TryRoot(0.5 * ( s - sq2) + shift, ref t);
            }

            return double.IsPositiveInfinity(t) ? -1.0 : t;
        }

        public static double EstimateProjectileTime(Vector3d relativePosition, Vector3d relativeVelocity, Vector3d relativeAcceleration, double projectileSpeed)
        {
            double a = 0.25 * relativeAcceleration.sqrMagnitude;
            double b = Vector3d.Dot(relativeVelocity, relativeAcceleration);
            double c = relativeVelocity.sqrMagnitude + Vector3d.Dot(relativePosition, relativeAcceleration) - projectileSpeed * projectileSpeed;
            double d = 2.0 * Vector3d.Dot(relativePosition, relativeVelocity);
            double e = relativePosition.sqrMagnitude;
            // Newton-Raphson method or Jenkins-Traub algorithm
            // x_n+1 = x_n - f(x_n)/f'(x_n)
            return -1.0;
        }
    }

    /// <summary>
    /// Struct representing a quadrilateral defined by four 2D points in clockwise order.
    /// </summary>
    public struct Quadrilateral
    {
        private static readonly Quadrilateral zeroQuad = new Quadrilateral(Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);

        /// <summary>
        /// Bottom left corner
        /// </summary>
        public Vector2 p1;
        /// <summary>
        /// Top left corner
        /// </summary>
        public Vector2 p2;
        /// <summary>
        /// Top right corner
        /// </summary>
        public Vector2 p3;
        /// <summary>
        /// Bottom right corner
        /// </summary>
        public Vector2 p4;

        public Quadrilateral(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4)
        {
            this.p1 = p1;
            this.p2 = p2;
            this.p3 = p3;
            this.p4 = p4;
        }

        public static Quadrilateral zero
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return zeroQuad;
            }
        }
    }

    public static class SpaceGeometry
    {
        public static bool QuadrilateralIsZero(Quadrilateral quad)
        {
            return quad.p1 == Vector2.zero && quad.p2 == Vector2.zero && quad.p3 == Vector2.zero && quad.p4 == Vector2.zero;
        }

        /// <summary>
        /// Gets the world axis-aligned bounding box of a set of renderers relative to a camera.
        /// </summary>
        /// <param name="renderers"></param>
        /// <param name="camera"></param>
        /// <returns>The axis-aligned bounding box.</returns>
        public static Quadrilateral GetAxisAlignedBoundingBox(Renderer[] renderers, Camera camera)
        {
            if (renderers.Length == 0)
            {
                return Quadrilateral.zero;
            }

            Vector3 min = renderers[0].bounds.min;
            Vector3 max = renderers[0].bounds.max;

            for (int i = 1; i < renderers.Length; i++)
            {
                Bounds bounds = renderers[i].bounds;
                min = Vector3.Min(min, bounds.min);
                max = Vector3.Max(max, bounds.max);
            }

            Vector2 minScreen = camera.WorldToScreenPoint(min);
            Vector2 maxScreen = camera.WorldToScreenPoint(max);

            return new Quadrilateral(
                minScreen, 
                new Vector2(minScreen.x, maxScreen.y), 
                maxScreen, 
                new Vector2(maxScreen.x, minScreen.y)
            );
        }

        /// <summary>
        /// Rotates a set of points by a given angle counterclockwise in radians around the pivot point.
        /// </summary>
        /// <param name="points">2D points to rotate</param>
        /// <param name="pivot">The point to rotate around</param>
        /// <param name="angle">Angle in radians</param>
        /// <returns>Array of rotated points.</returns>
        public static Vector2[] RotatePoints(Vector2[] points, Vector2 pivot, float angle)
        {
            Vector2[] rotatedPoints = new Vector2[points.Length];
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);
            for (int i = 0; i < points.Length; i++)
            {
                Vector2 point = points[i] - pivot;
                rotatedPoints[i] = new Vector2(point.x * cos - point.y * sin, point.x * sin + point.y * cos) + pivot;
            }
            return rotatedPoints;
        }

        /// <summary>
        /// Rotates a quadrilateral by a given angle counterclockwise in radians around the pivot point.
        /// </summary>
        /// <param name="quad">Quadrilateral to rotate</param>
        /// <param name="pivot">The point to rotate around</param>
        /// <param name="angle">Angle in radians</param>
        /// <returns>Rotated quadrilateral.</returns>
        public static Quadrilateral RotateQuadrilateral(Quadrilateral quad, Vector2 pivot, float angle)
        {
            Vector2[] points = new Vector2[4] { quad.p1, quad.p2, quad.p3, quad.p4 };
            Vector2[] rotatedPoints = RotatePoints(points, pivot, angle);
            return new Quadrilateral(rotatedPoints[0], rotatedPoints[1], rotatedPoints[2], rotatedPoints[3]);
        }

        /// <summary>
        /// Gets the minimum bounding box of a set of renderers relative to a camera, rotated to better fit the renderers. This is more expensive than GetAxisAlignedBoundingBox but can provide a tighter fit.
        /// </summary>
        /// <param name="renderers"></param>
        /// <param name="camera"></param>
        /// <returns>The minimum rotated bounding box in 2D space.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Quadrilateral GetMinimumBoundingBox(Renderer[] renderers, Camera camera)
        {
            if (renderers.Length == 0)
                return Quadrilateral.zero;

            Vector3[] corners = new Vector3[8];
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);
            bool visible = false;
            Matrix4x4 viewProjection = camera.projectionMatrix * camera.worldToCameraMatrix;
            foreach (Renderer renderer in renderers)
            {
                Bounds localBounds = renderer.localBounds;
                Vector3 boundsMin = localBounds.min;
                Vector3 boundsMax = localBounds.max;

                corners[0] = new Vector3(boundsMin.x, boundsMin.y, boundsMin.z);
                corners[1] = new Vector3(boundsMin.x, boundsMin.y, boundsMax.z);
                corners[2] = new Vector3(boundsMin.x, boundsMax.y, boundsMin.z);
                corners[3] = new Vector3(boundsMin.x, boundsMax.y, boundsMax.z);
                corners[4] = new Vector3(boundsMax.x, boundsMin.y, boundsMin.z);
                corners[5] = new Vector3(boundsMax.x, boundsMin.y, boundsMax.z);
                corners[6] = new Vector3(boundsMax.x, boundsMax.y, boundsMin.z);
                corners[7] = new Vector3(boundsMax.x, boundsMax.y, boundsMax.z);

                Matrix4x4 localToClip = viewProjection * renderer.localToWorldMatrix;

                for (int i = 0; i < 8; i++)
                {
                    Vector4 clip = localToClip * new Vector4(
                        corners[i].x,
                        corners[i].y,
                        corners[i].z,
                        1f
                    );

                    if (clip.w <= 0) // Point is behind the camera
                        continue;

                    visible = true;

                    float invW = 1f / clip.w;
                    Vector2 screen = new Vector2(
                        (clip.x * invW * 0.5f + 0.5f) * camera.pixelWidth,
                        (clip.y * invW * 0.5f + 0.5f) * camera.pixelHeight
                    );

                    min = Vector2.Min(min, screen);
                    max = Vector2.Max(max, screen);
                }
            }

            if (!visible)
                return Quadrilateral.zero;

            return new Quadrilateral(
                min,
                new Vector2(min.x, max.y),
                max,
                new Vector2(max.x, min.y)
            );
        }

        /// <summary>
        /// Gets the minimum bounding box of an ellipsoid defined by its center, scale, and rotation relative to a camera.
        /// </summary>
        /// <param name="center"></param>
        /// <param name="scale"></param>
        /// <param name="rotation"></param>
        /// <param name="camera"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Quadrilateral GetEllipsoidBoundingBox(Vector3 center, Vector3 scale, Quaternion rotation, Camera camera)
        {
            Vector3 right = rotation * Vector3.right * scale.x;
            Vector3 up = rotation * Vector3.up * scale.y;
            Vector3 forward = rotation * Vector3.forward * scale.z;

            Vector3[] corners =
            {
                center - right - up - forward,
                center - right - up + forward,
                center - right + up - forward,
                center - right + up + forward,
                center + right - up - forward,
                center + right - up + forward,
                center + right + up - forward,
                center + right + up + forward
            };

            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);
            bool visible = false;
            Matrix4x4 viewProjection = camera.projectionMatrix * camera.worldToCameraMatrix;
            for (int i = 0; i < 8; i++)
            {
                Vector4 clip = viewProjection * new Vector4(
                    corners[i].x,
                    corners[i].y,
                    corners[i].z,
                    1f
                );

                if (clip.w <= 0) // Point is behind the camera
                    continue;

                visible = true;

                float invW = 1f / clip.w;
                Vector2 screen = new Vector2(
                    (clip.x * invW * 0.5f + 0.5f) * camera.pixelWidth,
                    (clip.y * invW * 0.5f + 0.5f) * camera.pixelHeight
                );

                min = Vector2.Min(min, screen);
                max = Vector2.Max(max, screen);
            }

            if (!visible)
                return Quadrilateral.zero;

            return new Quadrilateral(
                min,
                new Vector2(min.x, max.y),
                max,
                new Vector2(max.x, min.y)
            );
        }
    }
}
