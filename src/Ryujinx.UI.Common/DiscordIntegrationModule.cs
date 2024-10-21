using DiscordRPC;
using Humanizer;
using LibHac.Bcat;
using Ryujinx.Common;
using Ryujinx.HLE.Loaders.Processes;
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

        private static readonly string _description = ReleaseInformation.IsValid
                ? $"v{ReleaseInformation.Version} {ReleaseInformation.ReleaseChannelOwner}/{ReleaseInformation.ReleaseChannelRepo}@{ReleaseInformation.BuildGitHash}"
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
                    LargeImageKey = _discordGameAssetKeys.Contains(procRes.ProgramIdText.ToLower()) ? procRes.ProgramIdText : "game",
                    LargeImageText = TruncateToByteLength($"{appMeta.Title} | {procRes.DisplayVersion}"),
                    SmallImageKey = "ryujinx",
                    SmallImageText = TruncateToByteLength(_description)
                },
                Details = TruncateToByteLength($"Playing {appMeta.Title}"),
                State = appMeta.LastPlayed.HasValue && appMeta.TimePlayed.TotalSeconds > 5
                    ? $"Total play time: {appMeta.TimePlayed.Humanize(2, false)}"
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

        private static readonly string[] _discordGameAssetKeys =
        [
            "01002da013484000", // The Legend of Zelda: Skyward Sword HD
            "01007ef00011e000", // The Legend of Zelda: Breath of the Wild
            "0100f2c0115b6000", // The Legend of Zelda: Tears of the Kingdom
            "01008cf01baac000", // The Legend of Zelda: Echoes of Wisdom
            "01006bb00c6f0000", // The Legend of Zelda: Link's Awakening

            "0100000000010000", // SUPER MARIO ODYSSEY
            "010015100b514000", // Super Mario Bros. Wonder
            "0100152000022000", // Mario Kart 8 Deluxe
            "010049900f546000", // Super Mario 3D All-Stars
            "010028600ebda000", // Super Mario 3D World + Bowser's Fury
            "0100ecd018ebe000", // Paper Mario: The Thousand-Year Door

            "010048701995e000", // Luigi's Mansion 2 HD
            "0100dca0064a6000", // Luigi's Mansion 3

            "01008f6008c5e000", // Pokémon Violet
            "0100abf008968000", // Pokémon Sword
            "01008db008c2c000", // Pokémon Shield
            "0100000011d90000", // Pokémon Brilliant Diamond
            "01001f5010dfa000", // Pokémon Legends: Arceus

            "0100aa80194b0000", // Pikmin 1
            "0100d680194b2000", // Pikmin 2
            "0100f4c009322000", // Pikmin 3 Deluxe
            "0100b7c00933a000", // Pikmin 4

            "0100c2500fc20000", // Splatoon 3
            "0100ba0018500000", // Splatoon 3: Splatfest World Premiere
            "01000a10041ea000", // The Elder Scrolls V: Skyrim
            "01007820196a6000", // Red Dead Redemption
            "0100744001588000", // Cars 3: Driven to Win
            "01002b00111a2000", // Hyrule Warriors: Age of Calamity
            "01006f8002326000", // Animal Crossing: New Horizons
            "01004d300c5ae000", // Kirby and the Forgotten Land
            "0100853015e86000", // No Man's Sky
            "01008d100d43e000", // Saints Row IV
            "0100de600beee000", // Saints Row: The Third - The Full Package
            "0100d7a01b7a2000", // Star Wars: Bounty Hunter
            "0100dbf01000a000", // Burnout Paradise Remastered
            "0100e46006708000", // Terraria
            "010056e00853a000", // A Hat in Time
            "01006a800016e000", // Super Smash Bros. Ultimate
            "01007bb017812000", // Portal
            "0100abd01785c000", // Portal 2
            "01008e200c5c2000", // Muse Dash
            "01001180021fa000", // Shovel Knight: Specter of Torment
            "010012101468c000", // Metroid Prime Remastered
            "0100c9a00ece6000", // Nintendo 64 - Nintendo Switch Online
        ];
    }
}
