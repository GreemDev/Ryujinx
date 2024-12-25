﻿using LibHac.Common;
using LibHac.Ncm;
using LibHac.Ns;
using LibHac.Tools.FsSystem.NcaUtils;
using Ryujinx.HLE;
using Ryujinx.HLE.FileSystem;
using Ryujinx.UI.App.Common;

namespace Ryujinx.UI.Common.Helper
{
        public readonly struct AppletMetadata
        {
            private readonly ContentManager _contentManager;
            
            public string Name { get; }
            public ulong ProgramId { get; }
            
            public string Version { get; }

            public AppletMetadata(ContentManager contentManager, string name, ulong programId, string version = "1.0.0") 
                : this(name, programId, version)
            {
                _contentManager = contentManager;
            }
            
            public AppletMetadata(string name, ulong programId, string version = "1.0.0")
            {
                Name = name;
                ProgramId = programId;
                Version = version;
            }
            
            public string GetContentPath(ContentManager contentManager)
                => (contentManager ?? _contentManager)
                    .GetInstalledContentPath(ProgramId, StorageId.BuiltInSystem, NcaContentType.Program);

            public bool CanStart(ContentManager contentManager, out ApplicationData appData, out BlitStruct<ApplicationControlProperty> appControl)
            {
                contentManager ??= _contentManager;
                if (contentManager == null)
                {
                    appData = null;
                    appControl = new BlitStruct<ApplicationControlProperty>(0);
                    return false;
                }
                
                appData = new()
                {
                    Name = Name,
                    Id = ProgramId,
                    Path = GetContentPath(contentManager)
                };

                if (string.IsNullOrEmpty(appData.Path))
                {
                    appControl = new BlitStruct<ApplicationControlProperty>(0);
                    return false;
                }

                appControl = StructHelpers.CreateCustomNacpData(Name, Version);
                return true;
            }
        }
}
