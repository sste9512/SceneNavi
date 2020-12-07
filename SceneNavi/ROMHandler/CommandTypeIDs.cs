namespace SceneNavi.ROMHandler
{
    public enum CommandTypeIDs : byte
    {
        Spawns = 0x00,
        Actors = 0x01,
        Unknown0x02 = 0x02,
        Collision = 0x03,
        Rooms = 0x04,
        WindSettings = 0x05,
        Entrances = 0x06,
        SpecialObjects = 0x07,
        RoomBehavior = 0x08,
        Unknown0x09 = 0x09,
        MeshHeader = 0x0A,
        Objects = 0x0B,
        Unknown0x0C = 0x0C,
        Waypoints = 0x0D,
        Transitions = 0x0E,
        EnvironmentSettings = 0x0F,
        SettingsTime = 0x10,
        SettingsSkyboxScene = 0x11,
        SettingsSkyboxRoom = 0x12,
        Exits = 0x13,
        EndOfHeader = 0x14,
        SettingsSoundScene = 0x15,
        SettingsSoundRoom = 0x16,
        Cutscenes = 0x17,
        SubHeaders = 0x18,
        SceneBehavior = 0x19
    }
}