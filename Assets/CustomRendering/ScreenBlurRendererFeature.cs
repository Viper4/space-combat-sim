using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

public class ScreenBlurRendererFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        [Tooltip("Shader used for the screen blur.")]
        public Shader blurShader;
    }

    public Settings settings = new Settings();

    private Material blurMaterial;
    private ScreenBlurPass blurPass;

    private static readonly int BlurRadiusID =
        Shader.PropertyToID("_BlurRadius");

    private static readonly int BlurStrengthID =
        Shader.PropertyToID("_BlurStrength");

    public override void Create()
    {
        if (settings.blurShader == null)
        {
            Debug.LogError(
                "ScreenBlurRendererFeature: No blur shader assigned."
            );

            return;
        }

        blurMaterial =
            CoreUtils.CreateEngineMaterial(settings.blurShader);

        blurPass =
            new ScreenBlurPass(blurMaterial)
            {
                renderPassEvent =
                    RenderPassEvent.AfterRenderingPostProcessing
            };
    }

    public override void AddRenderPasses(
        ScriptableRenderer renderer,
        ref RenderingData renderingData)
    {
        if (blurMaterial == null)
            return;

        CameraType cameraType =
            renderingData.cameraData.cameraType;

        if (cameraType == CameraType.Preview ||
            cameraType == CameraType.Reflection)
        {
            return;
        }

        // Get the current volume stack for this camera.
        VolumeStack stack = VolumeManager.instance.stack;

        ScreenBlur volume = stack.GetComponent<ScreenBlur>();

        if (volume == null ||
            !volume.IsActive())
        {
            return;
        }

        blurMaterial.SetFloat(
            BlurRadiusID,
            volume.radius.value
        );

        blurMaterial.SetFloat(
            BlurStrengthID,
            volume.strength.value
        );

        renderer.EnqueuePass(blurPass);
    }

    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(blurMaterial);
    }

    private class ScreenBlurPass : ScriptableRenderPass
    {
        private readonly Material material;

        private class PassData
        {
            public TextureHandle source;
            public TextureHandle destination;
            public Material material;
            public int passIndex;
        }

        public ScreenBlurPass(Material material)
        {
            this.material = material;

            ConfigureInput(
                ScriptableRenderPassInput.Color
            );
        }

        public override void RecordRenderGraph(
            RenderGraph renderGraph,
            ContextContainer frameData)
        {
            UniversalResourceData resourceData =
                frameData.Get<UniversalResourceData>();

            if (resourceData.isActiveTargetBackBuffer)
                return;

            TextureHandle cameraColor =
                resourceData.activeColorTexture;

            UniversalCameraData cameraData =
                frameData.Get<UniversalCameraData>();

            RenderTextureDescriptor descriptor =
                cameraData.cameraTargetDescriptor;

            descriptor.depthBufferBits = 0;
            descriptor.msaaSamples = 1;

            TextureHandle horizontalTexture =
                UniversalRenderer.CreateRenderGraphTexture(
                    renderGraph,
                    descriptor,
                    "_ScreenBlurHorizontal",
                    false
                );

            TextureHandle verticalTexture =
                UniversalRenderer.CreateRenderGraphTexture(
                    renderGraph,
                    descriptor,
                    "_ScreenBlurVertical",
                    false
                );

            // ------------------------------------------------------------
            // Horizontal blur
            // ------------------------------------------------------------

            using (
                var builder =
                renderGraph.AddRasterRenderPass<PassData>(
                    "Screen Blur - Horizontal",
                    out var passData
                )
            )
            {
                passData.source =
                    cameraColor;

                passData.destination =
                    horizontalTexture;

                passData.material =
                    material;

                passData.passIndex =
                    0;

                builder.UseTexture(
                    passData.source,
                    AccessFlags.Read
                );

                builder.SetRenderAttachment(
                    passData.destination,
                    0,
                    AccessFlags.Write
                );

                builder.SetRenderFunc(
                    static (
                        PassData data,
                        RasterGraphContext context
                    ) =>
                    {
                        Blitter.BlitTexture(
                            context.cmd,
                            data.source,
                            new Vector4(
                                1f,
                                1f,
                                0f,
                                0f
                            ),
                            data.material,
                            data.passIndex
                        );
                    }
                );
            }

            // ------------------------------------------------------------
            // Vertical blur
            // ------------------------------------------------------------

            using (
                var builder =
                renderGraph.AddRasterRenderPass<PassData>(
                    "Screen Blur - Vertical",
                    out var passData
                )
            )
            {
                passData.source =
                    horizontalTexture;

                passData.destination =
                    verticalTexture;

                passData.material =
                    material;

                passData.passIndex =
                    1;

                builder.UseTexture(
                    passData.source,
                    AccessFlags.Read
                );

                builder.SetRenderAttachment(
                    passData.destination,
                    0,
                    AccessFlags.Write
                );

                builder.SetRenderFunc(
                    static (
                        PassData data,
                        RasterGraphContext context
                    ) =>
                    {
                        Blitter.BlitTexture(
                            context.cmd,
                            data.source,
                            new Vector4(
                                1f,
                                1f,
                                0f,
                                0f
                            ),
                            data.material,
                            data.passIndex
                        );
                    }
                );
            }

            resourceData.cameraColor =
                verticalTexture;
        }
    }
}