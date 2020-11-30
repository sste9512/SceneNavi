using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

using Nini.Config;

namespace SceneNavi
{
    /* Based on http://stackoverflow.com/a/15869868 */
   
    
    
    // This has been moved to its interface version in the configurations directory, for Config.net
    static class Configuration
    {
        static readonly string ConfigName = "Main";

        static string _configPath;
        static string _configFilename;

        static readonly IConfigSource _source;

        public static string FullConfigFilename => (Path.Combine(_configPath, _configFilename));

        public static string UpdateServer
        {
            get => (_source.Configs[ConfigName].GetString("UpdateServer",
                $"http://magicstone.de/dzd/progupdates/{System.Windows.Forms.Application.ProductName}.txt"));
            set => _source.Configs[ConfigName].Set("UpdateServer", value);
        }

        public static bool RenderRoomActors
        {
            get => (_source.Configs[ConfigName].GetBoolean("RenderRoomActors", true));
            set => _source.Configs[ConfigName].Set("RenderRoomActors", value);
        }

        public static bool RenderSpawnPoints
        {
            get => (_source.Configs[ConfigName].GetBoolean("RenderSpawnPoints", true));
            set => _source.Configs[ConfigName].Set("RenderSpawnPoints", value);
        }

        public static bool RenderTransitions
        {
            get => (_source.Configs[ConfigName].GetBoolean("RenderTransitions", true));
            set => _source.Configs[ConfigName].Set("RenderTransitions", value);
        }

        public static string LastRom
        {
            get => (_source.Configs[ConfigName].GetString("LastROM", string.Empty));
            set => _source.Configs[ConfigName].Set("LastROM", value);
        }

        public static bool RenderPathWaypoints
        {
            get => (_source.Configs[ConfigName].GetBoolean("RenderPathWaypoints", false));
            set => _source.Configs[ConfigName].Set("RenderPathWaypoints", value);
        }

        public static bool LinkAllWPinPath
        {
            get => (_source.Configs[ConfigName].GetBoolean("LinkAllWPinPath", true));
            set => _source.Configs[ConfigName].Set("LinkAllWPinPath", value);
        }

        public static bool RenderTextures
        {
            get => (_source.Configs[ConfigName].GetBoolean("RenderTextures", true));
            set => _source.Configs[ConfigName].Set("RenderTextures", value);
        }

        public static bool RenderCollision
        {
            get => (_source.Configs[ConfigName].GetBoolean("RenderCollision", false));
            set => _source.Configs[ConfigName].Set("RenderCollision", value);
        }

        public static bool RenderCollisionAsWhite
        {
            get => (_source.Configs[ConfigName].GetBoolean("RenderCollisionAsWhite", false));
            set => _source.Configs[ConfigName].Set("RenderCollisionAsWhite", value);
        }

        public static bool OglvSync
        {
            get => (_source.Configs[ConfigName].GetBoolean("OGLVSync", true));
            set => _source.Configs[ConfigName].Set("OGLVSync", value);
        }

        public static ToolModes LastToolMode
        {
            get => ((ToolModes)Enum.Parse(typeof(ToolModes), (_source.Configs[ConfigName].GetString("LastToolMode", "Camera"))));
            set => _source.Configs[ConfigName].Set("LastToolMode", value);
        }

        public static string LastSceneFile
        {
            get => (_source.Configs[ConfigName].GetString("LastSceneFile", string.Empty));
            set => _source.Configs[ConfigName].Set("LastSceneFile", value);
        }

        public static string LastRoomFile
        {
            get => (_source.Configs[ConfigName].GetString("LastRoomFile", string.Empty));
            set => _source.Configs[ConfigName].Set("LastRoomFile", value);
        }

        public static CombinerTypes CombinerType
        {
            get => ((CombinerTypes)Enum.Parse(typeof(CombinerTypes), (_source.Configs[ConfigName].GetString("CombinerType", "None"))));
            set => _source.Configs[ConfigName].Set("CombinerType", value);
        }

        public static bool ShownExtensionWarning
        {
            get => (_source.Configs[ConfigName].GetBoolean("ShownExtensionWarning", false));
            set => _source.Configs[ConfigName].Set("ShownExtensionWarning", value);
        }

        public static bool ShownIntelWarning
        {
            get => (_source.Configs[ConfigName].GetBoolean("ShownIntelWarning", false));
            set => _source.Configs[ConfigName].Set("ShownIntelWarning", value);
        }

        public static bool RenderWaterboxes
        {
            get => (_source.Configs[ConfigName].GetBoolean("RenderWaterboxes", true));
            set => _source.Configs[ConfigName].Set("RenderWaterboxes", value);
        }

        public static bool ShowWaterboxesPerRoom
        {
            get => (_source.Configs[ConfigName].GetBoolean("ShowWaterboxesPerRoom", true));
            set => _source.Configs[ConfigName].Set("ShowWaterboxesPerRoom", value);
        }

        public static bool IsRestarting
        {
            get => (_source.Configs[ConfigName].GetBoolean("IsRestarting", false));
            set => _source.Configs[ConfigName].Set("IsRestarting", value);
        }

        public static int AntiAliasingSamples
        {
            get => (_source.Configs[ConfigName].GetInt("AntiAliasingSamples", 0));
            set => _source.Configs[ConfigName].Set("AntiAliasingSamples", value);
        }

        public static bool EnableAntiAliasing
        {
            get => (_source.Configs[ConfigName].GetBoolean("EnableAntiAliasing", false));
            set => _source.Configs[ConfigName].Set("EnableAntiAliasing", value);
        }

        public static bool EnableMipmaps
        {
            get => (_source.Configs[ConfigName].GetBoolean("EnableMipmaps", false));
            set => _source.Configs[ConfigName].Set("EnableMipmaps", value);
        }

        static Configuration()
        {
            PrepareConfig();

            _source = new XmlConfigSource(FullConfigFilename) {AutoSave = true};

            CreateConfigSections();
        }

        private static void CreateConfigSections()
        {
            if (_source.Configs[ConfigName] == null) _source.AddConfig(ConfigName);
        }

        private static void PrepareConfig()
        {
            _configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), System.Windows.Forms.Application.ProductName);
            _configFilename = $"{ConfigName}.xml";
            Directory.CreateDirectory(_configPath);

            if (!File.Exists(FullConfigFilename)) File.WriteAllText(FullConfigFilename, "<Nini>\n</Nini>\n");
        }
    }
}
