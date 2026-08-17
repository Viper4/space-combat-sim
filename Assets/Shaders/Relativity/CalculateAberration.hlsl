#ifndef CALCULATE_ABERRATION_INCLUDED
#define CALCULATE_ABERRATION_INCLUDED

// Calculates the apparent position of a moving world-space point for a moving
// observer using the same retarded-position + Lorentz-transform approach used by A Slower Speed of Light / OpenRelativity.
// Also calculates and returns the relative velocity accounting for relativity.
void CalculateAberration_float(
    float3 AbsWorldPos,
    float3 CameraPos,
    float3 ObserverVel,
    float3 SourceVel,
    float SpeedOfLight,
    out float3 AberratedPosition,
    out float3 RelativeVelocity)
{
    const float EPSILON = 1e-6;

    float3 relativePosition = AbsWorldPos - CameraPos;
    float distanceSquared = dot(relativePosition, relativePosition);

    if (distanceSquared <= EPSILON * EPSILON)
    {
        AberratedPosition = AbsWorldPos;
        RelativeVelocity = SourceVel - ObserverVel;
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
    float3 retardedPosition = AbsWorldPos - SourceVel * tPast;

    // Lorentz-transform the emission event into the observer's instantaneous rest frame.
    float observerSpeedSquared = dot(ObserverVel, ObserverVel);

    if (observerSpeedSquared <= EPSILON * EPSILON)
    {
        // Observer is stationary, so there is no spatial Lorentz transform.
        AberratedPosition = retardedPosition;
        RelativeVelocity = SourceVel;
    }
    else
    {
        // Decompose the retarded position into components parallel and perpendicular to the observer's velocity.
        float3 observerVelocityDir = normalize(ObserverVel);
        float posParallelScalar = dot(retardedPosition, observerVelocityDir);
        float3 posParallel = posParallelScalar * observerVelocityDir;
        float3 posPerpendicular = retardedPosition - posParallel;

        // Lorentz transformation of the spatial position at emission time.
        // In the observer frame:
        //   r'_perp     = r_perp
        //   r'_parallel = gamma * (r_parallel + v_observer * tPast)
        // 
        // tPast is positive so + not -

        float betaSquared = observerSpeedSquared / cSquared;
        betaSquared = min(betaSquared, 1.0 - EPSILON);
        float gamma = rsqrt(max(1.0 - betaSquared, EPSILON));
        AberratedPosition = posPerpendicular + gamma * (posParallel + ObserverVel * tPast);

        // Calculate relative velocity for future calculations like doppler shift
        // Decompose SourceVel into components parallel and perpendicular to the observer's velocity
        // v_r = (v_s - v_o) / (1 - (v_s*v_o)/c^2) when v_s and v_o are collinear
        float relVelDenominator = max(1.0 - dot(SourceVel, ObserverVel) / cSquared, EPSILON);

        float sourceParallelScalar = dot(SourceVel, observerVelocityDir);
        float3 sourceParallel = sourceParallelScalar * observerVelocityDir;
        float3 sourcePerpendicular = SourceVel - sourceParallel;

        float3 transformedParallel = (sourceParallel - ObserverVel) / relVelDenominator;
        float3 transformedPerpendicular = sourcePerpendicular / (gamma * relVelDenominator);

        RelativeVelocity = transformedParallel + transformedPerpendicular;
    }
}

#endif
