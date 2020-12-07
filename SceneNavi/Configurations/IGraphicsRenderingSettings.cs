namespace SceneNavi.Configurations
{
    public interface IGraphicsRenderingSettings
    {
        bool OglVSync { get; set; }
        int AntiAliasingSamples { get; set; }
        bool EnableAntiAliasing { get; set; }
        bool EnableMipmaps { get; set; }
        double OglSceneScale { get; set; }
    }
}