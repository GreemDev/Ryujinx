using OpenTK.Graphics.OpenGL;
using Ryujinx.Common;
using Ryujinx.Graphics.GAL;
using System;

namespace Ryujinx.Graphics.OpenGL.Image
{
    class TextureCopy : IDisposable
    {
        private readonly OpenGLRenderer _renderer;

        private int _srcFramebuffer;
        private int _dstFramebuffer;

        private int _copyPboHandle;
        private int _copyPboSize;

        public IntermediatePool IntermediatePool { get; }

        public TextureCopy(OpenGLRenderer renderer)
        {
            _renderer = renderer;
            IntermediatePool = new IntermediatePool(renderer);
        }

        public void Copy(
            TextureView src,
            TextureView dst,
            Extents2D srcRegion,
            Extents2D dstRegion,
            bool linearFilter,
            int srcLayer = 0,
            int dstLayer = 0,
            int srcLevel = 0,
            int dstLevel = 0)
        {
            int levels = Math.Min(src.Info.Levels - srcLevel, dst.Info.Levels - dstLevel);
            int layers = Math.Min(src.Info.GetLayers() - srcLayer, dst.Info.GetLayers() - dstLayer);

            Copy(src, dst, srcRegion, dstRegion, linearFilter, srcLayer, dstLayer, srcLevel, dstLevel, layers, levels);
        }

        public void Copy(
            TextureView src,
            TextureView dst,
            Extents2D srcRegion,
            Extents2D dstRegion,
            bool linearFilter,
            int srcLayer,
            int dstLayer,
            int srcLevel,
            int dstLevel,
            int layers,
            int levels)
        {
            TextureView srcConverted = src.Format.IsBgr() != dst.Format.IsBgr() ? BgraSwap(src) : src;

            (int oldDrawFramebufferHandle, int oldReadFramebufferHandle) = ((Pipeline)_renderer.Pipeline).GetBoundFramebuffers();

            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, GetSrcFramebufferLazy());
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, GetDstFramebufferLazy());

            if (srcLevel != 0)
            {
                srcRegion = srcRegion.Reduce(srcLevel);
            }

            if (dstLevel != 0)
            {
                dstRegion = dstRegion.Reduce(dstLevel);
            }

            for (int level = 0; level < levels; level++)
            {
                for (int layer = 0; layer < layers; layer++)
                {
                    if ((srcLayer | dstLayer) != 0 || layers > 1)
                    {
                        Attach(FramebufferTarget.ReadFramebuffer, src.Format, srcConverted.Handle, srcLevel + level, srcLayer + layer);
                        Attach(FramebufferTarget.DrawFramebuffer, dst.Format, dst.Handle, dstLevel + level, dstLayer + layer);
                    }
                    else
                    {
                        Attach(FramebufferTarget.ReadFramebuffer, src.Format, srcConverted.Handle, srcLevel + level);
                        Attach(FramebufferTarget.DrawFramebuffer, dst.Format, dst.Handle, dstLevel + level);
                    }

                    ClearBufferMask mask = GetMask(src.Format);

                    if ((mask & (ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit)) != 0 || src.Format.IsInteger())
                    {
                        linearFilter = false;
                    }

                    BlitFramebufferFilter filter = linearFilter
                        ? BlitFramebufferFilter.Linear
                        : BlitFramebufferFilter.Nearest;

                    GL.ReadBuffer(ReadBufferMode.ColorAttachment0);
                    GL.DrawBuffer(DrawBufferMode.ColorAttachment0);

                    GL.Disable(EnableCap.RasterizerDiscard);
                    GL.Disable(IndexedEnableCap.ScissorTest, 0);

                    GL.BlitFramebuffer(
                        srcRegion.X1,
                        srcRegion.Y1,
                        srcRegion.X2,
                        srcRegion.Y2,
                        dstRegion.X1,
                        dstRegion.Y1,
                        dstRegion.X2,
                        dstRegion.Y2,
                        mask,
                        filter);
                }

                if (level < levels - 1)
                {
                    srcRegion = srcRegion.Reduce(1);
                    dstRegion = dstRegion.Reduce(1);
                }
            }

