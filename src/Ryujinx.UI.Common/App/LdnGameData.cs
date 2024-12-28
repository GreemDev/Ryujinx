using LibHac.Ns;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ryujinx.UI.App.Common
{
    public struct LdnGameData
    {
        public string Id { get; set; }
        public int PlayerCount { get; set; }
        public int MaxPlayerCount { get; set; }
        public string GameName { get; set; }
        public string TitleId { get; set; }
        public string Mode { get; set; }
        public string Status { get; set; }
        public IEnumerable<string> Players { get; set; }

        public static Array GetArrayForApp(
            IEnumerable<LdnGameData> receivedData, ref ApplicationControlProperty acp)
        {
            LibHac.Common.FixedArrays.Array8<ulong> communicationId = acp.LocalCommunicationId;

            return new Array(receivedData.Where(game =>
                communicationId.Items.Contains(Convert.ToUInt64(game.TitleId, 16))
            ));
        }

        public class Array
        {
            private readonly LdnGameData[] _ldnDatas;
            
            internal Array(IEnumerable<LdnGameData> receivedData)
            {
                _ldnDatas = receivedData.ToArray();
            }

            public int PlayerCount => _ldnDatas.Sum(it => it.PlayerCount);
            public int GameCount => _ldnDatas.Length;
        }
    }
}
