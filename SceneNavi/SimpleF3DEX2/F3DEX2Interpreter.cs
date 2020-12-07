using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using SceneNavi.Forms;
using SceneNavi.ROMHandler;
using SceneNavi.SimpleF3DEX2.CombinerEmulation;
using SceneNavi.Utilities.OpenGLHelpers;

namespace SceneNavi.SimpleF3DEX2
{
    public class F3Dex2Interpreter
    {
        public delegate void UCodeCommandDelegate(uint w0, uint w1);

        class Macro
        {
            public delegate void MacroDelegate(uint[] w0, uint[] w1);

            public MacroDelegate Function { get; private set; }
            public General.UcodeCmds[] Commands { get; private set; }

            public Macro(MacroDelegate func, General.UcodeCmds[] cmds)
            {
                Function = func;
                Commands = cmds;
            }
        }

        UCodeCommandDelegate[] _ucodecmds;
        List<Macro> _macros;
        bool _inmacro;

        internal List<SimpleTriangle> LastTriList { get; private set; }

        internal Vertex[] VertexBuffer { get; private set; }
        uint _rdphalf1, _texaddress;
        internal uint LastComb0 { get; private set; }
        internal uint LastComb1 { get; private set; }
        public uint GeometryMode { get; private set; }
        public uint OtherModeH { get; private set; }
        public SimpleF3DEX2.OtherModeL OtherModeL { get; private set; }
        internal Color4 PrimColor { get; private set; }
        internal Color4 EnvColor { get; private set; }
        Stack<Matrix4d> _mtxstack;
        internal Texture[] Textures { get; private set; }
        Color4[] _palette;
        List<TextureCache> _texcache;
        int _activetex;
        bool _multitex;
        public float[] ScaleS { get; private set; }
        public float[] ScaleT { get; private set; }

        ArbCombineManager _arbCombiner;
        GlslCombineManager _glslCombiner;

        ROMHandler.BaseRomHandler _baseRom;
        Stack<DisplayListEx> _activeGldl;

        public F3Dex2Interpreter(ROMHandler.BaseRomHandler baseRom)
        {
            _baseRom = baseRom;
            _activeGldl = new Stack<DisplayListEx>();

            InitializeParser();
            InitializeMacros();

            LastTriList = new List<SimpleTriangle>();

            VertexBuffer = new Vertex[32];
            _rdphalf1 = GeometryMode = OtherModeH = LastComb0 = LastComb1 = 0;
            OtherModeL = SimpleF3DEX2.OtherModeL.Empty;
            _mtxstack = new Stack<Matrix4d>();
            _mtxstack.Push(Matrix4d.Identity);

            Textures = new Texture[2];
            Textures[0] = new Texture();
            Textures[1] = new Texture();
            _palette = new Color4[256];
            _texcache = new List<TextureCache>();
            _activetex = 0;
            _multitex = false;
            ScaleS = new float[2];
            ScaleT = new float[2];

            InitCombiner();
        }

        public void InitCombiner()
        {
            if (Configuration.CombinerType == CombinerTypes.ArbCombiner && _arbCombiner == null)
            {
                Program.Status.Message = "Initializing ARB combiner...";
                _arbCombiner = new ArbCombineManager();
            }
            else if (Configuration.CombinerType == CombinerTypes.GlslCombiner && _glslCombiner == null)
            {
                Program.Status.Message = "Initializing GLSL combiner...";
                _glslCombiner = new GlslCombineManager(this);
            }
        }

        public void ResetCaches()
        {
            ResetTextureCache();

            if (Configuration.CombinerType == CombinerTypes.ArbCombiner && _arbCombiner != null) _arbCombiner.ResetFragmentCache();

            if (LastTriList != null) LastTriList.Clear();
        }

        public void ResetTextureCache()
        {
            if (_texcache != null)
            {
                foreach (var tc in _texcache) if (GL.IsTexture(tc.GLID)) GL.DeleteTexture(tc.GLID);
                _texcache.Clear();
            }
        }

