using LibHac.Ns;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ryujinx.UI.App.Common
{
    public class LdnGameDataArray
    {
        private readonly LdnGameData[] _ldnDatas;

        public LdnGameDataArray(IEnumerable<LdnGameData> receivedData, ref ApplicationControlProperty acp)
        {
            LibHac.Common.FixedArrays.Array8<ulong> communicationId = acp.LocalCommunicationId;

            _ldnDatas = receivedData.Where(game =>
                communicationId.Items.Contains(Convert.ToUInt64(game.TitleId, 16))
            ).ToArray();
        }

        public int PlayerCount => _ldnDatas.Sum(it => it.PlayerCount);
        public int GameCount => _ldnDatas.Length;
    }
}
