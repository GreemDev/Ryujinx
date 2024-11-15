using DiscordRPC;
using Humanizer;
using Humanizer.Localisation;
using Ryujinx.Common;
using Ryujinx.HLE.Loaders.Processes;
using Ryujinx.UI.App.Common;
using Ryujinx.UI.Common.Configuration;
using System.Linq;
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
                    LargeImageKey = _discordGameAssetKeys.Contains(procRes.ProgramIdText) ? procRes.ProgramIdText : "game",
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

        private static readonly string[] _discordGameAssetKeys =
        [
            "010055d009f78000", // Fire Emblem: Three Houses
            "0100a12011cc8000", // Fire Emblem: Shadow Dragon
            "0100a6301214e000", // Fire Emblem Engage
            "0100f15003e64000", // Fire Emblem Warriors
            "010071f0143ea000", // Fire Emblem Warriors: Three Hopes

            "01007e3006dda000", // Kirby Star Allies
            "01004d300c5ae000", // Kirby and the Forgotten Land
            "01006b601380e000", // Kirby's Return to Dream Land Deluxe
            "01003fb00c5a8000", // Super Kirby Clash
            "0100227010460000", // Kirby Fighters 2
            "0100a8e016236000", // Kirby's Dream Buffet

            "01007ef00011e000", // The Legend of Zelda: Breath of the Wild
            "01006bb00c6f0000", // The Legend of Zelda: Link's Awakening
            "01002da013484000", // The Legend of Zelda: Skyward Sword HD
            "0100f2c0115b6000", // The Legend of Zelda: Tears of the Kingdom
            "01008cf01baac000", // The Legend of Zelda: Echoes of Wisdom
            "01000b900d8b0000", // Cadence of Hyrule
            "0100ae00096ea000", // Hyrule Warriors: Definitive Edition
            "01002b00111a2000", // Hyrule Warriors: Age of Calamity

            "010048701995e000", // Luigi's Mansion 2 HD
            "0100dca0064a6000", // Luigi's Mansion 3

            "010093801237c000", // Metroid Dread
            "010012101468c000", // Metroid Prime Remastered

            "0100000000010000", // SUPER MARIO ODYSSEY
            "0100ea80032ea000", // Super Mario Bros. U Deluxe
            "01009b90006dc000", // Super Mario Maker 2
            "010049900f546000", // Super Mario 3D All-Stars
            "010049900F546001", // ^ 64
            "010049900F546002", // ^ Sunshine
            "010049900F546003", // ^ Galaxy
            "010028600ebda000", // Super Mario 3D World + Bowser's Fury
            "010015100b514000", // Super Mario Bros. Wonder
            "0100152000022000", // Mario Kart 8 Deluxe
            "010036b0034e4000", // Super Mario Party
            "01006fe013472000", // Mario Party Superstars
            "0100965017338000", // Super Mario Party Jamboree
            "01006d0017f7a000", // Mario & Luigi: Brothership
            "010067300059a000", // Mario + Rabbids: Kingdom Battle
            "0100317013770000", // Mario + Rabbids: Sparks of Hope
            "0100a3900c3e2000", // Paper Mario: The Origami King
            "0100ecd018ebe000", // Paper Mario: The Thousand-Year Door
            "0100bc0018138000", // Super Mario RPG
            "0100bde00862a000", // Mario Tennis Aces
            "0100c9c00e25c000", // Mario Golf: Super Rush
            "010019401051c000", // Mario Strikers: Battle League
            "010003000e146000", // Mario & Sonic at the Olympic Games Tokyo 2020
            "0100b99019412000", // Mario vs. Donkey Kong

            "0100aa80194b0000", // Pikmin 1
            "0100d680194b2000", // Pikmin 2
            "0100f4c009322000", // Pikmin 3 Deluxe
            "0100b7c00933a000", // Pikmin 4

            "010003f003a34000", // Pokémon: Let's Go Pikachu!
            "0100187003a36000", // Pokémon: Let's Go Eevee!
            "0100abf008968000", // Pokémon Sword
            "01008db008c2c000", // Pokémon Shield
            "0100000011d90000", // Pokémon Brilliant Diamond
            "010018e011d92000", // Pokémon Shining Pearl
            "01001f5010dfa000", // Pokémon Legends: Arceus
            "0100a3d008c5c000", // Pokémon Scarlet
            "01008f6008c5e000", // Pokémon Violet
            "0100b3f000be2000", // Pokkén Tournament DX
            "0100f4300bf2c000", // New Pokémon Snap

            "01003bc0000a0000", // Splatoon 2 (US)
            "0100f8f0000a2000", // Splatoon 2 (EU)
            "01003c700009c000", // Splatoon 2 (JP)
            "0100c2500fc20000", // Splatoon 3
            "0100ba0018500000", // Splatoon 3: Splatfest World Premiere

            "010040600c5ce000", // Tetris 99
            "0100277011f1a000", // Super Mario Bros. 35
            "0100ad9012510000", // PAC-MAN 99
            "0100ccf019c8c000", // F-ZERO 99
            "0100d870045b6000", // NES - Nintendo Switch Online
            "01008d300c50c000", // SNES - Nintendo Switch Online
            "0100c9a00ece6000", // N64 - Nintendo Switch Online
            "0100e0601c632000", // N64 - Nintendo Switch Online 18+
            "0100c62011050000", // GB - Nintendo Switch Online
            "010012f017576000", // GBA - Nintendo Switch Online

            "01000320000cc000", // 1-2 Switch
            "0100300012f2a000", // Advance Wars 1+2: Re-Boot Camp
            "01006f8002326000", // Animal Crossing: New Horizons
            "0100620012d6e000", // Big Brain Academy: Brain vs. Brain
            "010018300d006000", // BOXBOY! + BOXGIRL!
            "0100c1f0051b6000", // Donkey Kong Country: Tropical Freeze
            "0100ed000d390000", // Dr. Kawashima's Brain Training
            "010067b017588000", // Endless Ocean Luminous
            "0100d2f00d5c0000", // Nintendo Switch Sports
            "01006b5012b32000", // Part Time UFO
            "0100704000B3A000", // Snipperclips
            "01006a800016e000", // Super Smash Bros. Ultimate
            "0100a9400c9c2000", // Tokyo Mirage Sessions #FE Encore

            "010076f0049a2000", // Bayonetta
            "01007960049a0000", // Bayonetta 2
            "01004a4010fea000", // Bayonetta 3
            "0100cf5010fec000", // Bayonetta Origins: Cereza and the Lost Demon

            "0100dcd01525a000", // Persona 3 Portable
            "010062b01525c000", // Persona 4 Golden
            "010075a016a3a000", // Persona 4 Arena Ultimax
            "01005ca01580e000", // Persona 5 Royal
            "0100801011c3e000", // Persona 5 Strikers
            "010087701b092000", // Persona 5 Tactica

            "01009aa000faa000", // Sonic Mania
            "01004ad014bf0000", // Sonic Frontiers
            "01005ea01c0fc000", // SONIC X SHADOW GENERATIONS
            "01005ea01c0fc001", // ^

            "010056e00853a000", // A Hat in Time
            "0100dbf01000a000", // Burnout Paradise Remastered
            "0100744001588000", // Cars 3: Driven to Win
            "0100b41013c82000", // Cruis'n Blast
            "01008c8012920000", // Dying Light Platinum Edition
            "01000a10041ea000", // The Elder Scrolls V: Skyrim
            "0100770008dd8000", // Monster Hunter Generations Ultimate
            "0100b04011742000", // Monster Hunter Rise
            "0100853015e86000", // No Man's Sky
            "01007bb017812000", // Portal
            "0100abd01785c000", // Portal 2
            "01008e200c5c2000", // Muse Dash
            "01007820196a6000", // Red Dead Redemption
            "01002f7013224000", // Rune Factory 5
            "01008d100d43e000", // Saints Row IV
            "0100de600beee000", // Saints Row: The Third - The Full Package
            "01001180021fa000", // Shovel Knight: Specter of Torment
            "0100d7a01b7a2000", // Star Wars: Bounty Hunter
            "0100800015926000", // Suika Game
            "0100e46006708000", // Terraria
            "010080b00ad66000", // Undertale
        ];
    }
}