        private void InitializeParser()
        {
            _ucodecmds = new UCodeCommandDelegate[256];
            for (var i = 0; i < _ucodecmds.Length; i++) _ucodecmds[i] = new UCodeCommandDelegate((w0, w1) => { });
            _ucodecmds[(byte)General.UcodeCmds.VTX] = CommandVtx;
            _ucodecmds[(byte)General.UcodeCmds.TRI1] = CommandTri1;
            _ucodecmds[(byte)General.UcodeCmds.TRI2] = CommandTri2;
            _ucodecmds[(byte)General.UcodeCmds.DL] = CommandDl;
            _ucodecmds[(byte)General.UcodeCmds.RDPHALF_1] = CommandRdpHalf1;
            _ucodecmds[(byte)General.UcodeCmds.BRANCH_Z] = CommandBranchZ;
            _ucodecmds[(byte)General.UcodeCmds.GEOMETRYMODE] = CommandGeometryMode;
            _ucodecmds[(byte)General.UcodeCmds.MTX] = CommandMtx;
            _ucodecmds[(byte)General.UcodeCmds.POPMTX] = CommandPopMtx;
            _ucodecmds[(byte)General.UcodeCmds.SETOTHERMODE_H] = CommandSetOtherModeH;
            _ucodecmds[(byte)General.UcodeCmds.SETOTHERMODE_L] = CommandSetOtherModeL;
            _ucodecmds[(byte)General.UcodeCmds.TEXTURE] = CommandTexture;
            _ucodecmds[(byte)General.UcodeCmds.SETTIMG] = CommandSetTImage;
            _ucodecmds[(byte)General.UcodeCmds.SETTILE] = CommandSetTile;
            _ucodecmds[(byte)General.UcodeCmds.SETTILESIZE] = CommandSetTileSize;
            _ucodecmds[(byte)General.UcodeCmds.LOADBLOCK] = CommandLoadBlock;
            _ucodecmds[(byte)General.UcodeCmds.SETCOMBINE] = CommandSetCombine;
            _ucodecmds[(byte)General.UcodeCmds.SETPRIMCOLOR] = CommandSetPrimColor;
            _ucodecmds[(byte)General.UcodeCmds.SETENVCOLOR] = CommandSetEnvColor;
        }

        private void InitializeMacros()
        {
            _macros = new List<Macro>
            {
                new Macro(MacroLoadTextureBlock,
                    new General.UcodeCmds[]
                    {
                        General.UcodeCmds.SETTIMG, General.UcodeCmds.SETTILE, General.UcodeCmds.RDPLOADSYNC,
                        General.UcodeCmds.LOADBLOCK, General.UcodeCmds.RDPPIPESYNC, General.UcodeCmds.SETTILE,
                        General.UcodeCmds.SETTILESIZE
                    }),
                new Macro(MacroLoadTlut,
                    new General.UcodeCmds[]
                    {
                        General.UcodeCmds.SETTIMG, General.UcodeCmds.RDPTILESYNC, General.UcodeCmds.SETTILE,
                        General.UcodeCmds.RDPLOADSYNC, General.UcodeCmds.LOADTLUT, General.UcodeCmds.RDPPIPESYNC
                    })
            };
        }

