using System;

namespace SceneNavi.Models
{
    [Flags]
    public enum DefaultTypes
    {
        None = 0x00,
        RoomActor = 0x01,
        TransitionActor = 0x02,
        SpawnPoint = 0x04
    };
}