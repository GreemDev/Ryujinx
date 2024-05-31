using Ryujinx.Common.Logging;
using SharpMetal.Foundation;
using SharpMetal.Metal;
using System;
using System.Runtime.Versioning;

namespace Ryujinx.Graphics.Metal
{
    [SupportedOSPlatform("macos")]
    public struct RenderPipelineHash
    {
        public MTLFunction VertexFunction;
        public MTLFunction FragmentFunction;
        public struct ColorAttachmentHash
        {
            public MTLPixelFormat PixelFormat;
            public bool BlendingEnabled;
            public MTLBlendOperation RgbBlendOperation;
            public MTLBlendOperation AlphaBlendOperation;
            public MTLBlendFactor SourceRGBBlendFactor;
            public MTLBlendFactor DestinationRGBBlendFactor;
            public MTLBlendFactor SourceAlphaBlendFactor;
            public MTLBlendFactor DestinationAlphaBlendFactor;
            public MTLColorWriteMask WriteMask;
        }
        [System.Runtime.CompilerServices.InlineArray(Constants.MaxColorAttachments)]
        public struct ColorAttachmentHashArray
        {
            public ColorAttachmentHash data;
        }
        public ColorAttachmentHashArray ColorAttachments;
        public struct DepthStencilAttachmentHash
        {
            public MTLPixelFormat DepthPixelFormat;
            public MTLPixelFormat StencilPixelFormat;
        }
        public DepthStencilAttachmentHash DepthStencilAttachment;
        public struct VertexDescriptorHash
        {
            public struct AttributeHash
            {
                public MTLVertexFormat Format;
                public ulong Offset;
                public ulong BufferIndex;
            }
            [System.Runtime.CompilerServices.InlineArray(Constants.MaxVertexAttributes)]
            public struct AttributeHashArray
            {
                public AttributeHash data;
            }
            public AttributeHashArray Attributes;
            public struct LayoutHash
            {
                public ulong Stride;
                public MTLVertexStepFunction StepFunction;
                public ulong StepRate;
            }
            [System.Runtime.CompilerServices.InlineArray(Constants.MaxVertexLayouts)]
            public struct LayoutHashArray
            {
                public LayoutHash data;
            }
            public LayoutHashArray Layouts;
        }
        public VertexDescriptorHash VertexDescriptor;

        public override bool Equals(object obj)
        {
            if (obj is not RenderPipelineHash other)
            {
                return false;
            }

            if (VertexFunction != other.VertexFunction)
            {
                return false;
            }
            if (FragmentFunction != other.FragmentFunction)
            {
                return false;
            }
            if (DepthStencilAttachment.DepthPixelFormat != other.DepthStencilAttachment.DepthPixelFormat)
            {
                return false;
            }
            if (DepthStencilAttachment.StencilPixelFormat != other.DepthStencilAttachment.StencilPixelFormat)
            {
                return false;
            }
            for (int i = 0; i < Constants.MaxColorAttachments; i++)
            {
                if (ColorAttachments[i].PixelFormat != other.ColorAttachments[i].PixelFormat)
                {
                    return false;
                }
                if (ColorAttachments[i].BlendingEnabled != other.ColorAttachments[i].BlendingEnabled)
                {
                    return false;
                }
                if (ColorAttachments[i].RgbBlendOperation != other.ColorAttachments[i].RgbBlendOperation)
                {
                    return false;
                }
                if (ColorAttachments[i].AlphaBlendOperation != other.ColorAttachments[i].AlphaBlendOperation)
                {
                    return false;
                }
                if (ColorAttachments[i].SourceRGBBlendFactor != other.ColorAttachments[i].SourceRGBBlendFactor)
                {
                    return false;
                }
                if (ColorAttachments[i].DestinationRGBBlendFactor != other.ColorAttachments[i].DestinationRGBBlendFactor)
                {
                    return false;
                }
                if (ColorAttachments[i].SourceAlphaBlendFactor != other.ColorAttachments[i].SourceAlphaBlendFactor)
                {
                    return false;
                }
                if (ColorAttachments[i].DestinationAlphaBlendFactor != other.ColorAttachments[i].DestinationAlphaBlendFactor)
                {
                    return false;
                }
                if (ColorAttachments[i].WriteMask != other.ColorAttachments[i].WriteMask)
                {
                    return false;
                }
            }
            for (int i = 0; i < Constants.MaxVertexAttributes; i++)
            {
                if (VertexDescriptor.Attributes[i].Format != other.VertexDescriptor.Attributes[i].Format)
                {
                    return false;
                }
                if (VertexDescriptor.Attributes[i].Offset != other.VertexDescriptor.Attributes[i].Offset)
                {
                    return false;
                }
                if (VertexDescriptor.Attributes[i].BufferIndex != other.VertexDescriptor.Attributes[i].BufferIndex)
                {
                    return false;
                }
            }
            for (int i = 0; i < Constants.MaxVertexLayouts; i++)
            {
                if (VertexDescriptor.Layouts[i].Stride != other.VertexDescriptor.Layouts[i].Stride)
                {
                    return false;
                }
                if (VertexDescriptor.Layouts[i].StepFunction != other.VertexDescriptor.Layouts[i].StepFunction)
                {
                    return false;
                }
                if (VertexDescriptor.Layouts[i].StepRate != other.VertexDescriptor.Layouts[i].StepRate)
                {
                    return false;
                }
            }

            return true;
        }
    }

