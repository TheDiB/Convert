// -----------------------------------------------------------------------------
// LZMA Decoder - Public Domain (Igor Pavlov, 7-Zip SDK)
// Adapté pour Convert par Damien & Copilot
// -----------------------------------------------------------------------------

using System;
using System.IO;

namespace SevenZip
{
    public class LzmaDecoder
    {
        private readonly SevenZip.Compression.LZMA.Decoder _decoder;

        public LzmaDecoder()
        {
            _decoder = new SevenZip.Compression.LZMA.Decoder();
        }

        public void Decode(Stream input, Stream output, long compressedSize, long uncompressedSize)
        {
            _decoder.Code(input, output, compressedSize, uncompressedSize, null);
        }
    }
}

namespace SevenZip.Compression.LZMA
{
    public class Decoder
    {
        const uint kNumTopBits = 24;
        const uint kTopValue = (1 << (int)kNumTopBits);

        const int kNumBitModelTotalBits = 11;
        const uint kBitModelTotal = (1 << kNumBitModelTotalBits);
        const int kNumMoveBits = 5;

        const int kNumMoveReducingBits = 2;
        const uint kBitModelTotalReducing = (kBitModelTotal >> kNumMoveReducingBits);
        const int kNumBitPriceShiftBits = 6;

        class RangeDecoder
        {
            public uint Range;
            public uint Code;
            public Stream Stream;

            public void Init(Stream stream)
            {
                Stream = stream;
                Code = 0;
                Range = 0xFFFFFFFF;

                for (int i = 0; i < 5; i++)
                    Code = (Code << 8) | (byte)Stream.ReadByte();
            }

            public uint DecodeDirectBits(int numTotalBits)
            {
                uint result = 0;
                for (int i = numTotalBits; i > 0; i--)
                {
                    Range >>= 1;
                    uint t = (Code - Range) >> 31;
                    Code -= Range & (t - 1);
                    result = (result << 1) | (1 - t);

                    if (Range < kTopValue)
                    {
                        Range <<= 8;
                        Code = (Code << 8) | (byte)Stream.ReadByte();
                    }
                }
                return result;
            }

            public uint DecodeBit(uint[] probs, uint index)
            {
                uint prob = probs[index];
                uint newBound = (Range >> kNumBitModelTotalBits) * prob;

                if (Code < newBound)
                {
                    Range = newBound;
                    probs[index] += (kBitModelTotal - prob) >> kNumMoveBits;

                    if (Range < kTopValue)
                    {
                        Range <<= 8;
                        Code = (Code << 8) | (byte)Stream.ReadByte();
                    }
                    return 0;
                }
                else
                {
                    Range -= newBound;
                    Code -= newBound;
                    probs[index] -= prob >> kNumMoveBits;

                    if (Range < kTopValue)
                    {
                        Range <<= 8;
                        Code = (Code << 8) | (byte)Stream.ReadByte();
                    }
                    return 1;
                }
            }
        }

        class BitDecoder
        {
            uint Prob;

            public void Init() => Prob = kBitModelTotal >> 1;

            public uint Decode(RangeDecoder rangeDecoder)
            {
                uint newBound = (rangeDecoder.Range >> kNumBitModelTotalBits) * Prob;

                if (rangeDecoder.Code < newBound)
                {
                    rangeDecoder.Range = newBound;
                    Prob += (kBitModelTotal - Prob) >> kNumMoveBits;

                    if (rangeDecoder.Range < kTopValue)
                    {
                        rangeDecoder.Range <<= 8;
                        rangeDecoder.Code = (rangeDecoder.Code << 8) | (byte)rangeDecoder.Stream.ReadByte();
                    }
                    return 0;
                }
                else
                {
                    rangeDecoder.Range -= newBound;
                    rangeDecoder.Code -= newBound;
                    Prob -= Prob >> kNumMoveBits;

                    if (rangeDecoder.Range < kTopValue)
                    {
                        rangeDecoder.Range <<= 8;
                        rangeDecoder.Code = (rangeDecoder.Code << 8) | (byte)rangeDecoder.Stream.ReadByte();
                    }
                    return 1;
                }
            }
        }

