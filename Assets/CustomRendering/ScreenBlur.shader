Shader "Hidden/ScreenBlur"
{
    Properties
    {
        _BlurRadius ("Blur Radius", Range(0, 20)) = 5
        _BlurStrength ("Blur Strength", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
        }

        ZTest Always
        ZWrite Off
        Cull Off

        HLSLINCLUDE

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D_X(_BlitTexture);
        SAMPLER(sampler_ScreenBlurLinearClamp);

        CBUFFER_START(UnityPerMaterial)

            float _BlurRadius;
            float _BlurStrength;

        CBUFFER_END

        struct Attributes
        {
            uint vertexID : SV_VertexID;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 texcoord : TEXCOORD0;
        };

        Varyings Vert(Attributes input)
        {
            Varyings output;

            output.positionCS =
                GetFullScreenTriangleVertexPosition(input.vertexID);

            output.texcoord =
                GetFullScreenTriangleTexCoord(input.vertexID);

            return output;
        }

        half4 SampleBlur(
            float2 uv,
            float2 direction
        )
        {
            float2 texelSize = 1.0 / _ScreenParams.xy;

            float2 offset =
                direction *
                texelSize *
                _BlurRadius;

            half4 result = 0;

            result += SAMPLE_TEXTURE2D_X(
                _BlitTexture,
                sampler_ScreenBlurLinearClamp,
                uv - offset * 4.0
            ) * 0.016;

            result += SAMPLE_TEXTURE2D_X(
                _BlitTexture,
                sampler_ScreenBlurLinearClamp,
                uv - offset * 3.0
            ) * 0.061;

            result += SAMPLE_TEXTURE2D_X(
                _BlitTexture,
                sampler_ScreenBlurLinearClamp,
                uv - offset * 2.0
            ) * 0.122;

            result += SAMPLE_TEXTURE2D_X(
                _BlitTexture,
                sampler_ScreenBlurLinearClamp,
                uv - offset
            ) * 0.194;

            result += SAMPLE_TEXTURE2D_X(
                _BlitTexture,
                sampler_ScreenBlurLinearClamp,
                uv
            ) * 0.214;

            result += SAMPLE_TEXTURE2D_X(
                _BlitTexture,
                sampler_ScreenBlurLinearClamp,
                uv + offset
            ) * 0.194;

            result += SAMPLE_TEXTURE2D_X(
                _BlitTexture,
                sampler_ScreenBlurLinearClamp,
                uv + offset * 2.0
            ) * 0.122;

            result += SAMPLE_TEXTURE2D_X(
                _BlitTexture,
                sampler_ScreenBlurLinearClamp,
                uv + offset * 3.0
            ) * 0.061;

            result += SAMPLE_TEXTURE2D_X(
                _BlitTexture,
                sampler_ScreenBlurLinearClamp,
                uv + offset * 4.0
            ) * 0.016;

            return result;
        }

        half4 HorizontalBlur(Varyings input) : SV_Target
        {
            half4 original =
                SAMPLE_TEXTURE2D_X(
                    _BlitTexture,
                    sampler_ScreenBlurLinearClamp,
                    input.texcoord
                );

            half4 blurred =
                SampleBlur(
                    input.texcoord,
                    float2(1.0, 0.0)
                );

            return lerp(original, blurred, _BlurStrength);
        }

        half4 VerticalBlur(Varyings input) : SV_Target
        {
            half4 original =
                SAMPLE_TEXTURE2D_X(
                    _BlitTexture,
                    sampler_ScreenBlurLinearClamp,
                    input.texcoord
                );

            half4 blurred =
                SampleBlur(
                    input.texcoord,
                    float2(0.0, 1.0)
                );

            return lerp(original, blurred, _BlurStrength);
        }

        ENDHLSL

        Pass
        {
            Name "Horizontal Blur"

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment HorizontalBlur

            ENDHLSL
        }

        Pass
        {
            Name "Vertical Blur"

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment VerticalBlur

            ENDHLSL
        }
    }
}