using DiscordRPC;
using Humanizer;
using Humanizer.Localisation;
using Ryujinx.Common;
using Ryujinx.HLE.Loaders.Processes;
using Ryujinx.UI.App.Common;
using Ryujinx.UI.Common.Configuration;
using System.Text;

namespace Ryujinx.UI.Common
{
    public static class DiscordIntegrationModule
    {
        public static Timestamps StartedAt { get; set; }

        private static string VersionString
            => (ReleaseInformation.IsCanaryBuild ? "Canary " : string.Empty) + $"v{ReleaseInformation.Version}"; 

        private static readonly string _description = 
            ReleaseInformation.IsValid 
                    ? $"{VersionString} {ReleaseInformation.ReleaseChannelOwner}/{ReleaseInformation.ReleaseChannelSourceRepo}@{ReleaseInformation.BuildGitHash}" 
                    : "dev build";

        private const string ApplicationId = "1293250299716173864";

        private const int ApplicationByteLimit = 128;
        private const string Ellipsis = "…";

        private static DiscordRpcClient _discordClient;
        private static RichPresence _discordPresenceMain;

        public static void Initialize()
        {
            _discordPresenceMain = new RichPresence
            {
                Assets = new Assets
                {
                    LargeImageKey = "ryujinx",
                    LargeImageText = TruncateToByteLength(_description)
                },
                Details = "Main Menu",
                State = "Idling",
                Timestamps = StartedAt
            };

            ConfigurationState.Instance.EnableDiscordIntegration.Event += Update;
        }

        private static void Update(object sender, ReactiveEventArgs<bool> evnt)
        {
            if (evnt.OldValue != evnt.NewValue)
            {
                // If the integration was active, disable it and unload everything
                if (evnt.OldValue)
                {
                    _discordClient?.Dispose();

                    _discordClient = null;
                }

                // If we need to activate it and the client isn't active, initialize it
                if (evnt.NewValue && _discordClient == null)
                {
                    _discordClient = new DiscordRpcClient(ApplicationId);

                    _discordClient.Initialize();
                    _discordClient.SetPresence(_discordPresenceMain);
                }
            }
        }

        public static void SwitchToPlayingState(ApplicationMetadata appMeta, ProcessResult procRes)
        {
            _discordClient?.SetPresence(new RichPresence
            {
                Assets = new Assets
                {
                    LargeImageKey = TitleIDs.GetDiscordGameAsset(procRes.ProgramIdText),
                    LargeImageText = TruncateToByteLength($"{appMeta.Title} (v{procRes.DisplayVersion})"),
                    SmallImageKey = "ryujinx",
                    SmallImageText = TruncateToByteLength(_description)
                },
                Details = TruncateToByteLength($"Playing {appMeta.Title}"),
                State = appMeta.LastPlayed.HasValue && appMeta.TimePlayed.TotalSeconds > 5
                    ? $"Total play time: {appMeta.TimePlayed.Humanize(2, false, maxUnit: TimeUnit.Hour)}"
                    : "Never played",
                Timestamps = Timestamps.Now
            });
        }

        public static void SwitchToMainState() => _discordClient?.SetPresence(_discordPresenceMain);

        private static string TruncateToByteLength(string input)
        {
            if (Encoding.UTF8.GetByteCount(input) <= ApplicationByteLimit)
            {
                return input;
            }

            // Find the length to trim the string to guarantee we have space for the trailing ellipsis.
            int trimLimit = ApplicationByteLimit - Encoding.UTF8.GetByteCount(Ellipsis);

            // Make sure the string is long enough to perform the basic trim.
            // Amount of bytes != Length of the string
            if (input.Length > trimLimit)
            {
                // Basic trim to best case scenario of 1 byte characters.
                input = input[..trimLimit];
            }

            while (Encoding.UTF8.GetByteCount(input) > trimLimit)
            {
                // Remove one character from the end of the string at a time.
                input = input[..^1];
            }

            return input.TrimEnd() + Ellipsis;
        }

        public static void Exit()
        {
            _discordClient?.Dispose();
        }
    }
}
