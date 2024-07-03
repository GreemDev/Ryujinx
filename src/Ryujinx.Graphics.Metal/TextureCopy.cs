using Ryujinx.Common;
using Ryujinx.Common.Logging;
using Ryujinx.Graphics.GAL;
using SharpMetal.Metal;
using System;
using System.Runtime.Versioning;

namespace Ryujinx.Graphics.Metal
{
    [SupportedOSPlatform("macos")]
    static class TextureCopy
    {
        public static void Copy(
            CommandBufferScoped cbs,
            MTLTexture srcImage,
            MTLTexture dstImage,
            TextureCreateInfo srcInfo,
            TextureCreateInfo dstInfo,
            int srcViewLayer,
            int dstViewLayer,
            int srcViewLevel,
            int dstViewLevel,
            int srcLayer,
            int dstLayer,
            int srcLevel,
            int dstLevel)
        {
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

            Copy(
                cbs,
                srcImage,
                dstImage,
                srcInfo,
                dstInfo,
                srcViewLayer,
                dstViewLayer,
                srcViewLevel,
                dstViewLevel,
                srcLayer,
                dstLayer,
                srcLevel,
                dstLevel,
                depth,
                levels);
        }

        public static void Copy(
            CommandBufferScoped cbs,
            MTLTexture srcImage,
            MTLTexture dstImage,
            TextureCreateInfo srcInfo,
            TextureCreateInfo dstInfo,
            int srcViewLayer,
            int dstViewLayer,
            int srcViewLevel,
            int dstViewLevel,
            int srcDepthOrLayer,
            int dstDepthOrLayer,
            int srcLevel,
            int dstLevel,
            int depthOrLayers,
            int levels)
        {
            MTLBlitCommandEncoder blitCommandEncoder = cbs.Encoders.EnsureBlitEncoder();

            int srcZ;
            int srcLayer;
            int srcDepth;
            int srcLayers;

            if (srcInfo.Target == Target.Texture3D)
            {
                srcZ = srcDepthOrLayer;
                srcLayer = 0;
                srcDepth = depthOrLayers;
                srcLayers = 1;
            }
            else
            {
                srcZ = 0;
                srcLayer = srcDepthOrLayer;
                srcDepth = 1;
                srcLayers = depthOrLayers;
            }

            int dstZ;
            int dstLayer;
            int dstLayers;

            if (dstInfo.Target == Target.Texture3D)
            {
                dstZ = dstDepthOrLayer;
                dstLayer = 0;
                dstLayers = 1;
            }
            else
            {
                dstZ = 0;
                dstLayer = dstDepthOrLayer;
                dstLayers = depthOrLayers;
            }

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

                int copyWidth = sizeInBlocks ? BitUtils.DivRoundUp(width, blockWidth) : width;
                int copyHeight = sizeInBlocks ? BitUtils.DivRoundUp(height, blockHeight) : height;

                int layers = Math.Max(dstLayers - dstLayer, srcLayers);

                for (int layer = 0; layer < layers; layer++)
                {
                    if (srcInfo.Samples > 1 && srcInfo.Samples != dstInfo.Samples)
                    {
                        // TODO
                        
                        Logger.Warning?.PrintMsg(LogClass.Gpu, "Unsupported mismatching sample count copy");
                    }
                    else
                    {
                        blitCommandEncoder.CopyFromTexture(
                            srcImage,
                            (ulong)(srcViewLevel + srcLevel + level),
                            (ulong)(srcViewLayer + srcLayer + layer),
                            new MTLOrigin { z = (ulong)srcZ },
                            new MTLSize { width = (ulong)copyWidth, height = (ulong)copyHeight, depth = (ulong)srcDepth },
                            dstImage,
                            (ulong)(dstViewLevel + dstLevel + level),
                            (ulong)(dstViewLayer + dstLayer + layer),
                            new MTLOrigin { z = (ulong)dstZ });
                    }
                }

                width = Math.Max(1, width >> 1);
                height = Math.Max(1, height >> 1);

                if (srcInfo.Target == Target.Texture3D)
                {
                    srcDepth = Math.Max(1, srcDepth >> 1);
                }
            }
        }
    }
}
