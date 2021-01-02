﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;

namespace SceneNavi.Controls
{
    class GlRendererControl : GLControl
    {
        public GlRendererControl()
            : base(new GraphicsMode(GraphicsMode.Default.ColorFormat, GraphicsMode.Default.Depth,
                GraphicsMode.Default.Stencil, Configuration.AntiAliasingSamples))
        {








        }
    }
}
