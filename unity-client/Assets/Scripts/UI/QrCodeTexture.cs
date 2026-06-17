using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace AppreciatorsTcg.UI
{
    public static class QrCodeTexture
    {
        private const int Version = 5;
        private const int Size = Version * 4 + 17;
        private const int DataCodewords = 108;
        private const int EccCodewords = 26;
        private const int MaskPattern = 0;

        private static readonly int[] AlignmentCenters = { 6, 30 };
        private static readonly int[] Exp = new int[512];
        private static readonly int[] Log = new int[256];
        private static bool gfReady;

        public static Texture2D Create(string text, int pixelsPerModule = 8, int borderModules = 4)
        {
            bool[,] modules = Encode(text);
            int moduleCount = Size + borderModules * 2;
            int textureSize = moduleCount * pixelsPerModule;
            Texture2D texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            Color32 light = new Color32(248, 252, 255, 255);
            Color32 dark = new Color32(3, 5, 14, 255);

            for (int y = 0; y < textureSize; y++)
            {
                int moduleY = y / pixelsPerModule - borderModules;
                for (int x = 0; x < textureSize; x++)
                {
                    int moduleX = x / pixelsPerModule - borderModules;
                    bool isDark = moduleX >= 0 && moduleX < Size && moduleY >= 0 && moduleY < Size && modules[moduleX, moduleY];
                    texture.SetPixel(x, textureSize - 1 - y, isDark ? dark : light);
                }
            }

            texture.Apply(false, false);
            return texture;
        }

        private static bool[,] Encode(string text)
        {
            byte[] data = Encoding.UTF8.GetBytes(text ?? string.Empty);
            if (data.Length > 106)
            {
                throw new ArgumentException("Invite QR link is too long for the prototype QR encoder.");
            }

            bool[,] modules = new bool[Size, Size];
            bool[,] isFunction = new bool[Size, Size];
            DrawFunctionPatterns(modules, isFunction);

            byte[] dataCodewords = CreateDataCodewords(data);
            byte[] allCodewords = AppendErrorCorrection(dataCodewords);
            DrawCodewords(modules, isFunction, allCodewords);
            DrawFormatBits(modules, isFunction);
            return modules;
        }

        private static void DrawFunctionPatterns(bool[,] modules, bool[,] isFunction)
        {
            DrawFinderPattern(modules, isFunction, 0, 0);
            DrawFinderPattern(modules, isFunction, Size - 7, 0);
            DrawFinderPattern(modules, isFunction, 0, Size - 7);

            for (int i = 8; i < Size - 8; i++)
            {
                SetFunction(modules, isFunction, i, 6, i % 2 == 0);
                SetFunction(modules, isFunction, 6, i, i % 2 == 0);
            }

            foreach (int x in AlignmentCenters)
            {
                foreach (int y in AlignmentCenters)
                {
                    if (isFunction[x, y])
                    {
                        continue;
                    }

                    DrawAlignmentPattern(modules, isFunction, x, y);
                }
            }

            for (int i = 0; i <= 8; i++)
            {
                if (i != 6)
                {
                    SetFunction(modules, isFunction, 8, i, false);
                    SetFunction(modules, isFunction, i, 8, false);
                }
            }

            for (int i = 0; i < 8; i++)
            {
                SetFunction(modules, isFunction, Size - 1 - i, 8, false);
                SetFunction(modules, isFunction, 8, Size - 1 - i, false);
            }

            SetFunction(modules, isFunction, 8, Size - 8, true);
        }

        private static void DrawFinderPattern(bool[,] modules, bool[,] isFunction, int left, int top)
        {
            for (int dy = -1; dy <= 7; dy++)
            {
                for (int dx = -1; dx <= 7; dx++)
                {
                    int x = left + dx;
                    int y = top + dy;
                    if (x < 0 || x >= Size || y < 0 || y >= Size)
                    {
                        continue;
                    }

                    bool dark = dx >= 0 && dx <= 6 && dy >= 0 && dy <= 6 &&
                        (dx == 0 || dx == 6 || dy == 0 || dy == 6 || (dx >= 2 && dx <= 4 && dy >= 2 && dy <= 4));
                    SetFunction(modules, isFunction, x, y, dark);
                }
            }
        }

        private static void DrawAlignmentPattern(bool[,] modules, bool[,] isFunction, int centerX, int centerY)
        {
            for (int dy = -2; dy <= 2; dy++)
            {
                for (int dx = -2; dx <= 2; dx++)
                {
                    bool dark = Math.Max(Math.Abs(dx), Math.Abs(dy)) != 1;
                    SetFunction(modules, isFunction, centerX + dx, centerY + dy, dark);
                }
            }
        }

        private static void SetFunction(bool[,] modules, bool[,] isFunction, int x, int y, bool dark)
        {
            modules[x, y] = dark;
            isFunction[x, y] = true;
        }

        private static byte[] CreateDataCodewords(byte[] data)
        {
            List<int> bits = new List<int>();
            AppendBits(bits, 0b0100, 4);
            AppendBits(bits, data.Length, 8);
            foreach (byte value in data)
            {
                AppendBits(bits, value, 8);
            }

            int capacityBits = DataCodewords * 8;
            int terminatorBits = Math.Min(4, capacityBits - bits.Count);
            AppendBits(bits, 0, terminatorBits);
            while (bits.Count % 8 != 0)
            {
                bits.Add(0);
            }

            byte[] result = new byte[DataCodewords];
            int byteCount = bits.Count / 8;
            for (int i = 0; i < byteCount; i++)
            {
                int value = 0;
                for (int j = 0; j < 8; j++)
                {
                    value = (value << 1) | bits[i * 8 + j];
                }

                result[i] = (byte)value;
            }

            byte pad = 0xEC;
            for (int i = byteCount; i < result.Length; i++)
            {
                result[i] = pad;
                pad = pad == 0xEC ? (byte)0x11 : (byte)0xEC;
            }

            return result;
        }

        private static void AppendBits(List<int> bits, int value, int length)
        {
            for (int i = length - 1; i >= 0; i--)
            {
                bits.Add((value >> i) & 1);
            }
        }

        private static byte[] AppendErrorCorrection(byte[] dataCodewords)
        {
            byte[] ecc = ReedSolomonRemainder(dataCodewords, EccCodewords);
            byte[] result = new byte[dataCodewords.Length + ecc.Length];
            Buffer.BlockCopy(dataCodewords, 0, result, 0, dataCodewords.Length);
            Buffer.BlockCopy(ecc, 0, result, dataCodewords.Length, ecc.Length);
            return result;
        }

        private static void DrawCodewords(bool[,] modules, bool[,] isFunction, byte[] codewords)
        {
            int bitIndex = 0;
            bool upward = true;
            for (int right = Size - 1; right >= 1; right -= 2)
            {
                if (right == 6)
                {
                    right = 5;
                }

                for (int vertical = 0; vertical < Size; vertical++)
                {
                    int y = upward ? Size - 1 - vertical : vertical;
                    for (int column = 0; column < 2; column++)
                    {
                        int x = right - column;
                        if (isFunction[x, y])
                        {
                            continue;
                        }

                        bool dark = false;
                        if (bitIndex < codewords.Length * 8)
                        {
                            dark = ((codewords[bitIndex >> 3] >> (7 - (bitIndex & 7))) & 1) != 0;
                            bitIndex += 1;
                        }

                        if (((x + y) & 1) == 0)
                        {
                            dark = !dark;
                        }

                        modules[x, y] = dark;
                    }
                }

                upward = !upward;
            }
        }

        private static void DrawFormatBits(bool[,] modules, bool[,] isFunction)
        {
            int data = (1 << 3) | MaskPattern;
            int rem = data;
            for (int i = 0; i < 10; i++)
            {
                rem = (rem << 1) ^ (((rem >> 9) & 1) != 0 ? 0x537 : 0);
            }

            int bits = ((data << 10) | rem) ^ 0x5412;

            for (int i = 0; i <= 5; i++)
            {
                SetFunction(modules, isFunction, 8, i, GetBit(bits, i));
            }

            SetFunction(modules, isFunction, 8, 7, GetBit(bits, 6));
            SetFunction(modules, isFunction, 8, 8, GetBit(bits, 7));
            SetFunction(modules, isFunction, 7, 8, GetBit(bits, 8));

            for (int i = 9; i < 15; i++)
            {
                SetFunction(modules, isFunction, 14 - i, 8, GetBit(bits, i));
            }

            for (int i = 0; i < 8; i++)
            {
                SetFunction(modules, isFunction, Size - 1 - i, 8, GetBit(bits, i));
            }

            for (int i = 8; i < 15; i++)
            {
                SetFunction(modules, isFunction, 8, Size - 15 + i, GetBit(bits, i));
            }

            SetFunction(modules, isFunction, 8, Size - 8, true);
        }

        private static bool GetBit(int value, int index)
        {
            return ((value >> index) & 1) != 0;
        }

        private static byte[] ReedSolomonRemainder(byte[] data, int degree)
        {
            EnsureGaloisField();
            byte[] divisor = ReedSolomonDivisor(degree);
            byte[] result = new byte[degree];

            foreach (byte dataByte in data)
            {
                int factor = dataByte ^ result[0];
                Array.Copy(result, 1, result, 0, result.Length - 1);
                result[result.Length - 1] = 0;

                for (int i = 0; i < result.Length; i++)
                {
                    result[i] ^= GfMultiply(divisor[i], factor);
                }
            }

            return result;
        }

        private static byte[] ReedSolomonDivisor(int degree)
        {
            byte[] result = new byte[degree];
            result[degree - 1] = 1;
            int root = 1;

            for (int i = 0; i < degree; i++)
            {
                for (int j = 0; j < result.Length; j++)
                {
                    result[j] = GfMultiply(result[j], root);
                    if (j + 1 < result.Length)
                    {
                        result[j] ^= result[j + 1];
                    }
                }

                root = GfMultiply(root, 0x02);
            }

            return result;
        }

        private static void EnsureGaloisField()
        {
            if (gfReady)
            {
                return;
            }

            int x = 1;
            for (int i = 0; i < 255; i++)
            {
                Exp[i] = x;
                Log[x] = i;
                x <<= 1;
                if ((x & 0x100) != 0)
                {
                    x ^= 0x11D;
                }
            }

            for (int i = 255; i < Exp.Length; i++)
            {
                Exp[i] = Exp[i - 255];
            }

            gfReady = true;
        }

        private static byte GfMultiply(int x, int y)
        {
            if (x == 0 || y == 0)
            {
                return 0;
            }

            return (byte)Exp[Log[x] + Log[y]];
        }
    }
}
