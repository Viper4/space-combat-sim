#ifndef DOPPLER_COLOR_INCLUDED
#define DOPPLER_COLOR_INCLUDED

// Soft 0-1 visibility window over the human visible range, fading out
// over ~40nm at each edge rather than cutting off hard.
float VisibilityWindow(in float wavelength)
{
    // Visible spectrum is from 380nm to 740nm
    const float MIN_WAVELENGTH = 380.0;
    const float MAX_WAVELENGTH = 740.0;
    const float BUFFER = 15.0;

    float lowRamp  = smoothstep(MIN_WAVELENGTH, MIN_WAVELENGTH + BUFFER, wavelength);
    float highRamp = 1.0 - smoothstep(MAX_WAVELENGTH - BUFFER, MAX_WAVELENGTH, wavelength);

    return saturate(lowRamp * highRamp);
}

float Gaussian(float wavelength, float center, float std)
{
    float x = (wavelength - center) / max(std, 0.001);
    return exp(-0.5 * x * x);
}

// Fast approximation of relativistic Doppler color shifting. Treats RGB as broad spectral bands to hopefully produce more natural
// transitions through cyan/blue and yellow/red than an RGB lerp.
void DopplerColorShift_float(
    float4 Color,
    float beta,
    float cosTheta,
    float gamma,
    float beamingFactor,
    float redStd,
    float greenStd,
    float blueStd,
    float violetStd,
    out float4 OutColor)
{
    float dopplerFactor = gamma * (1.0 - beta * cosTheta);
    float invDopplerFactor = 1.0 / dopplerFactor;
    float brightness = pow(dopplerFactor, beamingFactor);

    float3 rgb = max(Color.rgb, 0.0);

    // Approximate RGB wavelengths.
    const float RED_WAVELENGTH   = 650.0;
    const float GREEN_WAVELENGTH = 540.0;
    const float BLUE_WAVELENGTH  = 475.0;
    const float VIOLET_WAVELENGTH = 380.0;

    // dopplerFactor scales wavelength:
    // lambda_observed = lambda_emitted * dopplerFactor
    // < 1 -> blueshift
    // > 1 -> redshift

    // Calculating doppler factor in the scenario with motion in arbitrary direction measured in source frame
    // so must multiply frequency by dopplerFactor (divide wavelength by factor)
    float shiftedR = RED_WAVELENGTH * invDopplerFactor;
    float shiftedG = GREEN_WAVELENGTH * invDopplerFactor;
    float shiftedB = BLUE_WAVELENGTH * invDopplerFactor;

    // Measure how far the light has shifted out of visible spectrum to turn this object invisible with alpha
    float visR = VisibilityWindow(shiftedR);
    float visG = VisibilityWindow(shiftedG);
    float visB = VisibilityWindow(shiftedB);
    float visibility = max(visR * rgb.r, max(visG * rgb.g, visB * rgb.b));

    // Treat each RGB channel as one wavelength of red light, green light, and blue light with intensities RGB.r, RGB.g, and RGB.b
    // Capture the intensity of red wavelength light for each shifted RGB channel wavelength of light using a Gaussian,
    // then sum their intensities into the R channel
    float r = Gaussian(shiftedR, RED_WAVELENGTH, redStd) * rgb.r + 
              Gaussian(shiftedG, RED_WAVELENGTH, redStd) * rgb.g +
              Gaussian(shiftedB, RED_WAVELENGTH, redStd) * rgb.b;

    float g = Gaussian(shiftedR, GREEN_WAVELENGTH, greenStd) * rgb.r + 
              Gaussian(shiftedG, GREEN_WAVELENGTH, greenStd) * rgb.g +
              Gaussian(shiftedB, GREEN_WAVELENGTH, greenStd) * rgb.b;

    float b = Gaussian(shiftedR, BLUE_WAVELENGTH, blueStd) * rgb.r + 
              Gaussian(shiftedG, BLUE_WAVELENGTH, blueStd) * rgb.g +
              Gaussian(shiftedB, BLUE_WAVELENGTH, blueStd) * rgb.b;

    // Have to add violet wavelength back in separately from RGB
    // For some reason, the shifted green and blue wavelengths create violet bands 
    // around where the pure blue and aqua (blue+green) bands should be. Just removed those for now, but maybe physically
    // they mix with other wavelengths (like yellow or something) to form a realistic "true" aqua->blue transition in the blueshift.
    float violet = Gaussian(shiftedR, VIOLET_WAVELENGTH, violetStd) * rgb.r;
    
    // Pure violet is usually (127,0,255) in RGB => 0.5 red and 1.0 blue ratio
    r += violet * 0.5;
    b += violet * 1.0;

    float3 shiftedColor = float3(r, g, b);

    OutColor = float4(max(shiftedColor, 0.0) * brightness, Color.a * visibility);
}

#endif
