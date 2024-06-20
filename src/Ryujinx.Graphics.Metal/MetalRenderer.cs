using Ryujinx.Common.Configuration;
using Ryujinx.Common.Logging;
using Ryujinx.Graphics.GAL;
using Ryujinx.Graphics.Shader.Translation;
using SharpMetal.Metal;
using SharpMetal.QuartzCore;
using System;
using System.Runtime.Versioning;

namespace Ryujinx.Graphics.Metal
{
    [SupportedOSPlatform("macos")]
    public sealed class MetalRenderer : IRenderer
    {
        private readonly MTLDevice _device;
        private readonly MTLCommandQueue _queue;
        private readonly Func<CAMetalLayer> _getMetalLayer;

        private Pipeline _pipeline;
        private HelperShader _helperShader;
        private BufferManager _bufferManager;
        private Window _window;
        private CommandBufferPool _commandBufferPool;

        public event EventHandler<ScreenCaptureImageInfo> ScreenCaptured;
        public bool PreferThreading => true;
        public IPipeline Pipeline => _pipeline;
        public IWindow Window => _window;
        public HelperShader HelperShader => _helperShader;
        public BufferManager BufferManager => _bufferManager;
        public CommandBufferPool CommandBufferPool => _commandBufferPool;
        public Action<Action> InterruptAction { get; private set; }
        public SyncManager SyncManager { get; private set; }

        public MetalRenderer(Func<CAMetalLayer> metalLayer)
        {
            _device = MTLDevice.CreateSystemDefaultDevice();

            if (_device.ArgumentBuffersSupport != MTLArgumentBuffersTier.Tier2)
            {
                throw new NotSupportedException("Metal backend requires Tier 2 Argument Buffer support.");
            }

            _queue = _device.NewCommandQueue(CommandBufferPool.MaxCommandBuffers);
            _getMetalLayer = metalLayer;
        }

        public void Initialize(GraphicsDebugLevel logLevel)
        {
            var layer = _getMetalLayer();
            layer.Device = _device;
            layer.FramebufferOnly = false;

            _commandBufferPool = new CommandBufferPool(_device, _queue);
            _window = new Window(this, layer);
            _pipeline = new Pipeline(_device, this, _queue);
            _bufferManager = new BufferManager(_device, this, _pipeline);

            _pipeline.InitEncoderStateManager(_bufferManager);

            _helperShader = new HelperShader(_device, this, _pipeline);
            SyncManager = new SyncManager(this);
        }

        public void BackgroundContextAction(Action action, bool alwaysBackground = false)
        {
            Logger.Warning?.Print(LogClass.Gpu, "Not Implemented!");
        }

        public BufferHandle CreateBuffer(int size, BufferAccess access)
        {
            return _bufferManager.CreateWithHandle(size);
        }

        public BufferHandle CreateBuffer(IntPtr pointer, int size)
        {
            return _bufferManager.Create(pointer, size);
        }

        public BufferHandle CreateBufferSparse(ReadOnlySpan<BufferRange> storageBuffers)
        {
            throw new NotImplementedException();
        }

        public IImageArray CreateImageArray(int size, bool isBuffer)
        {
            throw new NotImplementedException();
        }

        public IProgram CreateProgram(ShaderSource[] shaders, ShaderInfo info)
        {
            return new Program(shaders, _device);
        }

        public ISampler CreateSampler(SamplerCreateInfo info)
        {
            return new Sampler(_device, info);
        }

        public ITexture CreateTexture(TextureCreateInfo info)
        {
            if (info.Target == Target.TextureBuffer)
            {
                return new TextureBuffer(this, info);
            }

            return new Texture(_device, this, _pipeline, info);
        }

        public ITextureArray CreateTextureArray(int size, bool isBuffer)
        {
            throw new NotImplementedException();
        }

        public bool PrepareHostMapping(IntPtr address, ulong size)
        {
            // TODO: Metal Host Mapping
            return false;
        }

        public void CreateSync(ulong id, bool strict)
        {
            SyncManager.Create(id, strict);
        }

        public void DeleteBuffer(BufferHandle buffer)
        {
            _bufferManager.Delete(buffer);
        }

        public PinnedSpan<byte> GetBufferData(BufferHandle buffer, int offset, int size)
        {
            return _bufferManager.GetData(buffer, offset, size);
        }

