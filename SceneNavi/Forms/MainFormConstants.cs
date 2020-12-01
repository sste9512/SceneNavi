using System.Collections.Generic;
using SceneNavi;

static internal class MainFormConstants
{
    public static readonly Dictionary<ToolModes, string[]> ToolModeNametable = new Dictionary<ToolModes, string[]>()
    {
        { ToolModes.Camera, new string[] { "Camera mode", "Mouse can only move around camera" } },
        { ToolModes.MoveableObjs, new string[] { "Moveable objects mode", "Mouse will select and modify moveable objects (ex. actors)" } },
        { ToolModes.StaticObjs, new string[] { "Static objects mode", "Mouse will select and modify static objects (ex. collision)" } },
    };
}