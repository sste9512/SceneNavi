using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Configuration;
using OpenTK.Graphics.OpenGL;

namespace SceneNavi.Framework.Client.Hardware
{
    public interface IKeyboardContext
    {
        int ProjectId { get; set; }
        Dictionary<byte, Action<object>> KeyMappings { get; set; }
    }
}