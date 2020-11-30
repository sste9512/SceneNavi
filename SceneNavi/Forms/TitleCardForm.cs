using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Drawing.Imaging;

namespace SceneNavi
{

    public interface ITitleCardFormSettings
    {   
        int TitleCardWidth { get; set; } //= 144;
        int TitleCardHeight { get; set; } //= 0;
    }
    
    public partial class TitleCardForm : Form
    {
        const int TitleCardWidth = 144;
        int _titleCardHeight = 0;

      
        
        private ITitleCardFormSettings _titleCardFormSettings;
        readonly ROMHandler.BaseRomHandler _baseRom;
        readonly ROMHandler.SceneTableEntryOcarina _scene;

        Bitmap _output;
        Rectangle _outputRect;

        
        
        
        public TitleCardForm(ROMHandler.BaseRomHandler baseRom, ROMHandler.SceneTableEntryOcarina sceneTableEntry, ITitleCardFormSettings titleCardFormSettings)
        {
            InitializeComponent();

            _baseRom = baseRom;
            _scene = sceneTableEntry;
            _titleCardFormSettings = titleCardFormSettings;

            ofdImage.SetCommonImageFilter("png");
            sfdImage.SetCommonImageFilter("png");

            ReadImageFromRom();
        }

        private void ReadImageFromRom()
        {
            _titleCardHeight = (int)((_scene.LabelEndAddress - _scene.LabelStartAddress) / TitleCardWidth);

            var textureBuffer = new byte[TitleCardWidth * _titleCardHeight * 4];
            SimpleF3DEX2.ImageHelper.Ia8(TitleCardWidth, _titleCardHeight, (TitleCardWidth / 8), _baseRom.Data, (int)_scene.LabelStartAddress, ref textureBuffer);
            textureBuffer.SwapRGBAToBGRA();

            _output = new Bitmap(TitleCardWidth, _titleCardHeight, PixelFormat.Format32bppArgb);
            _outputRect = new Rectangle(0, 0, _output.Width, _output.Height);
            var bmpData = _output.LockBits(_outputRect, ImageLockMode.ReadWrite, _output.PixelFormat);
            var ptr = bmpData.Scan0;

            System.Runtime.InteropServices.Marshal.Copy(textureBuffer, 0, ptr, textureBuffer.Length);
           
            _output.UnlockBits(bmpData);

            pbTitleCard.ClientSize = new Size(TitleCardWidth * 2, _titleCardHeight * 2);
        }

        private void pbTitleCard_Paint(object sender, PaintEventArgs e)
        {
            e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            e.Graphics.DrawImage(_output, new Rectangle(_outputRect.X, _outputRect.Y, _outputRect.Width * 2, _outputRect.Height * 2), _outputRect, GraphicsUnit.Pixel);
        }

        private void btnExport_Click(object sender, EventArgs e)
        {
            if (sfdImage.ShowDialog() != DialogResult.OK) return;
            var temp = new Bitmap(_output);
            temp.Save(sfdImage.FileName);
            temp.Dispose();
        }

        private void btnImport_Click(object sender, EventArgs e)
        {
            if (ofdImage.ShowDialog() != DialogResult.OK) return;
           
            var import = ImportImage(ofdImage.FileName);
          
            if (import == null) return;
            
            _output = import;
            pbTitleCard.Invalidate();
        }

        private Bitmap ImportImage(string fileName)
        {
            var import = new Bitmap(fileName);

            if (import.Width != TitleCardWidth || import.Height != _titleCardHeight)
            {
                MessageBox.Show("Selected image has wrong size; image cannot be used.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return null;
            }

            var offset = _scene.LabelStartAddress;

            for (var y = 0; y < import.Height; y++)
            {
                for (var x = 0; x < import.Width; x++)
                {
                    var pixelColor = import.GetPixel(x, y);
                    var intensity = (pixelColor.R + pixelColor.G + pixelColor.B) / 3;

                    var newColor = Color.FromArgb(pixelColor.A, intensity, intensity, intensity);
                    import.SetPixel(x, y, newColor);

                    var packed = (byte)(((byte)intensity).Scale(0, 0xFF, 0, 0xF) << 4);
                    packed |= pixelColor.A.Scale(0, 0xFF, 0, 0xF);
                    _baseRom.Data[offset] = packed;

                    offset++;
                }
            }

            var temp = new Bitmap(import);
            import.Dispose();

            return temp;
        }
    }
}
