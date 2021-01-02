namespace SceneNavi
{
    public interface IMainFormConfig
    {
        bool IndividualFileMode { get; set; }
        bool SupportsCreateShader { get; set; }
        bool SupportsGenProgramsArb { get; set; }
        bool DisplayListsDirty { get; set; }
        bool CollisionDirty { get; set; }
        bool WaterBoxesDirty { get; set; }
        bool ObjectsDoubleBuffered { get; set; }
        bool PathWayPointsDoubleBuffered { get; set; }
    }
}