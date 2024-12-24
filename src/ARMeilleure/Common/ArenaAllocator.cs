using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace ARMeilleure.Common
{
    unsafe sealed class ArenaAllocator : Allocator
    {
        private class PageInfo
        {
            public byte* Pointer;
            public byte Unused;
            public int UnusedCounter;
        }

        private int _lastReset;
        private ulong _index;
        private int _pageIndex;
        private PageInfo _page;
        private List<PageInfo> _pages;
        private readonly ulong _pageSize;
        private readonly uint _pageCount;
        private readonly List<nint> _extras;

        public ArenaAllocator(uint pageSize, uint pageCount)
        {
            _lastReset = Environment.TickCount;

            // Set _index to pageSize so that the first allocation goes through the slow path.
            _index = pageSize;
            _pageIndex = -1;

            _page = null;
            _pages = [];
            _pageSize = pageSize;
            _pageCount = pageCount;

            _extras = [];
        }

        public Span<T> AllocateSpan<T>(ulong count) where T : unmanaged
        {
            return new Span<T>(Allocate<T>(count), (int)count);
        }

        public override void* Allocate(ulong size)
        {
            if (_index + size <= _pageSize)
            {
                byte* result = _page.Pointer + _index;

                _index += size;

                return result;
            }

            return AllocateSlow(size);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void* AllocateSlow(ulong size)
        {
            if (size > _pageSize)
            {
                void* extra = NativeAllocator.Instance.Allocate(size);

                _extras.Add((nint)extra);

                return extra;
            }

            if (_index + size > _pageSize)
            {
                _index = 0;
                _pageIndex++;
            }

            if (_pageIndex < _pages.Count)
            {
                _page = _pages[_pageIndex];
                _page.Unused = 0;
            }
            else
            {
                _page = new PageInfo
                {
                    Pointer = (byte*)NativeAllocator.Instance.Allocate(_pageSize)
                };

                _pages.Add(_page);
            }

            byte* result = _page.Pointer + _index;

            _index += size;

            return result;
        }

        public override void Free(void* block) { }

        public void Reset()
        {
            _index = _pageSize;
            _pageIndex = -1;
            _page = null;

            // Free excess pages that was allocated.
            while (_pages.Count > _pageCount)
            {
                NativeAllocator.Instance.Free(_pages[^1].Pointer);

                _pages.RemoveAt(_pages.Count - 1);
            }

            // Free extra blocks that are not page-sized
            foreach (nint ptr in _extras)
            {
                NativeAllocator.Instance.Free((void*)ptr);
            }

            _extras.Clear();

            // Free pooled pages that has not been used in a while. Remove pages at the back first, because we try to
            // keep the pages at the front alive, since they're more likely to be hot and in the d-cache.
            bool removing = true;

            // If arena is used frequently, keep pages for longer. Otherwise keep pages for a shorter amount of time.
            int now = Environment.TickCount;
            int count = (now - _lastReset) switch
            {
                >= 5000 => 0,
                >= 2500 => 50,
                >= 1000 => 100,
                >= 10 => 1500,
                _ => 5000,
            };

            for (int i = _pages.Count - 1; i >= 0; i--)
            {
                PageInfo page = _pages[i];

                if (page.Unused == 0)
                {
                    page.UnusedCounter = 0;
                }

                page.UnusedCounter += page.Unused;
                page.Unused = 1;

                // If page not used after `count` resets, remove it.
                if (removing && page.UnusedCounter >= count)
                {
                    NativeAllocator.Instance.Free(page.Pointer);

                    _pages.RemoveAt(i);
                }
                else
                {
                    removing = false;
                }
            }

            _lastReset = now;
        }

        protected override void Dispose(bool disposing)
        {
            if (_pages != null)
            {
                foreach (PageInfo info in _pages)
                {
                    NativeAllocator.Instance.Free(info.Pointer);
                }

                foreach (nint ptr in _extras)
                {
                    NativeAllocator.Instance.Free((void*)ptr);
                }

                _pages = null;
            }
        }

        ~ArenaAllocator()
        {
            Dispose(false);
        }
    }
}
