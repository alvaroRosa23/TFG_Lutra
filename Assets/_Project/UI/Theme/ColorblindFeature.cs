using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using Lutra.Core.Data.Models;

namespace Lutra.UI.Theme
{
    /// <summary>
    /// Renderer Feature que aplica una corrección de color de daltonismo (daltonización) sobre el framebuffer.
    /// Solo afecta a lo que dibuja la cámara: los Canvas deben estar en Screen Space - Camera
    /// (un Canvas en Screen Space - Overlay se dibuja después de URP y no pasa por este filtro).
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

        // ── Daltonización ────────────────────────────────────────────────
        // El shader simula cómo ve el color la persona (matriz Sim), calcula lo que pierde
        // (original − simulado) y lo redistribuye a los canales que sí distingue (matriz Shift).
        //
        // Simulación: Machado, Oliveira y Fernandes (2009), severidad 1.0, en RGB lineal
        // (el proyecto usa espacio de color Linear). Redistribución: método de Fidaner, Lin y
        // Ozguven (2005) — en protanopia y deuteranopia el error del rojo pasa al verde y al azul;
        // en tritanopia, por simetría, el error del azul pasa al rojo y al verde.
        // Nota: las matrices anteriores (0.625, 0.375…) eran de SIMULACIÓN y no corregían nada.

        private static readonly Vector3 Prot_SimR = new( 0.152286f,  1.052583f, -0.204868f);
        private static readonly Vector3 Prot_SimG = new( 0.114503f,  0.786281f,  0.099216f);
        private static readonly Vector3 Prot_SimB = new(-0.003882f, -0.048116f,  1.051998f);

        private static readonly Vector3 Deut_SimR = new( 0.367322f,  0.860646f, -0.227968f);
        private static readonly Vector3 Deut_SimG = new( 0.280085f,  0.672501f,  0.047413f);
        private static readonly Vector3 Deut_SimB = new(-0.011820f,  0.042940f,  0.968881f);

        private static readonly Vector3 Trit_SimR = new( 1.255528f, -0.076749f, -0.178779f);
        private static readonly Vector3 Trit_SimG = new(-0.078411f,  0.930809f,  0.147602f);
        private static readonly Vector3 Trit_SimB = new( 0.004733f,  0.691367f,  0.304143f);

        // Rojo-verde (protanopia y deuteranopia): el error del rojo se reparte en verde y azul
        private static readonly Vector3 RedGreen_ShiftR = new(0.0f, 0.0f, 0.0f);
        private static readonly Vector3 RedGreen_ShiftG = new(0.7f, 1.0f, 0.0f);
        private static readonly Vector3 RedGreen_ShiftB = new(0.7f, 0.0f, 1.0f);

        // Azul-amarillo (tritanopia): el error del azul se reparte en rojo y verde
        private static readonly Vector3 BlueYellow_ShiftR = new(1.0f, 0.0f, 0.7f);
        private static readonly Vector3 BlueYellow_ShiftG = new(0.0f, 1.0f, 0.7f);
        private static readonly Vector3 BlueYellow_ShiftB = new(0.0f, 0.0f, 0.0f);

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
                case ColorblindMode.Protanopia:
                    _setMatrices(Prot_SimR, Prot_SimG, Prot_SimB, RedGreen_ShiftR, RedGreen_ShiftG, RedGreen_ShiftB);
                    break;
                case ColorblindMode.Deuteranopia:
                    _setMatrices(Deut_SimR, Deut_SimG, Deut_SimB, RedGreen_ShiftR, RedGreen_ShiftG, RedGreen_ShiftB);
                    break;
                case ColorblindMode.Tritanopia:
                    _setMatrices(Trit_SimR, Trit_SimG, Trit_SimB, BlueYellow_ShiftR, BlueYellow_ShiftG, BlueYellow_ShiftB);
                    break;
            }
        }

        private void _setMatrices(Vector3 simR, Vector3 simG, Vector3 simB, Vector3 shiftR, Vector3 shiftG, Vector3 shiftB)
        {
            _material.SetVector("_SimR", simR);
            _material.SetVector("_SimG", simG);
            _material.SetVector("_SimB", simB);
            _material.SetVector("_ShiftR", shiftR);
            _material.SetVector("_ShiftG", shiftG);
            _material.SetVector("_ShiftB", shiftB);
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