            Attach(FramebufferTarget.ReadFramebuffer, src.Format, 0);
            Attach(FramebufferTarget.DrawFramebuffer, dst.Format, 0);

            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, oldReadFramebufferHandle);
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, oldDrawFramebufferHandle);

            ((Pipeline)_renderer.Pipeline).RestoreScissor0Enable();
            ((Pipeline)_renderer.Pipeline).RestoreRasterizerDiscard();

            if (srcConverted != src)
            {
                srcConverted.Dispose();
            }
        }

        public void CopyUnscaled(
            ITextureInfo src,
            ITextureInfo dst,
            int srcLayer,
            int dstLayer,
            int srcLevel,
            int dstLevel)
        {
            TextureCreateInfo srcInfo = src.Info;
            TextureCreateInfo dstInfo = dst.Info;

            int srcDepth = srcInfo.GetDepthOrLayers();
            int srcLevels = srcInfo.Levels;

            int dstDepth = dstInfo.GetDepthOrLayers();
            int dstLevels = dstInfo.Levels;

            if (dstInfo.Target == Target.Texture3D)
            {
                dstDepth = Math.Max(1, dstDepth >> dstLevel);
            }

            int depth = Math.Min(srcDepth, dstDepth);
            int levels = Math.Min(srcLevels, dstLevels);

            CopyUnscaled(src, dst, srcLayer, dstLayer, srcLevel, dstLevel, depth, levels);
        }

        public void CopyUnscaled(
            ITextureInfo src,
            ITextureInfo dst,
            int srcLayer,
            int dstLayer,
            int srcLevel,
            int dstLevel,
            int depth,
            int levels)
        {
            TextureCreateInfo srcInfo = src.Info;
            TextureCreateInfo dstInfo = dst.Info;

            int srcHandle = src.Handle;
            int dstHandle = dst.Handle;

            int srcWidth = srcInfo.Width;
            int srcHeight = srcInfo.Height;

            int dstWidth = dstInfo.Width;
            int dstHeight = dstInfo.Height;

            srcWidth = Math.Max(1, srcWidth >> srcLevel);
            srcHeight = Math.Max(1, srcHeight >> srcLevel);

            dstWidth = Math.Max(1, dstWidth >> dstLevel);
            dstHeight = Math.Max(1, dstHeight >> dstLevel);

            int blockWidth = 1;
            int blockHeight = 1;
            bool sizeInBlocks = false;

            // When copying from a compressed to a non-compressed format,
            // the non-compressed texture will have the size of the texture
            // in blocks (not in texels), so we must adjust that size to
            // match the size in texels of the compressed texture.
            if (!srcInfo.IsCompressed && dstInfo.IsCompressed)
            {
                srcWidth *= dstInfo.BlockWidth;
                srcHeight *= dstInfo.BlockHeight;
                blockWidth = dstInfo.BlockWidth;
                blockHeight = dstInfo.BlockHeight;

                sizeInBlocks = true;
            }
            else if (srcInfo.IsCompressed && !dstInfo.IsCompressed)
            {
                dstWidth *= srcInfo.BlockWidth;
                dstHeight *= srcInfo.BlockHeight;
                blockWidth = srcInfo.BlockWidth;
                blockHeight = srcInfo.BlockHeight;
            }

            int width = Math.Min(srcWidth, dstWidth);
            int height = Math.Min(srcHeight, dstHeight);

            for (int level = 0; level < levels; level++)
            {
                // Stop copy if we are already out of the levels range.
                if (level >= srcInfo.Levels || dstLevel + level >= dstInfo.Levels)
                {
                    break;
                }

                if ((width % blockWidth != 0 || height % blockHeight != 0) && src is TextureView srcView && dst is TextureView dstView)
                {
                    PboCopy(srcView, dstView, srcLayer, dstLayer, srcLevel + level, dstLevel + level, width, height);
                }
                else
                {
                    int copyWidth = sizeInBlocks ? BitUtils.DivRoundUp(width, blockWidth) : width;
                    int copyHeight = sizeInBlocks ? BitUtils.DivRoundUp(height, blockHeight) : height;

                    if (HwCapabilities.Vendor == HwCapabilities.GpuVendor.IntelWindows)
                    {
                        GL.CopyImageSubData(
                            src.Storage.Handle,
                            src.Storage.Info.Target.ConvertToImageTarget(),
                            src.FirstLevel + srcLevel + level,
                            0,
                            0,
                            src.FirstLayer + srcLayer,
                            dst.Storage.Handle,
                            dst.Storage.Info.Target.ConvertToImageTarget(),
                            dst.FirstLevel + dstLevel + level,
                            0,
                            0,
                            dst.FirstLayer + dstLayer,
                            copyWidth,
                            copyHeight,
                            depth);
                    }
                    else
                    {
                        GL.CopyImageSubData(
                            srcHandle,
                            srcInfo.Target.ConvertToImageTarget(),
                            srcLevel + level,
                            0,
                            0,
                            srcLayer,
                            dstHandle,
                            dstInfo.Target.ConvertToImageTarget(),
                            dstLevel + level,
                            0,
                            0,
                            dstLayer,
                            copyWidth,
                            copyHeight,
                            depth);
                    }
                }

                width = Math.Max(1, width >> 1);
                height = Math.Max(1, height >> 1);

                if (srcInfo.Target == Target.Texture3D)
                {
                    depth = Math.Max(1, depth >> 1);
                }
            }
        }

        private static FramebufferAttachment AttachmentForFormat(Format format)
        {
            if (format == Format.D24UnormS8Uint || format == Format.D32FloatS8Uint)
            {
                return FramebufferAttachment.DepthStencilAttachment;
            }
            else if (FormatTable.IsDepthOnly(format))
            {
                return FramebufferAttachment.DepthAttachment;
            }
            else if (format == Format.S8Uint)
            {
                return FramebufferAttachment.StencilAttachment;
            }
            else
            {
                return FramebufferAttachment.ColorAttachment0;
            }
        }

        private static void Attach(FramebufferTarget target, Format format, int handle, int level = 0)
        {
            FramebufferAttachment attachment = AttachmentForFormat(format);

            GL.FramebufferTexture(target, attachment, handle, level);
        }

        private static void Attach(FramebufferTarget target, Format format, int handle, int level, int layer)
        {
            FramebufferAttachment attachment = AttachmentForFormat(format);

            GL.FramebufferTextureLayer(target, attachment, handle, level, layer);
        }

        private static ClearBufferMask GetMask(Format format)
        {
            if (FormatTable.IsPackedDepthStencil(format))
            {
                return ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit;
            }
            else if (FormatTable.IsDepthOnly(format))
            {
                return ClearBufferMask.DepthBufferBit;
            }
            else if (format == Format.S8Uint)
            {
                return ClearBufferMask.StencilBufferBit;
            }
            else
            {
                return ClearBufferMask.ColorBufferBit;
            }
        }

        public TextureView BgraSwap(TextureView from)
        {
            TextureView to = (TextureView)_renderer.CreateTexture(from.Info);

            EnsurePbo(from);

            GL.BindBuffer(BufferTarget.PixelPackBuffer, _copyPboHandle);

            from.WriteToPbo(0, forceBgra: true);

            GL.BindBuffer(BufferTarget.PixelPackBuffer, 0);
            GL.BindBuffer(BufferTarget.PixelUnpackBuffer, _copyPboHandle);

            to.ReadFromPbo(0, _copyPboSize);

            GL.BindBuffer(BufferTarget.PixelUnpackBuffer, 0);

            return to;
        }

        public void PboCopy(TextureView from, TextureView to, int srcLayer, int dstLayer, int srcLevel, int dstLevel, int width, int height)
        {
            int dstWidth = width;
            int dstHeight = height;

            // The size of the source texture.
            int unpackWidth = from.Width;
            int unpackHeight = from.Height;

            if (from.Info.IsCompressed != to.Info.IsCompressed)
            {
                if (from.Info.IsCompressed)
                {
                    // Dest size is in pixels, but should be in blocks
                    dstWidth = BitUtils.DivRoundUp(width, from.Info.BlockWidth);
                    dstHeight = BitUtils.DivRoundUp(height, from.Info.BlockHeight);

                    // When copying from a compressed texture, the source size must be taken in blocks for unpacking to the uncompressed block texture.
                    unpackWidth = BitUtils.DivRoundUp(from.Info.Width, from.Info.BlockWidth);
                    unpackHeight = BitUtils.DivRoundUp(from.Info.Height, from.Info.BlockHeight);
                }
                else
                {
                    // When copying to a compressed texture, the source size must be scaled by the block width for unpacking on the compressed target.
                    unpackWidth = from.Info.Width * to.Info.BlockWidth;
                    unpackHeight = from.Info.Height * to.Info.BlockHeight;
                }
            }

            EnsurePbo(from);

            GL.BindBuffer(BufferTarget.PixelPackBuffer, _copyPboHandle);

            // The source texture is written out in full, then the destination is taken as a slice from the data using unpack params.
            // The offset points to the base at which the requested layer is at.

            int offset = from.WriteToPbo2D(0, srcLayer, srcLevel);

            // If the destination size is not an exact match for the source unpack parameters, we need to set them to slice the data correctly.

            bool slice = (unpackWidth != dstWidth || unpackHeight != dstHeight);

            if (slice)
            {
                // Set unpack parameters to take a slice of width/height:
                GL.PixelStore(PixelStoreParameter.UnpackRowLength, unpackWidth);
                GL.PixelStore(PixelStoreParameter.UnpackImageHeight, unpackHeight);

                if (to.Info.IsCompressed)
                {
                    GL.PixelStore(PixelStoreParameter.UnpackCompressedBlockWidth, to.Info.BlockWidth);
                    GL.PixelStore(PixelStoreParameter.UnpackCompressedBlockHeight, to.Info.BlockHeight);
                    GL.PixelStore(PixelStoreParameter.UnpackCompressedBlockDepth, 1);
                    GL.PixelStore(PixelStoreParameter.UnpackCompressedBlockSize, to.Info.BytesPerPixel);
                }
            }

            GL.BindBuffer(BufferTarget.PixelPackBuffer, 0);
            GL.BindBuffer(BufferTarget.PixelUnpackBuffer, _copyPboHandle);

            to.ReadFromPbo2D(offset, dstLayer, dstLevel, dstWidth, dstHeight);

            if (slice)
            {
                // Reset unpack parameters
                GL.PixelStore(PixelStoreParameter.UnpackRowLength, 0);
                GL.PixelStore(PixelStoreParameter.UnpackImageHeight, 0);

                if (to.Info.IsCompressed)
                {
                    GL.PixelStore(PixelStoreParameter.UnpackCompressedBlockWidth, 0);
                    GL.PixelStore(PixelStoreParameter.UnpackCompressedBlockHeight, 0);
                    GL.PixelStore(PixelStoreParameter.UnpackCompressedBlockDepth, 0);
                    GL.PixelStore(PixelStoreParameter.UnpackCompressedBlockSize, 0);
                }
            }

            GL.BindBuffer(BufferTarget.PixelUnpackBuffer, 0);
        }

        private void EnsurePbo(TextureView view)
        {
            int requiredSize = 0;

            for (int level = 0; level < view.Info.Levels; level++)
            {
                requiredSize += view.Info.GetMipSize(level);
            }

            if (_copyPboSize < requiredSize && _copyPboHandle != 0)
            {
                GL.DeleteBuffer(_copyPboHandle);

                _copyPboHandle = 0;
            }

            if (_copyPboHandle == 0)
            {
                _copyPboHandle = GL.GenBuffer();
                _copyPboSize = requiredSize;

                GL.BindBuffer(BufferTarget.PixelPackBuffer, _copyPboHandle);
                GL.BufferData(BufferTarget.PixelPackBuffer, requiredSize, nint.Zero, BufferUsageHint.DynamicCopy);
            }
        }

        private int GetSrcFramebufferLazy()
        {
            if (_srcFramebuffer == 0)
            {
                _srcFramebuffer = GL.GenFramebuffer();
            }

            return _srcFramebuffer;
        }

        private int GetDstFramebufferLazy()
        {
            if (_dstFramebuffer == 0)
            {
                _dstFramebuffer = GL.GenFramebuffer();
            }

            return _dstFramebuffer;
        }

        public void Dispose()
        {
            if (_srcFramebuffer != 0)
            {
                GL.DeleteFramebuffer(_srcFramebuffer);

                _srcFramebuffer = 0;
            }

            if (_dstFramebuffer != 0)
            {
                GL.DeleteFramebuffer(_dstFramebuffer);

                _dstFramebuffer = 0;
            }

            if (_copyPboHandle != 0)
            {
                GL.DeleteBuffer(_copyPboHandle);

                _copyPboHandle = 0;
            }

            IntermediatePool.Dispose();
        }
    }
}
