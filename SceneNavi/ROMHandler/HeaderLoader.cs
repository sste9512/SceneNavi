using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SceneNavi.HeaderCommands;
using SceneNavi.ROMHandler.Interfaces;

namespace SceneNavi.ROMHandler
{
    public class HeaderLoader
    {
        /* Used to simplify association of room headers with scene header, for grouping headers by "stage" */

        /* Speaking of stages, some conversion stuff... */

        /* Command IDs */

        /* Translation table for commands */
        public static readonly System.Collections.Hashtable CommandHumanNames = new System.Collections.Hashtable()
        {
            {CommandTypeIDs.Spawns, "Spawn points"},
            {CommandTypeIDs.Actors, "Actors"},
            {CommandTypeIDs.Unknown0x02, "Unknown 0x02"},
            {CommandTypeIDs.Collision, "Collision"},
            {CommandTypeIDs.Rooms, "Rooms"},
            {CommandTypeIDs.WindSettings, "Wind settings"},
            {CommandTypeIDs.Entrances, "Entrances"},
            {CommandTypeIDs.SpecialObjects, "Special objects"},
            {CommandTypeIDs.RoomBehavior, "Room behavior"},
            {CommandTypeIDs.Unknown0x09, "Unknown 0x09"},
            {CommandTypeIDs.MeshHeader, "Mesh header"},
            {CommandTypeIDs.Objects, "Objects"},
            {CommandTypeIDs.Unknown0x0C, "Unknown 0x0C"},
            {CommandTypeIDs.Waypoints, "Waypoints"},
            {CommandTypeIDs.Transitions, "Transition actors"},
            {CommandTypeIDs.EnvironmentSettings, "Enviroments settings"},
            {CommandTypeIDs.SettingsTime, "Time settings"},
            {CommandTypeIDs.SettingsSkyboxScene, "Skybox settings (scene)"},
            {CommandTypeIDs.SettingsSkyboxRoom, "Skybox settings (room)"},
            {CommandTypeIDs.Exits, "Exits"},
            {CommandTypeIDs.EndOfHeader, "End of header"},
            {CommandTypeIDs.SettingsSoundScene, "Sound settings (scene)"},
            {CommandTypeIDs.SettingsSoundRoom, "Sound settings (room)"},
            {CommandTypeIDs.Cutscenes, "Cutscenes"},
            {CommandTypeIDs.SubHeaders, "Sub-headers"},
            {CommandTypeIDs.SceneBehavior, "Scene behavior"}
        };

        /* Command ID to implementing class associations; add here to add new header command classes! */
        private static readonly System.Collections.Hashtable CommandTypes = new System.Collections.Hashtable()
        {
            {CommandTypeIDs.Rooms, typeof(Rooms)},
            {CommandTypeIDs.MeshHeader, typeof(MeshHeader)},
            {CommandTypeIDs.Actors, typeof(Actors)},
            {CommandTypeIDs.Transitions, typeof(Actors)},
            {CommandTypeIDs.Spawns, typeof(Actors)},
            {CommandTypeIDs.Objects, typeof(Objects)},
            {CommandTypeIDs.SpecialObjects, typeof(SpecialObjects)},
            {CommandTypeIDs.Waypoints, typeof(Waypoints)},
            {CommandTypeIDs.Collision, typeof(Collision)},
            {CommandTypeIDs.SettingsSoundScene, typeof(SettingsSoundScene)},
            {CommandTypeIDs.EnvironmentSettings, typeof(EnvironmentSettings)},
        };

        public List<Generic> Commands { get; }
        public int Offset { get; }
        private byte Segment { get; }
        public int Number { get; }
        private string Description => $"0x{(Offset | (Segment << 24)):X8}";

        public IHeaderParent Parent { get; }

        public HeaderLoader(BaseRomHandler baseRom, IHeaderParent parent, byte segment, int offset, int number)
        {
            Parent = parent;

            Offset = offset;
            Segment = segment;
            Number = number;

            Commands = new List<Generic>();
            Generic command = null;
            while ((command = new Generic(baseRom, parent, segment, ref offset)).Command != CommandTypeIDs.EndOfHeader)
            {
                var commandType = (Type) CommandTypes[command.Command];
                var inst =
                    Activator.CreateInstance((commandType == null ? typeof(Generic) : commandType), command);
                Commands.Add((Generic) inst);
            }
        }

        public override string ToString()
        {
            return Description;
        }
    }
}