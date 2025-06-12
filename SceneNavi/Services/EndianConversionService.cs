using System;
using System.IO;
using System.Threading.Tasks;

namespace SceneNavi.Services
{
    /// <summary>
    /// Service for converting ROM files between big endian and little endian byte ordering
    /// </summary>
    public class EndianConversionService
    {
        /// <summary>
        /// Specifies the endian format
        /// </summary>
        public enum EndianFormat
        {
            BigEndian,
            LittleEndian
        }

        /// <summary>
        /// Converts a ROM file from one endian format to another
        /// </summary>
        /// <param name="inputFilePath">Path to the input ROM file</param>
        /// <param name="outputFilePath">Path where the converted ROM will be saved</param>
        /// <param name="targetEndian">The target endian format to convert to</param>
        /// <param name="chunkSize">Size of chunks to process at once (in bytes, must be even)</param>
        /// <returns>True if conversion was successful, false otherwise</returns>
        public bool ConvertRomEndianness(string inputFilePath, string outputFilePath, EndianFormat targetEndian, int chunkSize = 1024 * 1024)
        {
            try
            {
                if (!File.Exists(inputFilePath))
                {
                    throw new FileNotFoundException("Input ROM file not found", inputFilePath);
                }

                if (chunkSize % 2 != 0)
                {
                    throw new ArgumentException("Chunk size must be an even number", nameof(chunkSize));
                }

                using (var inputStream = new FileStream(inputFilePath, FileMode.Open, FileAccess.Read))
                using (var outputStream = new FileStream(outputFilePath, FileMode.Create, FileAccess.Write))
                {
                    byte[] buffer = new byte[chunkSize];
                    int bytesRead;

                    while ((bytesRead = inputStream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        // Process even number of bytes (2-byte words)
                        int alignedLength = bytesRead - (bytesRead % 2);
                        
                        if (alignedLength > 0)
                        {
                            // Process the data in 2-byte chunks
                            for (int i = 0; i < alignedLength; i += 2)
                            {
                                // Swap bytes using the existing Endian utility
                                ushort word = BitConverter.ToUInt16(buffer, i);
                                word = Endian.SwapUInt16(word);
                                
                                byte[] swapped = BitConverter.GetBytes(word);
                                buffer[i] = swapped[0];
                                buffer[i + 1] = swapped[1];
                            }
                            
                            // Write the processed bytes
                            outputStream.Write(buffer, 0, alignedLength);
                        }
                        
                        // If we had an odd number of bytes, write the last byte unchanged
                        if (bytesRead > alignedLength)
                        {
                            outputStream.WriteByte(buffer[bytesRead - 1]);
                        }
                    }
                }
                
                return true;
            }
            catch (Exception ex)
            {
                // In a real application, you would log this exception
                Console.WriteLine($"Error converting ROM endianness: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Asynchronously converts a ROM file from one endian format to another
        /// </summary>
        /// <param name="inputFilePath">Path to the input ROM file</param>
        /// <param name="outputFilePath">Path where the converted ROM will be saved</param>
        /// <param name="targetEndian">The target endian format to convert to</param>
        /// <param name="chunkSize">Size of chunks to process at once (in bytes, must be even)</param>
        /// <returns>Task representing the asynchronous operation</returns>
        public async Task<bool> ConvertRomEndiannessAsync(string inputFilePath, string outputFilePath, EndianFormat targetEndian, int chunkSize = 1024 * 1024)
        {
            try
            {
                if (!File.Exists(inputFilePath))
                {
                    throw new FileNotFoundException("Input ROM file not found", inputFilePath);
                }

                if (chunkSize % 2 != 0)
                {
                    throw new ArgumentException("Chunk size must be an even number", nameof(chunkSize));
                }

                using (var inputStream = new FileStream(inputFilePath, FileMode.Open, FileAccess.Read))
                using (var outputStream = new FileStream(outputFilePath, FileMode.Create, FileAccess.Write))
                {
                    byte[] buffer = new byte[chunkSize];
                    int bytesRead;

                    while ((bytesRead = await inputStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        // Process even number of bytes (2-byte words)
                        int alignedLength = bytesRead - (bytesRead % 2);
                        
                        if (alignedLength > 0)
                        {
                            // Process the data in 2-byte chunks
                            for (int i = 0; i < alignedLength; i += 2)
                            {
                                // Swap bytes using the existing Endian utility
                                ushort word = BitConverter.ToUInt16(buffer, i);
                                word = Endian.SwapUInt16(word);
                                
                                byte[] swapped = BitConverter.GetBytes(word);
                                buffer[i] = swapped[0];
                                buffer[i + 1] = swapped[1];
                            }
                            
                            // Write the processed bytes
                            await outputStream.WriteAsync(buffer, 0, alignedLength);
                        }
                        
                        // If we had an odd number of bytes, write the last byte unchanged
                        if (bytesRead > alignedLength)
                        {
                            outputStream.WriteByte(buffer[bytesRead - 1]);
                        }
                    }
                }
                
                return true;
            }
            catch (Exception ex)
            {
                // In a real application, you would log this exception
                Console.WriteLine($"Error converting ROM endianness: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Detects the endian format of a ROM file by analyzing its content
        /// This is a simple implementation and may need to be adjusted based on specific ROM formats
        /// </summary>
        /// <param name="filePath">Path to the ROM file</param>
        /// <returns>Detected endian format or null if detection fails</returns>
        public EndianFormat? DetectRomEndianness(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    throw new FileNotFoundException("ROM file not found", filePath);
                }

                // This is a simplified detection method
                // In a real application, you would need a more sophisticated algorithm
                // based on known magic numbers or file signatures for the specific ROM format
                
                using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    if (stream.Length < 4)
                    {
                        // File is too small to determine endianness
                        return null;
                    }

                    byte[] header = new byte[4];
                    stream.Read(header, 0, 4);

                    // This is a basic detection based on N64 ROM header
                    // For real applications, you would need more sophisticated logic
                    if (header[0] == 0x80 && header[1] == 0x37)
                    {
                        return EndianFormat.BigEndian;
                    }
                    else if (header[0] == 0x37 && header[1] == 0x80)
                    {
                        return EndianFormat.LittleEndian;
                    }
                    else
                    {
                        // Could not determine endianness with this simple check
                        return null;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error detecting ROM endianness: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Converts a ROM file between endian formats using a specified word size
        /// </summary>
        /// <param name="inputFilePath">Path to the input ROM file</param>
        /// <param name="outputFilePath">Path where the converted ROM will be saved</param>
        /// <param name="targetEndian">The target endian format to convert to</param>
        /// <param name="wordSize">Size of words to swap (2, 4, or 8 bytes)</param>
        /// <param name="chunkSize">Size of chunks to process at once (in bytes)</param>
        /// <returns>True if conversion was successful, false otherwise</returns>
        public bool ConvertRomEndiannessAdvanced(string inputFilePath, string outputFilePath, EndianFormat targetEndian, int wordSize = 2, int chunkSize = 1024 * 1024)
        {
            try
            {
                if (!File.Exists(inputFilePath))
                {
                    throw new FileNotFoundException("Input ROM file not found", inputFilePath);
                }

                // Validate word size (must be 2, 4, or 8)
                if (wordSize != 2 && wordSize != 4 && wordSize != 8)
                {
                    throw new ArgumentException("Word size must be 2, 4, or 8 bytes", nameof(wordSize));
                }

                // Ensure chunk size is a multiple of word size
                if (chunkSize % wordSize != 0)
                {
                    chunkSize = (chunkSize / wordSize) * wordSize;
                    if (chunkSize == 0) chunkSize = wordSize;
                }

                using (var inputStream = new FileStream(inputFilePath, FileMode.Open, FileAccess.Read))
                using (var outputStream = new FileStream(outputFilePath, FileMode.Create, FileAccess.Write))
                {
                    byte[] buffer = new byte[chunkSize];
                    int bytesRead;

                    while ((bytesRead = inputStream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        // Process multiples of word size
                        int alignedLength = bytesRead - (bytesRead % wordSize);
                        
                        if (alignedLength > 0)
                        {
                            for (int i = 0; i < alignedLength; i += wordSize)
                            {
                                // Swap bytes according to word size using existing Endian utility
                                switch (wordSize)
                                {
                                    case 2: // 16-bit words
                                        ushort word16 = BitConverter.ToUInt16(buffer, i);
                                        word16 = Endian.SwapUInt16(word16);
                                        
                                        byte[] swapped16 = BitConverter.GetBytes(word16);
                                        Buffer.BlockCopy(swapped16, 0, buffer, i, 2);
                                        break;
                                        
                                    case 4: // 32-bit words
                                        uint word32 = BitConverter.ToUInt32(buffer, i);
                                        word32 = Endian.SwapUInt32(word32);
                                        
                                        byte[] swapped32 = BitConverter.GetBytes(word32);
                                        Buffer.BlockCopy(swapped32, 0, buffer, i, 4);
                                        break;
                                        
                                    case 8: // 64-bit words
                                        ulong word64 = BitConverter.ToUInt64(buffer, i);
                                        word64 = Endian.SwapUInt64(word64);
                                        
                                        byte[] swapped64 = BitConverter.GetBytes(word64);
                                        Buffer.BlockCopy(swapped64, 0, buffer, i, 8);
                                        break;
                                }
                            }
                            
                            // Write the processed bytes
                            outputStream.Write(buffer, 0, alignedLength);
                        }
                        
                        // Handle any remaining bytes (less than a full word)
                        if (bytesRead > alignedLength)
                        {
                            // Just copy remaining bytes as-is
                            outputStream.Write(buffer, alignedLength, bytesRead - alignedLength);
                        }
                    }
                }
                
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error converting ROM endianness: {ex.Message}");
                return false;
            }
        }
    }
} 