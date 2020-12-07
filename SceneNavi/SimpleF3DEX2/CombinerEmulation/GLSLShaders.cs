﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;

namespace SceneNavi.SimpleF3DEX2.CombinerEmulation
{
    internal class GlslShaders
    {
        private const string ShaderPrefix =
            "/*\n" +
            " * {0} - {1} Shader\n" +
            " * Combiner Raw: 0x{2:X8} 0x{3:X8}\n" +
            " * Lighting: {4}\n" +
            " */\n\n";

        private const string ShaderVersion = "#version 120\n";

        private const string VertexShaderVariables =
            "varying vec3 N;\n" +
            "varying vec3 v;\n";

        private const string FragmentShaderVariables = "varying vec3 N;\n" +
            "varying vec3 v;\n" +
            "uniform sampler2D tex0;\n" +
            "uniform sampler2D tex1;\n" +
            "uniform vec4 primColor;\n" +
            "uniform vec4 envColor;\n" +
            "vec4 combColor;\n" +
            "vec4 combAlpha;\n" +
            "vec4 outColor;\n" +
            "vec4 outAlpha;\n";

        private const string ShaderMainPrefix = "void main() {";
        private const string ShaderMainSuffix = "}";

        private const string FragmentShaderCommon =
            "vec4 tex0color = texture2D(tex0, gl_TexCoord[0].st);\n" +
            "vec4 tex1color = texture2D(tex1, gl_TexCoord[1].st);";

        private const string VertexShaderCommon =
            "v = vec3(gl_ModelViewMatrix * gl_Vertex);\n" +
            "N = normalize(gl_NormalMatrix * gl_Normal);\n" +
            "gl_TexCoord[0] = gl_MultiTexCoord0;\n" +
            "gl_TexCoord[1] = gl_MultiTexCoord1;\n" +
            "gl_Position = gl_ModelViewProjectionMatrix * gl_Vertex;";

        private const string FragmentShaderLighting =
            "vec3 L = normalize(gl_LightSource[0].position.xyz - v);\n" +
            "vec3 E = normalize(-v);\n" +
            "vec3 R = normalize(-reflect(L, N));\n" +
            "vec4 Iamb = gl_FrontLightProduct[0].ambient;\n" +
            "vec4 Idiff = gl_FrontLightProduct[0].diffuse * max(dot(N, L), 0.0);\n" +
            "vec4 Ispec = gl_FrontLightProduct[0].specular * pow(max(dot(R, E), 0.0), 0.3 * gl_FrontMaterial.shininess);\n";

        public uint Mux0 { get; private set; }
        public uint Mux1 { get; private set; }
        public bool HasLightingEnabled { get; private set; }

        public UnpackedCombinerMux Unpacked { get; private set; }

        public int VertexObject { get; private set; }
        public int FragmentObject { get; private set; }
        public int ProgramId { get; private set; }

        public bool Textured { get; private set; }

        private F3Dex2Interpreter _f3Dex2;

