using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;

namespace SceneNavi.SimpleF3DEX2.CombinerEmulation
{
    internal class GlslCombineManager
    {
        bool _supported;
        F3DEX2Interpreter _f3Dex2;
        List<GlslShaders> _shaderCache;

        public GlslCombineManager(F3DEX2Interpreter f3dex2)
        {
            _supported = ((GraphicsContext.CurrentContext as IGraphicsContextInternal).GetAddress("glCreateShader") != IntPtr.Zero);
            _f3Dex2 = f3dex2;
            _shaderCache = new List<GlslShaders>();

            /*foreach (uint[] knownMux in KnownCombinerMuxes.Muxes)
            {
                BindCombiner(knownMux[0], knownMux[1], true);
                BindCombiner(knownMux[0], knownMux[1], false);
            }*/
        }

        public void BindCombiner(uint m0, uint m1, bool tex)
        {
            if (!_supported) return;

            if (m0 == 0 && m1 == 0) return;

            var shader = _shaderCache.FirstOrDefault(x => x.Mux0 == m0 && x.Mux1 == m1 &&
                x.HasLightingEnabled == Convert.ToBoolean(_f3Dex2.GeometryMode & (uint)General.GeometryMode.LIGHTING) &&
                x.Textured == tex);

            if (shader != null)
                GL.UseProgram(shader.ProgramId);
            else
            {
                shader = new GlslShaders(m0, m1, _f3Dex2, tex);
                _shaderCache.Add(shader);
            }

            GL.Uniform1(GL.GetUniformLocation(shader.ProgramId, "tex0"), 0);
            GL.Uniform1(GL.GetUniformLocation(shader.ProgramId, "tex1"), 1);
            GL.Uniform4(GL.GetUniformLocation(shader.ProgramId, "primColor"), _f3Dex2.PrimColor);
            GL.Uniform4(GL.GetUniformLocation(shader.ProgramId, "envColor"), _f3Dex2.EnvColor);
        }
    }
}
