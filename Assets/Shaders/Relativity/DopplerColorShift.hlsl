#ifndef DOPPLER_COLOR_INCLUDED
#define DOPPLER_COLOR_INCLUDED

// Soft 0-1 visibility window over the human visible range, fading out
// over ~40nm at each edge rather than cutting off hard.
float VisibilityWindow(in float wavelength)
{
    // Visible spectrum is from 380nm to 740nm
    const float MIN_WAVELENGTH = 380.0;
    const float MAX_WAVELENGTH = 740.0;
    const float VISIBLE_SPECTRUM_EXTRA = 20.0; // Go a little past the boundaries for a better fade to black+alpha fade
    const float BUFFER = 50.0;

    float lowRamp  = smoothstep(MIN_WAVELENGTH-VISIBLE_SPECTRUM_EXTRA, MIN_WAVELENGTH + BUFFER, wavelength);
    float highRamp = 1.0 - smoothstep(MAX_WAVELENGTH - BUFFER, MAX_WAVELENGTH+VISIBLE_SPECTRUM_EXTRA, wavelength);

    return saturate(lowRamp * highRamp);
}

float Gaussian(float wavelength, float center, float std)
{
    float x = (wavelength - center) / max(std, 0.001);
    return exp(-0.5 * x * x);
}

// Fast approximation of relativistic Doppler color shifting. Treats RGB as broad spectral bands + violet
void DopplerColorShift_float(
    float4 Color,
    float3 RelativeVelocity,
    float3 ViewDir,
    float SpeedOfLight,
    float BeamingFactor,
    float RedStd,
    float GreenStd,
    float BlueStd,
    float VioletStd,
    out float4 ShiftedColor)
{
    const float EPSILON = 1e-6;

    float relativeSpeedSquared = dot(RelativeVelocity, RelativeVelocity);
    float relativeBetaSquared = min(relativeSpeedSquared / (SpeedOfLight * SpeedOfLight), 1.0 - EPSILON);
    float relativeBeta = sqrt(relativeBetaSquared);
    float relativeSpeed = sqrt(relativeSpeedSquared);
    float3 relativeVelocityDir = RelativeVelocity / max(relativeSpeed, EPSILON);

    float cosTheta = dot(relativeVelocityDir, -ViewDir); // Want vector from camera to surface, so negate viewDir
    float invDopplerFactor = abs(sqrt(1.0 - relativeBetaSquared) / (1.0 - relativeBeta * cosTheta));

    float brightness = pow(1.0 / invDopplerFactor, BeamingFactor);

    float3 rgb = max(Color.rgb, 0.0);

    // Approximate RGB wavelengths
    const float RED_WAVELENGTH   = 650.0;
    const float GREEN_WAVELENGTH = 540.0;
    const float BLUE_WAVELENGTH  = 475.0;
    const float VIOLET_WAVELENGTH = 380.0;

    // Calculating doppler factor in the scenario with motion in arbitrary direction measured in source frame
    // so must multiply frequency by dopplerFactor D (multiply wavelength by 1/D)
    float shiftedR = RED_WAVELENGTH * invDopplerFactor;
    float shiftedG = GREEN_WAVELENGTH * invDopplerFactor;
    float shiftedB = BLUE_WAVELENGTH * invDopplerFactor;

    // Measure how far the light has shifted out of visible spectrum to turn this object invisible with alpha
    float visR = VisibilityWindow(shiftedR);
    float visG = VisibilityWindow(shiftedG);
    float visB = VisibilityWindow(shiftedB);
    float visibility = max(visR, max(visG, visB));

    // Treat each RGB channel as one wavelength of red light, green light, and blue light with intensities RGB.r, RGB.g, and RGB.b
    // Capture the intensity of red wavelength light for each shifted RGB channel wavelength of light using a Gaussian,
    // then sum their intensities into the R channel
    float r = Gaussian(shiftedR, RED_WAVELENGTH, RedStd) * rgb.r + 
              Gaussian(shiftedG, RED_WAVELENGTH, RedStd) * rgb.g +
              Gaussian(shiftedB, RED_WAVELENGTH, RedStd) * rgb.b;

    float g = Gaussian(shiftedR, GREEN_WAVELENGTH, GreenStd) * rgb.r + 
              Gaussian(shiftedG, GREEN_WAVELENGTH, GreenStd) * rgb.g +
              Gaussian(shiftedB, GREEN_WAVELENGTH, GreenStd) * rgb.b;

    float b = Gaussian(shiftedR, BLUE_WAVELENGTH, BlueStd) * rgb.r + 
              Gaussian(shiftedG, BLUE_WAVELENGTH, BlueStd) * rgb.g +
              Gaussian(shiftedB, BLUE_WAVELENGTH, BlueStd) * rgb.b;

    // Have to add violet wavelength back in separately from RGB
    // For some reason, the shifted green and blue wavelengths create violet bands 
    // around where the pure blue and aqua (blue+green) bands should be. Just removed those for now, but maybe physically
    // they mix with other wavelengths (like yellow or something) to form a realistic "true" aqua->blue transition in the blueshift.
    float violet = Gaussian(shiftedR, VIOLET_WAVELENGTH, VioletStd) * rgb.r;
    
    // Pure violet is usually (127,0,255) in RGB => 0.5 red and 1.0 blue ratio
    r += violet * 0.5;
    b += violet * 1.0;

    float3 shiftedRGB = float3(r, g, b);

    ShiftedColor = float4(max(shiftedRGB, 0.0) * brightness, Color.a * visibility);
}

#endif
