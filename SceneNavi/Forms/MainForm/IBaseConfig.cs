using SceneNavi.Forms;

namespace SceneNavi.Configurations
{
    public interface IBaseConfig
    {
        string UpdateServer { get; set; }
        string LastRom { get; set; }
        bool LinkAllWPinPath { get; set; }
        ToolModes LastToolMode { get; set; }
        string LastSceneFile { get; set; }
        string LastRoomFile { get; set; }
        CombinerTypes CombinerType { get; set; }
        bool ShownExtensionWarning { get; set; }
        bool ShownIntelWarning { get; set; }
        bool IsRestarting { get; set; }
    }
}