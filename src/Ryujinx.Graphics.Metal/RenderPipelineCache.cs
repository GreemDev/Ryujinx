using Ryujinx.Common.Logging;
using Ryujinx.Graphics.GAL;
using SharpMetal.Foundation;
using SharpMetal.Metal;
using System;
using System.Collections.Generic;
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
                public int Offset;
                public int BufferIndex;
            }
            [System.Runtime.CompilerServices.InlineArray(Constants.MaxVertexAttributes)]
            public struct AttributeHashArray
            {
                public AttributeHash data;
            }
            public AttributeHashArray Attributes;
            public struct LayoutHash
            {
                public MTLVertexFormat Format;
                public int Stride;
                public int StepFunction;
                public int StepRate;
            }
            [System.Runtime.CompilerServices.InlineArray(Constants.MaxVertexLayouts)]
            public struct LayoutHashArray
            {
                public LayoutHash data;
            }
            public LayoutHashArray Layouts;
        }
        public VertexDescriptorHash VertexDescriptor;
    }

    [SupportedOSPlatform("macos")]
    public class RenderPipelineCache : StateCache<MTLRenderPipelineState, MTLRenderPipelineDescriptor, RenderPipelineHash>
    {
        private readonly MTLDevice _device;

        public RenderPipelineCache(MTLDevice device) {
            _device = device;
        }

        protected override RenderPipelineHash GetHash(MTLRenderPipelineDescriptor descriptor) {
            var hash = new RenderPipelineHash();

            // Functions
            hash.VertexFunction = descriptor.VertexFunction;
            hash.FragmentFunction = descriptor.FragmentFunction;

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
                    DestinationAlphaBlendFactor = attachment.DestinationAlphaBlendFactor
                };
            }

            // Depth stencil attachment
            hash.DepthStencilAttachment = new RenderPipelineHash.DepthStencilAttachmentHash
            {
                DepthPixelFormat = descriptor.DepthAttachmentPixelFormat,
                StencilPixelFormat = descriptor.StencilAttachmentPixelFormat
            };

            // Vertex descriptor
            hash.VertexDescriptor = new RenderPipelineHash.VertexDescriptorHash();

            // Attributes
            for (int i = 0; i < Constants.MaxVertexAttributes; i++)
            {
                var attribute = descriptor.VertexDescriptor.Attributes.Object((ulong)i);
                hash.VertexDescriptor.Attributes[i] = new RenderPipelineHash.VertexDescriptorHash.AttributeHash
                {
                    Format = attribute.Format,
                    Offset = (int)attribute.Offset,
                    BufferIndex = (int)attribute.BufferIndex
                };
            }

            // Layouts
            for (int i = 0; i < Constants.MaxVertexLayouts; i++)
            {
                var layout = descriptor.VertexDescriptor.Layouts.Object((ulong)i);
                hash.VertexDescriptor.Layouts[i] = new RenderPipelineHash.VertexDescriptorHash.LayoutHash
                {
                    Stride = (int)layout.Stride,
                    StepFunction = (int)layout.StepFunction,
                    StepRate = (int)layout.StepRate
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
