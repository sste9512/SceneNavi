using System;
using OpenTK.Graphics;

namespace SceneNavi.SimpleF3DEX2
{
    public static class ImageHelper
    {
        #region RGBA

        private static void Rgba16(int width, int height, int lineSize, byte[] source, int sourceOffset,
            ref byte[] target)
        {
            var targetOffset = 0;

            for (var j = 0; j < height; j++)
            {
                for (var i = 0; i < width; i++)
                {
                    if (sourceOffset >= source.Length) return;

                    var raw = (UInt16) ((source[sourceOffset] << 8) | source[sourceOffset + 1]);
                    target[targetOffset] = (byte) ((raw & 0xF800) >> 8);
                    target[targetOffset + 1] = (byte) (((raw & 0x07C0) << 5) >> 8);
                    target[targetOffset + 2] = (byte) (((raw & 0x003E) << 18) >> 16);
                    target[targetOffset + 3] = 0;
                    if ((raw & 0x0001) == 1) target[targetOffset + 3] = 0xFF;

                    sourceOffset += 2;
                    targetOffset += 4;
                }

                sourceOffset += lineSize * 4 - width;
            }
        }

        private static void Rgba32(byte[] source, int sourceOffset, ref byte[] target)
        {
            Buffer.BlockCopy(source, (int) sourceOffset, target, 0, (int) target.Length);
        }

        #endregion

        #region CI

        private static void Ci4(int width, int height, int lineSize, byte[] source, int sourceOffset, ref byte[] target,
            int palette, Color4[] palColors)
        {
            var targetOffset = 0;

            for (var j = 0; j < height; j++)
            {
                for (var i = 0; i < width; i++)
                {
                    if (sourceOffset >= source.Length) return;

                    var ciIndex = (byte) ((source[sourceOffset]) + (palette << 4));

                    target[targetOffset] = (byte) palColors[ciIndex].R;
                    target[targetOffset + 1] = (byte) palColors[ciIndex].G;
                    target[targetOffset + 2] = (byte) palColors[ciIndex].B;
                    target[targetOffset + 3] = (byte) palColors[ciIndex].A;

                    sourceOffset++;
                    targetOffset += 4;
                }

                sourceOffset += lineSize * 8 - width;
            }
        }

        private static void Ci8(int width, int height, int lineSize, byte[] source, int sourceOffset, ref byte[] target,
            Color4[] palColors)
        {
            var targetOffset = 0;

            for (var j = 0; j < height; j++)
            {
                for (var i = 0; i < width / 2; i++)
                {
                    if (sourceOffset >= source.Length) return;

                    var ciIndex1 = (byte) ((source[sourceOffset] & 0xF0) >> 4);
                    var ciIndex2 = (byte) (source[sourceOffset] & 0x0F);

                    target[targetOffset] = (byte) palColors[ciIndex1].R;
                    target[targetOffset + 1] = (byte) palColors[ciIndex1].G;
                    target[targetOffset + 2] = (byte) palColors[ciIndex1].B;
                    target[targetOffset + 3] = (byte) palColors[ciIndex1].A;

                    target[targetOffset + 4] = (byte) palColors[ciIndex2].R;
                    target[targetOffset + 5] = (byte) palColors[ciIndex2].G;
                    target[targetOffset + 6] = (byte) palColors[ciIndex2].B;
                    target[targetOffset + 7] = (byte) palColors[ciIndex2].A;

                    sourceOffset++;
                    targetOffset += 8;
                }

                sourceOffset += lineSize * 8 - (width / 2);
            }
        }

        #endregion

        #region IA

        private static void Ia4(int width, int height, int lineSize, byte[] source, int sourceOffset, ref byte[] target)
        {
            var targetOffset = 0;

            for (var j = 0; j < height; j++)
            {
                for (var i = 0; i < width / 2; i++)
                {
                    if (sourceOffset >= source.Length) return;

                    var raw = (byte) ((source[sourceOffset] & 0xF0) >> 4);
                    target[targetOffset] = (byte) ((raw & 0x0E) << 4);
                    target[targetOffset + 1] = (byte) ((raw & 0x0E) << 4);
                    target[targetOffset + 2] = (byte) ((raw & 0x0E) << 4);
                    target[targetOffset + 3] = 0;
                    if ((raw & 0x0001) == 1) target[targetOffset + 3] = 0xFF;

                    raw = (byte) (source[sourceOffset] & 0x0F);
                    target[targetOffset + 4] = (byte) ((raw & 0x0E) << 4);
                    target[targetOffset + 5] = (byte) ((raw & 0x0E) << 4);
                    target[targetOffset + 6] = (byte) ((raw & 0x0E) << 4);
                    target[targetOffset + 7] = 0;
                    if ((raw & 0x0001) == 1) target[targetOffset + 7] = 0xFF;

                    sourceOffset++;
                    targetOffset += 8;
                }

                sourceOffset += lineSize * 8 - (width / 2);
            }
        }