        public void Render(uint adr, bool call = false, DisplayListEx gldl = null)
        {
            try
            {
                _activeGldl.Push(gldl);

                /* Set some defaults */
                if (!call)
                {
                    GL.DepthMask(true);
                    if (Initialization.SupportsFunction("glGenProgramsARB")) GL.Disable((EnableCap)All.FragmentProgram);
                    if (Initialization.SupportsFunction("glCreateShader")) GL.UseProgram(0);

                    PrimColor = EnvColor = new Color4(0.5f, 0.5f, 0.5f, 0.5f);

                    /* If emulating combiner, set more defaults / load values */
                    if (Configuration.CombinerType == CombinerTypes.ArbCombiner)
                    {
                        GL.Arb.BindProgram(AssemblyProgramTargetArb.FragmentProgram, 0);
                        GL.Arb.ProgramEnvParameter4(AssemblyProgramTargetArb.FragmentProgram, 0, PrimColor.R, PrimColor.G, PrimColor.B, PrimColor.A);
                        GL.Arb.ProgramEnvParameter4(AssemblyProgramTargetArb.FragmentProgram, 1, EnvColor.R, EnvColor.G, EnvColor.B, EnvColor.A);
                    }

                    /* Clear out texture units */
                    for (var i = 0; i < (Initialization.SupportsFunction("glActiveTextureARB") ? 2 : 1); i++)
                    {
                        Initialization.ActiveTextureChecked(TextureUnit.Texture0 + i);
                        GL.BindTexture(TextureTarget.Texture2D, MiscDrawingHelpers.DummyTextureID);
                    }
                }

                /* Ucode interpreter starts here */
                var seg = (byte)(adr >> 24);
                adr &= 0xFFFFFF;

                var segdata = (byte[])_baseRom.SegmentMapping[seg];

                while (adr < segdata.Length)
                {
                    var cmd = segdata[adr];

                    /* EndDL */
                    if (cmd == (byte)General.UcodeCmds.ENDDL) break;

                    /* Try to detect macros if any are defined */
                    _inmacro = false;
                    if (_macros != null)
                    {
                        foreach (var m in _macros)
                        {
                            if (adr + ((m.Commands.Length + 3) * 8) > segdata.Length) break;

                            var nextcmd = new General.UcodeCmds[m.Commands.Length];
                            var nextw0 = new uint[nextcmd.Length + 2];
                            var nextw1 = new uint[nextcmd.Length + 2];

                            for (var i = 0; i < nextw0.Length; i++)
                            {
                                nextw0[i] = Endian.SwapUInt32(BitConverter.ToUInt32(segdata, (int)adr + (i * 8)));
                                nextw1[i] = Endian.SwapUInt32(BitConverter.ToUInt32(segdata, (int)adr + (i * 8) + 4));
                                if (i < m.Commands.Length) nextcmd[i] = (General.UcodeCmds)(nextw0[i] >> 24);
                            }

                            if (_inmacro = (Enumerable.SequenceEqual(m.Commands, nextcmd)))
                            {
                                m.Function(nextw0, nextw1);
                                adr += (uint)(m.Commands.Length * 8);
                                break;
                            }
                        }
                    }

                    /* No macro detected */
                    if (!_inmacro)
                    {
                        /* Execute command */
                        _ucodecmds[cmd](Endian.SwapUInt32(BitConverter.ToUInt32(segdata, (int)adr)), Endian.SwapUInt32(BitConverter.ToUInt32(segdata, (int)adr + 4)));
                        adr += 8;

                        /* Texture loading hack; if SetCombine OR LoadBlock command detected, try loading textures again (fixes Water Temple 1st room, borked walls; SM64toZ64 conversions?) */
                        if (Configuration.RenderTextures && (cmd == (byte)General.UcodeCmds.SETCOMBINE || cmd == (byte)General.UcodeCmds.LOADBLOCK) && Textures[0] != null) LoadTextures();
                    }
                }
            }
            catch (EntryPointNotFoundException)
            {
                //TODO handle this?
            }
            finally
            {
                _activeGldl.Pop();
            }
        }

        private void MacroLoadTextureBlock(uint[] w0, uint[] w1)
        {
            if (!Configuration.RenderTextures) return;

            _activetex = (int)((w1[6] >> 24) & 0x01);
            _multitex = (_activetex == 1);

            CommandSetTImage(w0[0], w1[0]);
            CommandSetTile(w0[5], w1[5]);
            CommandSetTileSize(w0[6], w1[6]);

            if ((Textures[_activetex].Format == 0x40 || Textures[_activetex].Format == 0x48 || Textures[_activetex].Format == 0x50) &&
                ((w0[7] >> 24) == (byte)General.UcodeCmds.SETTIMG) || ((w0[8] >> 24) == (byte)General.UcodeCmds.SETTIMG)) return;

            LoadTextures();
        }

        private void MacroLoadTlut(uint[] w0, uint[] w1)
        {
            if (!Configuration.RenderTextures) return;

            var adr = w1[0];
            var seg = (byte)(adr >> 24);
            adr &= 0xFFFFFF;

            var psize = ((w1[4] & 0x00FFF000) >> 14) + 1;

            var segdata = (byte[])_baseRom.SegmentMapping[seg];
            if (segdata == null) return;

            for (var i = 0; i < psize; i++)
            {
                var r = (ushort)((segdata[adr] << 8) | segdata[adr + 1]);

                _palette[i].R = (byte)((r & 0xF800) >> 8);
                _palette[i].G = (byte)(((r & 0x07C0) << 5) >> 8);
                _palette[i].B = (byte)(((r & 0x003E) << 18) >> 16);
                _palette[i].A = 0;
                if ((r & 0x0001) == 1) _palette[i].A = 0xFF;

                adr += 2;
            }

            LoadTextures();
        }

        private void CommandVtx(uint w0, uint w1)
        {
            /* Vtx */
            var n = (byte)((w0 >> 12) & 0xFF);
            var v0 = (byte)(((w0 >> 1) & 0x7F) - n);
            if (n > VertexBuffer.Length || v0 > VertexBuffer.Length) return;

            for (var i = 0; i < n; i++) VertexBuffer[v0 + i] = new Vertex(_baseRom, (byte[])_baseRom.SegmentMapping[(byte)(w1 >> 24)], (uint)(w1 + i * 16), _mtxstack.Peek());
        }

