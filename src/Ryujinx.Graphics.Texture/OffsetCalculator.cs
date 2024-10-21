using Ryujinx.Common;
using System;
using System.Runtime.CompilerServices;
using static Ryujinx.Graphics.Texture.BlockLinearConstants;

namespace Ryujinx.Graphics.Texture
{
    public class OffsetCalculator
    {
        private readonly int _width;
        private readonly int _height;
        private readonly int _stride;
        private readonly bool _isLinear;
        private readonly int _bytesPerPixel;

        private readonly BlockLinearLayout _layoutConverter;

        // Variables for built-in iteration.
        private int _yPart;

        public OffsetCalculator(
            int width,
            int height,
            int stride,
            bool isLinear,
            int gobBlocksInY,
            int gobBlocksInZ,
            int bytesPerPixel)
        {
            _width = width;
            _height = height;
            _stride = stride;
            _isLinear = isLinear;
            _bytesPerPixel = bytesPerPixel;

            int wAlignment = GobStride / bytesPerPixel;

            int wAligned = BitUtils.AlignUp(width, wAlignment);

            if (!isLinear)
            {
                _layoutConverter = new BlockLinearLayout(
                    wAligned,
                    height,
                    gobBlocksInY,
                    gobBlocksInZ,
                    bytesPerPixel);
            }
        }

        public OffsetCalculator(
            int width,
            int height,
            int stride,
            bool isLinear,
            int gobBlocksInY,
            int bytesPerPixel) : this(width, height, stride, isLinear, gobBlocksInY, 1, bytesPerPixel)
        {
        }

        public void SetY(int y)
        {
            if (_isLinear)
            {
                _yPart = y * _stride;
            }
            else
            {
                _layoutConverter.SetY(y);
            }
        }

        public int GetOffset(int x, int y)
        {
            if (_isLinear)
                return x * _bytesPerPixel + y * _stride;

            return _layoutConverter.GetOffset(x, y, 0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetOffset(int x)
        {
            if (_isLinear)
                return x * _bytesPerPixel + _yPart;

            return _layoutConverter.GetOffset(x);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetOffsetWithLineOffset64(int x)
        {
            if (_isLinear)
                return x + _yPart;

            return _layoutConverter.GetOffsetWithLineOffset64(x);
        }

        public (int offset, int size) GetRectangleRange(int x, int y, int width, int height)
        {
            if (!_isLinear)
                return _layoutConverter.GetRectangleRange(x, y, width, height);

            int start = y * Math.Abs(_stride) + x * _bytesPerPixel;
            int end = (y + height - 1) * Math.Abs(_stride) + (x + width) * _bytesPerPixel;
            return (y * _stride + x * _bytesPerPixel, end - start);
        }

        public bool LayoutMatches(OffsetCalculator other)
        {
            if (_isLinear)
                return other._isLinear &&
                       _width == other._width &&
                       _height == other._height &&
                       _stride == other._stride &&
                       _bytesPerPixel == other._bytesPerPixel;


            return !other._isLinear && _layoutConverter.LayoutMatches(other._layoutConverter);
        }
    }
}