        class BitTreeDecoder
        {
            readonly BitDecoder[] Models;
            readonly int NumBitLevels;

            public BitTreeDecoder(int numBitLevels)
            {
                NumBitLevels = numBitLevels;
                Models = new BitDecoder[1 << numBitLevels];
            }

            public void Init()
            {
                for (int i = 1; i < Models.Length; i++)
                    Models[i].Init();
            }

            public uint Decode(RangeDecoder rangeDecoder)
            {
                uint m = 1;
                for (int bitIndex = NumBitLevels; bitIndex > 0; bitIndex--)
                    m = (m << 1) + Models[m].Decode(rangeDecoder);
                return m - ((uint)1 << NumBitLevels);
            }

            public uint ReverseDecode(RangeDecoder rangeDecoder)
            {
                uint m = 1;
                uint symbol = 0;

                for (int bitIndex = 0; bitIndex < NumBitLevels; bitIndex++)
                {
                    uint bit = Models[m].Decode(rangeDecoder);
                    m = (m << 1) + bit;
                    symbol |= (bit << bitIndex);
                }
                return symbol;
            }

            public static uint ReverseDecode(BitDecoder[] Models, uint startIndex, RangeDecoder rangeDecoder, int NumBitLevels)
            {
                uint m = 1;
                uint symbol = 0;

                for (int bitIndex = 0; bitIndex < NumBitLevels; bitIndex++)
                {
                    uint bit = Models[startIndex + m].Decode(rangeDecoder);
                    m = (m << 1) + bit;
                    symbol |= (bit << bitIndex);
                }
                return symbol;
            }
        }

        // ------------------------------
        // LZMA Decoder State
        // ------------------------------

        RangeDecoder rangeDecoder = new RangeDecoder();

        BitDecoder[] IsMatchDecoders = new BitDecoder[192];
        BitDecoder[] IsRepDecoders = new BitDecoder[12];
        BitDecoder[] IsRepG0Decoders = new BitDecoder[12];
        BitDecoder[] IsRepG1Decoders = new BitDecoder[12];
        BitDecoder[] IsRepG2Decoders = new BitDecoder[12];
        BitDecoder[] IsRep0LongDecoders = new BitDecoder[192];

        BitTreeDecoder[] PosSlotDecoder = new BitTreeDecoder[4];
        BitDecoder[] PosDecoders = new BitDecoder[114];
        BitTreeDecoder PosAlignDecoder = new BitTreeDecoder(4);

        LenDecoder LocalLenDecoder = new LenDecoder();
        LenDecoder RepLenDecoder = new LenDecoder();

        LiteralDecoder literalDecoder = new LiteralDecoder();

        uint DictionarySize;
        uint DictionarySizeCheck;
        uint PosStateMask;

        public Decoder()
        {
            for (int i = 0; i < 192; i++)
                IsMatchDecoders[i] = new BitDecoder();

            for (int i = 0; i < 12; i++)
            {
                IsRepDecoders[i] = new BitDecoder();
                IsRepG0Decoders[i] = new BitDecoder();
                IsRepG1Decoders[i] = new BitDecoder();
                IsRepG2Decoders[i] = new BitDecoder();
            }

            for (int i = 0; i < 192; i++)
                IsRep0LongDecoders[i] = new BitDecoder();

            for (int i = 0; i < 4; i++)
                PosSlotDecoder[i] = new BitTreeDecoder(6);
        }

        public void SetDecoderProperties(byte[] properties)
        {
            int lc = properties[0] % 9;
            int remainder = properties[0] / 9;
            int lp = remainder % 5;
            int pb = remainder / 5;

            literalDecoder.Create(lp, lc);

            uint dictionarySize = 0;
            for (int i = 0; i < 4; i++)
                dictionarySize += ((uint)properties[1 + i]) << (i * 8);

            SetDictionarySize(dictionarySize);
            SetPosBitsProperties(pb);
        }

