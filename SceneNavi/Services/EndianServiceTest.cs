using System;
using System.IO;
using System.Windows.Forms;

namespace SceneNavi.Services
{
    /// <summary>
    /// A simple utility class to test the EndianConversionService
    /// </summary>
    public class EndianServiceTest
    {
        private readonly EndianConversionService _service;

        public EndianServiceTest()
        {
            _service = new EndianConversionService();
        }

        /// <summary>
        /// Shows a dialog to select a ROM file and convert its endianness
        /// </summary>
        public void ConvertRomFile()
        {
            try
            {
                // Create open file dialog
                using (OpenFileDialog openFileDialog = new OpenFileDialog())
                {
                    openFileDialog.Filter = "ROM Files|*.rom;*.z64;*.v64;*.n64|All Files|*.*";
                    openFileDialog.Title = "Select a ROM file to convert";

                    if (openFileDialog.ShowDialog() == DialogResult.OK)
                    {
                        // Detect current endianness
                        var currentEndianness = _service.DetectRomEndianness(openFileDialog.FileName);
                        
                        if (!currentEndianness.HasValue)
                        {
                            MessageBox.Show("Could not detect the endianness of the selected ROM file.", 
                                "Endianness Detection Failed", 
                                MessageBoxButtons.OK, 
                                MessageBoxIcon.Warning);
                            return;
                        }

                        // Determine target endianness (opposite of current)
                        var targetEndianness = currentEndianness.Value == EndianConversionService.EndianFormat.BigEndian
                            ? EndianConversionService.EndianFormat.LittleEndian
                            : EndianConversionService.EndianFormat.BigEndian;

                        // Ask for confirmation
                        string message = $"The selected ROM appears to be in {currentEndianness.Value} format.\n\n" +
                                         $"Do you want to convert it to {targetEndianness} format?";
                        
                        if (MessageBox.Show(message, "Confirm Conversion", 
                                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                        {
                            return;
                        }

                        // Get output file name
                        using (SaveFileDialog saveFileDialog = new SaveFileDialog())
                        {
                            string extension = Path.GetExtension(openFileDialog.FileName);
                            string fileName = Path.GetFileNameWithoutExtension(openFileDialog.FileName);
                            
                            saveFileDialog.Filter = "ROM Files|*" + extension;
                            saveFileDialog.Title = "Save Converted ROM";
                            saveFileDialog.FileName = $"{fileName}_{targetEndianness}{extension}";

                            if (saveFileDialog.ShowDialog() == DialogResult.OK)
                            {
                                // Perform the conversion
                                bool success = _service.ConvertRomEndianness(
                                    openFileDialog.FileName, 
                                    saveFileDialog.FileName,
                                    targetEndianness);

                                if (success)
                                {
                                    MessageBox.Show($"ROM successfully converted to {targetEndianness} format and saved to:\n\n{saveFileDialog.FileName}", 
                                        "Conversion Complete", 
                                        MessageBoxButtons.OK, 
                                        MessageBoxIcon.Information);
                                }
                                else
                                {
                                    MessageBox.Show("Failed to convert the ROM file. Please check the console for error details.", 
                                        "Conversion Failed", 
                                        MessageBoxButtons.OK, 
                                        MessageBoxIcon.Error);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred: {ex.Message}", 
                    "Error", 
                    MessageBoxButtons.OK, 
                    MessageBoxIcon.Error);
            }
        }
    }
} 