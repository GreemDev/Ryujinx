using Ryujinx.Graphics.GAL;
using SharpMetal.Metal;
using System;
using System.Buffers;
using System.Runtime.Versioning;

namespace Ryujinx.Graphics.Metal
{
    [SupportedOSPlatform("macos")]
    class TextureBuffer : ITexture
    {
        private readonly MetalRenderer _renderer;

        private BufferHandle _bufferHandle;
        private int _offset;
        private int _size;

        private int _bufferCount;

        public int Width { get; }
        public int Height { get; }

        public MTLPixelFormat MtlFormat { get; }

        public TextureBuffer(MetalRenderer renderer, TextureCreateInfo info)
        {
            _renderer = renderer;
            Width = info.Width;
            Height = info.Height;
            MtlFormat = FormatTable.GetFormat(info.Format);
        }

        public void CopyTo(ITexture destination, int firstLayer, int firstLevel)
        {
            throw new NotSupportedException();
        }

        public void CopyTo(ITexture destination, int srcLayer, int dstLayer, int srcLevel, int dstLevel)
        {
            throw new NotSupportedException();
        }

        public void CopyTo(ITexture destination, Extents2D srcRegion, Extents2D dstRegion, bool linearFilter)
        {
            throw new NotSupportedException();
        }

        public ITexture CreateView(TextureCreateInfo info, int firstLayer, int firstLevel)
        {
            throw new NotSupportedException();
        }

        public PinnedSpan<byte> GetData()
        {
            return _renderer.GetBufferData(_bufferHandle, _offset, _size);
        }

        public PinnedSpan<byte> GetData(int layer, int level)
        {
            return GetData();
        }

        public void CopyTo(BufferRange range, int layer, int level, int stride)
        {
            throw new NotImplementedException();
        }

        public void Release()
        {

        }

        public void SetData(IMemoryOwner<byte> data)
        {
            _renderer.SetBufferData(_bufferHandle, _offset, data.Memory.Span);
            data.Dispose();
        }

        public void SetData(IMemoryOwner<byte> data, int layer, int level)
        {
            throw new NotSupportedException();
        }

        public void SetData(IMemoryOwner<byte> data, int layer, int level, Rectangle<int> region)
        {
            throw new NotSupportedException();
        }

        public void SetStorage(BufferRange buffer)
        {
            if (_bufferHandle == buffer.Handle &&
                _offset == buffer.Offset &&
                _size == buffer.Size &&
                _bufferCount == _renderer.BufferManager.BufferCount)
            {
                return;
            }

            _bufferHandle = buffer.Handle;
            _offset = buffer.Offset;
            _size = buffer.Size;
            _bufferCount = _renderer.BufferManager.BufferCount;

            Release();
        }
    }
}
