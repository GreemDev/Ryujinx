using System;
using System.Diagnostics;
using System.Text;

namespace Ryujinx.Horizon.Sdk.Ngc.Detail
{
    class AhoCorasick
    {
        public delegate bool MatchCallback(ReadOnlySpan<byte> text, int matchStartOffset, int matchEndOffset, int nodeId, ref MatchState state);
        public delegate bool MatchCallback<T>(ReadOnlySpan<byte> text, int matchStartOffset, int matchEndOffset, int nodeId, ref T state);

        private readonly SparseSet _wordMap = new();
        private readonly CompressedArray _wordLengths = new();
        private readonly SparseSet _multiWordMap = new();
        private readonly CompressedArray _multiWordIndices = new();
        private readonly SparseSet _nodeMap = new();
        private uint _nodesPerCharacter;
        private readonly Bp _bp = new();

        public bool Import(ref BinaryReader reader)
        {
            if (!_wordLengths.Import(ref reader) ||
                !_wordMap.Import(ref reader) ||
                !_multiWordIndices.Import(ref reader) ||
                !_multiWordMap.Import(ref reader))
            {
                return false;
            }

            if (!reader.Read(out _nodesPerCharacter))
            {
                return false;
            }

            return _nodeMap.Import(ref reader) && _bp.Import(ref reader);
        }

        public void Match(ReadOnlySpan<byte> utf8Text, MatchCallback callback, ref MatchState state)
        {
            int nodeId = 0;

            for (int index = 0; index < utf8Text.Length; index++)
            {
                long c = utf8Text[index];

                while (true)
                {
                    long nodeSparseIndex = _nodesPerCharacter * c + (uint)nodeId;
                    int nodePlainIndex = _nodeMap.Rank1(nodeSparseIndex);

                    if (nodePlainIndex != 0)
                    {
                        long foundNodeSparseIndex = _nodeMap.Select1Ex(nodePlainIndex - 1);

                        if (foundNodeSparseIndex > 0 && foundNodeSparseIndex == nodeSparseIndex)
                        {
                            nodeId = nodePlainIndex;

                            if (callback != null)
                            {
                                // Match full word.
                                if (_wordMap.Has(nodePlainIndex))
                                {
                                    int wordLength = _wordLengths[_wordMap.Rank1((uint)nodePlainIndex) - 1];
                                    int startIndex = index + 1 - wordLength;

                                    if (!callback(utf8Text, startIndex, index + 1, nodeId, ref state))
                                    {
                                        return;
                                    }
                                }

                                // If this is a phrase composed of multiple words, also match each sub-word.
                                while (_multiWordMap.Has(nodePlainIndex))
                                {
                                    nodePlainIndex = _multiWordIndices[_multiWordMap.Rank1((uint)nodePlainIndex) - 1];

                                    int wordLength = _wordMap.Has(nodePlainIndex) ? _wordLengths[_wordMap.Rank1(nodePlainIndex) - 1] : 0;
                                    int startIndex = index + 1 - wordLength;

                                    if (!callback(utf8Text, startIndex, index + 1, nodePlainIndex, ref state))
                                    {
                                        return;
                                    }
                                }
                            }

                            break;
                        }
                    }

                    if (nodeId == 0)
                    {
                        break;
                    }

                    int nodePos = _bp.ToPos(nodeId);
                    nodePos = _bp.Enclose(nodePos);
                    if (nodePos < 0)
                    {
                        return;
                    }

                    nodeId = _bp.ToNodeId(nodePos);
                }
            }
        }

