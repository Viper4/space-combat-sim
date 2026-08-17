#ifndef SAMPLE_6_FACES_INCLUDED
#define SAMPLE_6_FACES_INCLUDED

void Sample6Faces_float(
    float3 ViewDir,

    UnityTexture2D RightTex,
    UnityTexture2D LeftTex,
    UnityTexture2D UpTex,
    UnityTexture2D DownTex,
    UnityTexture2D FrontTex,
    UnityTexture2D BackTex,

    UnitySamplerState Sampler,

    out float4 Color)
{
    // Normalize the direction
    float3 dir = normalize(ViewDir);

    float3 absDir = abs(dir);

    float2 uv;

    //---------------------------------------
    // X Faces
    //---------------------------------------
    if (absDir.x >= absDir.y && absDir.x >= absDir.z)
    {
        // +X (Right)
        if (dir.x > 0.0)
        {
            uv.x = -dir.z / absDir.x;
            uv.y =  dir.y / absDir.x;

            // slightly inset by 0.001 to get rid of seams
            uv = uv * 0.499 + 0.5;

            Color = SAMPLE_TEXTURE2D(
                LeftTex.tex,
                Sampler.samplerstate,
                uv);
        }
        // -X (Left)
        else
        {
            uv.x =  dir.z / absDir.x;
            uv.y =  dir.y / absDir.x;

            uv = uv * 0.499 + 0.5;

            Color = SAMPLE_TEXTURE2D(
                RightTex.tex,
                Sampler.samplerstate,
                uv);
        }
    }

    //---------------------------------------
    // Y Faces
    //---------------------------------------
    else if (absDir.y >= absDir.x && absDir.y >= absDir.z)
    {
        // +Y (Up)
        if (dir.y > 0.0)
        {
            uv.x =  dir.x / absDir.y;
            uv.y = -dir.z / absDir.y;

            uv = uv * 0.499 + 0.5;

            Color = SAMPLE_TEXTURE2D(
                UpTex.tex,
                Sampler.samplerstate,
                uv);
        }
        // -Y (Down)
        else
        {
            uv.x =  dir.x / absDir.y;
            uv.y =  dir.z / absDir.y;

            uv = uv * 0.499 + 0.5;

            Color = SAMPLE_TEXTURE2D(
                DownTex.tex,
                Sampler.samplerstate,
                uv);
        }
    }

    //---------------------------------------
    // Z Faces
    //---------------------------------------
    else
    {
        // +Z (Front)
        if (dir.z > 0.0)
        {
            uv.x =  dir.x / absDir.z;
            uv.y =  dir.y / absDir.z;

            uv = uv * 0.499 + 0.5;

            Color = SAMPLE_TEXTURE2D(
                FrontTex.tex,
                Sampler.samplerstate,
                uv);
        }
        // -Z (Back)
        else
        {
            uv.x = -dir.x / absDir.z;
            uv.y =  dir.y / absDir.z;

            uv = uv * 0.499 + 0.5;

            Color = SAMPLE_TEXTURE2D(
                BackTex.tex,
                Sampler.samplerstate,
                uv);
        }
    }
}

#endif