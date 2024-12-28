using SharpMetal.Foundation;
using SharpMetal.ObjectiveCCore;
using System.Runtime.Versioning;

namespace Ryujinx.Graphics.Metal.SharpMetalExtensions
{
    [SupportedOSPlatform("macOS")]
    public static class NSHelper
    {
        public static unsafe string ToDotNetString(this NSString source)
        {
            char[] sourceBuffer = new char[source.Length];
            fixed (char* pSourceBuffer = sourceBuffer)
            {
                ObjectiveC.bool_objc_msgSend(source,
                    "getCString:maxLength:encoding:",
                    pSourceBuffer,
                    source.MaximumLengthOfBytes(NSStringEncoding.UTF16) + 1,
                    (ulong)NSStringEncoding.UTF16);
            }

            return new string(sourceBuffer);
        }
        
        public static NSString ToNSString(this string source) 
            => new(ObjectiveC.IntPtr_objc_msgSend(new ObjectiveCClass("NSString"), "stringWithUTF8String:", source));
    }
}