        public void Match<T>(ReadOnlySpan<byte> utf8Text, MatchCallback<T> callback, ref T state)
        {
            int nodeId = 0;

            for (int index = 0; index < utf8Text.Length; index++)
            {
                long c = utf8Text[index];

                while (true)
                {
                    long nodeSparseIndex = _nodesPerCharacter * c + (uint)nodeId;
                    int nodePlainIndex = _nodeMap.Rank1(nodeSparseIndex);

                    if (nodePlainIndex != 0)
                    {
                        long foundNodeSparseIndex = _nodeMap.Select1Ex(nodePlainIndex - 1);

                        if (foundNodeSparseIndex > 0 && foundNodeSparseIndex == nodeSparseIndex)
                        {
                            nodeId = nodePlainIndex;

                            if (callback != null)
                            {
                                // Match full word.
                                if (_wordMap.Has(nodePlainIndex))
                                {
                                    int wordLength = _wordLengths[_wordMap.Rank1((uint)nodePlainIndex) - 1];
                                    int startIndex = index + 1 - wordLength;

                                    if (!callback(utf8Text, startIndex, index + 1, nodeId, ref state))
                                    {
                                        return;
                                    }
                                }

                                // If this is a phrase composed of multiple words, also match each sub-word.
                                while (_multiWordMap.Has(nodePlainIndex))
                                {
                                    nodePlainIndex = _multiWordIndices[_multiWordMap.Rank1((uint)nodePlainIndex) - 1];

                                    int wordLength = _wordMap.Has(nodePlainIndex) ? _wordLengths[_wordMap.Rank1(nodePlainIndex) - 1] : 0;
                                    int startIndex = index + 1 - wordLength;

                                    if (!callback(utf8Text, startIndex, index + 1, nodePlainIndex, ref state))
                                    {
                                        return;
                                    }
                                }
                            }

                            break;
                        }
                    }

                    if (nodeId == 0)
                    {
                        break;
                    }

                    int nodePos = _bp.ToPos(nodeId);
                    nodePos = _bp.Enclose(nodePos);
                    if (nodePos < 0)
                    {
                        return;
                    }

                    nodeId = _bp.ToNodeId(nodePos);
                }
            }
        }

        public string GetWordList(bool includeMultiWord = true)
        {
            // Storage must be large enough to fit the largest word in the dictionary.
            // Since this is only used for debugging, it's fine to increase the size manually if needed.
            StringBuilder sb = new();
            Span<byte> storage = new byte[1024];

            // Traverse trie from the root.
            GetWord(sb, storage, 0, 0, includeMultiWord);

            return sb.ToString();
        }

        private void GetWord(StringBuilder sb, Span<byte> storage, int storageOffset, int nodeId, bool includeMultiWord)
        {
            int characters = (int)((_nodeMap.RangeEndValue + _nodesPerCharacter - 1) / _nodesPerCharacter);

            for (int c = 0; c < characters; c++)
            {
                long nodeSparseIndex = _nodesPerCharacter * c + (uint)nodeId;
                int nodePlainIndex = _nodeMap.Rank1(nodeSparseIndex);

                if (nodePlainIndex != 0)
                {
                    long foundNodeSparseIndex = _nodeMap.Select1Ex(nodePlainIndex - 1);

                    if (foundNodeSparseIndex > 0 && foundNodeSparseIndex == nodeSparseIndex)
                    {
                        storage[storageOffset] = (byte)c;
                        int nextNodeId = nodePlainIndex;

                        if (_wordMap.Has(nodePlainIndex))
                        {
                            sb.AppendLine(Encoding.UTF8.GetString(storage[..(storageOffset + 1)]));

                            // Some basic validation to ensure we imported the dictionary properly.
                            int wordLength = _wordLengths[_wordMap.Rank1((uint)nodePlainIndex) - 1];

                            Debug.Assert(storageOffset + 1 == wordLength);
                        }

                        if (includeMultiWord)
                        {
                            int lastMultiWordIndex = 0;
                            string multiWord = string.Empty;

                            while (_multiWordMap.Has(nodePlainIndex))
                            {
                                nodePlainIndex = _multiWordIndices[_multiWordMap.Rank1((uint)nodePlainIndex) - 1];

                                int wordLength = _wordMap.Has(nodePlainIndex) ? _wordLengths[_wordMap.Rank1(nodePlainIndex) - 1] : 0;
                                int startIndex = storageOffset + 1 - wordLength;

                                multiWord += Encoding.UTF8.GetString(storage[lastMultiWordIndex..startIndex]) + " ";
                                lastMultiWordIndex = startIndex;
                            }

                            if (lastMultiWordIndex != 0)
                            {
                                multiWord += Encoding.UTF8.GetString(storage[lastMultiWordIndex..(storageOffset + 1)]);

                                sb.AppendLine(multiWord);
                            }
                        }

                        GetWord(sb, storage, storageOffset + 1, nextNodeId, includeMultiWord);
                    }
                }
            }
        }
    }
}
