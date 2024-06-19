using Ryujinx.Graphics.GAL;
using SharpMetal.Foundation;
using SharpMetal.Metal;
using System;
using System.Buffers;
using System.Runtime.Versioning;

namespace Ryujinx.Graphics.Metal
{
    [SupportedOSPlatform("macos")]
    public class Texture : TextureBase, ITexture
    {
        public Texture(MTLDevice device, MetalRenderer renderer, Pipeline pipeline, TextureCreateInfo info) : base(device, renderer, pipeline, info)
        {
            var descriptor = new MTLTextureDescriptor
            {
                PixelFormat = FormatTable.GetFormat(Info.Format),
                Usage = MTLTextureUsage.Unknown,
                SampleCount = (ulong)Info.Samples,
                TextureType = Info.Target.Convert(),
                Width = (ulong)Info.Width,
                Height = (ulong)Info.Height,
                MipmapLevelCount = (ulong)Info.Levels
            };

            if (info.Target == Target.Texture3D)
            {
                descriptor.Depth = (ulong)Info.Depth;
            }
            else if (info.Target != Target.Cubemap)
            {
                descriptor.ArrayLength = (ulong)Info.Depth;
            }

            descriptor.Swizzle = GetSwizzle(info, descriptor.PixelFormat);

            _mtlTexture = _device.NewTexture(descriptor);
        }

        public Texture(MTLDevice device, MetalRenderer renderer, Pipeline pipeline, TextureCreateInfo info, MTLTexture sourceTexture, int firstLayer, int firstLevel) : base(device, renderer, pipeline, info)
        {
            var pixelFormat = FormatTable.GetFormat(Info.Format);
            var textureType = Info.Target.Convert();
            NSRange levels;
            levels.location = (ulong)firstLevel;
            levels.length = (ulong)Info.Levels;
            NSRange slices;
            slices.location = (ulong)firstLayer;
            slices.length = 1;

            if (info.Target != Target.Texture3D && info.Target != Target.Cubemap)
            {
                slices.length = (ulong)Info.Depth;
            }

            var swizzle = GetSwizzle(info, pixelFormat);

            _mtlTexture = sourceTexture.NewTextureView(pixelFormat, textureType, levels, slices, swizzle);
        }

        private MTLTextureSwizzleChannels GetSwizzle(TextureCreateInfo info, MTLPixelFormat pixelFormat)
        {
            var swizzleR = Info.SwizzleR.Convert();
            var swizzleG = Info.SwizzleG.Convert();
            var swizzleB = Info.SwizzleB.Convert();
            var swizzleA = Info.SwizzleA.Convert();

            if (info.Format == Format.R5G5B5A1Unorm ||
                info.Format == Format.R5G5B5X1Unorm ||
                info.Format == Format.R5G6B5Unorm)
            {
                (swizzleB, swizzleR) = (swizzleR, swizzleB);
            }
            else if (pixelFormat == MTLPixelFormat.ABGR4Unorm || info.Format == Format.A1B5G5R5Unorm)
            {
                var tempB = swizzleB;
                var tempA = swizzleA;

                swizzleB = swizzleG;
                swizzleA = swizzleR;
                swizzleR = tempA;
                swizzleG = tempB;
            }

            return new MTLTextureSwizzleChannels
            {
                red = swizzleR,
                green = swizzleG,
                blue = swizzleB,
                alpha = swizzleA
            };
        }

        public void CopyTo(ITexture destination, int firstLayer, int firstLevel)
        {
            var blitCommandEncoder = _pipeline.GetOrCreateBlitEncoder();

            if (destination is Texture destinationTexture)
            {
                if (destinationTexture.Info.Target == Target.Texture3D)
                {
                    blitCommandEncoder.CopyFromTexture(
                        _mtlTexture,
                        0,
                        (ulong)firstLevel,
                        new MTLOrigin { x = 0, y = 0, z = (ulong)firstLayer },
                        new MTLSize { width = (ulong)Math.Min(Info.Width, destinationTexture.Info.Width), height = (ulong)Math.Min(Info.Height, destinationTexture.Info.Height), depth = 1},
                        destinationTexture._mtlTexture,
                        0,
                        (ulong)firstLevel,
                        new MTLOrigin { x = 0, y = 0, z = (ulong)firstLayer });
                }
                else
                {
                    blitCommandEncoder.CopyFromTexture(
                        _mtlTexture,
                        (ulong)firstLayer,
                        (ulong)firstLevel,
                        destinationTexture._mtlTexture,
                        (ulong)firstLayer,
                        (ulong)firstLevel,
                        _mtlTexture.ArrayLength,
                        _mtlTexture.MipmapLevelCount);
                }
            }
        }