        void SetDictionarySize(uint dictionarySize)
        {
            DictionarySize = dictionarySize;
            DictionarySizeCheck = Math.Max(dictionarySize, 1);
        }

        void SetPosBitsProperties(int pb)
        {
            PosStateMask = ((uint)1 << pb) - 1;
        }

        public void Init(Stream inStream)
        {
            rangeDecoder.Init(inStream);

            for (int i = 0; i < 192; i++)
                IsMatchDecoders[i].Init();

            for (int i = 0; i < 12; i++)
            {
                IsRepDecoders[i].Init();
                IsRepG0Decoders[i].Init();
                IsRepG1Decoders[i].Init();
                IsRepG2Decoders[i].Init();
            }

            for (int i = 0; i < 192; i++)
                IsRep0LongDecoders[i].Init();

            for (int i = 0; i < 4; i++)
                PosSlotDecoder[i].Init();

            for (int i = 0; i < 114; i++)
                PosDecoders[i].Init();

            PosAlignDecoder.Init();
            LocalLenDecoder.Init();
            RepLenDecoder.Init();
            literalDecoder.Init();
        }

        public void Code(Stream inStream, Stream outStream, long inSize, long outSize, object _)
        {
            Init(inStream);

            byte[] dictionary = new byte[DictionarySizeCheck];
            uint dictionaryPos = 0;

            uint rep0 = 0, rep1 = 0, rep2 = 0, rep3 = 0;

            long nowPos64 = 0;
            byte prevByte = 0;

            while (outSize < 0 || nowPos64 < outSize)
            {
                uint posState = (uint)nowPos64 & PosStateMask;

                if (IsMatchDecoders[(nowPos64 & 0xFF) + (posState << 8)].Decode(rangeDecoder) == 0)
                {
                    byte b = literalDecoder.DecodeNormal(rangeDecoder, (uint)nowPos64, prevByte);
                    outStream.WriteByte(b);
                    prevByte = b;

                    dictionary[dictionaryPos++] = b;
                    if (dictionaryPos == DictionarySizeCheck)
                        dictionaryPos = 0;

                    nowPos64++;
                    continue;
                }

                uint len;

                if (IsRepDecoders[nowPos64 & 0xFF].Decode(rangeDecoder) == 1)
                {
                    if (IsRepG0Decoders[nowPos64 & 0xFF].Decode(rangeDecoder) == 0)
                    {
                        if (IsRep0LongDecoders[(nowPos64 & 0xFF) + (posState << 8)].Decode(rangeDecoder) == 0)
                        {
                            byte b = dictionary[(dictionaryPos - rep0 + DictionarySizeCheck) % DictionarySizeCheck];
                            outStream.WriteByte(b);
                            prevByte = b;

                            dictionary[dictionaryPos++] = b;
                            if (dictionaryPos == DictionarySizeCheck)
                                dictionaryPos = 0;

                            nowPos64++;
                            continue;
                        }
                    }
                    else
                    {
                        uint distance;
                        if (IsRepG1Decoders[nowPos64 & 0xFF].Decode(rangeDecoder) == 0)
                            distance = rep1;
                        else
                        {
                            if (IsRepG2Decoders[nowPos64 & 0xFF].Decode(rangeDecoder) == 0)
                                distance = rep2;
                            else
                            {
                                distance = rep3;
                                rep3 = rep2;
                            }
                            rep2 = rep1;
                        }
                        rep1 = rep0;
                        rep0 = distance;
                    }

                    len = RepLenDecoder.Decode(rangeDecoder, posState) + 2;
                }
                else
                {
                    rep3 = rep2;
                    rep2 = rep1;
                    rep1 = rep0;

                    len = LocalLenDecoder.Decode(rangeDecoder, posState) + 2;

                    uint posSlot = PosSlotDecoder[len <= 5 ? len - 2 : 3].Decode(rangeDecoder);

                    if (posSlot >= 4)
                    {
                        int numDirectBits = (int)((posSlot >> 1) - 1);
                        rep0 = ((2 | (posSlot & 1)) << numDirectBits);

                        if (posSlot < 14)
                            rep0 += BitTreeDecoder.ReverseDecode(PosDecoders, rep0 - posSlot - 1, rangeDecoder, numDirectBits);
                        else
                        {
                            rep0 += (rangeDecoder.DecodeDirectBits(numDirectBits - 4) << 4);
                            rep0 += PosAlignDecoder.ReverseDecode(rangeDecoder);
                        }
                    }
                    else
                        rep0 = posSlot;
                }

                if (rep0 >= nowPos64 || rep0 >= DictionarySizeCheck)
                    throw new InvalidDataException("LZMA data corrupted");

                uint srcPos = (dictionaryPos - rep0 + DictionarySizeCheck) % DictionarySizeCheck;

                for (uint i = 0; i < len; i++)
                {
                    byte b = dictionary[srcPos++];
                    if (srcPos == DictionarySizeCheck)
                        srcPos = 0;

                    outStream.WriteByte(b);
                    prevByte = b;

                    dictionary[dictionaryPos++] = b;
                    if (dictionaryPos == DictionarySizeCheck)
                        dictionaryPos = 0;

                    nowPos64++;
                    if (outSize >= 0 && nowPos64 >= outSize)
                        break;
                }
            }
        }

