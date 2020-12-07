using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using SceneNavi.Utilities.OpenGLHelpers;

namespace SceneNavi.Dependencies.Implementations
{


    public interface ITextPrinterSettings
    {   
        Font Font { get; set; }
        Color BackColor { get; set; }
    }

    public class TextPrinter : ITextPrinter
    {
        private readonly ITextPrinterSettings _textPrinterSettings;
        
        const float LastUsedMax = 300.0f;

        private WeakReference<GLControl> _parentReference;

        //GLControl _parentGlControl;

        List<CachedString> _cachedStrings;

        bool _started, _ended;
        bool _disposed;

//        public TextPrinter(ITextPrinterSettings textPrinterSettings)
//        {
//            _textPrinterSettings = textPrinterSettings;
//            
//        }

        public TextPrinter()
        {
            _cachedStrings = new List<CachedString>();
        }

     

        ~TextPrinter()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    foreach (var cached in _cachedStrings) cached.Dispose();
                }

                _disposed = true;
            }
        }

        public void Print(string text, Vector2d position)
        {
            Print(text, position, Color.Transparent);
        }

        public void Print(string text, Vector2d position, Color backColor)
        {
            var cached = _cachedStrings.FirstOrDefault(x => x.String == text && x.BackColor == backColor);

            if (cached == null)
            {
                cached = new CachedString(text, _textPrinterSettings.Font, _textPrinterSettings.BackColor);
                _cachedStrings.Add(cached);
            }

            _parentReference.TryGetTarget(out var target);
            
            cached.RefreshClientRect(target.ClientRectangle);
            cached.LastUsedAgo += 0.01f;
            cached.Print(position);
        }

        public void Begin(GLControl glControl)
        {
            if (_started && !_ended) throw new Exception("TextPrinter Begin without Flush");

            _parentReference = new WeakReference<GLControl>(glControl);
            _parentReference.TryGetTarget(out var target);

          //  _parentGlControl = glControl;

            GL.PushMatrix();

            Initialization.CreateViewportAndProjection(Initialization.ProjectionTypes.Orthographic, target.ClientRectangle, 0.0f, 300.0f);

            _started = true;
            _ended = false;
        }

        public void Flush()
        {
            if (!_started) throw new Exception("TextPrinter Flush without Begin");

            foreach (var cached in _cachedStrings.Where(x => x.LastUsedAgo >= LastUsedMax)) cached.Dispose();
            _cachedStrings.RemoveAll(x => x.LastUsedAgo >= LastUsedMax);

            GL.PopMatrix();

            _started = false;
            _ended = true;
        }

        sealed class CachedString : IDisposable
        {
            const float Padding = 4.0f;
            public string String { get; }
            public Color BackColor { get; }
            private int TextureGlid { get; }
            public float LastUsedAgo { get; set; }

            readonly Font _currentFont;
            readonly StringFormat _stringFormat;
            readonly double[] _texCoordData;
            readonly double[] _colorData;
            readonly ushort[] _indices;

            SizeF _stringSize;
            Rectangle _clientRect;

            bool _disposed;

            public CachedString(string text, Font font, Color backColor)
            {
                String = text;
                BackColor = backColor;

                var strippedText = System.Text.RegularExpressions.Regex.Replace(text, "<.*?>", String.Empty);
                var hasTags = (string.Compare(text, strippedText, StringComparison.Ordinal) != 0);

                _currentFont = font;
                _stringFormat = new StringFormat(StringFormat.GenericTypographic);
                if (hasTags) _stringFormat.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces;

                var tempSize = strippedText.MeasureString(font, _stringFormat);
                _stringSize = new SizeF((float)Math.Floor(tempSize.Width + Padding), (float)Math.Floor(tempSize.Height + Padding));

                var stringBmp = new Bitmap((int)_stringSize.Width, (int)_stringSize.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(stringBmp))
                {
                    g.Clear(backColor);
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;

                    if (hasTags)
                        ParseDrawString(g);
                    else
                        g.DrawString(text, font, Brushes.White, Padding / 2.0f, Padding / 2.0f, _stringFormat);
                }

                var bmpData = stringBmp.LockBits(new Rectangle(0, 0, stringBmp.Width, stringBmp.Height), System.Drawing.Imaging.ImageLockMode.ReadOnly, stringBmp.PixelFormat);

                TextureGlid = GL.GenTexture();
                GL.BindTexture(TextureTarget.Texture2D, TextureGlid);
                GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, stringBmp.Width, stringBmp.Height, 0, PixelFormat.Bgra, PixelType.UnsignedByte, bmpData.Scan0);
                stringBmp.UnlockBits(bmpData);

                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

                _texCoordData = new double[]
                {
                    0.0f, 1.0f,
                    1.0f, 1.0f,
                    1.0f, 0.0f,
                    0.0f, 0.0f
                };

                _colorData = new double[]
                {
                    1.0f, 1.0f, 1.0f, 1.0f,
                    1.0f, 1.0f, 1.0f, 1.0f,
                    1.0f, 1.0f, 1.0f, 1.0f,
                    1.0f, 1.0f, 1.0f, 1.0f
                };

                _indices = new ushort[]
                {
                    0, 1, 2, 3
                };

                LastUsedAgo = 0.0f;

                _disposed = false;
            }

            ~CachedString()
            {
                Dispose(false);
            }

            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            private void Dispose(bool disposing)
            {
                if (!_disposed)
                {
                    if (disposing)
                    {
                        if (GL.IsTexture(TextureGlid)) GL.DeleteTexture(TextureGlid);
                    }

                    _disposed = true;
                }
            }

            private void ParseDrawString(Graphics g)
            {
                var currentColor = Color.White;
                var position = new PointF(Padding / 2.0f, Padding / 2.0f);
                for (var i = 0; i < String.Length; i++)
                {
                    var ch = String[i];
                    var chString = new string(new char[] { ch });
                    var chSize = chString.MeasureString(_currentFont, _stringFormat);

                    if (ch == '\n')
                    {
                        position.X = (Padding / 2.0f);
                        position.Y += chSize.Height;
                        continue;
                    }
                    else if (ch == '<')
                    {
                        var sepIndex = String.IndexOf(':', i + 1);
                        var endIndex = String.IndexOf('>', sepIndex);
                        var tag = String.Substring(i + 1, (sepIndex - i) - 1);
                        var value = String.Substring(sepIndex + 1, (endIndex - sepIndex) - 1);
                        switch (tag)
                        {
                            case "color":
                                var colorValues = value.Split(new char[] { ',', ' ' });
                                if (colorValues.Length > 1)
                                {
                                    var colors = new byte[colorValues.Length];
                                    for (var ci = 0; ci < colors.Length; ci++)
                                    {
                                        byte.TryParse(colorValues[ci], out colors[ci]);
                                    }
                                    currentColor = Color.FromArgb(colorValues.Length == 4 ? colors[3] : 255, colors[0], colors[1], colors[2]);
                                }
                                else
                                    currentColor = Color.White;
                                break;
                        }

                        i = endIndex;
                    }
                    else
                    {
                        using (var brush = new SolidBrush(currentColor))
                        {
                            g.DrawString(chString, _currentFont, brush, position, _stringFormat);
                        }
                        position.X += chSize.Width;
                    }
                }
            }

            internal void RefreshClientRect(Rectangle newClientRect)
            {
                _clientRect = newClientRect;
            }

            public void Print(Vector2d location)
            {
                LastUsedAgo = 0.0f;

                if (location.X < 0) location.X = (_clientRect.Right - _stringSize.Width) + location.X;
                if (location.Y < 0) location.Y = (_clientRect.Bottom - _stringSize.Height) + location.Y;

                var vertexData = new double[]
                {
                    location.X + 0.0,               location.Y + _stringSize.Height,
                    location.X + _stringSize.Width,  location.Y + _stringSize.Height,
                    location.X + _stringSize.Width,  location.Y + 0.0,
                    location.X + 0.0,               location.Y + 0.0
                };

                GL.PushAttrib(AttribMask.AllAttribBits);
                GL.PushClientAttrib(ClientAttribMask.ClientAllAttribBits);

                GL.UseProgram(0);
                GL.Enable(EnableCap.Texture2D);
                GL.BindTexture(TextureTarget.Texture2D, TextureGlid);
                GL.Disable(EnableCap.Lighting);
                GL.Enable(EnableCap.Blend);
                GL.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);
                GL.Enable(EnableCap.AlphaTest);

                GL.EnableClientState(ArrayCap.TextureCoordArray);
                GL.EnableClientState(ArrayCap.ColorArray);
                GL.EnableClientState(ArrayCap.VertexArray);
                GL.TexCoordPointer(2, TexCoordPointerType.Double, 0, ref _texCoordData[0]);
                GL.ColorPointer(4, ColorPointerType.Double, 0, ref _colorData[0]);
                GL.VertexPointer(2, VertexPointerType.Double, 0, ref vertexData[0]);
                GL.DrawElements(PrimitiveType.Quads, 4, DrawElementsType.UnsignedShort, ref _indices[0]);

                GL.DisableClientState(ArrayCap.TextureCoordArray);
                GL.DisableClientState(ArrayCap.ColorArray);
                GL.DisableClientState(ArrayCap.VertexArray);

                GL.PopClientAttrib();
                GL.PopAttrib();
            }
        }
    }
}