    [SupportedOSPlatform("macos")]
    public class RenderPipelineCache : StateCache<MTLRenderPipelineState, MTLRenderPipelineDescriptor, RenderPipelineHash>
    {
        private readonly MTLDevice _device;

        public RenderPipelineCache(MTLDevice device)
        {
            _device = device;
        }

        protected override RenderPipelineHash GetHash(MTLRenderPipelineDescriptor descriptor)
        {
            var hash = new RenderPipelineHash
            {
                // Functions
                VertexFunction = descriptor.VertexFunction,
                FragmentFunction = descriptor.FragmentFunction,
                DepthStencilAttachment = new RenderPipelineHash.DepthStencilAttachmentHash
                {
                    DepthPixelFormat = descriptor.DepthAttachmentPixelFormat,
                    StencilPixelFormat = descriptor.StencilAttachmentPixelFormat
                },
            };

            // Color Attachments
            for (int i = 0; i < Constants.MaxColorAttachments; i++)
            {
                var attachment = descriptor.ColorAttachments.Object((ulong)i);
                hash.ColorAttachments[i] = new RenderPipelineHash.ColorAttachmentHash
                {
                    PixelFormat = attachment.PixelFormat,
                    BlendingEnabled = attachment.BlendingEnabled,
                    RgbBlendOperation = attachment.RgbBlendOperation,
                    AlphaBlendOperation = attachment.AlphaBlendOperation,
                    SourceRGBBlendFactor = attachment.SourceRGBBlendFactor,
                    DestinationRGBBlendFactor = attachment.DestinationRGBBlendFactor,
                    SourceAlphaBlendFactor = attachment.SourceAlphaBlendFactor,
                    DestinationAlphaBlendFactor = attachment.DestinationAlphaBlendFactor,
                    WriteMask = attachment.WriteMask
                };
            }

            // Vertex descriptor
            hash.VertexDescriptor = new RenderPipelineHash.VertexDescriptorHash();

            // Attributes
            for (int i = 0; i < Constants.MaxVertexAttributes; i++)
            {
                var attribute = descriptor.VertexDescriptor.Attributes.Object((ulong)i);
                hash.VertexDescriptor.Attributes[i] = new RenderPipelineHash.VertexDescriptorHash.AttributeHash
                {
                    Format = attribute.Format,
                    Offset = attribute.Offset,
                    BufferIndex = attribute.BufferIndex
                };
            }

            // Layouts
            for (int i = 0; i < Constants.MaxVertexLayouts; i++)
            {
                var layout = descriptor.VertexDescriptor.Layouts.Object((ulong)i);
                hash.VertexDescriptor.Layouts[i] = new RenderPipelineHash.VertexDescriptorHash.LayoutHash
                {
                    Stride = layout.Stride,
                    StepFunction = layout.StepFunction,
                    StepRate = layout.StepRate
                };
            }

            return hash;
        }

        protected override MTLRenderPipelineState CreateValue(MTLRenderPipelineDescriptor descriptor)
        {
            var error = new NSError(IntPtr.Zero);
            var pipelineState = _device.NewRenderPipelineState(descriptor, ref error);
            if (error != IntPtr.Zero)
            {
                Logger.Error?.PrintMsg(LogClass.Gpu, $"Failed to create Render Pipeline State: {StringHelper.String(error.LocalizedDescription)}");
            }

            return pipelineState;
        }
    }
}