        public GlslShaders(uint m0, uint m1, F3Dex2Interpreter f3dex2, bool tex)
        {
            _f3Dex2 = f3dex2;

            Mux0 = m0;
            Mux1 = m1;
            HasLightingEnabled = Convert.ToBoolean(f3dex2.GeometryMode & (uint)General.GeometryMode.LIGHTING);

            Unpacked = new UnpackedCombinerMux(m0, m1);

            Textured = tex;

            StringBuilder vs = new StringBuilder(), fs = new StringBuilder();
            vs.AppendFormat(ShaderPrefix, Program.AppNameVer, "Vertex", m0, m1, HasLightingEnabled);
            vs.AppendLine(ShaderVersion);
            vs.AppendLine(VertexShaderVariables);
            vs.AppendLine(ShaderMainPrefix);
            vs.AppendLine(VertexShaderCommon);

            fs.AppendFormat(ShaderPrefix, Program.AppNameVer, "Fragment", m0, m1, HasLightingEnabled);
            fs.AppendLine(ShaderVersion);
            fs.AppendLine(FragmentShaderVariables);
            fs.AppendLine(ShaderMainPrefix);

            // TODO  can this be made nicer? texturing and lighting?
            if (Textured) fs.AppendLine(FragmentShaderCommon);

            fs.AppendLine(FragmentShaderLighting);

            if (!HasLightingEnabled)
            {
                fs.AppendLine("vec4 lightColor = gl_Color;");
                vs.AppendLine("gl_FrontColor = gl_Color;");
            }
            else
                fs.AppendLine("vec4 lightColor = gl_Color + Iamb + Idiff + Ispec;");

            if (!Textured) fs.AppendLine("vec4 tex0color = lightColor;\nvec4 tex1color = lightColor;");
            fs.AppendLine();

            for (var i = 0; i < 2; i++)
            {
                var calc = new StringBuilder();
                calc.AppendFormat("{0} = vec4((", (i == 0 ? "combColor" : "outColor"));

                switch (Unpacked.cA[i])
                {
                    case UnpackedCombinerMux.ComponentsC16.CCMUX_COMBINED:
                        calc.Append("combColor");
                        break;
                    case UnpackedCombinerMux.ComponentsC16.CCMUX_TEXEL0:
                        calc.Append("tex0color");
                        break;
                    case UnpackedCombinerMux.ComponentsC16.CCMUX_TEXEL1:
                        calc.Append("tex1color");
                        break;
                    case UnpackedCombinerMux.ComponentsC16.CCMUX_PRIMITIVE:
                        calc.Append("primColor");
                        break;
                    case UnpackedCombinerMux.ComponentsC16.CCMUX_SHADE:
                        calc.Append("lightColor");
                        break;
                    case UnpackedCombinerMux.ComponentsC16.CCMUX_ENVIRONMENT:
                        calc.Append("envColor");
                        break;
                    case UnpackedCombinerMux.ComponentsC16.CCMUX_1:
                        calc.Append("vec4(1.0, 1.0, 1.0, 1.0)");
                        break;
                    case UnpackedCombinerMux.ComponentsC16.CCMUX_COMBINED_ALPHA:
                        calc.Append("comb.a");
                        break;
                    case UnpackedCombinerMux.ComponentsC16.CCMUX_TEXEL0_ALPHA:
                        calc.Append("tex0color.a");
                        break;
                    case UnpackedCombinerMux.ComponentsC16.CCMUX_TEXEL1_ALPHA:
                        calc.Append("tex1color.a");
                        break;
                    case UnpackedCombinerMux.ComponentsC16.CCMUX_PRIMITIVE_ALPHA:
                        calc.Append("primColor.a");
                        break;
                    case UnpackedCombinerMux.ComponentsC16.CCMUX_SHADE_ALPHA:
                        calc.Append("lightColor.a");
                        break;
                    case UnpackedCombinerMux.ComponentsC16.CCMUX_ENV_ALPHA:
                        calc.Append("envColor.a");
                        break;
                    case UnpackedCombinerMux.ComponentsC16.CCMUX_LOD_FRACTION:
                        calc.Append("vec4(0.0, 0.0, 0.0, 0.0)");
                        break;
                    case UnpackedCombinerMux.ComponentsC16.CCMUX_PRIM_LOD_FRAC:
                        calc.Append("vec4(1.0, 1.0, 1.0, 1.0)");    //unemulated for now
                        break;
                    case UnpackedCombinerMux.ComponentsC16.CCMUX_0:
                        calc.Append("vec4(0.0, 0.0, 0.0, 0.0)");
                        break;
                }

                calc.Append(" - ");

                switch (Unpacked.cB[i])
                {
                    case UnpackedCombinerMux.ComponentsC16.CCMUX_COMBINED:
                        calc.Append("combColor");
                        break;
                    case UnpackedCombinerMux.ComponentsC16.CCMUX_TEXEL0:
                        calc.Append("tex0color");
                        break;
                    case UnpackedCombinerMux.ComponentsC16.CCMUX_TEXEL1:
                        calc.Append("tex1color");
                        break;
                    case UnpackedCombinerMux.ComponentsC16.CCMUX_PRIMITIVE:
                        calc.Append("primColor");
                        break;
                    case UnpackedCombinerMux.ComponentsC16.CCMUX_SHADE:
                        calc.Append("lightColor");
                        break;
                    case UnpackedCombinerMux.ComponentsC16.CCMUX_ENVIRONMENT:
                        calc.Append("envColor");
                        break;
                    case UnpackedCombinerMux.ComponentsC16.CCMUX_1:
                        calc.Append("vec4(1.0, 1.0, 1.0, 1.0)");
                        break;
                    case UnpackedCombinerMux.ComponentsC16.CCMUX_COMBINED_ALPHA:
                        calc.Append("comb.a");
                        break;
                    case UnpackedCombinerMux.ComponentsC16.CCMUX_TEXEL0_ALPHA:
                        calc.Append("tex0color.a");
                        break;
                    case UnpackedCombinerMux.ComponentsC16.CCMUX_TEXEL1_ALPHA:
                        calc.Append("tex1color.a");
                        break;
                    case UnpackedCombinerMux.ComponentsC16.CCMUX_PRIMITIVE_ALPHA:
                        calc.Append("primColor.a");
                        break;
                    case UnpackedCombinerMux.ComponentsC16.CCMUX_SHADE_ALPHA:
                        calc.Append("lightColor.a");
                        break;
                    case UnpackedCombinerMux.ComponentsC16.CCMUX_ENV_ALPHA:
                        calc.Append("envColor.a");
                        break;
                    case UnpackedCombinerMux.ComponentsC16.CCMUX_LOD_FRACTION:
                        calc.Append("vec4(0.0, 0.0, 0.0, 0.0)");
                        break;
                    case UnpackedCombinerMux.ComponentsC16.CCMUX_PRIM_LOD_FRAC:
                        calc.Append("vec4(1.0, 1.0, 1.0, 1.0)");    //unemulated for now
                        break;
                    case UnpackedCombinerMux.ComponentsC16.CCMUX_0:
                        calc.Append("vec4(0.0, 0.0, 0.0, 0.0)");
                        break;
                }

                calc.Append(") * ");

                switch (Unpacked.cC[i])
                {
                    case UnpackedCombinerMux.ComponentsC32.CCMUX_COMBINED:
                        calc.Append("combColor");
                        break;
                    case UnpackedCombinerMux.ComponentsC32.CCMUX_TEXEL0:
                        calc.Append("tex0color");
                        break;
                    case UnpackedCombinerMux.ComponentsC32.CCMUX_TEXEL1:
                        calc.Append("tex1color");
                        break;
                    case UnpackedCombinerMux.ComponentsC32.CCMUX_PRIMITIVE:
                        calc.Append("primColor");
                        break;
                    case UnpackedCombinerMux.ComponentsC32.CCMUX_SHADE:
                        calc.Append("lightColor");
                        break;
                    case UnpackedCombinerMux.ComponentsC32.CCMUX_ENVIRONMENT:
                        calc.Append("envColor");
                        break;
                    case UnpackedCombinerMux.ComponentsC32.CCMUX_1:
                        calc.Append("vec4(1.0, 1.0, 1.0, 1.0)");
                        break;
                    case UnpackedCombinerMux.ComponentsC32.CCMUX_COMBINED_ALPHA:
                        calc.Append("comb.a");
                        break;
                    case UnpackedCombinerMux.ComponentsC32.CCMUX_TEXEL0_ALPHA:
                        calc.Append("tex0color.a");
                        break;
                    case UnpackedCombinerMux.ComponentsC32.CCMUX_TEXEL1_ALPHA:
                        calc.Append("tex1color.a");
                        break;
                    case UnpackedCombinerMux.ComponentsC32.CCMUX_PRIMITIVE_ALPHA:
                        calc.Append("primColor.a");
                        break;
                    case UnpackedCombinerMux.ComponentsC32.CCMUX_SHADE_ALPHA:
                        calc.Append("lightColor.a");
                        break;
                    case UnpackedCombinerMux.ComponentsC32.CCMUX_ENV_ALPHA:
                        calc.Append("envColor.a");
                        break;
                    case UnpackedCombinerMux.ComponentsC32.CCMUX_LOD_FRACTION:
                        calc.Append("vec4(1.0, 1.0, 1.0, 1.0)");    //unemulated for now
                        break;
                    case UnpackedCombinerMux.ComponentsC32.CCMUX_PRIM_LOD_FRAC:
                        calc.Append("vec4(1.0, 1.0, 1.0, 1.0)");    //unemulated for now
                        break;
                    case UnpackedCombinerMux.ComponentsC32.CCMUX_K5:
                        calc.Append("vec4(1.0, 1.0, 1.0, 1.0)");    //unemulated for now
                        break;
                    case UnpackedCombinerMux.ComponentsC32.CCMUX_0:
                        calc.Append("vec4(0.0, 0.0, 0.0, 0.0)");
                        break;
                }

                calc.Append(" + ");

                switch (Unpacked.cD[i])
                {
                    case UnpackedCombinerMux.ComponentsC8.CCMUX_COMBINED:
                        calc.Append("combColor");
                        break;
                    case UnpackedCombinerMux.ComponentsC8.CCMUX_TEXEL0:
                        calc.Append("tex0color");
                        break;
                    case UnpackedCombinerMux.ComponentsC8.CCMUX_TEXEL1:
                        calc.Append("tex1color");
                        break;
                    case UnpackedCombinerMux.ComponentsC8.CCMUX_PRIMITIVE:
                        calc.Append("primColor");
                        break;
                    case UnpackedCombinerMux.ComponentsC8.CCMUX_SHADE:
                        calc.Append("lightColor");
                        break;
                    case UnpackedCombinerMux.ComponentsC8.CCMUX_ENVIRONMENT:
                        calc.Append("envColor");
                        break;
                    case UnpackedCombinerMux.ComponentsC8.CCMUX_1:
                        calc.Append("vec4(1.0, 1.0, 1.0, 1.0)");
                        break;
                    case UnpackedCombinerMux.ComponentsC8.CCMUX_0:
                        calc.Append("vec4(0.0, 0.0, 0.0, 0.0)");
                        break;
                }

                calc.AppendLine(");");

                calc.AppendFormat("{0} = vec4((", (i == 0 ? "combAlpha" : "outAlpha"));

                switch (Unpacked.aA[i])
                {
                    case UnpackedCombinerMux.ComponentsA8.ACMUX_COMBINED:
                        calc.Append("combAlpha.a");
                        break;
                    case UnpackedCombinerMux.ComponentsA8.ACMUX_TEXEL0:
                        calc.Append("tex0color.a");
                        break;
                    case UnpackedCombinerMux.ComponentsA8.ACMUX_TEXEL1:
                        calc.Append("tex1color.a");
                        break;
                    case UnpackedCombinerMux.ComponentsA8.ACMUX_PRIMITIVE:
                        calc.Append("primColor.a");
                        break;
                    case UnpackedCombinerMux.ComponentsA8.ACMUX_SHADE:
                        calc.Append("lightColor.a");
                        break;
                    case UnpackedCombinerMux.ComponentsA8.ACMUX_ENVIRONMENT:
                        calc.Append("envColor.a");
                        break;
                    case UnpackedCombinerMux.ComponentsA8.ACMUX_1:
                        calc.Append("vec4(1.0, 1.0, 1.0, 1.0)");
                        break;
                    case UnpackedCombinerMux.ComponentsA8.ACMUX_0:
                        calc.Append("vec4(0.0, 0.0, 0.0, 0.0)");
                        break;
                }

                calc.Append(" - ");

                switch (Unpacked.aB[i])
                {
                    case UnpackedCombinerMux.ComponentsA8.ACMUX_COMBINED:
                        calc.Append("combAlpha.a");
                        break;
                    case UnpackedCombinerMux.ComponentsA8.ACMUX_TEXEL0:
                        calc.Append("tex0color.a");
                        break;
                    case UnpackedCombinerMux.ComponentsA8.ACMUX_TEXEL1:
                        calc.Append("tex1color.a");
                        break;
                    case UnpackedCombinerMux.ComponentsA8.ACMUX_PRIMITIVE:
                        calc.Append("primColor.a");
                        break;
                    case UnpackedCombinerMux.ComponentsA8.ACMUX_SHADE:
                        calc.Append("lightColor.a");
                        break;
                    case UnpackedCombinerMux.ComponentsA8.ACMUX_ENVIRONMENT:
                        calc.Append("envColor.a");
                        break;
                    case UnpackedCombinerMux.ComponentsA8.ACMUX_1:
                        calc.Append("vec4(1.0, 1.0, 1.0, 1.0)");
                        break;
                    case UnpackedCombinerMux.ComponentsA8.ACMUX_0:
                        calc.Append("vec4(0.0, 0.0, 0.0, 0.0)");
                        break;
                }

                calc.Append(") * ");

                switch (Unpacked.aC[i])
                {
                    case UnpackedCombinerMux.ComponentsA8.ACMUX_COMBINED:
                        calc.Append("combAlpha.a");
                        break;
                    case UnpackedCombinerMux.ComponentsA8.ACMUX_TEXEL0:
                        calc.Append("tex0color.a");
                        break;
                    case UnpackedCombinerMux.ComponentsA8.ACMUX_TEXEL1:
                        calc.Append("tex1color.a");
                        break;
                    case UnpackedCombinerMux.ComponentsA8.ACMUX_PRIMITIVE:
                        calc.Append("primColor.a");
                        break;
                    case UnpackedCombinerMux.ComponentsA8.ACMUX_SHADE:
                        calc.Append("lightColor.a");
                        break;
                    case UnpackedCombinerMux.ComponentsA8.ACMUX_ENVIRONMENT:
                        calc.Append("envColor.a");
                        break;
                    case UnpackedCombinerMux.ComponentsA8.ACMUX_1:
                        calc.Append("vec4(1.0, 1.0, 1.0, 1.0)");
                        break;
                    case UnpackedCombinerMux.ComponentsA8.ACMUX_0:
                        calc.Append("vec4(0.0, 0.0, 0.0, 0.0)");
                        break;
                }

                calc.Append(" + ");

                switch (Unpacked.aD[i])
                {
                    case UnpackedCombinerMux.ComponentsA8.ACMUX_COMBINED:
                        calc.Append("combAlpha.a");
                        break;
                    case UnpackedCombinerMux.ComponentsA8.ACMUX_TEXEL0:
                        calc.Append("tex0color.a");
                        break;
                    case UnpackedCombinerMux.ComponentsA8.ACMUX_TEXEL1:
                        calc.Append("tex1color.a");
                        break;
                    case UnpackedCombinerMux.ComponentsA8.ACMUX_PRIMITIVE:
                        calc.Append("primColor.a");
                        break;
                    case UnpackedCombinerMux.ComponentsA8.ACMUX_SHADE:
                        calc.Append("lightColor.a");
                        break;
                    case UnpackedCombinerMux.ComponentsA8.ACMUX_ENVIRONMENT:
                        calc.Append("envColor.a");
                        break;
                    case UnpackedCombinerMux.ComponentsA8.ACMUX_1:
                        calc.Append("vec4(1.0, 1.0, 1.0, 1.0)");
                        break;
                    case UnpackedCombinerMux.ComponentsA8.ACMUX_0:
                        calc.Append("vec4(0.0, 0.0, 0.0, 0.0)");
                        break;
                }

                calc.AppendLine(");");
                calc.AppendLine("gl_FragColor.rgb = outColor.rgb;");
                calc.AppendLine("gl_FragColor.a = outAlpha.a;");

                fs.AppendLine(calc.ToString());
                //fs.AppendLine("gl_FragColor = lightColor;");
            }

            vs.AppendLine(ShaderMainSuffix);
            fs.AppendLine(ShaderMainSuffix);

            /*System.IO.StreamWriter sw = new System.IO.StreamWriter(string.Format(@"C:\Temp\{0:X8}_{1:X8}_{2}.txt", m0, m1, HasLightingEnabled ? "light" : "nolight"));
            sw.Write(vs.ToString());
            sw.Write(fs.ToString());
            sw.Close();
            */
            int vo = -1, fo = -1, p = -1;
            CreateShaders(vs.ToString(), fs.ToString(), ref vo, ref fo, ref p);

            VertexObject = vo;
            FragmentObject = fo;
            ProgramId = p;
        }