        private void CommandTri1(uint w0, uint w1)
        {
            /* Tri1 */
            var idxs = new int[] { (int)((w0 & 0x00FF0000) >> 16) >> 1, (int)((w0 & 0x0000FF00) >> 8) >> 1, (int)(w0 & 0x000000FF) >> 1 };

            foreach (var idx in idxs) if (idx >= VertexBuffer.Length) return;
            General.RenderTriangles(this, idxs);

            if (_activeGldl.Peek() != null) _activeGldl.Peek().Triangles.Add(new DisplayListEx.Triangle(VertexBuffer[idxs[0]], VertexBuffer[idxs[1]], VertexBuffer[idxs[2]]));

            LastTriList.Add(new SimpleTriangle(VertexBuffer[idxs[0]].Position, VertexBuffer[idxs[1]].Position, VertexBuffer[idxs[2]].Position));
        }

        private void CommandTri2(uint w0, uint w1)
        {
            /* Tri2 */
            var idxs = new int[]
            {
                (int)((w0 & 0x00FF0000) >> 16) >> 1, (int)((w0 & 0x0000FF00) >> 8) >> 1, (int)(w0 & 0x000000FF) >> 1,
                (int)((w1 & 0x00FF0000) >> 16) >> 1, (int)((w1 & 0x0000FF00) >> 8) >> 1, (int)(w1 & 0x000000FF) >> 1
            };

            foreach (var idx in idxs) if (idx >= VertexBuffer.Length) return;
            General.RenderTriangles(this, idxs);

            if (_activeGldl != null)
            {
                if (_activeGldl.Peek() != null) _activeGldl.Peek().Triangles.Add(new DisplayListEx.Triangle(VertexBuffer[idxs[0]], VertexBuffer[idxs[1]], VertexBuffer[idxs[2]]));
                if (_activeGldl.Peek() != null) _activeGldl.Peek().Triangles.Add(new DisplayListEx.Triangle(VertexBuffer[idxs[3]], VertexBuffer[idxs[4]], VertexBuffer[idxs[5]]));
            }

            LastTriList.Add(new SimpleTriangle(VertexBuffer[idxs[0]].Position, VertexBuffer[idxs[1]].Position, VertexBuffer[idxs[2]].Position));
            LastTriList.Add(new SimpleTriangle(VertexBuffer[idxs[3]].Position, VertexBuffer[idxs[4]].Position, VertexBuffer[idxs[5]].Position));
        }

        private void CommandDl(uint w0, uint w1)
        {
            /* DL */
            if ((byte[])_baseRom.SegmentMapping[(byte)(w1 >> 24)] != null) Render(w1, true, _activeGldl.Peek());
        }

        private void CommandRdpHalf1(uint w0, uint w1)
        {
            /* RDPHalf_1 */
            _rdphalf1 = w1;
        }

        private void CommandBranchZ(uint w0, uint w1)
        {
            /* Branch_Z */
            if ((byte[])_baseRom.SegmentMapping[(byte)(_rdphalf1 >> 24)] != null) Render(_rdphalf1, true, _activeGldl.Peek());
        }

        private void CommandGeometryMode(uint w0, uint w1)
        {
            /* GeometryMode */
            var clr = ~(w0 & 0xFFFFFF);
            GeometryMode = (GeometryMode & ~clr) | w1;
            General.PerformModeChanges(this);

            if (Configuration.CombinerType == CombinerTypes.GlslCombiner && _glslCombiner != null) _glslCombiner.BindCombiner(LastComb0, LastComb1, Configuration.RenderTextures);
        }

        private void CommandMtx(uint w0, uint w1)
        {
            /* Mtx */
            var mseg = (byte)(w1 >> 24);
            var madr = (w1 & 0xFFFFFF);
            var msegdata = (byte[])_baseRom.SegmentMapping[mseg];

            if (mseg == 0x80) _mtxstack.Pop();
            if (msegdata == null) return;

            ushort mt1, mt2;
            var matrix = new double[16];

            for (var x = 0; x < 4; x++)
            {
                for (var y = 0; y < 4; y++)
                {
                    mt1 = Endian.SwapUInt16(BitConverter.ToUInt16(msegdata, (int)madr));
                    mt2 = Endian.SwapUInt16(BitConverter.ToUInt16(msegdata, (int)madr + 32));
                    matrix[(x * 4) + y] = ((mt1 << 16) | mt2) * (1.0f / 65536.0f);
                    madr += 2;
                }
            }

            var glmatrix = new Matrix4d(
                matrix[0], matrix[1], matrix[2], matrix[3],
                matrix[4], matrix[5], matrix[6], matrix[7],
                matrix[8], matrix[9], matrix[10], matrix[11],
                matrix[12], matrix[13], matrix[14], matrix[15]);

            _mtxstack.Push(glmatrix);
        }

