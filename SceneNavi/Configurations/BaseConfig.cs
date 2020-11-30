namespace SceneNavi.Configurations
{
    public interface IBaseConfig
    {
        string UpdateServer { get; set; }
        bool RenderRoomActors { get; set; }
        bool RenderSpawnPoints { get; set; }
        bool RenderTransitions { get; set; }
        string LastRom { get; set; }
        bool RenderPathWaypoints { get; set; }
        bool LinkAllWPinPath { get; set; }
        bool RenderTextures { get; set; }
        bool RenderCollision { get; set; }
        bool RenderCollisionAsWhite { get; set; }
        bool OglvSync { get; set; }
        ToolModes LastToolMode { get; set; }
        string LastSceneFile { get; set; }
        string LastRoomFile { get; set; }
        CombinerTypes CombinerType { get; set; }
        bool ShownExtensionWarning { get; set; }
        bool ShownIntelWarning { get; set; }
        bool RenderWaterboxes { get; set; }
        bool ShowWaterboxesPerRoom { get; set; }
        bool IsRestarting { get; set; }
        int AntiAliasingSamples { get; set; }
        bool EnableAntiAliasing { get; set; }
        bool EnableMipmaps { get; set; }
    }
}