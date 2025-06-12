namespace SceneNavi.Services
{
 using System;
using System.IO;
using System.IO.Compression;

namespace SceneNavi.Services
{
    public class DecompressionService
    {
        /// <summary>
        /// Decompresses bytes using the specified compression algorithm
        /// </summary>
        /// <param name="compressedData">The compressed byte array</param>
        /// <param name="compressionType">The type of compression used</param>
        /// <returns>Decompressed byte array</returns>
        public byte[] DecompressBytes(byte[] compressedData, CompressionType compressionType)
        {
            if (compressedData == null || compressedData.Length == 0)
                throw new ArgumentException("Compressed data cannot be null or empty");

            switch (compressionType)
            {
                case CompressionType.GZip:
                    return DecompressGZip(compressedData);
                case CompressionType.Deflate:
                    return DecompressDeflate(compressedData);
                case CompressionType.Yaz0:
                    return DecompressYaz0(compressedData);
                case CompressionType.LZ77:
                    return DecompressLZ77(compressedData);
                default:
                    throw new NotSupportedException($"Compression type {compressionType} is not supported");
            }
        }

        /// <summary>
        /// Auto-detects compression type and decompresses the data
        /// </summary>
        /// <param name="compressedData">The compressed byte array</param>
        /// <returns>Decompressed byte array</returns>
        public byte[] DecompressBytes(byte[] compressedData)
        {
            var compressionType = DetectCompressionType(compressedData);
            return DecompressBytes(compressedData, compressionType);
        }

        /// <summary>
        /// Detects the compression type based on magic bytes
        /// </summary>
        /// <param name="data">The data to analyze</param>
        /// <returns>Detected compression type</returns>
        public CompressionType DetectCompressionType(byte[] data)
        {
            if (data == null || data.Length < 4)
                throw new ArgumentException("Data is too short to detect compression type");

            // Check for Yaz0 magic bytes (common in Nintendo ROMs)
            if (data.Length >= 4 &&
                data[0] == 0x59 && data[1] == 0x61 && data[2] == 0x7A && data[3] == 0x30)
            {
                return CompressionType.Yaz0;
            }

            // Check for GZip magic bytes
            if (data.Length >= 2 && data[0] == 0x1F && data[1] == 0x8B)
            {
                return CompressionType.GZip;
            }

            // Check for common LZ77 patterns (this is heuristic)
            if (IsLikelyLZ77(data))
            {
                return CompressionType.LZ77;
            }

            // Default to Deflate for other cases
            return CompressionType.Deflate;
        }

        private byte[] DecompressGZip(byte[] compressedData)
        {
            using (var compressedStream = new MemoryStream(compressedData))
            using (var gzipStream = new GZipStream(compressedStream, CompressionMode.Decompress))
            using (var decompressedStream = new MemoryStream())
            {
                gzipStream.CopyTo(decompressedStream);
                return decompressedStream.ToArray();
            }
        }

        private byte[] DecompressDeflate(byte[] compressedData)
        {
            using (var compressedStream = new MemoryStream(compressedData))
            using (var deflateStream = new DeflateStream(compressedStream, CompressionMode.Decompress))
            using (var decompressedStream = new MemoryStream())
            {
                deflateStream.CopyTo(decompressedStream);
                return decompressedStream.ToArray();
            }
        }

        private byte[] DecompressYaz0(byte[] compressedData)
        {
            if (compressedData.Length < 16)
                throw new ArgumentException("Invalid Yaz0 data - too short");

            // Read Yaz0 header
            var decompressedSize = (compressedData[4] << 24) | (compressedData[5] << 16) |
                                   (compressedData[6] << 8) | compressedData[7];

            var decompressed = new byte[decompressedSize];
            var srcPos = 16; // Skip header
            var dstPos = 0;

            while (dstPos < decompressedSize && srcPos < compressedData.Length)
            {
                var validBits = compressedData[srcPos++];

                for (int i = 0; i < 8 && dstPos < decompressedSize && srcPos < compressedData.Length; i++)
                {
                    if ((validBits & (0x80 >> i)) != 0)
                    {
                        // Direct copy
                        decompressed[dstPos++] = compressedData[srcPos++];
                    }
                    else
                    {
                        // RLE copy
                        if (srcPos + 1 >= compressedData.Length) break;

                        var byte1 = compressedData[srcPos++];
                        var byte2 = compressedData[srcPos++];

                        var distance = ((byte1 & 0x0F) << 8) | byte2;
                        var length = (byte1 >> 4) + 3;

                        if (distance == 0) break;

                        var copyPos = dstPos - distance - 1;
                        for (int j = 0; j < length && dstPos < decompressedSize; j++)
                        {
                            if (copyPos >= 0)
                                decompressed[dstPos] = decompressed[copyPos];
                            dstPos++;
                            copyPos++;
                        }
                    }
                }
            }

            return decompressed;
        }

        private byte[] DecompressLZ77(byte[] compressedData)
        {
            if (compressedData.Length < 4)
                throw new ArgumentException("Invalid LZ77 data - too short");

            // Simple LZ77 implementation - may need adjustment based on specific ROM format
            var decompressedSize = compressedData[1] | (compressedData[2] << 8) | (compressedData[3] << 16);
            var decompressed = new byte[decompressedSize];
            var srcPos = 4;
            var dstPos = 0;

            while (dstPos < decompressedSize && srcPos < compressedData.Length)
            {
                var flags = compressedData[srcPos++];

                for (int i = 0; i < 8 && dstPos < decompressedSize && srcPos < compressedData.Length; i++)
                {
                    if ((flags & (0x80 >> i)) == 0)
                    {
                        // Literal byte
                        decompressed[dstPos++] = compressedData[srcPos++];
                    }
                    else
                    {
                        // Length-distance pair
                        if (srcPos + 1 >= compressedData.Length) break;

                        var byte1 = compressedData[srcPos++];
                        var byte2 = compressedData[srcPos++];

                        var length = ((byte1 >> 4) & 0x0F) + 3;
                        var distance = ((byte1 & 0x0F) << 8) | byte2;

                        var copyPos = dstPos - distance - 1;
                        for (int j = 0; j < length && dstPos < decompressedSize; j++)
                        {
                            if (copyPos >= 0 && copyPos < dstPos)
                                decompressed[dstPos] = decompressed[copyPos];
                            dstPos++;
                            copyPos++;
                        }
                    }
                }
            }

            return decompressed;
        }

        private bool IsLikelyLZ77(byte[] data)
        {
            // Simple heuristic to detect LZ77 format
            // This checks if first byte looks like a compression type identifier
            return data.Length >= 4 && (data[0] == 0x10 || data[0] == 0x11 || data[0] == 0x40);
        }

        /// <summary>
        /// Gets the decompressed size without fully decompressing (when possible)
        /// </summary>
        /// <param name="compressedData">The compressed data</param>
        /// <param name="compressionType">The compression type</param>
        /// <returns>Decompressed size or -1 if cannot be determined</returns>
        public int GetDecompressedSize(byte[] compressedData, CompressionType compressionType)
        {
            switch (compressionType)
            {
                case CompressionType.Yaz0:
                    if (compressedData.Length >= 8)
                        return (compressedData[4] << 24) | (compressedData[5] << 16) |
                               (compressedData[6] << 8) | compressedData[7];
                    break;
                case CompressionType.LZ77:
                    if (compressedData.Length >= 4)
                        return compressedData[1] | (compressedData[2] << 8) | (compressedData[3] << 16);
                    break;
            }

            return -1;
        }
    }

    public enum CompressionType
    {
        GZip,
        Deflate,
        Yaz0,
        LZ77
    }
}
}