        private void CommandPopMtx(uint w0, uint w1)
        {
            /* PopMtx */
            _mtxstack.Pop();
        }

        private void CommandSetOtherModeH(uint w0, uint w1)
        {
            /* SetOtherMode_H */
            /* useless~ */
            switch ((General.OtherModeHShifts)(32 - General.ShiftR(w0, 8, 8) - (General.ShiftR(w0, 0, 8) + 1)))
            {
                case General.OtherModeHShifts.TEXTLUT:
                    var tlutmode = (w1 >> (int)General.OtherModeHShifts.TEXTLUT);
                    break;
                default:
                    var length = (uint)(General.ShiftR(w0, 0, 8) + 1);
                    var shift = (uint)(32 - General.ShiftR(w0, 8, 8) - length);
                    var mask = (uint)(((1 << (int)length) - 1) << (int)shift);

                    OtherModeH &= ~mask;
                    OtherModeH |= w1 & mask;
                    break;
            }

            General.PerformModeChanges(this);

            if (Configuration.CombinerType == CombinerTypes.GlslCombiner && _glslCombiner != null) _glslCombiner.BindCombiner(LastComb0, LastComb1, Configuration.RenderTextures);
        }

        private void CommandSetOtherModeL(uint w0, uint w1)
        {
            /* SetOtherMode_L */
            if ((32 - ((w0 & 0x00FFFFFF) << 4 >> 4) - 1) == 3)
            {
                var data = OtherModeL.Data;
                data &= 0x00000007;
                data |= (w1 & 0xCCCCFFFF | w1 & 0x3333FFFF);
                OtherModeL = new SimpleF3DEX2.OtherModeL(data);
                General.PerformModeChanges(this);

                if (Configuration.CombinerType == CombinerTypes.GlslCombiner && _glslCombiner != null) _glslCombiner.BindCombiner(LastComb0, LastComb1, Configuration.RenderTextures);
            }
        }

        private void CommandTexture(uint w0, uint w1)
        {
            /* Texture */
            _activetex = 0;
            _multitex = false;

            Textures[0] = new Texture();
            Textures[1] = new Texture();

            var s = General.ShiftR(w1, 16, 16);
            var t = General.ShiftR(w1, 0, 16);

            ScaleS[0] = ScaleS[1] = ((float)(s + 1) / 65536.0f);
            ScaleT[0] = ScaleT[1] = ((float)(t + 1) / 65536.0f);
        }

        private void CommandSetTImage(uint w0, uint w1)
        {
            /* SetTImage */
            if (_inmacro)
                _texaddress = w1;
            else
                Textures[_activetex].Address = w1;
        }

        private void CommandSetTile(uint w0, uint w1)
        {
            /* SetTile */
            if (_inmacro)
                Textures[_activetex].Address = _texaddress;

            Textures[_activetex].Format = (byte)((w0 & 0xFF0000) >> 16);
            Textures[_activetex].CMS = (uint)General.ShiftR(w1, 8, 2);
            Textures[_activetex].CMT = (uint)General.ShiftR(w1, 18, 2);
            Textures[_activetex].LineSize = General.ShiftR(w0, 9, 9);
            Textures[_activetex].Palette = General.ShiftR(w1, 20, 4);
            Textures[_activetex].ShiftS = General.ShiftR(w1, 0, 4);
            Textures[_activetex].ShiftT = General.ShiftR(w1, 10, 4);
            Textures[_activetex].MaskS = General.ShiftR(w1, 4, 4);
            Textures[_activetex].MaskT = General.ShiftR(w1, 14, 4);
        }

        private void CommandSetTileSize(uint w0, uint w1)
        {
            /* SetTileSize */
            var uls = (uint)General.ShiftR(w0, 12, 12);
            var ult = (uint)General.ShiftR(w0, 0, 12);
            var lrs = (uint)General.ShiftR(w1, 12, 12);
            var lrt = (uint)General.ShiftR(w1, 0, 12);

            Textures[_activetex].Tile = General.ShiftR(w1, 24, 3);
            Textures[_activetex].ULS = General.ShiftR(uls, 2, 10);
            Textures[_activetex].ULT = General.ShiftR(ult, 2, 10);
            Textures[_activetex].LRS = General.ShiftR(lrs, 2, 10);
            Textures[_activetex].LRT = General.ShiftR(lrt, 2, 10);
        }

        private void CommandLoadBlock(uint w0, uint w1)
        {
            /* LoadBlock */
            CommandSetTileSize(w0, w1);
        }

