using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using Lutra.Core.Data.Models;

namespace Lutra.UI.Theme
{
    /// <summary>
    /// Renderer Feature que aplica una corrección de color de daltonismo sobre el framebuffer.
    /// Añadir al Renderer2D asset (Settings/Renderer2D) y asignar el shader Lutra/Colorblind.
    /// Activar/desactivar via ColorblindFeature.CurrentMode desde SettingsManager.
    /// </summary>
    public class ColorblindFeature : ScriptableRendererFeature
    {
        [SerializeField] private Shader _shader;

        private Material       _material;
        private ColorblindPass _pass;
        private ColorblindMode _lastLoggedMode = (ColorblindMode)(-1);

        /// <summary>Modo activo; escribe SettingsManager en Awake y al cambiar ajustes.</summary>
        public static ColorblindMode CurrentMode { get; set; } = ColorblindMode.None;

        // ── Matrices de corrección de color (daltonización) ─────────────

        private static readonly Vector3 Deut_R = new(0.625f, 0.375f, 0.000f);
        private static readonly Vector3 Deut_G = new(0.700f, 0.300f, 0.000f);
        private static readonly Vector3 Deut_B = new(0.000f, 0.300f, 0.700f);

        private static readonly Vector3 Prot_R = new(0.567f, 0.433f, 0.000f);
        private static readonly Vector3 Prot_G = new(0.558f, 0.442f, 0.000f);
        private static readonly Vector3 Prot_B = new(0.000f, 0.242f, 0.758f);

        private static readonly Vector3 Trit_R = new(0.950f, 0.050f, 0.000f);
        private static readonly Vector3 Trit_G = new(0.000f, 0.433f, 0.567f);
        private static readonly Vector3 Trit_B = new(0.000f, 0.475f, 0.525f);

        // ── ScriptableRendererFeature ────────────────────────────────────

        public override void Create()
        {
            if (_shader == null)
            {
                Debug.LogWarning("[ColorblindFeature] Shader no asignado. El efecto no se aplicará.");
                return;
            }

            _material = CoreUtils.CreateEngineMaterial(_shader);
            _pass = new ColorblindPass(_material)
            {
                renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing
            };
            Debug.Log($"[ColorblindFeature] Create() — material creado: {_material != null}, shader: {_shader.name}");
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (CurrentMode == ColorblindMode.None) return;
            if (_material == null || _pass == null)
            {
                Debug.LogWarning("[ColorblindFeature] AddRenderPasses — material o pass es null. ¿Se llamó Create()?");
                return;
            }

            var cameraType = renderingData.cameraData.cameraType;
            if (cameraType == CameraType.Preview || cameraType == CameraType.Reflection) return;

            _applyMatrix(CurrentMode);
            _pass.requiresIntermediateTexture = true;
            renderer.EnqueuePass(_pass);

            if (_lastLoggedMode != CurrentMode)
            {
                _lastLoggedMode = CurrentMode;
                _pass._diagLogged = false;
                Debug.Log($"[ColorblindFeature] Pass encolado — modo: {CurrentMode}");
            }
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(_material);
        }

        private void _applyMatrix(ColorblindMode mode)
        {
            switch (mode)
            {
                case ColorblindMode.Deuteranopia:
                    _material.SetVector("_RowR", Deut_R);
                    _material.SetVector("_RowG", Deut_G);
                    _material.SetVector("_RowB", Deut_B);
                    break;
                case ColorblindMode.Protanopia:
                    _material.SetVector("_RowR", Prot_R);
                    _material.SetVector("_RowG", Prot_G);
                    _material.SetVector("_RowB", Prot_B);
                    break;
                case ColorblindMode.Tritanopia:
                    _material.SetVector("_RowR", Trit_R);
                    _material.SetVector("_RowG", Trit_G);
                    _material.SetVector("_RowB", Trit_B);
                    break;
            }
        }

        // ── Inner render pass ────────────────────────────────────────────

        private sealed class ColorblindPass : ScriptableRenderPass
        {
            private readonly Material _mat;
            internal bool _diagLogged;

            private class PassData
            {
                internal TextureHandle src;
                internal TextureHandle tmp;
                internal Material      mat;
            }

            internal ColorblindPass(Material mat)
            {
                _mat = mat;
                profilingSampler = new ProfilingSampler("Colorblind");
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (_mat == null) return;

                var resourceData = frameData.Get<UniversalResourceData>();
                var cameraColor  = resourceData.cameraColor;

                if (!_diagLogged) { _diagLogged = true; Debug.Log($"[ColorblindFeature] RecordRenderGraph — cameraColor válido: {cameraColor.IsValid()}"); }

                if (!cameraColor.IsValid())
                {
                    Debug.LogWarning("[ColorblindFeature] cameraColor no válido — ¿requiresIntermediateTexture activado?");
                    return;
                }

                var desc = renderGraph.GetTextureDesc(cameraColor);
                desc.name        = "_ColorblindTemp";
                desc.clearBuffer = false;
                var temp = renderGraph.CreateTexture(desc);

                using (var builder = renderGraph.AddUnsafePass<PassData>("Colorblind", out var passData, profilingSampler))
                {
                    passData.src = cameraColor;
                    passData.tmp = temp;
                    passData.mat = _mat;

                    builder.AllowPassCulling(false);
                    builder.UseTexture(cameraColor, AccessFlags.ReadWrite);
                    builder.UseTexture(temp,        AccessFlags.ReadWrite);

                    builder.SetRenderFunc(static (PassData data, UnsafeGraphContext ctx) =>
                    {
                        var cmd = CommandBufferHelpers.GetNativeCommandBuffer(ctx.cmd);
                        // Resolver TextureHandle → RTHandle → RenderTargetIdentifier
                        RTHandle srcRT = data.src;
                        RTHandle tmpRT = data.tmp;
                        // src → tmp con material (aplica la matriz de corrección de color)
                        cmd.Blit(srcRT.nameID, tmpRT.nameID, data.mat, 0);
                        // tmp → src (copia el resultado de vuelta al buffer de cámara)
                        cmd.Blit(tmpRT.nameID, srcRT.nameID);
                    });
                }
            }
        }
    }
}
