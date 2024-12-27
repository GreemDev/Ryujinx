using SharpMetal;
using SharpMetal.ObjectiveCCore;
using SharpMetal.QuartzCore;
using System.Runtime.Versioning;
// ReSharper disable InconsistentNaming

namespace Ryujinx.Graphics.Metal.SharpMetalExtensions
{
    [SupportedOSPlatform("OSX")]
    public static class CAMetalLayerExtensions
    {
        private static readonly Selector sel_displaySyncEnabled = "displaySyncEnabled";
        private static readonly Selector sel_setDisplaySyncEnabled = "setDisplaySyncEnabled:";
        
        public static bool IsDisplaySyncEnabled(this CAMetalLayer metalLayer) 
            => ObjectiveCRuntime.bool_objc_msgSend(metalLayer.NativePtr, sel_displaySyncEnabled);

        public static void SetDisplaySyncEnabled(this CAMetalLayer metalLayer, bool enabled) 
            => ObjectiveCRuntime.objc_msgSend(metalLayer.NativePtr, sel_setDisplaySyncEnabled, enabled);
    }
}