        private void CommandSetCombine(uint w0, uint w1)
        {
            /* SetCombine */
            LastComb0 = (w0 & 0xFFFFFF);
            LastComb1 = w1;

            if (Configuration.CombinerType == CombinerTypes.ArbCombiner && _arbCombiner != null)
            {
                _arbCombiner.BindCombiner(LastComb0, LastComb1, Configuration.RenderTextures);
            }
            else if (Configuration.CombinerType == CombinerTypes.GlslCombiner && _glslCombiner != null)
            {
                _glslCombiner.BindCombiner(LastComb0, LastComb1, Configuration.RenderTextures);
            }
        }

        private void CommandSetPrimColor(uint w0, uint w1)
        {
            /* SetPrimColor */
            PrimColor = new Color4(
                General.ShiftR(w1, 24, 8) * 0.0039215689f,
                General.ShiftR(w1, 16, 8) * 0.0039215689f,
                General.ShiftR(w1, 8, 8) * 0.0039215689f,
                General.ShiftR(w1, 0, 8) * 0.0039215689f);

            if (Configuration.CombinerType == CombinerTypes.ArbCombiner)
            {
                var m = (float)General.ShiftL(w0, 8, 8);
                var l = (float)General.ShiftL(w0, 0, 8) * 0.0039215689f;

                GL.Arb.ProgramEnvParameter4(AssemblyProgramTargetArb.FragmentProgram, 0, PrimColor.R, PrimColor.G, PrimColor.B, PrimColor.A);
                GL.Arb.ProgramEnvParameter4(AssemblyProgramTargetArb.FragmentProgram, 2, l, l, l, l);
            }
            else if (Configuration.CombinerType == CombinerTypes.GlslCombiner && _glslCombiner != null)
            {
                _glslCombiner.BindCombiner(LastComb0, LastComb1, Configuration.RenderTextures);
            }
            else
            {
                /* Super-simple colorization faking */
                GL.Material(MaterialFace.FrontAndBack, MaterialParameter.Diffuse, new Color4(PrimColor.R, PrimColor.G, PrimColor.B, PrimColor.A));
                //GL.Light(LightName.Light0, LightParameter.Diffuse, new Color4(PrimColor.R, PrimColor.G, PrimColor.B, PrimColor.A));
            }
        }

        private void CommandSetEnvColor(uint w0, uint w1)
        {
            /* SetEnvColor */
            EnvColor = new Color4(
                General.ShiftR(w1, 24, 8) * 0.0039215689f,
                General.ShiftR(w1, 16, 8) * 0.0039215689f,
                General.ShiftR(w1, 8, 8) * 0.0039215689f,
                General.ShiftR(w1, 0, 8) * 0.0039215689f);

            if (Configuration.CombinerType == CombinerTypes.ArbCombiner)
            {
                GL.Arb.ProgramEnvParameter4(AssemblyProgramTargetArb.FragmentProgram, 1, EnvColor.R, EnvColor.G, EnvColor.B, EnvColor.A);
            }
            else if (Configuration.CombinerType == CombinerTypes.GlslCombiner && _glslCombiner != null)
            {
                _glslCombiner.BindCombiner(LastComb0, LastComb1, Configuration.RenderTextures);
            }
        }

        #region Texturing functions