        public void CopyTo(ITexture destination, int srcLayer, int dstLayer, int srcLevel, int dstLevel)
        {
            var blitCommandEncoder = _pipeline.GetOrCreateBlitEncoder();

            if (destination is Texture destinationTexture)
            {
                if (destinationTexture.Info.Target == Target.Texture3D)
                {
                    blitCommandEncoder.CopyFromTexture(
                        _mtlTexture,
                        0,
                        (ulong)srcLevel,
                        new MTLOrigin { x = 0, y = 0, z = (ulong)srcLayer },
                        new MTLSize { width = (ulong)Math.Min(Info.Width, destinationTexture.Info.Width), height = (ulong)Math.Min(Info.Height, destinationTexture.Info.Height), depth = 1},
                        destinationTexture._mtlTexture,
                        0,
                        (ulong)dstLevel,
                        new MTLOrigin { x = 0, y = 0, z = (ulong)dstLayer });
                }
                else
                {
                    blitCommandEncoder.CopyFromTexture(
                        _mtlTexture,
                        (ulong)srcLayer,
                        (ulong)srcLevel,
                        destinationTexture._mtlTexture,
                        (ulong)dstLayer,
                        (ulong)dstLevel,
                        _mtlTexture.ArrayLength,
                        _mtlTexture.MipmapLevelCount);
                }
            }
        }

        public void CopyTo(ITexture destination, Extents2D srcRegion, Extents2D dstRegion, bool linearFilter)
        {
            _pipeline.BlitColor(this, destination, srcRegion, dstRegion, linearFilter);
        }

        public void CopyTo(BufferRange range, int layer, int level, int stride)
        {
            var blitCommandEncoder = _pipeline.GetOrCreateBlitEncoder();
            var cbs = _pipeline.CurrentCommandBuffer;

            int outSize = Info.GetMipSize(level);

            ulong bytesPerRow = (ulong)Info.GetMipStride(level);
            ulong bytesPerImage = 0;
            if (_mtlTexture.TextureType == MTLTextureType.Type3D)
            {
                bytesPerImage = bytesPerRow * (ulong)Info.Height;
            }

            var autoBuffer = _renderer.BufferManager.GetBuffer(range.Handle, true);
            var mtlBuffer = autoBuffer.Get(cbs, range.Offset, outSize).Value;

            blitCommandEncoder.CopyFromTexture(
                _mtlTexture,
                (ulong)layer,
                (ulong)level,
                new MTLOrigin(),
                new MTLSize { width = _mtlTexture.Width, height = _mtlTexture.Height, depth = _mtlTexture.Depth },
                mtlBuffer,
                (ulong)range.Offset,
                bytesPerRow,
                bytesPerImage);
        }

        public ITexture CreateView(TextureCreateInfo info, int firstLayer, int firstLevel)
        {
            return new Texture(_device, _renderer, _pipeline, info, _mtlTexture, firstLayer, firstLevel);
        }

        public PinnedSpan<byte> GetData()
        {
            throw new NotImplementedException();
        }

        public PinnedSpan<byte> GetData(int layer, int level)
        {
            var blitCommandEncoder = _pipeline.GetOrCreateBlitEncoder();

            ulong bytesPerRow = (ulong)Info.GetMipStride(level);
            ulong length = bytesPerRow * (ulong)Info.Height;
            ulong bytesPerImage = 0;
            if (_mtlTexture.TextureType == MTLTextureType.Type3D)
            {
                bytesPerImage = length;
            }

            unsafe
            {

                var mtlBuffer = _device.NewBuffer(length, MTLResourceOptions.ResourceStorageModeShared);

                blitCommandEncoder.CopyFromTexture(
                    _mtlTexture,
                    (ulong)layer,
                    (ulong)level,
                    new MTLOrigin(),
                    new MTLSize { width = _mtlTexture.Width, height = _mtlTexture.Height, depth = _mtlTexture.Depth },
                    mtlBuffer,
                    0,
                    bytesPerRow,
                    bytesPerImage
                );

                return new PinnedSpan<byte>(mtlBuffer.Contents.ToPointer(), (int)length, () => mtlBuffer.Dispose());
            }
        }