        public static void Ia8(int width, int height, int lineSize, byte[] source, int sourceOffset, ref byte[] target)
        {
            var targetOffset = 0;

            for (var j = 0; j < height; j++)
            {
                for (var i = 0; i < width; i++)
                {
                    if (sourceOffset >= source.Length) return;

                    var raw = (byte) (source[sourceOffset]);
                    target[targetOffset] = (byte) ((raw & 0xF0) + 0x0F);
                    target[targetOffset + 1] = (byte) ((raw & 0xF0) + 0x0F);
                    target[targetOffset + 2] = (byte) ((raw & 0xF0) + 0x0F);
                    target[targetOffset + 3] = (byte) ((raw & 0x0F) << 4);

                    sourceOffset++;
                    targetOffset += 4;
                }

                sourceOffset += lineSize * 8 - width;
            }
        }

        private static void Ia16(int width, int height, int lineSize, byte[] source, int sourceOffset,
            ref byte[] target)
        {
            var targetOffset = 0;

            for (var j = 0; j < height; j++)
            {
                for (var i = 0; i < width; i++)
                {
                    if (sourceOffset >= source.Length) return;

                    target[targetOffset] = source[sourceOffset];
                    target[targetOffset + 1] = source[sourceOffset];
                    target[targetOffset + 2] = source[sourceOffset];
                    target[targetOffset + 3] = source[sourceOffset + 1];

                    sourceOffset += 2;
                    targetOffset += 4;
                }

                sourceOffset += lineSize * 4 - width;
            }
        }

        #endregion

        #region I

        private static void I4(int width, int height, int lineSize, byte[] source, int sourceOffset, ref byte[] target)
        {
            var targetOffset = 0;

            for (var j = 0; j < height; j++)
            {
                for (var i = 0; i < width / 2; i++)
                {
                    if (sourceOffset >= source.Length) return;

                    var raw = (byte) ((source[sourceOffset] & 0xF0) >> 4);
                    target[targetOffset] = (byte) ((raw & 0x0E) << 4);
                    target[targetOffset + 1] = (byte) ((raw & 0x0E) << 4);
                    target[targetOffset + 2] = (byte) ((raw & 0x0E) << 4);
                    target[targetOffset + 3] = (byte) ((raw & 0x0E) << 4);

                    raw = (byte) (source[sourceOffset] & 0x0F);
                    target[targetOffset + 4] = (byte) ((raw & 0x0E) << 4);
                    target[targetOffset + 5] = (byte) ((raw & 0x0E) << 4);
                    target[targetOffset + 6] = (byte) ((raw & 0x0E) << 4);
                    target[targetOffset + 7] = (byte) ((raw & 0x0E) << 4);

                    sourceOffset++;
                    targetOffset += 8;
                }

                sourceOffset += lineSize * 8 - (width / 2);
            }
        }

        private static void I8(int width, int height, int lineSize, byte[] source, int sourceOffset, ref byte[] target)
        {
            var targetOffset = 0;

            for (var j = 0; j < height; j++)
            {
                for (var i = 0; i < width; i++)
                {
                    if (sourceOffset >= source.Length) return;

                    target[targetOffset] = source[sourceOffset];
                    target[targetOffset + 1] = source[sourceOffset];
                    target[targetOffset + 2] = source[sourceOffset];
                    target[targetOffset + 3] = source[sourceOffset];

                    sourceOffset++;
                    targetOffset += 4;
                }

                sourceOffset += lineSize * 8 - width;
            }
        }

        #endregion

        #region Main Function

        public static void Convert(int format, byte[] source, int sourceOffset, ref byte[] target, int width,
            int height, int lineSize, int palette, Color4[] palColors)
        {
            try
            {
                if (sourceOffset < source.Length)
                {
                    switch (format)
                    {
                        case 0x00:
                        case 0x08:
                        case 0x10:
                            Rgba16(width, height, lineSize, source, sourceOffset, ref target);
                            break;
                        case 0x18:
                            Rgba32(source, sourceOffset, ref target);
                            break;
                        case 0x40:
                            //case 0x50:
                            Ci8(width, height, lineSize, source, sourceOffset, ref target, palColors);
                            break;
                        case 0x48:
                            Ci4(width, height, lineSize, source, sourceOffset, ref target, palette, palColors);
                            break;
                        case 0x60:
                            Ia4(width, height, lineSize, source, sourceOffset, ref target);
                            break;
                        case 0x68:
                            Ia8(width, height, lineSize, source, sourceOffset, ref target);
                            break;
                        case 0x70:
                            Ia16(width, height, lineSize, source, sourceOffset, ref target);
                            break;
                        case 0x80:
                        case 0x90:
                            I4(width, height, lineSize, source, sourceOffset, ref target);
                            break;
                        case 0x88:
                            I8(width, height, lineSize, source, sourceOffset, ref target);
                            break;
                        default:
                            // Unknown format -> blue texture
                            target.Fill(new byte[] {0x00, 0x00, 0xFF, 0xFF});
                            break;
                    }
                }
                else
                    target.Fill(new byte[] {0x00, 0xFF, 0x00, 0xFF});
            }
            catch
            {
                // Conversion error -> red texture
                target.Fill(new byte[] {0xFF, 0x00, 0x00, 0xFF});
            }
        }

        #endregion
    }
}