        private void LoadTextures()
        {
            switch (Configuration.CombinerType)
            {
                case CombinerTypes.None:
                    {
                        CalculateTextureSize(0);
                        Initialization.ActiveTextureChecked(TextureUnit.Texture0);
                        GL.Enable(EnableCap.Texture2D);
                        GL.BindTexture(TextureTarget.Texture2D, CheckTextureCache(0));
                    }
                    break;

                case CombinerTypes.GlslCombiner:
                    {
                        CalculateTextureSize(0);
                        Initialization.ActiveTextureChecked(TextureUnit.Texture0);
                        GL.Enable(EnableCap.Texture2D);
                        GL.BindTexture(TextureTarget.Texture2D, CheckTextureCache(0));

                        if (Initialization.SupportsFunction("glActiveTextureARB"))
                        {
                            CalculateTextureSize(1);
                            GL.ActiveTexture(TextureUnit.Texture1);
                            GL.Enable(EnableCap.Texture2D);
                            GL.BindTexture(TextureTarget.Texture2D, 0);

                            if (_multitex) GL.BindTexture(TextureTarget.Texture2D, CheckTextureCache(1));
                        }
                    }
                    break;

                case CombinerTypes.ArbCombiner:
                    {
                        Initialization.ActiveTextureChecked(TextureUnit.Texture0);
                        GL.Disable(EnableCap.Texture2D);
                        GL.BindTexture(TextureTarget.Texture2D, 0);
                        Initialization.ActiveTextureChecked(TextureUnit.Texture1);
                        GL.Disable(EnableCap.Texture2D);
                        GL.BindTexture(TextureTarget.Texture2D, 0);

                        CalculateTextureSize(0);
                        Initialization.ActiveTextureChecked(TextureUnit.Texture0);
                        GL.Enable(EnableCap.Texture2D);
                        GL.BindTexture(TextureTarget.Texture2D, CheckTextureCache(0));

                        if (_multitex)
                        {
                            CalculateTextureSize(1);
                            Initialization.ActiveTextureChecked(TextureUnit.Texture1);
                            GL.Enable(EnableCap.Texture2D);
                            GL.BindTexture(TextureTarget.Texture2D, CheckTextureCache(1));

                            GL.Disable(EnableCap.Texture2D);
                        }
                        else
                        {
                            Initialization.ActiveTextureChecked(TextureUnit.Texture1);
                            GL.Disable(EnableCap.Texture2D);
                        }

                        Initialization.ActiveTextureChecked(TextureUnit.Texture0);
                        GL.Disable(EnableCap.Texture2D);
                    }
                    break;
            }
        }

        private int CheckTextureCache(int tx)
        {
            var tag = _baseRom.SegmentMapping[(byte)(Textures[tx].Address >> 24)];

            foreach (var cached in _texcache)
            {
                if (cached.Tag == tag && cached.Format == Textures[tx].Format && cached.Address == Textures[tx].Address &&
                    cached.RealHeight == Textures[tx].RealHeight && cached.RealWidth == Textures[tx].RealWidth)
                    return cached.GLID;
            }

            var newcached = new TextureCache(tag, Textures[tx], LoadTexture(tx));
            _texcache.Add(newcached);
            return newcached.GLID;
        }

        private int LoadTexture(int tx)
        {
            var adr = Textures[tx].Address;
            var seg = (byte)(adr >> 24);
            adr &= 0xFFFFFF;

            var texbuf = new byte[Textures[tx].RealWidth * Textures[tx].RealHeight * 4];
            var segdata = (byte[])_baseRom.SegmentMapping[seg];

            if (segdata == null)
                texbuf.Fill(new byte[] { 0xFF, 0xFF, 0x00, 0xFF });
            else
                ImageHelper.Convert(
                    Textures[tx].Format,
                    segdata,
                    (int)adr,
                    ref texbuf,
                    (int)Textures[tx].Width,
                    (int)Textures[tx].Height,
                    (int)Textures[tx].LineSize,
                    (int)Textures[tx].Palette,
                    _palette);

            var glid = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, glid);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, (int)Textures[tx].RealWidth, (int)Textures[tx].RealHeight, 0, PixelFormat.Rgba, PixelType.UnsignedByte, texbuf);

