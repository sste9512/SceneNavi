namespace SceneNavi.Configurations
{
    public interface IViewPortRenderSettings
    {
        bool RenderPathWayPoints { get; set; }
        bool RenderRoomActors { get; set; }
        bool RenderSpawnPoints { get; set; }
        bool RenderTransitions { get; set; }
        bool RenderTextures { get; set; }
        bool RenderCollision { get; set; }
        bool RenderCollisionAsWhite { get; set; }
        bool RenderWaterBoxes { get; set; }
        bool ShowWaterBoxesPerRoom { get; set; }
    }
}