        // Modified from OpenTK examples \Source\Examples\OpenGL\2.x\SimpleGLSL.cs
        private void CreateShaders(string vs, string fs, ref int vertexObject, ref int fragmentObject, ref int program)
        {
            try
            {
                int statusCode;
                string info;

                vertexObject = GL.CreateShader(ShaderType.VertexShader);
                fragmentObject = GL.CreateShader(ShaderType.FragmentShader);

                /* Compile vertex shader */
                GL.ShaderSource(vertexObject, vs);
                GL.CompileShader(vertexObject);
                GL.GetShaderInfoLog(vertexObject, out info);
                GL.GetShader(vertexObject, ShaderParameter.CompileStatus, out statusCode);

                if (statusCode != 1) throw new ApplicationException(info);

                /* Compile vertex shader */
                GL.ShaderSource(fragmentObject, fs);
                GL.CompileShader(fragmentObject);
                GL.GetShaderInfoLog(fragmentObject, out info);
                GL.GetShader(fragmentObject, ShaderParameter.CompileStatus, out statusCode);

                if (statusCode != 1) throw new ApplicationException(info);

                program = GL.CreateProgram();
                GL.AttachShader(program, fragmentObject);
                GL.AttachShader(program, vertexObject);

                GL.LinkProgram(program);
                GL.UseProgram(program);
            }
            catch (ApplicationException ae)
            {
                System.Windows.Forms.MessageBox.Show(ae.ToString());
            }
        }
    }
}
