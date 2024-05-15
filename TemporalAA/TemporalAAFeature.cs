/*
MIT License

Copyright (c) 2022 Pascal Zwick

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Unity.Mathematics;

public class TemporalAAFeature : ScriptableRendererFeature
{
    static readonly double2[] Halton2364Seq =
        {
        new(0, 0), new(0.5, 0.3333333333333333), new(0.25, 0.6666666666666666),
        new(0.75, 0.1111111111111111), new double2(0.125, 0.4444444444444444),
        new double2(0.625, 0.7777777777777777), new double2(0.375, 0.2222222222222222),
        new double2(0.875, 0.5555555555555556), new double2(0.0625, 0.8888888888888888),
        new double2(0.5625, 0.037037037037037035), new double2(0.3125, 0.37037037037037035),
        new double2(0.8125, 0.7037037037037037), new double2(0.1875, 0.14814814814814814),
        new double2(0.6875, 0.48148148148148145), new double2(0.4375, 0.8148148148148147),
        new double2(0.9375, 0.25925925925925924), new double2(0.03125, 0.5925925925925926),
        new double2(0.53125, 0.9259259259259258), new double2(0.28125, 0.07407407407407407),
        new double2(0.78125, 0.4074074074074074), new double2(0.15625, 0.7407407407407407),
        new double2(0.65625, 0.18518518518518517), new double2(0.40625, 0.5185185185185185),
        new double2(0.90625, 0.8518518518518517), new double2(0.09375, 0.2962962962962963),
        new double2(0.59375, 0.6296296296296297), new double2(0.34375, 0.9629629629629629),
        new double2(0.84375, 0.012345679012345678), new double2(0.21875, 0.345679012345679),
        new double2(0.71875, 0.6790123456790123), new double2(0.46875, 0.12345679012345678),
        new double2(0.96875, 0.4567901234567901), new double2(0.015625, 0.7901234567901234),
        new double2(0.515625, 0.2345679012345679), new double2(0.265625, 0.5679012345679013),
        new double2(0.765625, 0.9012345679012346), new double2(0.140625, 0.04938271604938271),
        new double2(0.640625, 0.38271604938271603), new double2(0.390625, 0.7160493827160495),
        new double2(0.890625, 0.16049382716049382), new double2(0.078125, 0.49382716049382713),
        new(0.49609375, 0.7050754458161865)};

    [Range(0, 1)]
    public float TemporalFade = 0.95f;
    public float MovementBlending = 100;

    [Tooltip("If the resulting image appears upside down. Toggle this variable to unflip the image.")]
    public bool UseFlipUV = false;

    public Material TAAMaterial;
    
    public float JitterSpread = 1;
    [Range(1, 256)]
    public int HaltonLength = 8;

    class TemporalAAPass : ScriptableRenderPass
    {

        [Range(0, 1)]
        public float TemporalFade;
        public float MovementBlending;
        public bool UseFlipUV;

        public Material TAAMaterial;
        
        public float JitterSpread = 1;
        [Range(1, 256)]
        public int HaltonLength = 8;

        public static RenderTexture temp, temp1;

        private Matrix4x4 prevViewProjectionMatrix;
        
        private UnityEngine.Camera prevRenderCamera;

        private TemporalAAFeature feature;

        public TemporalAAPass(TemporalAAFeature inFeature) : base()
        {
            if (temp)
            {
                temp.Release();
                temp1.Release();
                temp = null;
            }

            feature = inFeature;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            base.OnCameraSetup(cmd, ref renderingData);

            if (renderingData.cameraData.cameraType != CameraType.Game)
                return;
            
            prevRenderCamera = renderingData.cameraData.camera;

            ConfigureInput(ScriptableRenderPassInput.Color);
            ConfigureInput(ScriptableRenderPassInput.Depth);

            RenderTextureDescriptor currentCameraDescriptor = renderingData.cameraData.cameraTargetDescriptor;

            if (temp && (currentCameraDescriptor.width != temp.width || currentCameraDescriptor.height != temp.height))
            {
                Debug.Log("Deleting Render Target: " + currentCameraDescriptor.width + " " + temp.width);

                temp.Release();
                temp1.Release();
                temp = null;
            }

            if (!temp)
            {
                //RenderTextureFormat.DefaultHDR
                temp = RenderTexture.GetTemporary(currentCameraDescriptor.width, currentCameraDescriptor.height, 0, RenderTextureFormat.DefaultHDR);
                temp1 = RenderTexture.GetTemporary(currentCameraDescriptor.width, currentCameraDescriptor.height, 0, RenderTextureFormat.DefaultHDR);

                //temp = new RenderTexture(currentCameraDescriptor.width, currentCameraDescriptor.height, UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_SFloat, 0);
                //temp1 = new RenderTexture(currentCameraDescriptor.width, currentCameraDescriptor.height, UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_SFloat, 0);

                Debug.Log("Allocating new Render Target");
            }
        }

        // Here you can implement the rendering logic.
        // Use <c>ScriptableRenderContext</c> to issue drawing commands or execute command buffers
        // https://docs.unity3d.com/ScriptReference/Rendering.ScriptableRenderContext.html
        // You don't have to call ScriptableRenderContext.submit, the render pipeline will call it at specific points in the pipeline.
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            
            if (renderingData.cameraData.cameraType != CameraType.Game)
                return;

            UpdateValuesFromFeature();
            CommandBuffer cmd = CommandBufferPool.Get("TemporalAAPass");


            TAAMaterial.SetTexture("_TemporalAATexture", temp);


            Matrix4x4 mt = renderingData.cameraData.camera.nonJitteredProjectionMatrix.inverse;
            TAAMaterial.SetMatrix("_invP", mt);

            mt = this.prevViewProjectionMatrix * renderingData.cameraData.camera.cameraToWorldMatrix;
            TAAMaterial.SetMatrix("_FrameMatrix", mt);

            TAAMaterial.SetFloat("_TemporalFade", TemporalFade);
            TAAMaterial.SetFloat("_MovementBlending", MovementBlending);
            TAAMaterial.SetInt("_UseFlipUV", UseFlipUV ? 1 : 0);

            Blit(cmd, BuiltinRenderTextureType.CurrentActive, temp1, TAAMaterial);

            Blit(cmd, temp1, renderingData.cameraData.renderer.cameraColorTarget);


            //Ping pong
            RenderTexture temp2 = temp;
            temp = temp1;
            temp1 = temp2;

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);


            this.prevViewProjectionMatrix = renderingData.cameraData.camera.nonJitteredProjectionMatrix * renderingData.cameraData.camera.worldToCameraMatrix;

            renderingData.cameraData.camera.ResetProjectionMatrix();
        }

        private void UpdateValuesFromFeature()
        {
            TemporalFade = feature.TemporalFade;
            MovementBlending = feature.MovementBlending;
            HaltonLength = feature.HaltonLength;
            JitterSpread = feature.JitterSpread;
        }

        /// Cleanup any allocated resources that were created during the execution of this render pass.
        public override void FrameCleanup(CommandBuffer cmd)
        {
            base.FrameCleanup(cmd);
            var cam = prevRenderCamera;
            if (!cam) {
                return;
            }
                
            cam.ResetWorldToCameraMatrix();
            cam.ResetProjectionMatrix();
                
            cam.nonJitteredProjectionMatrix = cam.projectionMatrix;
                
            Matrix4x4 p = cam.projectionMatrix;
            float2 jitter = (float2)(2 * Halton2364Seq[Time.frameCount % HaltonLength] - 1) * JitterSpread;
            p.m02 = jitter.x / (float)Screen.width;
            p.m12 = jitter.y / (float)Screen.height;
            cam.projectionMatrix = p;
        }
    }

    TemporalAAPass m_temporalPass;

    public override void Create()
    {
        m_temporalPass = new TemporalAAPass(this);
        m_temporalPass.TemporalFade = TemporalFade;
        m_temporalPass.MovementBlending = MovementBlending;
        m_temporalPass.TAAMaterial = TAAMaterial;
        m_temporalPass.HaltonLength = HaltonLength;
        m_temporalPass.JitterSpread = JitterSpread;
        m_temporalPass.UseFlipUV = this.UseFlipUV;

        // Configures where the render pass should be injected.
        m_temporalPass.renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
    }

    // Here you can inject one or multiple render passes in the renderer.
    // This method is called when setting up the renderer once per-camera.
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(m_temporalPass);
    }
}