        // ------------------------------
        // Literal Decoder
        // ------------------------------

        class LiteralDecoder
        {
            struct Decoder2
            {
                BitDecoder[] decoders;

                public void Create() => decoders = new BitDecoder[0x300];

                public void Init()
                {
                    foreach (var d in decoders)
                        d.Init();
                }

                public byte DecodeNormal(RangeDecoder rangeDecoder, uint pos, byte prevByte)
                {
                    uint symbol = 1;

                    do
                    {
                        symbol = (symbol << 1) | decoders[symbol].Decode(rangeDecoder);
                    }
                    while (symbol < 0x100);

                    return (byte)symbol;
                }
            }

            Decoder2[] coders;
            int numPrevBits;
            int numPosBits;
            uint posMask;

            public void Create(int lp, int lc)
            {
                numPrevBits = lc;
                numPosBits = lp;
                posMask = ((uint)1 << lp) - 1;

                uint numStates = (uint)1 << (lc + lp);
                coders = new Decoder2[numStates];

                for (uint i = 0; i < numStates; i++)
                {
                    coders[i].Create();
                }
            }

            public void Init()
            {
                foreach (var coder in coders)
                    coder.Init();
            }

            public byte DecodeNormal(RangeDecoder rangeDecoder, uint pos, byte prevByte)
            {
                uint state = ((pos & posMask) << numPrevBits) + (uint)(prevByte >> (8 - numPrevBits));
                return coders[state].DecodeNormal(rangeDecoder, pos, prevByte);
            }
        }

        // ------------------------------
        // Length Decoder
        // ------------------------------

        class LenDecoder
        {
            BitDecoder[] choice = new BitDecoder[2];
            BitTreeDecoder lowCoder = new BitTreeDecoder(3);
            BitTreeDecoder midCoder = new BitTreeDecoder(3);
            BitTreeDecoder highCoder = new BitTreeDecoder(8);

            public void Init()
            {
                choice[0].Init();
                choice[1].Init();
                lowCoder.Init();
                midCoder.Init();
                highCoder.Init();
            }

            public uint Decode(RangeDecoder rangeDecoder, uint posState)
            {
                if (choice[0].Decode(rangeDecoder) == 0)
                    return lowCoder.Decode(rangeDecoder);

                if (choice[1].Decode(rangeDecoder) == 0)
                    return 8 + midCoder.Decode(rangeDecoder);

                return 16 + highCoder.Decode(rangeDecoder);
            }
        }
    }
}
