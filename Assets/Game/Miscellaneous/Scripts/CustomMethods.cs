using UnityEngine;
using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using UnityEngine.Internal;

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

    public static class CustomMethods
    {
        private const double kilo = 1e3;
        private const double mega = 1e6;
        private const double giga = 1e9;
        private const double astronomicalUnit = 1.495978707e11;
        private const double lightYear = 9.46073e15;
        private const double c = 2.99792458e8;

        private const int minute = 60;
        private const int hour = 3600;
        private const int day = 86400;
        private const int year = 31536000;

        private const float epsilon = 0.0001f;

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

        public static Vector3 NormalizeEulerAngles(this Vector3 eulerAngles)
        {
            float x = NormalizeAngle(eulerAngles.x);
            float y = NormalizeAngle(eulerAngles.y);
            float z = NormalizeAngle(eulerAngles.z);
            return new Vector3(x, y, z);
        }

        public static Vector3 Clamp(Vector3 vector, Vector3 min, Vector3 max)
        {
            return new Vector3(Mathf.Clamp(vector.x, min.x, max.x), Mathf.Clamp(vector.y, min.y, max.y), Mathf.Clamp(vector.z, min.z, max.z));
        }

        public static Vector3 Clamp(Vector3 vector, float min, float max)
        {
            return new Vector3(Mathf.Clamp(vector.x, min, max), Mathf.Clamp(vector.y, min, max), Mathf.Clamp(vector.z, min, max));
        }

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

        public static float normalize(float value, float min, float max)
        {
            return (value - min) / (max - min);
        }

        // Assuming units in meters
        public static string DistanceToFormattedString(double distance, int decimals = 0)
        {
            double decimalOffset = Math.Pow(10, decimals);
            if(distance >= lightYear)
            {
                return (Math.Round(distance / lightYear * decimalOffset) / decimalOffset).ToString() + "ly";
            }
            else if(distance >= astronomicalUnit)
            {
                return (Math.Round(distance / astronomicalUnit * decimalOffset) / decimalOffset).ToString() + "AU";
            }
            else if (distance >= giga)
            {
                return (Math.Round(distance / giga * decimalOffset) / decimalOffset).ToString() + "Gm";
            }
            else if(distance >= mega)
            {
                return (Math.Round(distance / mega * decimalOffset) / decimalOffset).ToString() + "Mm";
            }
            else if(distance >= kilo)
            {
                return (Math.Round(distance / kilo * decimalOffset) / decimalOffset).ToString() + "km";
            }
            return (Math.Round(distance * decimalOffset) / decimalOffset).ToString() + "m";
        }

        // Assuming units in m/s
        public static string SpeedToFormattedString(double speed, int decimals = 0)
        {
            double decimalOffset = Math.Pow(10, decimals);

            if (Math.Abs(speed) >= c)
            {
                return (Math.Round(speed / c * decimalOffset) / decimalOffset).ToString() + "c";
            }
            else if (Math.Abs(speed) >= mega)
            {
                return (Math.Round(speed / mega * decimalOffset) / decimalOffset).ToString() + "Mm/s";
            }
            else if (Math.Abs(speed) >= kilo)
            {
                return (Math.Round(speed / kilo * decimalOffset) / decimalOffset).ToString() + "km/s";
            }
            return (Math.Round(speed * decimalOffset) / decimalOffset).ToString() + "m/s";
        }

        public static string SecondsToFormattedString(double seconds, int decimals = 0)
        {
            double decimalOffset = Math.Pow(10, decimals);
            if(seconds >= year)
            {
                return (Math.Round(seconds / year * decimalOffset) / decimalOffset).ToString() + " years";
            }
            else if(seconds >= day)
            {
                return (Math.Round(seconds / day * decimalOffset) / decimalOffset).ToString() + " days";
            }
            else if(seconds >= hour)
            {
                return (Math.Round(seconds / hour * decimalOffset) / decimalOffset).ToString() + " hours";
            }
            else if(seconds >= minute)
            {
                return (Math.Round(seconds / minute * decimalOffset) / decimalOffset).ToString() + " minutes";
            }
            return (Math.Round(seconds * decimalOffset) / decimalOffset).ToString() + " seconds";
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

        public static float CalculateArrivalTime(float closingAcceleration, float closingSpeed, float distance)
        {
            // 0 = (1/2)At^2 + Vt + dst
            // a = (1/2)A, b = V, c = dst
            if (distance < epsilon)
                return 0f;

            // Constant velocity
            if (Mathf.Abs(closingAcceleration) < epsilon)
            {
                if (closingSpeed <= 0)
                    return -1f; // moving away or stationary

                return distance / closingSpeed;
            }

            // Quadratic formula to get arrival time
            float discriminant = closingSpeed * closingSpeed + 2f * closingAcceleration * distance;

            if (discriminant < 0)
                return -1f; // Can never arrive

            float sqrtD = Mathf.Sqrt(discriminant);

            float t1 = (-closingSpeed + sqrtD) / closingAcceleration;
            float t2 = (-closingSpeed - sqrtD) / closingAcceleration;

            // Choose smallest positive root
            float arrival = float.PositiveInfinity;

            if (t1 > epsilon)
                arrival = t1;

            if (t2 > epsilon)
                arrival = Mathf.Min(arrival, t2);

            return float.IsInfinity(arrival) ? -1f : arrival;
        }
    }
}
