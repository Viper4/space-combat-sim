#ifndef CALCULATE_ABERRATED_POSITION_INCLUDED
#define CALCULATE_ABERRATED_POSITION_INCLUDED

// Calculates the apparent position of a world-space point for a moving
// observer using the same retarded-position + Lorentz-transform approach used by A Slower Speed of Light / OpenRelativity.
void CalculateAberratedPosition_float(
    float3 AbsWorldPos,
    float3 CameraPos,
    float3 ObserverVel,
    float3 SourceVel,
    float SpeedOfLight,
    float gamma,
    out float3 AberratedPosition)
{
    const float EPSILON = 1e-6;

    float3 relativePosition = AbsWorldPos - CameraPos;
    float distanceSquared = dot(relativePosition, relativePosition);

    if (distanceSquared <= EPSILON * EPSILON)
    {
        AberratedPosition = AbsWorldPos;
        return;
    }

    // At the current time the source is at relativePosition.
    // At a time tPast > 0 in the past, its position was: relativePosition - SourceVel * tPast
    //
    // The emitted light must have traveled c * tPast, so:
    // |relativePosition - SourceVel*tPast| = c*tPast
    //
    // This produces the quadratic:
    // (c^2 - v^2)t^2 + 2(dot(r, v))t - r^2 = 0
    // The positive root is the amount of look-back time tPast.
    float cSquared = SpeedOfLight * SpeedOfLight;
    float sourceSpeedSquared = dot(SourceVel, SourceVel);

    // Assume the source is subluminal (v < c)
    float denominator = max(cSquared - sourceSpeedSquared, EPSILON);
    float rDotV = dot(relativePosition, SourceVel);

    float discriminant = rDotV * rDotV + denominator * distanceSquared;
    discriminant = max(discriminant, 0.0);

    float tPast = (-rDotV + sqrt(discriminant)) / denominator;

    // Position of the source when it emitted the light, still expressed in the original/common inertial frame.
    float3 retardedPosition = AbsWorldPos - SourceVel * tPast; // Negated SourceVel so +

    // Lorentz-transform the emission event into the observer's instantaneous rest frame.
    float observerSpeedSquared = dot(ObserverVel, ObserverVel);

    // beta^2 = v^2 / c^2
    // float betaSquared = observerSpeedSquared / cSquared;
    // betaSquared = min(betaSquared, 1.0 - EPSILON);
    // gamma = rsqrt(max(1.0 - betaSquared, EPSILON));

    if (observerSpeedSquared <= EPSILON * EPSILON)
    {
        // Observer is stationary, so there is no spatial Lorentz transform.
        AberratedPosition = retardedPosition;
    }
    else
    {
        // Decompose the retarded position into components parallel and perpendicular to the observer's velocity.
        float3 observerVelocityDir = normalize(ObserverVel);
        float parallelScalar = dot(retardedPosition, observerVelocityDir);
        float3 parallel = parallelScalar * observerVelocityDir;
        float3 perpendicular = retardedPosition - parallel;

        // Lorentz transformation of the spatial position at emission time.
        // In the observer frame:
        //
        //   r'_perp     = r_perp
        //   r'_parallel = gamma * (r_parallel - v_observer * tPast)
        AberratedPosition = perpendicular + gamma * (parallel - ObserverVel * tPast);
    }
}

#endif
