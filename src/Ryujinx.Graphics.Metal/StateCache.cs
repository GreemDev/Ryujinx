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
    public abstract class StateCache<T, DescriptorT, HashT>
    {
        private Dictionary<HashT, T> Cache = new();

        protected abstract HashT GetHash(DescriptorT descriptor);

        protected abstract T CreateValue(DescriptorT descriptor);

        public T GetOrCreate(DescriptorT descriptor)
        {
            var hash = GetHash(descriptor);
            if (Cache.TryGetValue(hash, out T value))
            {
                return value;
            }
            else
            {
                var newValue = CreateValue(descriptor);
                Cache.Add(hash, newValue);

                return newValue;
            }
        }
    }
}
