#ifndef RELATIVITY_INCLUDED
#define RELATIVITY_INCLUDED

// -----------------------------------------------------------------------------
// Shader properties supplied from C#
//
// _RelVelocity      : world-space velocity of this object (m/s)
// _ObserverVelocity : world-space velocity of the observer/camera (m/s)
// _SpeedOfLight     : speed of light (same units as velocities)
// -----------------------------------------------------------------------------

float3 _RelVelocity;
float3 _ObserverVelocity;
float  _SpeedOfLight;

//------------------------------------------------------------------------------
// Utility
//------------------------------------------------------------------------------

float Gamma(float beta)
{
    beta = saturate(beta * 0.9999);
    return rsqrt(1.0 - beta * beta);
}

//------------------------------------------------------------------------------
// Relative velocity
//------------------------------------------------------------------------------

// Simple approximation.
// (Not full Lorentz velocity addition.)
float3 GetRelativeVelocity()
{
    return _RelVelocity - _ObserverVelocity;
}

//------------------------------------------------------------------------------
// Relativistic Aberration
//------------------------------------------------------------------------------

float3 ApplyAberration(float3 viewDir)
{
    float3 v = GetRelativeVelocity();

    float speed = length(v);
    if (speed < 0.001)
        return normalize(viewDir);

    float beta = saturate(speed / _SpeedOfLight);

    float3 n = v / speed;

    float cosTheta = dot(viewDir, n);

    float cosPrime =
        (cosTheta + beta) /
        (1.0 + beta * cosTheta);

    float sinPrime =
        sqrt(max(0.0, 1.0 - cosPrime * cosPrime));

    float3 tangent =
        normalize(viewDir - cosTheta * n);

    if (all(abs(tangent) < 1e-6))
        tangent = float3(0,0,1);

    return normalize(
        tangent * sinPrime +
        n * cosPrime
    );
}

//------------------------------------------------------------------------------
// Doppler factor
//------------------------------------------------------------------------------

float GetDopplerFactor(float3 viewDir)
{
    float3 v = GetRelativeVelocity();

    float speed = length(v);
    if (speed < 0.001)
        return 1.0;

    float beta = saturate(speed / _SpeedOfLight);
    float gamma = Gamma(beta);

    float3 n = normalize(v);

    float cosTheta = dot(viewDir, n);

    // Relativistic Doppler factor
    return gamma * (1.0 + beta * cosTheta);
}

//------------------------------------------------------------------------------
// Convert Doppler factor into RGB tint.
//
// This is only an artistic approximation.
//------------------------------------------------------------------------------
float3 DopplerColor(float doppler)
{
    float3 color = float3(1,1,1);

    if (doppler > 1.0)
    {
        // Blueshift
        float t = saturate((doppler - 1.0) * 2.0);

        color.r *= lerp(1.0, 0.6, t);
        color.g *= lerp(1.0, 0.85, t);
        color.b *= lerp(1.0, 1.4, t);
    }
    else
    {
        // Redshift
        float t = saturate((1.0 - doppler) * 2.0);

        color.r *= lerp(1.0, 1.4, t);
        color.g *= lerp(1.0, 0.7, t);
        color.b *= lerp(1.0, 0.45, t);
    }

    return color;
}

//------------------------------------------------------------------------------
// Searchlight effect
//------------------------------------------------------------------------------

float SearchlightBoost(float3 viewDir)
{
    float3 v = GetRelativeVelocity();

    float speed = length(v);
    if (speed < 0.001)
        return 1.0;

    float beta = saturate(speed / _SpeedOfLight);

    float3 n = normalize(v);

    float forward = saturate(dot(viewDir, n));

    // Simple approximation.
    return lerp(
        1.0,
        1.0 + beta * beta * 8.0,
        pow(forward, 8.0)
    );
}

//------------------------------------------------------------------------------
// Apply all relativistic effects.
//
// baseColor should be the lighting result before emission/post-processing.
// viewDir should point FROM the fragment TO the camera.
//------------------------------------------------------------------------------

float3 ApplyRelativity(float3 baseColor, float3 viewDir)
{
    float doppler = GetDopplerFactor(viewDir);

    float3 color =
        baseColor *
        DopplerColor(doppler);

    color *= SearchlightBoost(viewDir);

    return color;
}

#endif