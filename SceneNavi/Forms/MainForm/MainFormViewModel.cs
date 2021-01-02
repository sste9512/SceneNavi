using System.Collections.Generic;

namespace SceneNavi.Forms.MainForm
{
    public class MainFormViewModel
    {
          public static readonly Dictionary<ToolModes, string[]> ToolModeNametable = new Dictionary<ToolModes, string[]>()
        {
            { ToolModes.Camera, new string[] { "Camera mode", "Mouse can only move around camera" } },
            { ToolModes.MoveableObjs, new string[] { "Moveable objects mode", "Mouse will select and modify moveable objects (ex. actors)" } },
            { ToolModes.StaticObjs, new string[] { "Static objects mode", "Mouse will select and modify static objects (ex. collision)" } },
        };

        public static readonly Dictionary<CombinerTypes, string[]> CombinerTypeNametable = new Dictionary<CombinerTypes, string[]>()
        {
            { CombinerTypes.None, new string[] { "None", "Does not try to emulate combiner; necessary on older or low-end hardware" } },
            { CombinerTypes.ArbCombiner, new string[] { "ARB Assembly Combiner", "Uses stable, mostly complete ARB combiner emulation; might not work on Intel hardware" } },
            { CombinerTypes.GlslCombiner, new string[] { "Experimental GLSL Combiner", "Uses experimental GLSL-based combiner emulation; not complete yet" } },
        };

        public static readonly string[] RequiredOglExtensionsGeneral = { "GL_ARB_multisample" };
        public static readonly string[] RequiredOglExtensionsCombinerGeneral = { "GL_ARB_multitexture" };
        public static readonly string[] RequiredOglExtensionsArbCombiner = { "GL_ARB_fragment_program" };
        public static readonly string[] RequiredOglExtensionsGlslCombiner = { "GL_ARB_shading_language_100", "GL_ARB_shader_objects", "GL_ARB_fragment_shader", "GL_ARB_vertex_shader" };
        public static string[] AllRequiredOglExtensions
        {
            get
            {
                var all = new List<string>();
                all.AddRange(RequiredOglExtensionsGeneral);
                all.AddRange(RequiredOglExtensionsCombinerGeneral);
                all.AddRange(RequiredOglExtensionsArbCombiner);
                all.AddRange(RequiredOglExtensionsGlslCombiner);
                return all.ToArray();
            }
        }
    }
}