        public Capabilities GetCapabilities()
        {
            // TODO: Finalize these values
            return new Capabilities(
                api: TargetApi.Metal,
                vendorName: HardwareInfoTools.GetVendor(),
                SystemMemoryType.UnifiedMemory,
                hasFrontFacingBug: false,
                hasVectorIndexingBug: false,
                needsFragmentOutputSpecialization: true,
                reduceShaderPrecision: true,
                supportsAstcCompression: true,
                supportsBc123Compression: true,
                supportsBc45Compression: true,
                supportsBc67Compression: true,
                supportsEtc2Compression: true,
                supports3DTextureCompression: true,
                supportsBgraFormat: true,
                supportsR4G4Format: false,
                supportsR4G4B4A4Format: true,
                supportsScaledVertexFormats: false,
                supportsSnormBufferTextureFormat: true,
                supportsSparseBuffer: false,
                supports5BitComponentFormat: true,
                supportsBlendEquationAdvanced: false,
                supportsFragmentShaderInterlock: true,
                supportsFragmentShaderOrderingIntel: false,
                supportsGeometryShader: false,
                supportsGeometryShaderPassthrough: false,
                supportsTransformFeedback: false,
                supportsImageLoadFormatted: false,
                supportsLayerVertexTessellation: false,
                supportsMismatchingViewFormat: true,
                supportsCubemapView: true,
                supportsNonConstantTextureOffset: false,
                supportsQuads: false,
                // TODO: Metal Bindless Support
                supportsSeparateSampler: false,
                supportsShaderBallot: false,
                supportsShaderBarrierDivergence: false,
                supportsShaderFloat64: false,
                supportsTextureGatherOffsets: false,
                supportsTextureShadowLod: false,
                supportsVertexStoreAndAtomics: false,
                supportsViewportIndexVertexTessellation: false,
                supportsViewportMask: false,
                supportsViewportSwizzle: false,
                supportsIndirectParameters: true,
                supportsDepthClipControl: false,
                uniformBufferSetIndex: 0,
                storageBufferSetIndex: 1,
                textureSetIndex: 2,
                imageSetIndex: 3,
                extraSetBaseIndex: 0,
                maximumExtraSets: 0,
                maximumUniformBuffersPerStage: Constants.MaxUniformBuffersPerStage,
                maximumStorageBuffersPerStage: Constants.MaxStorageBuffersPerStage,
                maximumTexturesPerStage: Constants.MaxTexturesPerStage,
                maximumImagesPerStage: Constants.MaxTextureBindings,
                maximumComputeSharedMemorySize: (int)_device.MaxThreadgroupMemoryLength,
                maximumSupportedAnisotropy: 0,
                shaderSubgroupSize: 256,
                storageBufferOffsetAlignment: 16,
                textureBufferOffsetAlignment: 16,
                gatherBiasPrecision: 0
            );
        }

        public ulong GetCurrentSync()
        {
            return SyncManager.GetCurrent();
        }

        public HardwareInfo GetHardwareInfo()
        {
            return new HardwareInfo(HardwareInfoTools.GetVendor(), HardwareInfoTools.GetModel(), "Apple");
        }

        public IProgram LoadProgramBinary(byte[] programBinary, bool hasFragmentShader, ShaderInfo info)
        {
            throw new NotImplementedException();
        }

        public void SetBufferData(BufferHandle buffer, int offset, ReadOnlySpan<byte> data)
        {
            _bufferManager.SetData(buffer, offset, data, _pipeline.CurrentCommandBuffer, _pipeline.EndRenderPassDelegate);
        }

        public void UpdateCounters()
        {
            // https://developer.apple.com/documentation/metal/gpu_counters_and_counter_sample_buffers/creating_a_counter_sample_buffer_to_store_a_gpu_s_counter_data_during_a_pass?language=objc
        }

        public void PreFrame()
        {
            SyncManager.Cleanup();
        }

        public ICounterEvent ReportCounter(CounterType type, EventHandler<ulong> resultHandler, float divisor, bool hostReserved)
        {
            // https://developer.apple.com/documentation/metal/gpu_counters_and_counter_sample_buffers/creating_a_counter_sample_buffer_to_store_a_gpu_s_counter_data_during_a_pass?language=objc
            var counterEvent = new CounterEvent();
            resultHandler?.Invoke(counterEvent, type == CounterType.SamplesPassed ? (ulong)1 : 0);
            return counterEvent;
        }

        public void ResetCounter(CounterType type)
        {
            // https://developer.apple.com/documentation/metal/gpu_counters_and_counter_sample_buffers/creating_a_counter_sample_buffer_to_store_a_gpu_s_counter_data_during_a_pass?language=objc
        }

        public void WaitSync(ulong id)
        {
            SyncManager.Wait(id);
        }

        public void FlushAllCommands()
        {
            _pipeline.FlushCommandsImpl();
        }

        public void RegisterFlush()
        {
            SyncManager.RegisterFlush();

            // Periodically free unused regions of the staging buffer to avoid doing it all at once.
            _bufferManager.StagingBuffer.FreeCompleted();
        }

        public void SetInterruptAction(Action<Action> interruptAction)
        {
            InterruptAction = interruptAction;
        }

        public void Screenshot()
        {
            // TODO: Screenshots
        }

        public void Dispose()
        {
            _pipeline.Dispose();
            _window.Dispose();
        }
    }
}
