using DiscordRPC;
using Humanizer;
using Ryujinx.Common;
using Ryujinx.UI.App.Common;
using Ryujinx.UI.Common.Configuration;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Ryujinx.UI.Common
{
    public static class DiscordIntegrationModule
    {
        public static Timestamps StartedAt { get; set; }
        
        private const string Description = "A simple, experimental Nintendo Switch emulator.";
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
                    LargeImageText = Description
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

        private static readonly string[] _discordGameAssets = [ 
            "0100f2c0115b6000", // Tears of the Kingdom
            "0100744001588000", // Cars 3: Driven to Win

        ];

        public static void SwitchToPlayingState(string titleId, ApplicationMetadata appMeta)
        {
            _discordClient?.SetPresence(new RichPresence
            {
                Assets = new Assets
                {
                    LargeImageKey = _discordGameAssets.Contains(titleId.ToLower()) ? titleId : "game",
                    LargeImageText = TruncateToByteLength(appMeta.Title, ApplicationByteLimit),
                    SmallImageKey = "ryujinx",
                    SmallImageText = Description
                },
                Details = TruncateToByteLength($"Playing {appMeta.Title}", ApplicationByteLimit),
                State = $"Total play time: {appMeta.TimePlayed.Humanize(2, false)}",
                Timestamps = Timestamps.Now
            });
        }

        private static string TruncateToByteLength(string input, int byteLimit)
        {
            if (Encoding.UTF8.GetByteCount(input) <= byteLimit)
            {
                return input;
            }

            // Find the length to trim the string to guarantee we have space for the trailing ellipsis.
            int trimLimit = byteLimit - Encoding.UTF8.GetByteCount(Ellipsis);

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
