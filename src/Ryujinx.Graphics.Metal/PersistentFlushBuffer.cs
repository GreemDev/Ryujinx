using System;
using System.Runtime.Versioning;

namespace Ryujinx.Graphics.Metal
{
    [SupportedOSPlatform("macos")]
    internal class PersistentFlushBuffer : IDisposable
    {
        private readonly MetalRenderer _renderer;
        private readonly Pipeline _pipeline;

        private BufferHolder _flushStorage;

        public PersistentFlushBuffer(MetalRenderer renderer, Pipeline pipeline)
        {
            _renderer = renderer;
            _pipeline = pipeline;
        }

        private BufferHolder ResizeIfNeeded(int size)
        {
            var flushStorage = _flushStorage;

            if (flushStorage == null || size > _flushStorage.Size)
            {
                flushStorage?.Dispose();

                flushStorage = _renderer.BufferManager.Create(size);
                _flushStorage = flushStorage;
            }

            return flushStorage;
        }

        public Span<byte> GetBufferData(CommandBufferPool cbp, BufferHolder buffer, int offset, int size)
        {
            var flushStorage = ResizeIfNeeded(size);
            Auto<DisposableBuffer> srcBuffer;

            using (var cbs = cbp.Rent())
            {
                srcBuffer = buffer.GetBuffer();
                var dstBuffer = flushStorage.GetBuffer();

                if (srcBuffer.TryIncrementReferenceCount())
                {
                    BufferHolder.Copy(_pipeline, cbs, srcBuffer, dstBuffer, offset, 0, size, registerSrcUsage: false);
                }
                else
                {
                    // Source buffer is no longer alive, don't copy anything to flush storage.
                    srcBuffer = null;
                }
            }

            flushStorage.WaitForFences();
            srcBuffer?.DecrementReferenceCount();
            return flushStorage.GetDataStorage(0, size);
        }

        public void Dispose()
        {
            _flushStorage.Dispose();
        }
    }
}