            if (Configuration.EnableMipmaps)
            {
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.GenerateMipmap, (float)All.True);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (float)All.LinearMipmapLinear);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (float)All.Linear);
            }
            else
            {
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (float)All.Linear);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (float)All.Linear);
            }

            if (Textures[tx].CMS == 2 || Textures[tx].CMS == 3)
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)All.ClampToEdge);
            else if (Textures[tx].CMS == 1)
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)All.MirroredRepeatArb);
            else
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)All.Repeat);

            if (Textures[tx].CMT == 2 || Textures[tx].CMT == 3)
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)All.ClampToEdge);
            else if (Textures[tx].CMT == 1)
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)All.MirroredRepeatArb);
            else
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)All.Repeat);

            return glid;
        }

        private void CalculateTextureSize(int tx)
        {
            int maxTexel = 0, lineShift = 0;

            switch (Textures[tx].Format)
            {
                /* 4-bit */
                case 0x00:
                    // RGBA
                    maxTexel = 4096; lineShift = 4;
                    break;
                case 0x40:
                    // CI
                    maxTexel = 4096; lineShift = 4;
                    break;
                case 0x60:
                    // IA
                    maxTexel = 8192; lineShift = 4;
                    break;
                case 0x80:
                    // I
                    maxTexel = 8192; lineShift = 4;
                    break;

                /* 8-bit */
                case 0x08:
                    // RGBA
                    maxTexel = 2048; lineShift = 3;
                    break;
                case 0x48:
                    // CI
                    maxTexel = 2048; lineShift = 3;
                    break;
                case 0x68:
                    // IA
                    maxTexel = 4096; lineShift = 3;
                    break;
                case 0x88:
                    // I
                    maxTexel = 4096; lineShift = 3;
                    break;

                /* 16-bit */
                case 0x10:
                    // RGBA
                    maxTexel = 2048; lineShift = 2;
                    break;
                case 0x50:
                    // CI
                    maxTexel = 2048; lineShift = 0;
                    break;
                case 0x70:
                    // IA
                    maxTexel = 2048; lineShift = 2;
                    break;
                case 0x90:
                    // I
                    maxTexel = 2048; lineShift = 0;
                    break;

                /* 32-bit */
                case 0x18:
                    // RGBA
                    maxTexel = 1024; lineShift = 2;
                    break;

                default:
                    return;
            }

            var lineWidth = ((int)Textures[tx].LineSize << lineShift);

            var tileWidth = ((int)Textures[tx].LRS - (int)Textures[tx].ULS) + 1;
            var tileHeight = ((int)Textures[tx].LRT - (int)Textures[tx].ULT) + 1;

            var maskWidth = 1 << (int)Textures[tx].MaskS;
            var maskHeight = 1 << (int)Textures[tx].MaskT;

            var lineHeight = 0;
            if (lineWidth > 0)
                lineHeight = Math.Min(maxTexel / lineWidth, tileHeight);

            if ((Textures[tx].MaskS > 0) && ((maskWidth * maskHeight) <= maxTexel))
                Textures[tx].Width = maskWidth;
            else if ((tileWidth * tileHeight) <= maxTexel)
                Textures[tx].Width = tileWidth;
            else
                Textures[tx].Width = lineWidth;

            if ((Textures[tx].MaskT > 0) && ((maskWidth * maskHeight) <= maxTexel))
                Textures[tx].Height = maskHeight;
            else if ((tileWidth * tileHeight) <= maxTexel)
                Textures[tx].Height = tileHeight;
            else
                Textures[tx].Height = lineHeight;

            var clampWidth = 0;
            var clampHeight = 0;

            if (Textures[tx].CMS == 1)
                clampWidth = tileWidth;
            else
                clampWidth = (int)Textures[tx].Width;

            if (Textures[tx].CMT == 1)
                clampHeight = tileHeight;
            else
                clampHeight = (int)Textures[tx].Height;

            if (clampWidth > 256) Textures[tx].CMS &= ~(uint)0x01;
            if (clampHeight > 256) Textures[tx].CMT &= ~(uint)0x01;

            if (maskWidth > Textures[tx].Width)
            {
                Textures[tx].MaskS = General.PowOf((int)Textures[tx].Width);
                maskWidth = 1 << (int)Textures[tx].MaskS;
            }
            if (maskHeight > Textures[tx].Height)
            {
                Textures[tx].MaskT = General.PowOf((int)Textures[tx].Height);
                maskHeight = 1 << (int)Textures[tx].MaskT;
            }

            if (Textures[tx].CMS == 2 || Textures[tx].CMS == 3)
                Textures[tx].RealWidth = General.Pow2(clampWidth);
            else if (Textures[tx].CMS == 1)
                Textures[tx].RealWidth = General.Pow2(maskWidth);
            else
                Textures[tx].RealWidth = General.Pow2((int)Textures[tx].Width);

            if (Textures[tx].CMT == 2 || Textures[tx].CMT == 3)
                Textures[tx].RealHeight = General.Pow2(clampHeight);
            else if (Textures[tx].CMT == 1)
                Textures[tx].RealHeight = General.Pow2(maskHeight);
            else
                Textures[tx].RealHeight = General.Pow2((int)Textures[tx].Height);

            Textures[tx].ScaleS = 1.0f / (float)Textures[tx].RealWidth;
            Textures[tx].ScaleT = 1.0f / (float)Textures[tx].RealHeight;

            Textures[tx].ShiftScaleS = 1.0f;
            Textures[tx].ShiftScaleT = 1.0f;

            if (Textures[tx].ShiftS > 10)
                Textures[tx].ShiftScaleS = (float)(1 << (int)(16 - Textures[tx].ShiftS));
            else if (Textures[tx].ShiftS > 0)
                Textures[tx].ShiftScaleS /= (float)(1 << (int)Textures[tx].ShiftS);

            if (Textures[tx].ShiftT > 10)
                Textures[tx].ShiftScaleT = (float)(1 << (16 - (int)Textures[tx].ShiftT));
            else if (Textures[tx].ShiftT > 0)
                Textures[tx].ShiftScaleT /= (float)(1 << (int)Textures[tx].ShiftT);
        }

        #endregion
    }
}
