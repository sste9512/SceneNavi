using System.Collections.Generic;

namespace SceneNavi.ROMHandler.Interfaces
{
    public interface ISceneTableEntry : IHeaderParent
    {
        ushort GetNumber();
        void SetNumber(ushort number);

        string GetDMAFilename();
        string GetName();

        uint GetSceneStartAddress();
        uint GetSceneEndAddress();

        bool IsValid();
        bool IsAllZero();

        byte[] GetData();
        List<HeaderLoader> GetSceneHeaders();
        bool IsInROM();
        bool IsNameExternal();
        HeaderLoader GetCurrentSceneHeader();
        void SetCurrentSceneHeader(HeaderLoader header);

        HeaderCommands.Actors GetActiveTransitionData();
        HeaderCommands.Actors GetActiveSpawnPointData();
        HeaderCommands.SpecialObjects GetActiveSpecialObjs();
        HeaderCommands.Waypoints GetActiveWaypoints();
        HeaderCommands.Collision GetActiveCollision();
        HeaderCommands.SettingsSoundScene GetActiveSettingsSoundScene();
        HeaderCommands.EnvironmentSettings GetActiveEnvSettings();

        void SaveTableEntry();
        void ReadScene(HeaderCommands.Rooms forcerooms = null);
    }
}
