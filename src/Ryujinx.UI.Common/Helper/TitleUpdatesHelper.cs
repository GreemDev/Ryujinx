using LibHac.Common;
using LibHac.Common.Keys;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.Ncm;
using LibHac.Ns;
using LibHac.Tools.FsSystem;
using LibHac.Tools.FsSystem.NcaUtils;
using Ryujinx.Common.Configuration;
using Ryujinx.Common.Logging;
using Ryujinx.Common.Utilities;
using Ryujinx.HLE.FileSystem;
using Ryujinx.HLE.Loaders.Processes.Extensions;
using Ryujinx.HLE.Utilities;
using Ryujinx.UI.Common.Configuration;
using Ryujinx.UI.Common.Models;
using System;
using System.Collections.Generic;
using System.IO;
using ContentType = LibHac.Ncm.ContentType;
using Path = System.IO.Path;
using SpanHelpers = LibHac.Common.SpanHelpers;
using TitleUpdateMetadata = Ryujinx.Common.Configuration.TitleUpdateMetadata;

namespace Ryujinx.UI.Common.Helper
{
    public static class TitleUpdatesHelper
    {
        private static readonly TitleUpdateMetadataJsonSerializerContext _serializerContext = new(JsonHelper.GetDefaultSerializerOptions());

        public static List<(TitleUpdateModel, bool IsSelected)> LoadTitleUpdatesJson(VirtualFileSystem vfs, ulong applicationIdBase)
        {
            var titleUpdatesJsonPath = PathToGameUpdatesJson(applicationIdBase);

            if (!File.Exists(titleUpdatesJsonPath))
            {
                return [];
            }

            try
            {
                var titleUpdateWindowData = JsonHelper.DeserializeFromFile(titleUpdatesJsonPath, _serializerContext.TitleUpdateMetadata);
                return LoadTitleUpdates(vfs, titleUpdateWindowData, applicationIdBase);
            }
            catch
            {
                Logger.Warning?.Print(LogClass.Application, $"Failed to deserialize title update data for {applicationIdBase:x16} at {titleUpdatesJsonPath}");
                return [];
            }
        }

        public static void SaveTitleUpdatesJson(VirtualFileSystem vfs, ulong applicationIdBase, List<(TitleUpdateModel, bool IsSelected)> updates)
        {
            var titleUpdateWindowData = new TitleUpdateMetadata
            {
                Selected = "",
                Paths = [],
            };

            foreach ((TitleUpdateModel update, bool isSelected) in updates)
            {
                titleUpdateWindowData.Paths.Add(update.Path);
                if (isSelected)
                {
                    if (!string.IsNullOrEmpty(titleUpdateWindowData.Selected))
                    {
                        Logger.Error?.Print(LogClass.Application,
                            $"Tried to save two updates as 'IsSelected' for {applicationIdBase:x16}");
                        return;
                    }

                    titleUpdateWindowData.Selected = update.Path;
                }
            }

            var titleUpdatesJsonPath = PathToGameUpdatesJson(applicationIdBase);
            JsonHelper.SerializeToFile(titleUpdatesJsonPath, titleUpdateWindowData, _serializerContext.TitleUpdateMetadata);
        }

        private static List<(TitleUpdateModel, bool IsSelected)> LoadTitleUpdates(VirtualFileSystem vfs, TitleUpdateMetadata titleUpdateMetadata, ulong applicationIdBase)
        {
            var result = new List<(TitleUpdateModel, bool IsSelected)>();

            IntegrityCheckLevel checkLevel = ConfigurationState.Instance.System.EnableFsIntegrityChecks
                ? IntegrityCheckLevel.ErrorOnInvalid
                : IntegrityCheckLevel.None;

            foreach (string path in titleUpdateMetadata.Paths)
            {
                if (!File.Exists(path))
                {
                    continue;
                }

                try
                {
                    using IFileSystem pfs = PartitionFileSystemUtils.OpenApplicationFileSystem(path, vfs);

                    Dictionary<ulong, ContentMetaData> updates =
                        pfs.GetContentData(ContentMetaType.Patch, vfs, checkLevel);

                    Nca patchNca = null;
                    Nca controlNca = null;

                    if (!updates.TryGetValue(applicationIdBase, out ContentMetaData content))
                    {
                        continue;
                    }

                    patchNca = content.GetNcaByType(vfs.KeySet, ContentType.Program);
                    controlNca = content.GetNcaByType(vfs.KeySet, ContentType.Control);

                    if (controlNca == null || patchNca == null)
                    {
                        continue;
                    }

                    ApplicationControlProperty controlData = new();

                    using UniqueRef<IFile> nacpFile = new();

                    controlNca.OpenFileSystem(NcaSectionType.Data, IntegrityCheckLevel.None)
                        .OpenFile(ref nacpFile.Ref, "/control.nacp".ToU8Span(), OpenMode.Read).ThrowIfFailure();
                    nacpFile.Get.Read(out _, 0, SpanHelpers.AsByteSpan(ref controlData), ReadOption.None)
                        .ThrowIfFailure();

                    var displayVersion = controlData.DisplayVersionString.ToString();
                    var update = new TitleUpdateModel(content.ApplicationId, content.Version.Version,
                        displayVersion, path);

                    result.Add((update, path == titleUpdateMetadata.Selected));
                }
                catch (MissingKeyException exception)
                {
                    Logger.Warning?.Print(LogClass.Application,
                        $"Your key set is missing a key with the name: {exception.Name}");
                }
                catch (InvalidDataException)
                {
                    Logger.Warning?.Print(LogClass.Application,
                        $"The header key is incorrect or missing and therefore the NCA header content type check has failed. Errored File: {path}");
                }
                catch (IOException exception)
                {
                    Logger.Warning?.Print(LogClass.Application, exception.Message);
                }
                catch (Exception exception)
                {
                    Logger.Warning?.Print(LogClass.Application,
                        $"The file encountered was not of a valid type. File: '{path}' Error: {exception}");
                }
            }

            return result;
        }

        private static string PathToGameUpdatesJson(ulong applicationIdBase)
        {
            return Path.Combine(AppDataManager.GamesDirPath, applicationIdBase.ToString("x16"), "updates.json");
        }
    }
}