        public unsafe void SetData(IMemoryOwner<byte> data)
        {
            var blitCommandEncoder = _pipeline.GetOrCreateBlitEncoder();

            var dataSpan = data.Memory.Span;
            var mtlBuffer = _device.NewBuffer((ulong)dataSpan.Length, MTLResourceOptions.ResourceStorageModeShared);
            var bufferSpan = new Span<byte>(mtlBuffer.Contents.ToPointer(), dataSpan.Length);
            dataSpan.CopyTo(bufferSpan);

            int width = Info.Width;
            int height = Info.Height;
            int depth = Info.Depth;
            int levels = Info.GetLevelsClamped();
            int layers = Info.GetLayers();
            bool is3D = Info.Target == Target.Texture3D;

            int offset = 0;

            for (int level = 0; level < levels; level++)
            {
                int mipSize = Info.GetMipSize2D(level);
                int endOffset = offset + mipSize;

                if ((uint)endOffset > (uint)dataSpan.Length)
                {
                    return;
                }

                for (int layer = 0; layer < layers; layer++)
                {
                    blitCommandEncoder.CopyFromBuffer(
                        mtlBuffer,
                        (ulong)offset,
                        (ulong)Info.GetMipStride(level),
                        (ulong)mipSize,
                        new MTLSize { width = (ulong)width, height = (ulong)height, depth = is3D ? (ulong)depth : 1 },
                        _mtlTexture,
                        (ulong)layer,
                        (ulong)level,
                        new MTLOrigin()
                    );

                    offset += mipSize;
                }

                width = Math.Max(1, width >> 1);
                height = Math.Max(1, height >> 1);

                if (is3D)
                {
                    depth = Math.Max(1, depth >> 1);
                }
            }

            // Cleanup
            mtlBuffer.Dispose();
        }

        public void SetData(IMemoryOwner<byte> data, int layer, int level)
        {
            var blitCommandEncoder = _pipeline.GetOrCreateBlitEncoder();

            ulong bytesPerRow = (ulong)Info.GetMipStride(level);
            ulong bytesPerImage = 0;
            if (_mtlTexture.TextureType == MTLTextureType.Type3D)
            {
                bytesPerImage = bytesPerRow * (ulong)Info.Height;
            }

            unsafe
            {
                var dataSpan = data.Memory.Span;
                var mtlBuffer = _device.NewBuffer((ulong)dataSpan.Length, MTLResourceOptions.ResourceStorageModeShared);
                var bufferSpan = new Span<byte>(mtlBuffer.Contents.ToPointer(), dataSpan.Length);
                dataSpan.CopyTo(bufferSpan);

                blitCommandEncoder.CopyFromBuffer(
                    mtlBuffer,
                    0,
                    bytesPerRow,
                    bytesPerImage,
                    new MTLSize { width = _mtlTexture.Width, height = _mtlTexture.Height, depth = _mtlTexture.Depth },
                    _mtlTexture,
                    (ulong)layer,
                    (ulong)level,
                    new MTLOrigin()
                );

                // Cleanup
                mtlBuffer.Dispose();
            }
        }

        public void SetData(IMemoryOwner<byte> data, int layer, int level, Rectangle<int> region)
        {
            var blitCommandEncoder = _pipeline.GetOrCreateBlitEncoder();

            ulong bytesPerRow = (ulong)Info.GetMipStride(level);
            ulong bytesPerImage = 0;
            if (_mtlTexture.TextureType == MTLTextureType.Type3D)
            {
                bytesPerImage = bytesPerRow * (ulong)Info.Height;
            }

            unsafe
            {
                var dataSpan = data.Memory.Span;
                var mtlBuffer = _device.NewBuffer((ulong)dataSpan.Length, MTLResourceOptions.ResourceStorageModeShared);
                var bufferSpan = new Span<byte>(mtlBuffer.Contents.ToPointer(), dataSpan.Length);
                dataSpan.CopyTo(bufferSpan);

                blitCommandEncoder.CopyFromBuffer(
                    mtlBuffer,
                    0,
                    bytesPerRow,
                    bytesPerImage,
                    new MTLSize { width = (ulong)region.Width, height = (ulong)region.Height, depth = 1 },
                    _mtlTexture,
                    (ulong)layer,
                    (ulong)level,
                    new MTLOrigin { x = (ulong)region.X, y = (ulong)region.Y }
                );

                // Cleanup
                mtlBuffer.Dispose();
            }
        }

        public void SetStorage(BufferRange buffer)
        {
            throw new NotImplementedException();
        }
    }
}
