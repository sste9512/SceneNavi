using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;

using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using SceneNavi.HeaderCommands;
using SceneNavi.Models;
using SceneNavi.ROMHandler;
using SceneNavi.ROMHandler.Interfaces;
using SceneNavi.Utilities.OpenGLHelpers;
using IContainer = Autofac.IContainer;

namespace SceneNavi
{
    /*
     * As usual, my GUI code is a mess! :D
     * There's some useful stuff in here, like the OpenGL picking code, but overall this is probably the least interesting part of the program...
     * ...like, excluding constants and enums or something anyway.
     */

    public enum ToolModes { Camera, MoveableObjs, StaticObjs };
    public enum CombinerTypes { None, ArbCombiner, GLSLCombiner }

    public partial class MainForm : Form
    {
        static readonly Dictionary<ToolModes, string[]> toolModeNametable = new Dictionary<ToolModes, string[]>()
        {
            { ToolModes.Camera, new string[] { "Camera mode", "Mouse can only move around camera" } },
            { ToolModes.MoveableObjs, new string[] { "Moveable objects mode", "Mouse will select and modify moveable objects (ex. actors)" } },
            { ToolModes.StaticObjs, new string[] { "Static objects mode", "Mouse will select and modify static objects (ex. collision)" } },
        };

        static readonly Dictionary<CombinerTypes, string[]> combinerTypeNametable = new Dictionary<CombinerTypes, string[]>()
        {
            { CombinerTypes.None, new string[] { "None", "Does not try to emulate combiner; necessary on older or low-end hardware" } },
            { CombinerTypes.ArbCombiner, new string[] { "ARB Assembly Combiner", "Uses stable, mostly complete ARB combiner emulation; might not work on Intel hardware" } },
            { CombinerTypes.GLSLCombiner, new string[] { "Experimental GLSL Combiner", "Uses experimental GLSL-based combiner emulation; not complete yet" } },
        };

        static readonly string[] requiredOglExtensionsGeneral = new string[] { "GL_ARB_multisample" };
        static readonly string[] requiredOglExtensionsCombinerGeneral = new string[] { "GL_ARB_multitexture" };
        static readonly string[] requiredOglExtensionsARBCombiner = new string[] { "GL_ARB_fragment_program" };
        static readonly string[] requiredOglExtensionsGLSLCombiner = new string[] { "GL_ARB_shading_language_100", "GL_ARB_shader_objects", "GL_ARB_fragment_shader", "GL_ARB_vertex_shader" };

        static string[] allRequiredOglExtensions
        {
            get
            {
                var all = new List<string>();
                all.AddRange(requiredOglExtensionsGeneral);
                all.AddRange(requiredOglExtensionsCombinerGeneral);
                all.AddRange(requiredOglExtensionsARBCombiner);
                all.AddRange(requiredOglExtensionsGLSLCombiner);
                return all.ToArray();
            }
        }

        bool ready, busy;
        bool[] keysDown = new bool[ushort.MaxValue];
        TextPrinter glText;
        Camera camera;
        FPSMonitor fpsMonitor;

        double oglSceneScale;

        bool supportsCreateShader, supportsGenProgramsARB;

        ToolModes internalToolMode;
        ToolModes currentToolMode
        {
            get => internalToolMode;
            set
            {
                Configuration.LastToolMode = internalToolMode = (Enum.IsDefined(typeof(ToolModes), value) ? internalToolMode = value : internalToolMode = ToolModes.Camera);
                bsiToolMode.Text = toolModeNametable[internalToolMode][0];
                if (mouseModeToolStripMenuItem.DropDownItems.Count > 0)
                {
                    (mouseModeToolStripMenuItem.DropDownItems[(int)internalToolMode] as Controls.ToolStripRadioButtonMenuItem).Checked = true;
                }
            }
        }

        CombinerTypes internalCombinerType;
        CombinerTypes currentCombinerType
        {
            get => internalCombinerType;
            set
            {
                Configuration.CombinerType = internalCombinerType = (Enum.IsDefined(typeof(CombinerTypes), value) ? internalCombinerType = value : internalCombinerType = CombinerTypes.None);
                if (_baseRom != null) _baseRom.Renderer.InitCombiner();
                displayListsDirty = true;
            }
        }

        BaseRomHandler _baseRom;
        bool individualFileMode;
        Dictionary<byte, string> bgms;

        ISceneTableEntry currentScene;
        RoomInfoClass currentRoom;
        List<MeshHeader> allMeshHeaders;
        Collision.Polygon currentCollisionPolygon;
        Collision.PolygonType currentColPolygonType;
        Collision.Waterbox currentWaterbox;
        DisplayListEx.Triangle currentRoomTriangle;
        SimpleF3DEX2.Vertex currentRoomVertex;
        EnvironmentSettings.Entry currentEnvSettings;

        ISceneTableEntry tempScene;
        Rooms tempRooms;

        // weird but works?
        PathHeader activePathHeader
        {
            get => (cbPathHeaders.SelectedItem as PathHeader);
            set => RefreshPathWaypoints();
        }

        BindingSource roomActorComboBinding, transitionComboBinding, spawnPointComboBinding, collisionPolyDataBinding, colPolyTypeDataBinding, waypointPathComboDataBinding, waterboxComboDataBinding;

        bool displayListsDirty, collisionDirty, waterboxesDirty;
        DisplayList collisionDL, waterboxDL;
        TabPage lastTabPage;

        IPickableObject pickedObject;
        Vector2d pickObjDisplacement, pickObjLastPosition, pickObjPosition;

        List<Option> roomsForWaterboxSelection;

        public MainForm()
        {
            InitializeComponent();

            Application.Idle += Application_Idle;
            Application.ApplicationExit += Application_ApplicationExit;
            Program.Status.MessageChanged += StatusMsg_OnStatusMessageChanged;

            dgvObjects.DoubleBuffered(true);
            dgvPathWaypoints.DoubleBuffered(true);

            SetFormTitle();
             
          
            
        }
        
        private void StatusMsg_OnStatusMessageChanged(object sender, StatusMessageHandler.MessageChangedEventArgs e)
        {
            tsslStatus.Text = e.Message;
            statusStrip1.Invoke((MethodInvoker)(() => statusStrip1.Update()));
        }

        private void SetFormTitle()
        {
            var filenamePart = ((_baseRom != null && _baseRom.Loaded) ? $" - [{Path.GetFileName(_baseRom.Filename)}]" : string.Empty);
            var scenePart = (individualFileMode ? $" ({Path.GetFileName(Configuration.LastSceneFile)})" : string.Empty);
            Text = string.Concat(Program.AppNameVer, filenamePart, scenePart);
        }

        private void Application_Idle(object sender, EventArgs e)
        {
            if (ready)
            {
                camera.KeyUpdate(keysDown);
                customGLControl.Invalidate();

                bsiCamCoords.Text = string.Format(System.Globalization.CultureInfo.InvariantCulture, "Cam X: {0:00.000}, Y: {1:00.000}, Z: {2:00.000}", camera.Pos.X, camera.Pos.Y, camera.Pos.Z);
            }
        }

        private void Application_ApplicationExit(object sender, EventArgs e)
        {
            glText?.Dispose();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            ResetCurrentData();
        }

        private void SettingsGUIInit()
        {
            /* Read settings */
            enableTexturesToolStripMenuItem.Checked = Configuration.RenderTextures;
            renderCollisionToolStripMenuItem.Checked = Configuration.RenderCollision;

            whiteToolStripMenuItem.Checked = Configuration.RenderCollisionAsWhite;
            typebasedToolStripMenuItem.Checked = !whiteToolStripMenuItem.Checked;

            renderRoomActorsToolStripMenuItem.Checked = Configuration.RenderRoomActors;
            renderSpawnPointsToolStripMenuItem.Checked = Configuration.RenderSpawnPoints;
            renderTransitionsToolStripMenuItem.Checked = Configuration.RenderTransitions;

            renderPathWaypointsToolStripMenuItem.Checked = Configuration.RenderPathWaypoints;
            linkAllWaypointsInPathToolStripMenuItem.Checked = Configuration.LinkAllWPinPath;

            renderWaterboxesToolStripMenuItem.Checked = Configuration.RenderWaterboxes;

            showWaterboxesPerRoomToolStripMenuItem.Checked = Configuration.ShowWaterboxesPerRoom;

            enableVSyncToolStripMenuItem.Checked = customGLControl.VSync = Configuration.OglvSync;
            enableAntiAliasingToolStripMenuItem.Checked = Configuration.EnableAntiAliasing;
            enableMipmapsToolStripMenuItem.Checked = Configuration.EnableMipmaps;

            currentToolMode = Configuration.LastToolMode;
            currentCombinerType = Configuration.CombinerType;

            /* Create tool mode menu */
            var i = 0;
            foreach (var kvp in toolModeNametable)
            {
                var tsmi = new Controls.ToolStripRadioButtonMenuItem(kvp.Value[0]) { Tag = kvp.Key, CheckOnClick = true, HelpText = kvp.Value[1] };
                if (currentToolMode == kvp.Key) tsmi.Checked = true;

                tsmi.Click += (s, ex) =>
                {
                    var tag = ((ToolStripMenuItem)s).Tag;
                    if (tag is ToolModes) currentToolMode = ((ToolModes)tag);
                };

                mouseModeToolStripMenuItem.DropDownItems.Add(tsmi);
                i++;
            }

            /* Create combiner type menu */
            i = 0;
            foreach (var kvp in combinerTypeNametable)
            {
                var tsmi = new Controls.ToolStripRadioButtonMenuItem(kvp.Value[0]) { Tag = kvp.Key, CheckOnClick = true, HelpText = kvp.Value[1] };
                if (currentCombinerType == kvp.Key) tsmi.Checked = true;

                tsmi.Click += (s, ex) =>
                {
                    var tag = ((ToolStripMenuItem)s).Tag;
                    if (tag is CombinerTypes) currentCombinerType = ((CombinerTypes)tag);
                };

                combinerTypeToolStripMenuItem.DropDownItems.Add(tsmi);
                i++;
            }

            /* Initialize help */
            InitializeMenuHelp();
        }

        private void InitializeMenuHelp()
        {
            /* Kinda buggy in practice (ex. with disabled menu items...) */
            foreach (var menuItem in menuStrip1.Items.FlattenMenu().ToList())
            {
                if (menuItem.HelpText == null) continue;

                menuItem.Hint += ((s, e) =>
                {
                    if (Program.IsHinting) return;
                    Program.IsHinting = true;
                    Program.Status.Message = (s as Controls.ToolStripHintMenuItem).HelpText;
                });
                menuItem.Unhint += ((s, e) =>
                {
                    if (!Program.IsHinting) return;
                    Program.IsHinting = false;
                    CreateStatusString();
                });
            }
        }

        private void CreateSceneTree()
        {
            tvScenes.Nodes.Clear();
            TreeNode root = null;

            if (!individualFileMode)
            {
                root = new TreeNode(
                    $"{_baseRom.Title} ({_baseRom.GameId}, v1.{_baseRom.Version}; {_baseRom.Scenes.Count} scenes)") { Tag = _baseRom };
                foreach (var ste in _baseRom.Scenes)
                {
                    var scene = new TreeNode($"{ste.GetName()} (0x{ste.GetSceneStartAddress():X})") { Tag = ste };

                    if (ste.GetSceneHeaders().Count != 0)
                    {
                        var rooms = ste.GetSceneHeaders()[0].Commands.FirstOrDefault(x => x.Command == CommandTypeIDs.Rooms) as Rooms;
                        if (rooms == null) continue;

                        foreach (var shead in ste.GetSceneHeaders())
                        {
                            var rhs = new List<HeaderLoader>();
                            foreach (var ric in rooms.RoomInformation)
                                if (ric.Headers.Count != 0) rhs.Add(ric.Headers[shead.Number]);

                            var hp = new HeaderPair(shead, rhs);

                            var de = new System.Collections.DictionaryEntry();
                            foreach (System.Collections.DictionaryEntry d in _baseRom.XmlStageDescriptions.Names)
                            {
                                var sk = d.Key as StageKey;
                                if (sk.SceneAddress == ste.GetSceneStartAddress() && sk.HeaderNumber == hp.SceneHeader.Number)
                                {
                                    de = d;
                                    hp.Description = (string)de.Value;
                                    break;
                                }
                            }

                            var sheadnode = new TreeNode((de.Value == null ? $"Stage #{shead.Number}" : (string)de.Value)) { Tag = hp };
                            foreach (var ric in rooms.RoomInformation)
                            {
                                var room = new TreeNode($"{ric.Description} (0x{ric.Start:X})") { Tag = ric };
                                sheadnode.Nodes.Add(room);
                            }

                            scene.Nodes.Add(sheadnode);
                        }
                    }

                    root.Nodes.Add(scene);
                }

                root.Expand();
                tvScenes.Nodes.Add(root);
            }
            else
            {
                root = new TreeNode(tempScene.GetName()) { Tag = tempScene };
                var rooms = tempScene.GetSceneHeaders()[0].Commands.FirstOrDefault(x => x.Command == CommandTypeIDs.Rooms) as Rooms;

                TreeNode nodeToSelect = null;
                if (rooms != null)
                {
                    foreach (var shead in tempScene.GetSceneHeaders())
                    {
                        var rhs = new List<HeaderLoader>();
                        foreach (var ric in rooms.RoomInformation)
                            if (ric.Headers.Count != 0) rhs.Add(ric.Headers[shead.Number]);

                        var hp = new HeaderPair(shead, rhs);

                        var sheadnode = new TreeNode($"Stage #{shead.Number}") { Tag = hp };
                        foreach (var ric in rooms.RoomInformation)
                        {
                            var room = new TreeNode($"{ric.Description} (0x{ric.Start:X})") { Tag = ric };
                            sheadnode.Nodes.Add(room);
                        }
                        sheadnode.Expand();
                        root.Nodes.Add(sheadnode);
                        if (nodeToSelect == null) nodeToSelect = sheadnode.FirstNode;
                    }
                }

                root.Expand();
                tvScenes.Nodes.Add(root);
                tvScenes.SelectedNode = nodeToSelect;
            }
        }

        private void PopulateMiscControls()
        {
            if (_baseRom == null) return;

            bgms = new Dictionary<byte, string>();
            foreach (System.Collections.DictionaryEntry de in _baseRom.XmlSongNames.Names) bgms.Add((byte)de.Key, (string)de.Value);
        }

        private void openROMToolStripMenuItem_Click(object sender, EventArgs e)
        {
            /* Get last ROM */
            if (Configuration.LastRom != string.Empty)
            {
                ofdOpenROM.InitialDirectory = Path.GetDirectoryName(Configuration.LastRom);
                ofdOpenROM.FileName = Path.GetFileName(Configuration.LastRom);
            }

            if (ofdOpenROM.ShowDialog() == DialogResult.OK)
            {
                Program.Status.Message = "Loading; please wait...";
                Cursor.Current = Cursors.WaitCursor;

                individualFileMode = false;
                displayListsDirty = collisionDirty = waterboxesDirty = true;

                Configuration.LastRom = ofdOpenROM.FileName;
                _baseRom = new BaseRomHandler(ofdOpenROM.FileName);

                if (_baseRom.Loaded)
                {
                    ResetCurrentData();

                    PopulateMiscControls();

                    CreateSceneTree();
                    SetFormTitle();
#if BLAHBLUB
                    //header dumper
                    System.IO.TextWriter tw = System.IO.File.CreateText("D:\\roms\\n64\\headers.txt");
                    tw.WriteLine("ROM: {0} ({1}, v1.{2}; {3})", ROM.Title, ROM.GameID, ROM.Version, ROM.BuildDateString);
                    foreach (SceneTableEntry ste in ROM.Scenes)
                    {
                        HeaderCommands.Rooms rooms = null;
                        tw.WriteLine(" SCENE: " + ste.Name);
                        foreach (HeaderLoader hl in ste.SceneHeaders)
                        {
                            tw.WriteLine("  HEADER: " + hl.Description);
                            foreach (HeaderCommands.Generic cmd in hl.Commands/*.OrderBy(x => (x.Data >> 56))*/)
                            {
                                if (cmd is HeaderCommands.Rooms) rooms = (cmd as HeaderCommands.Rooms);

                                //if (!((cmd.Data >> 56) == (byte)HeaderLoader.CommandTypeIDs.SubHeaders) && !(cmd is HeaderCommands.Actors) && !(cmd is HeaderCommands.Collision) &&
                                //    !(cmd is HeaderCommands.MeshHeader) && !(cmd is HeaderCommands.Objects) && !(cmd is HeaderCommands.Rooms) && !(cmd is HeaderCommands.SpecialObjects) &&
                                //    !(cmd is HeaderCommands.Waypoints))
                                tw.WriteLine("   COMMAND: " + cmd.ByteString + "; " + cmd.Description);
                            }
                        }

                        if (rooms != null)
                        {
                            foreach (HeaderCommands.Rooms.RoomInfoClass ric in rooms.RoomInformation)
                            {
                                tw.WriteLine("  ROOM: " + ric.Description);
                                foreach (HeaderLoader hl in ric.Headers)
                                {
                                    tw.WriteLine("   HEADER: " + hl.Description);
                                    foreach (HeaderCommands.Generic cmd in hl.Commands/*.OrderBy(x => (x.Data >> 56))*/)
                                    {
                                        //if (!((cmd.Data >> 56) == (byte)HeaderLoader.CommandTypeIDs.SubHeaders) && !(cmd is HeaderCommands.Actors) && !(cmd is HeaderCommands.Collision) &&
                                        //    !(cmd is HeaderCommands.MeshHeader) && !(cmd is HeaderCommands.Objects) && !(cmd is HeaderCommands.Rooms) && !(cmd is HeaderCommands.SpecialObjects) &&
                                        //    !(cmd is HeaderCommands.Waypoints))
                                        tw.WriteLine("    COMMAND: " + cmd.ByteString + "; " + cmd.Description);
                                    }
                                }
                            }
                        }

                        tw.WriteLine();
                    }
                    tw.Close();
#endif
                }
                else
                {
                    Program.Status.Message = "Error loading ROM";
                }

                Cursor.Current = DefaultCursor;

                editDataTablesToolStripMenuItem.Enabled = saveToolStripMenuItem.Enabled = openSceneToolStripMenuItem.Enabled = rOMInformationToolStripMenuItem.Enabled = customGLControl.Enabled = _baseRom.Loaded;
            }
        }

        private void openSceneToolStripMenuItem_Click(object sender, EventArgs e)
        {
            /* Get last scene and room */
            if (Configuration.LastSceneFile != string.Empty)
            {
                ofdOpenScene.InitialDirectory = Path.GetDirectoryName(Configuration.LastSceneFile);
                ofdOpenScene.FileName = Path.GetFileName(Configuration.LastSceneFile);
            }

            if (Configuration.LastRoomFile != string.Empty)
            {
                ofdOpenRoom.InitialDirectory = Path.GetDirectoryName(Configuration.LastRoomFile);
                ofdOpenRoom.FileName = Path.GetFileName(Configuration.LastRoomFile);
            }

            if (ofdOpenScene.ShowDialog() == DialogResult.OK)
            {
                Configuration.LastSceneFile = ofdOpenScene.FileName;

                if ((tempScene = (!_baseRom.IsMajora ? new SceneTableEntryOcarina(_baseRom, ofdOpenScene.FileName) : (ISceneTableEntry)new SceneTableEntryMajora(_baseRom, ofdOpenScene.FileName))) != null)
                {
                    if (ofdOpenRoom.ShowDialog() != DialogResult.OK) return;

                    Configuration.LastRoomFile = ofdOpenRoom.FileName;

                    individualFileMode = true;
                    displayListsDirty = collisionDirty = waterboxesDirty = true;

                    ResetCurrentData(true);
                    tempScene.ReadScene((tempRooms = new Rooms(_baseRom, tempScene, ofdOpenRoom.FileName)));
                    CreateSceneTree();

                    SetFormTitle();

                    openSceneToolStripMenuItem.Enabled = false;
                    closeSceneToolStripMenuItem.Enabled = true;
                }
            }
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Program.Status.Message = "Saving; please wait...";

            Cursor.Current = Cursors.WaitCursor;
            SaveAllData();
            Cursor.Current = DefaultCursor;

            RefreshCurrentData();
        }

        private void closeSceneToolStripMenuItem_Click(object sender, EventArgs e)
        {
            individualFileMode = false;
            displayListsDirty = collisionDirty = waterboxesDirty = true;

            ResetCurrentData();
            CreateSceneTree();
            SetFormTitle();

            closeSceneToolStripMenuItem.Enabled = false;
            openSceneToolStripMenuItem.Enabled = true;
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void SaveAllData()
        {
            if (individualFileMode)
            {
                if (tempRooms.RoomInformation.Count != 1) throw new Exception("Zero or more than one individual room file loaded; this should not happen!");

                ParseStoreHeaders(tempScene.GetSceneHeaders(), tempScene.GetData(), 0);
                ParseStoreHeaders(tempRooms.RoomInformation[0].Headers, tempRooms.RoomInformation[0].Data, 0);

                var bwScene = new BinaryWriter(File.Open(Configuration.LastSceneFile, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite));
                bwScene.Write(tempScene.GetData());
                bwScene.Close();

                var bwRoom = new BinaryWriter(File.Open(Configuration.LastRoomFile, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite));
                bwRoom.Write(tempRooms.RoomInformation[0].Data);
                bwRoom.Close();
            }
            else
            {
                /* Store scene table entries & scenes */
                foreach (var ste in _baseRom.Scenes)
                {
                    ste.SaveTableEntry();
                    ParseStoreHeaders(ste.GetSceneHeaders(), _baseRom.Data, (int)ste.GetSceneStartAddress());
                }

                /* Store entrance table entries */
                foreach (var ete in _baseRom.Entrances) ete.SaveTableEntry();

                /* Copy code data */
                Buffer.BlockCopy(_baseRom.CodeData, 0, _baseRom.Data, (int)_baseRom.Code.PStart, _baseRom.CodeData.Length);

                /* Write to file */
                var bw = new BinaryWriter(File.Open(_baseRom.Filename, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite));
                bw.Write(_baseRom.Data);
                bw.Close();
            }
        }

        private void ParseStoreHeaders(List<HeaderLoader> headers, byte[] databuf, int baseadr)
        {
            foreach (var hl in headers)
            {
                /* Fetch and parse room headers first */
                if (!individualFileMode)
                {
                    var rooms = (hl.Commands.FirstOrDefault(x => x.Command == CommandTypeIDs.Rooms) as Rooms);
                    if (rooms != null)
                    {
                        foreach (var ric in rooms.RoomInformation) ParseStoreHeaders(ric.Headers, databuf, (int)ric.Start);
                    }
                }

                /* Now store all storeable commands */
                foreach (IStoreable hc in hl.Commands.Where(x => x is IStoreable))
                    hc.Store(databuf, baseadr);
            }
        }

        private void ResetCurrentData(bool norefresh = false)
        {
            currentScene = null;
            currentRoom = null;
            currentRoomTriangle = null;
            currentRoomVertex = null;
            currentEnvSettings = null;

            if (!norefresh) RefreshCurrentData();
        }

        private void CreateStatusString()
        {
            var infostrs = new List<string>();

            if (currentScene != null)
            {
                if (currentRoom == null)
                {
                    infostrs.Add($"{currentScene.GetName()}");

                    var rooms = (currentScene.GetCurrentSceneHeader().Commands.FirstOrDefault(x => x.Command == CommandTypeIDs.Rooms) as Rooms);
                    if (rooms != null) infostrs.Add(
                        $"{rooms.RoomInformation.Count} room{(rooms.RoomInformation.Count != 1 ? "s" : "")}");
                }
                else if (currentRoom != null)
                {
                    infostrs.Add($"{currentScene.GetName()}, {currentRoom.Description}");
                }
            }
            else
            {
                infostrs.Add(
                    $"Ready{((Configuration.ShownIntelWarning || Configuration.ShownExtensionWarning) ? " (limited combiner)" : string.Empty)}");
                if (_baseRom != null && _baseRom.Scenes != null) infostrs.Add(
                    $"{_baseRom.Title} ({_baseRom.GameId}, v1.{_baseRom.Version}; {_baseRom.Scenes.Count} scenes)");
            }

            if (currentRoom != null && currentRoom.ActiveRoomActorData != null)
            {
                infostrs.Add(
                    $"{currentRoom.ActiveRoomActorData.ActorList.Count} room actor{(currentRoom.ActiveRoomActorData.ActorList.Count != 1 ? "s" : "")}");
            }

            if (currentScene != null && currentScene.GetActiveTransitionData() != null && currentRoom == null)
            {
                infostrs.Add(
                    $"{currentScene.GetActiveTransitionData().ActorList.Count} transition actor{(currentScene.GetActiveTransitionData().ActorList.Count != 1 ? "s" : "")}");
            }

            if (currentScene != null && currentScene.GetActiveSpawnPointData() != null && currentRoom == null)
            {
                infostrs.Add(
                    $"{currentScene.GetActiveSpawnPointData().ActorList.Count} spawn point{(currentScene.GetActiveSpawnPointData().ActorList.Count != 1 ? "s" : "")}");
            }

            if (currentRoom != null && currentRoom.ActiveObjects != null)
            {
                infostrs.Add(
                    $"{currentRoom.ActiveObjects.ObjectList.Count} object{(currentRoom.ActiveObjects.ObjectList.Count != 1 ? "s" : "")}");
            }

            if (currentScene != null && currentScene.GetActiveWaypoints() != null && currentRoom == null)
            {
                infostrs.Add(
                    $"{currentScene.GetActiveWaypoints().Paths.Count} path{(currentScene.GetActiveWaypoints().Paths.Count != 1 ? "s" : "")}");
            }

            Program.Status.Message = string.Join("; ", infostrs);
        }

        private void RefreshCurrentData()
        {
            CreateStatusString();

            if (currentScene != null)
            {
                if (!_baseRom.IsMajora)
                {
                    var steOcarina = (currentScene as SceneTableEntryOcarina);
                    editAreaTitleCardToolStripMenuItem.Enabled = (!_baseRom.IsMajora && steOcarina.LabelStartAddress != 0 && steOcarina.LabelEndAddress != 0);
                }

                var rooms = (currentScene.GetCurrentSceneHeader().Commands.FirstOrDefault(x => x.Command == CommandTypeIDs.Rooms) as Rooms);
                if (rooms != null)
                {
                    roomsForWaterboxSelection = new List<Option>();
                    roomsForWaterboxSelection.Add(new Option() { Description = "(All Rooms)", Value = 0x3F });
                    foreach (var ric in rooms.RoomInformation)
                        roomsForWaterboxSelection.Add(new Option() { Description = ric.Description, Value = ric.Number });
                }

                if (currentRoom == null)
                {
                    _baseRom.SegmentMapping.Remove((byte)0x02);
                    _baseRom.SegmentMapping.Add((byte)0x02, currentScene.GetData());

                    allMeshHeaders = new List<MeshHeader>();

                    if (rooms != null)
                    {
                        foreach (var hl in rooms.RoomInformation.SelectMany(x => x.Headers))
                            allMeshHeaders.Add(hl.Commands.FirstOrDefault(x => x.Command == CommandTypeIDs.MeshHeader) as MeshHeader);
                    }
                    allMeshHeaders = allMeshHeaders.Distinct().ToList();
                }
                else if (currentRoom != null)
                {
                    _baseRom.SegmentMapping.Remove((byte)0x02);
                    _baseRom.SegmentMapping.Remove((byte)0x03);
                    _baseRom.SegmentMapping.Add((byte)0x02, currentScene.GetData());
                    _baseRom.SegmentMapping.Add((byte)0x03, currentRoom.Data);
                }
            }
            else
            {
                editAreaTitleCardToolStripMenuItem.Enabled = false;
            }

            if (currentRoom != null && currentRoom.ActiveRoomActorData != null)
            {
                RefreshRoomActorList();
            }
            else
            {
                cbActors.Enabled = false;
                cbActors.DataSource = null;
            }

            if (currentScene != null && currentScene.GetActiveTransitionData() != null)
            {
                RefreshTransitionList();
            }
            else
            {
                cbTransitions.Enabled = false;
                cbTransitions.DataSource = null;
            }

            if (currentScene != null && currentScene.GetActiveSpawnPointData() != null)
            {
                RefreshSpawnPointList();
            }
            else
            {
                cbSpawnPoints.Enabled = false;
                cbSpawnPoints.DataSource = null;
            }

            if (currentScene != null && currentScene.GetActiveSpecialObjs() != null)
            {
                cbSpecialObjs.Enabled = true;
                cbSpecialObjs.DisplayMember = "Name";
                cbSpecialObjs.ValueMember = "ObjectNumber";
                cbSpecialObjs.DataSource = new BindingSource() { DataSource = SpecialObjects.Types };
                cbSpecialObjs.DataBindings.Clear();
                cbSpecialObjs.DataBindings.Add("SelectedValue", currentScene.GetActiveSpecialObjs(), "SelectedSpecialObjects");
            }
            else
            {
                cbSpecialObjs.Enabled = false;
                cbSpecialObjs.DataSource = null;
                cbSpecialObjs.DataBindings.Clear();
            }

            if (currentRoom != null && currentRoom.ActiveObjects != null)
            {
                dgvObjects.Enabled = true;
                dgvObjects.DataSource = new BindingSource() { DataSource = currentRoom.ActiveObjects.ObjectList };
                dgvObjects.Columns["Address"].Visible = false;
                dgvObjects.Columns["Number"].DefaultCellStyle.Format = "X4";
                //dgvObjects.Columns["Name"].ReadOnly = !ROM.HasFileNameTable;
                dgvObjects.Columns["Name"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            }
            else
            {
                dgvObjects.Enabled = false;
                dgvObjects.DataSource = null;
            }

            if (currentScene != null && currentScene.GetActiveWaypoints() != null)
            {
                RefreshWaypointPathList(currentScene.GetActiveWaypoints());
            }
            else
            {
                cbPathHeaders.Enabled = false;
                cbPathHeaders.DataSource = null;
            }

            if (currentScene != null && currentScene.GetActiveCollision() != null)
            {
                RefreshCollisionPolyAndTypeLists();
            }
            else
            {
                cbCollisionPolys.Enabled = cbCollisionPolyTypes.Enabled = txtColPolyRawData.Enabled = nudColPolyType.Enabled = cbColPolyGroundTypes.Enabled = false;
                cbCollisionPolys.DataSource = cbCollisionPolyTypes.DataSource = cbColPolyGroundTypes.DataSource = null;
                txtColPolyRawData.Text = string.Empty;
            }

            if (currentScene != null && currentScene.GetActiveCollision() != null && currentScene.GetActiveCollision().Waterboxes.Count > 0)
            {
                var wblist = new List<Collision.Waterbox>
                {
                    new Collision.Waterbox()
                };
                wblist.AddRange(currentScene.GetActiveCollision().Waterboxes);

                waterboxComboDataBinding = new BindingSource();
                waterboxComboDataBinding.DataSource = wblist;
                cbWaterboxes.DataSource = waterboxComboDataBinding;
                cbWaterboxes.DisplayMember = "Description";
                cbWaterboxes.Enabled = true;
            }
            else
            {
                cbWaterboxes.Enabled = tlpExWaterboxes.Visible = false;
                cbWaterboxes.DataSource = null;
            }

            RefreshPathWaypoints();

            if (currentScene != null && currentScene.GetActiveSettingsSoundScene() != null)
            {
                cbSceneMetaBGM.Enabled = true;
                cbSceneMetaBGM.ValueMember = "Key";
                cbSceneMetaBGM.DisplayMember = "Value";
                cbSceneMetaBGM.DataSource = new BindingSource() { DataSource = bgms.OrderBy(x => x.Key).ToList() };
                cbSceneMetaBGM.DataBindings.Clear();
                cbSceneMetaBGM.DataBindings.Add("SelectedValue", currentScene.GetActiveSettingsSoundScene(), "TrackID");
                nudSceneMetaReverb.Value = currentScene.GetActiveSettingsSoundScene().Reverb;
                nudSceneMetaNightSFX.Value = currentScene.GetActiveSettingsSoundScene().NightSfxId;
                nudSceneMetaReverb.Enabled = nudSceneMetaNightSFX.Enabled = true;
            }
            else
            {
                cbSceneMetaBGM.Enabled = false;
                cbSceneMetaBGM.DataBindings.Clear();
                cbSceneMetaBGM.SelectedItem = null;
                nudSceneMetaReverb.Value = nudSceneMetaNightSFX.Value = 0;
                nudSceneMetaReverb.Enabled = nudSceneMetaNightSFX.Enabled = false;
            }

            collisionDirty = true;
            waterboxesDirty = true;
        }

        private void RefreshWaypointPathList(Waypoints wp)
        {
            if (wp == null) return;

            var pathlist = new List<PathHeader> {new PathHeader()};
            pathlist.AddRange(wp.Paths);

            waypointPathComboDataBinding = new BindingSource {DataSource = pathlist};
            cbPathHeaders.DataSource = waypointPathComboDataBinding;
            cbPathHeaders.DisplayMember = "Description";
            cbPathHeaders.Enabled = true;
        }

        private void RefreshPathWaypoints()
        {
            if (activePathHeader != null && activePathHeader.Points != null)
            {
                dgvPathWaypoints.Enabled = true;
                dgvPathWaypoints.DataSource = new BindingSource() { DataSource = activePathHeader.Points };
                dgvPathWaypoints.ClearSelection();
                dgvPathWaypoints.Columns["Address"].Visible = false;
                dgvPathWaypoints.Columns["X"].AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
                dgvPathWaypoints.Columns["Y"].AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
                dgvPathWaypoints.Columns["Z"].AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            }
            else
            {
                dgvPathWaypoints.Enabled = false;
                dgvPathWaypoints.DataSource = null;
            }
        }

        private void dgvObjects_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            var dgv = (sender as DataGridView);

            if (dgv.Columns[e.ColumnIndex].Name == "Number")
            {
                if (e != null && e.Value != null && e.DesiredType.Equals(typeof(string)))
                {
                    try
                    {
                        e.Value = $"0x{e.Value:X4}";
                        e.FormattingApplied = true;
                    }
                    catch
                    {
                        /* Not hexadecimal */
                    }
                }
            }
        }

        private void dgvObjects_CellParsing(object sender, DataGridViewCellParsingEventArgs e)
        {
            var dgv = (sender as DataGridView);

            if (dgv.Columns[e.ColumnIndex].Name == "Number")
            {
                if (e != null && e.Value != null && e.DesiredType.Equals(typeof(ushort)))
                {
                    var str = (e.Value as string);
                    var ishex = str.StartsWith("0x");

                    ushort val = 0;
                    if (ushort.TryParse((ishex ? str.Substring(2) : str), (ishex ? System.Globalization.NumberStyles.AllowHexSpecifier : System.Globalization.NumberStyles.None),
                        System.Globalization.CultureInfo.InvariantCulture, out val))
                    {
                        e.Value = val;
                        e.ParsingApplied = true;
                    }
                }
            }
        }

        private void dgvObjects_EditingControlShowing(object sender, DataGridViewEditingControlShowingEventArgs e)
        {
            var dgv = (sender as DataGridView);

            var column = dgv.CurrentCell.ColumnIndex;
            var name = dgv.Columns[column].DataPropertyName;

            if (name.Equals("Name") && e.Control is TextBox)
            {
                var tb = e.Control as TextBox;
                tb.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
                tb.AutoCompleteCustomSource = _baseRom.ObjectNameAcStrings;
                tb.AutoCompleteSource = AutoCompleteSource.CustomSource;
            }
        }

        private void dgvObjects_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            if (e.Exception is FormatException) System.Media.SystemSounds.Hand.Play();
        }

        private void tvScenes_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (e.Node.Tag is BaseRomHandler)
                ResetCurrentData();
            if (e.Node.Tag is ISceneTableEntry)
            {
                if (currentScene != (e.Node.Tag as ISceneTableEntry))
                {
                    currentScene = (e.Node.Tag as ISceneTableEntry);
                    currentScene.SetCurrentSceneHeader(currentScene.GetSceneHeaders()[0]);
                    currentEnvSettings = currentScene.GetActiveEnvSettings().EnvSettingList.First();
                }
                currentRoom = null;
                currentRoomTriangle = null;
                currentRoomVertex = null;
            }
            else if (e.Node.Tag is HeaderPair)
            {
                var hp = (e.Node.Tag as HeaderPair);

                if (hp.SceneHeader.Parent != currentScene) currentScene = (hp.SceneHeader.Parent as ISceneTableEntry);
                currentScene.SetCurrentSceneHeader(hp.SceneHeader);
                currentEnvSettings = currentScene.GetActiveEnvSettings().EnvSettingList.First();

                currentRoom = null;
                currentRoomTriangle = null;
                currentRoomVertex = null;
            }
            else if (e.Node.Tag is RoomInfoClass)
            {
                var hp = (e.Node.Parent.Tag as HeaderPair);

                if (hp.SceneHeader.Parent != currentScene) currentScene = (hp.SceneHeader.Parent as ISceneTableEntry);
                currentScene.SetCurrentSceneHeader(hp.SceneHeader);
                currentEnvSettings = currentScene.GetActiveEnvSettings().EnvSettingList.First();

                currentRoom = (e.Node.Tag as RoomInfoClass);
                if (hp.SceneHeader.Number < currentRoom.Headers.Count)
                    currentRoom.CurrentRoomHeader = currentRoom.Headers[hp.SceneHeader.Number];

                currentRoomTriangle = null;
                currentRoomVertex = null;
            }

            RefreshCurrentData();
        }

        private void tvScenes_MouseUp(object sender, MouseEventArgs e)
        {
            var tree = (sender as Controls.TreeViewEx);

            if (e.Button == MouseButtons.Right)
            {
                var pt = new Point(e.X, e.Y);
                tree.PointToClient(pt);

                var Node = tree.GetNodeAt(pt);
                if (Node != null)
                {
                    if (Node.Bounds.Contains(pt))
                    {
                        tree.SelectedNode = Node;
                        cmsSceneTree.Show(tree, pt);
                    }
                }
            }
        }

        private void propertiesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // TODO  make more useful! print statistics about the object in question in the msgbox? like actor counts, etc?
            var tag = tvScenes.SelectedNode.Tag;

            if (tag is BaseRomHandler)
            {
                //meh
                rOMInformationToolStripMenuItem_Click(rOMInformationToolStripMenuItem, EventArgs.Empty);
            }
            else if (tag is ISceneTableEntry)
            {
                var ste = (tag as ISceneTableEntry);

                MessageBox.Show(
                    $"Filename: {ste.GetDMAFilename()}\nROM location: 0x{ste.GetSceneStartAddress():X} - 0x{ste.GetSceneEndAddress():X}\nScene headers: {ste.GetSceneHeaders().Count} headers",
                    "Scene Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else if (tag is HeaderPair)
            {
                var hp = (tag as HeaderPair);
                MessageBox.Show(
                    $"Stage: {hp.Description}\nScene header: #{hp.SceneHeader.Number} (0x{hp.SceneHeader.Offset:X})\n",
                    "Stage Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else if (tag is RoomInfoClass)
            {
                var ric = (tag as RoomInfoClass);
                MessageBox.Show(
                    $"Filename: {ric.DmaFilename}\nROM location: 0x{ric.Start:X} - 0x{ric.End:X}\nRoom headers: {ric.Headers.Count} headers",
                    "Room Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void StartupExtensionChecks()
        {
            // !!
            // !!TODO!! clean up mess below <.<
            // !!


            // TODO  check for actual function addresses instead of just extension support
            // ex.    bool hasActiveTexture = ((GraphicsContext.CurrentContext as IGraphicsContextInternal).GetAddress("glActiveTexture") != IntPtr.Zero);
            // might help with intel support? at least on more modern intel chipsets? dunno, whatever, might be something to do for the future

            // TEMP TEMP  removed intel check until next public version, want feedback

            /* Check for those damn Intel chips and their shitty drivers(?), then disable combiner emulation if found. I'm sick of bug reports I can't fix because Intel is dumb. */
            /*if (Initialization.VendorIsIntel)
            {
                if (!Configuration.ShownIntelWarning)
                {
                    DisableCombiner(true, false);

                    Configuration.ShownIntelWarning = true;

                    MessageBox.Show(
                        "Your graphics hardware has been detected as being Intel-based. Because of known problems with Intel hardware and proper OpenGL support, " +
                        "combiner emulation has been disabled and correct graphics rendering cannot be guaranteed.", "Intel Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            */
            /* With Intel out of the way, check if all necessary GL extensions etc. are supported */
            supportsCreateShader = Initialization.SupportsFunction("glCreateShader");
            supportsGenProgramsARB = Initialization.SupportsFunction("glGenProgramsARB");

            var extErrorMessages = new StringBuilder();
            var extMissAll = new List<string>();

            var extMissGeneral = Initialization.CheckForExtensions(requiredOglExtensionsGeneral);
            extMissAll.AddRange(extMissGeneral);
            if (extMissGeneral.Contains("GL_ARB_multisample"))
            {
                enableAntiAliasingToolStripMenuItem.Checked = Configuration.EnableAntiAliasing = false;
                enableAntiAliasingToolStripMenuItem.Enabled = false;
                extErrorMessages.AppendLine("Multisampling is not supported. Anti-aliasing support has been disabled.");
            }

            var extMissCombinerGeneral = Initialization.CheckForExtensions(requiredOglExtensionsCombinerGeneral);
            extMissAll.AddRange(extMissCombinerGeneral);
            if (extMissCombinerGeneral.Contains("GL_ARB_multitexture"))
            {
                DisableCombiner(true, true);
                extErrorMessages.AppendLine("Multitexturing is not supported. Combiner emulation has been disabled and correct graphics rendering cannot be guaranteed.");
            }
            else
            {
                var extMissARBCombiner = Initialization.CheckForExtensions(requiredOglExtensionsARBCombiner);
                extMissAll.AddRange(extMissARBCombiner);
                if (extMissARBCombiner.Count > 0 || !supportsGenProgramsARB)
                {
                    extErrorMessages.AppendLine("ARB Fragment Programs are not supported. ARB Assembly Combiner has been disabled.");
                }

                var extMissGLSLCombiner = Initialization.CheckForExtensions(requiredOglExtensionsGLSLCombiner);
                extMissAll.AddRange(extMissGLSLCombiner);
                if (extMissGLSLCombiner.Count > 0)
                {
                    extErrorMessages.AppendLine("OpenGL Shading Language is not supported. GLSL Combiner has been disabled.");
                }

                DisableCombiner((extMissARBCombiner.Count > 0 || !supportsGenProgramsARB), (extMissGLSLCombiner.Count > 0));
            }

            if (extMissAll.Count > 0 || !supportsGenProgramsARB)
            {
                if (!Configuration.ShownExtensionWarning)
                {
                    Configuration.ShownExtensionWarning = true;

                    var sb = new StringBuilder();

                    if (extMissAll.Count > 0)
                    {
                        sb.AppendFormat("The following OpenGL Extension{0} not supported by your hardware:\n", ((extMissAll.Count - 1) > 0 ? "s are" : " is"));
                        sb.AppendLine();
                        foreach (var str in extMissAll) sb.AppendFormat("* {0}\n", str);
                        sb.AppendLine();
                    }

                    if (!supportsGenProgramsARB)
                    {
                        //TODO make nicer, like exts above, not just bools?
                        sb.AppendFormat("The OpenGL function call glGenProgramARB is not supported by your hardware.");
                        sb.AppendLine();
                        sb.AppendLine();
                    }

                    sb.Append(extErrorMessages);

                    MessageBox.Show(sb.ToString(), "Extension Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        private void DisableCombiner(bool arb, bool glsl)
        {
            if ((arb && currentCombinerType == CombinerTypes.ArbCombiner) || (glsl && currentCombinerType == CombinerTypes.GLSLCombiner))
                currentCombinerType = CombinerTypes.None;

            foreach (ToolStripMenuItem tsmi in combinerTypeToolStripMenuItem.DropDownItems)
            {
                if (tsmi.Tag is CombinerTypes &&
                    ((((CombinerTypes)tsmi.Tag) == CombinerTypes.ArbCombiner && arb) ||
                    (((CombinerTypes)tsmi.Tag) == CombinerTypes.GLSLCombiner && glsl)))
                {
                    tsmi.Enabled = false;
                    tsmi.Checked = false;
                }
            }
        }

        private void customGLControl_Load(object sender, EventArgs e)
        {
            SettingsGUIInit();

            StartupExtensionChecks();

            Initialization.SetDefaults();

            glText = new TextPrinter(new Font("Verdana", 9.0f, FontStyle.Bold));
            camera = new Camera();
            fpsMonitor = new FPSMonitor();

            oglSceneScale = 0.02;

            ready = true;
        }

        private void customGLControl_Paint(object sender, PaintEventArgs e)
        {
            if (!ready) return;

            try
            {
                fpsMonitor.Update();

                RenderInit(((GLControl)sender).ClientRectangle, Color.LightBlue);

                if (_baseRom != null && _baseRom.Loaded)
                {
                    /* Scene/rooms */
                    RenderScene();

                    /* Prepare for actors */
                    GL.PushAttrib(AttribMask.AllAttribBits);
                    GL.Disable(EnableCap.Texture2D);
                    GL.Disable(EnableCap.Lighting);
                    if (supportsGenProgramsARB) GL.Disable((EnableCap)All.FragmentProgram);
                    if (supportsCreateShader) GL.UseProgram(0);
                    {
                        /* Room actors */
                        if (Configuration.RenderRoomActors && currentRoom != null && currentRoom.ActiveRoomActorData != null)
                            foreach (var ac in currentRoom.ActiveRoomActorData.ActorList)
                                ac.Render(ac == (cbActors.SelectedItem as Actors.Entry) &&
                                    cbActors.Visible ? PickableObjectRenderType.Selected : PickableObjectRenderType.Normal);

                        /* Spawn points */
                        if (Configuration.RenderSpawnPoints && currentScene != null && currentScene.GetActiveSpawnPointData() != null)
                            foreach (var ac in currentScene.GetActiveSpawnPointData().ActorList)
                                ac.Render(ac == (cbSpawnPoints.SelectedItem as Actors.Entry) &&
                                    cbSpawnPoints.Visible ? PickableObjectRenderType.Selected : PickableObjectRenderType.Normal);

                        /* Transitions */
                        if (Configuration.RenderTransitions && currentScene != null && currentScene.GetActiveTransitionData() != null)
                            foreach (var ac in currentScene.GetActiveTransitionData().ActorList)
                                ac.Render(ac == (cbTransitions.SelectedItem as Actors.Entry) &&
                                    cbTransitions.Visible ? PickableObjectRenderType.Selected : PickableObjectRenderType.Normal);

                        /* Path waypoints */
                        if (Configuration.RenderPathWaypoints && activePathHeader != null && activePathHeader.Points != null)
                        {
                            /* Link waypoints? */
                            if (Configuration.LinkAllWPinPath)
                            {
                                GL.LineWidth(4.0f);
                                GL.Color3(0.25, 0.5, 1.0);

                                GL.Begin(PrimitiveType.LineStrip);
                                foreach (var wp in activePathHeader.Points) GL.Vertex3(wp.X, wp.Y, wp.Z);
                                GL.End();
                            }

                            var selwp = (dgvPathWaypoints.SelectedCells.Count != 0 ? dgvPathWaypoints.SelectedCells[0].OwningRow.DataBoundItem as Waypoint : null);
                            foreach (var wp in activePathHeader.Points)
                                wp.Render(wp == selwp && cbPathHeaders.Visible ? PickableObjectRenderType.Selected : PickableObjectRenderType.Normal);
                        }
                    }
                    GL.PopAttrib();

                    /* Collision */
                    if (Configuration.RenderCollision && currentScene != null && currentScene.GetActiveCollision() != null)
                    {
                        if (!collisionDirty && collisionDL != null)
                        {
                            collisionDL.Render();
                        }
                        else
                        {
                            collisionDirty = false;

                            if (collisionDL != null) collisionDL.Dispose();
                            collisionDL = new DisplayList(ListMode.CompileAndExecute);

                            GL.PushAttrib(AttribMask.AllAttribBits);
                            GL.Disable(EnableCap.Texture2D);
                            GL.Disable(EnableCap.Lighting);
                            if (supportsGenProgramsARB) GL.Disable((EnableCap)All.FragmentProgram);
                            if (supportsCreateShader) GL.UseProgram(0);
                            GL.DepthRange(0.0, 0.99999);

                            if (Configuration.RenderCollisionAsWhite) GL.Color4(1.0, 1.0, 1.0, 0.5);

                            GL.Begin(PrimitiveType.Triangles);
                            foreach (var poly in currentScene.GetActiveCollision().Polygons)
                            {
                                if (poly == currentCollisionPolygon && cbCollisionPolys.Visible)
                                {
                                    GL.Color4(0.5, 0.5, 1.0, 0.5);
                                    poly.Render(PickableObjectRenderType.NoColor);
                                    if (Configuration.RenderCollisionAsWhite) GL.Color4(1.0, 1.0, 1.0, 0.5);
                                }
                                else
                                {
                                    if (Configuration.RenderCollisionAsWhite)
                                        poly.Render(PickableObjectRenderType.NoColor);
                                    else
                                        poly.Render(PickableObjectRenderType.Normal);
                                }
                            }
                            GL.End();

                            GL.DepthRange(0.0, 0.99998);
                            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);
                            GL.LineWidth(2.0f);
                            GL.Color3(Color.Black);
                            GL.Begin(PrimitiveType.Triangles);
                            foreach (var poly in currentScene.GetActiveCollision().Polygons) poly.Render(PickableObjectRenderType.NoColor);
                            GL.End();

                            GL.PopAttrib();

                            collisionDL.End();
                        }
                    }

                    /* Waterboxes */
                    if (Configuration.RenderWaterboxes && currentScene != null && currentScene.GetActiveCollision() != null)
                    {
                        if (!waterboxesDirty && waterboxDL != null)
                        {
                            waterboxDL.Render();
                        }
                        else
                        {
                            waterboxesDirty = false;

                            if (waterboxDL != null) waterboxDL.Dispose();
                            waterboxDL = new DisplayList(ListMode.CompileAndExecute);

                            GL.PushAttrib(AttribMask.AllAttribBits);
                            GL.Disable(EnableCap.Texture2D);
                            GL.Disable(EnableCap.Lighting);
                            if (supportsGenProgramsARB) GL.Disable((EnableCap)All.FragmentProgram);
                            if (supportsCreateShader) GL.UseProgram(0);
                            GL.Disable(EnableCap.CullFace);

                            GL.Begin(PrimitiveType.Quads);
                            foreach (var wb in currentScene.GetActiveCollision().Waterboxes)
                            {
                                var alpha = ((Configuration.ShowWaterboxesPerRoom && currentRoom != null && (wb.RoomNumber != currentRoom.Number && wb.RoomNumber != 0x3F)) ? 0.1 : 0.5);

                                if (wb == currentWaterbox && cbWaterboxes.Visible)
                                    GL.Color4(0.5, 1.0, 0.5, alpha);
                                else
                                    GL.Color4(0.0, 0.5, 1.0, alpha);

                                wb.Render(PickableObjectRenderType.Normal);
                            }
                            GL.End();

                            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);
                            GL.LineWidth(2.0f);
                            GL.Begin(PrimitiveType.Quads);
                            foreach (var wb in currentScene.GetActiveCollision().Waterboxes)
                            {
                                var alpha = ((Configuration.ShowWaterboxesPerRoom && currentRoom != null && (wb.RoomNumber != currentRoom.Number && wb.RoomNumber != 0x3F)) ? 0.1 : 0.5);
                                GL.Color4(0.0, 0.0, 0.0, alpha);
                                wb.Render(PickableObjectRenderType.Normal);
                            }
                            GL.End();

                            GL.Enable(EnableCap.CullFace);
                            GL.PopAttrib();

                            GL.Color4(Color.White);

                            waterboxDL.End();
                        }
                    }

                    /* Render selected room triangle overlay */
                    if (currentRoomTriangle != null && !Configuration.RenderCollision)
                    {
                        currentRoomTriangle.Render(PickableObjectRenderType.Normal);
                    }

                    /* 2D text overlay */
                    RenderTextOverlay();
                }

                ((GLControl)sender).SwapBuffers();
            }
            catch (EntryPointNotFoundException)
            {
                //
            }
        }

        private void RenderInit(Rectangle rect, Color clearColor)
        {
            GL.ClearColor(clearColor);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            Initialization.CreateViewportAndProjection(Initialization.ProjectionTypes.Perspective, rect, 0.001f, (currentEnvSettings != null ? (currentEnvSettings.DrawDistance / 50.0f) : 300.0f));
            camera.Position();
            GL.Scale(oglSceneScale, oglSceneScale, oglSceneScale);
        }

        private void RenderScene()
        {
            GL.PushAttrib(AttribMask.AllAttribBits);

            if (currentScene != null && currentEnvSettings != null) currentEnvSettings.CreateLighting();

            if (currentRoom != null && currentRoom.ActiveMeshHeader != null)
            {
                /* Render single room */
                RenderMeshHeader(currentRoom.ActiveMeshHeader);
                displayListsDirty = false;
            }
            else if (currentScene != null && currentScene.GetCurrentSceneHeader() != null)
            {
                /* Render all rooms */
                foreach (var ric in
                    (currentScene.GetCurrentSceneHeader().Commands.FirstOrDefault(x => x.Command == CommandTypeIDs.Rooms) as Rooms).RoomInformation)
                {
                    _baseRom.SegmentMapping.Remove((byte)0x02);
                    _baseRom.SegmentMapping.Remove((byte)0x03);
                    _baseRom.SegmentMapping.Add((byte)0x02, (ric.Parent as ISceneTableEntry).GetData());
                    _baseRom.SegmentMapping.Add((byte)0x03, ric.Data);

                    if (ric.Headers.Count == 0) continue;

                    var mh = (ric.Headers[0].Commands.FirstOrDefault(x => x.Command == CommandTypeIDs.MeshHeader) as MeshHeader);
                    if (mh == null) continue;

                    RenderMeshHeader(mh);
                }
                displayListsDirty = false;
            }

            GL.PopAttrib();
        }

        private void RenderMeshHeader(MeshHeader mh)
        {
            if (mh.DLs == null || displayListsDirty || mh.CachedWithTextures != Configuration.RenderTextures || mh.CachedWithCombinerType != Configuration.CombinerType)
            {
                /* Display lists aren't yet cached OR cached DLs are wrong */
                if (mh.DLs != null)
                {
                    foreach (DisplayList gldl in mh.DLs) gldl.Dispose();
                    mh.DLs.Clear();
                }

                mh.CreateDisplayLists(Configuration.RenderTextures, Configuration.CombinerType);
                RefreshCurrentData();
            }

            /* Render DLs */
            foreach (DisplayList gldl in mh.DLs) gldl.Render();

            /* Bounds test */
            /*GL.PushAttrib(AttribMask.AllAttribBits);
            GL.UseProgram(0);
            GL.Disable(EnableCap.Lighting);
            GL.Disable(EnableCap.Fog);
            if (supportsGenProgramsARB) GL.Disable((EnableCap)All.FragmentProgram);
            if (supportsCreateShader) GL.UseProgram(0);
            for (int i = 0; i < mh.MinClipBounds.Count; i++)
            {
                GL.Color4(Color.FromArgb((mh.GetHashCode() & 0xFFFFFF) | (0xFF << 24)));
                GL.Begin(PrimitiveType.Lines);
                GL.Vertex3(mh.MinClipBounds[i]);
                GL.Vertex3(mh.MaxClipBounds[i]);
                GL.End();
                //OpenGLHelpers.MiscDrawingHelpers.DrawBox(mh.MinClipBounds[i], mh.MaxClipBounds[i]);
            }
            GL.PopAttrib();*/
        }

        private void RenderTextOverlay()
        {
            glText.Begin(customGLControl);
            if (!Configuration.OglvSync) glText.Print(string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:0.00} FPS", fpsMonitor.Value), new Vector2d(10.0, 10.0), Color.FromArgb(128, Color.Black));
            glText.Flush();
        }

        private IPickableObject TryPickObject(int x, int y, bool moveable)
        {
            if (currentScene == null) return null;

            IPickableObject picked = null;
            var pickobjs = new List<IPickableObject>();

            /* Room model triangle vertices */
            if (!Configuration.RenderCollision && currentRoomTriangle != null)
                pickobjs.AddRange(currentRoomTriangle.Vertices);

            /* Room model triangles */
            if (currentRoom != null && currentRoom.ActiveMeshHeader != null && !Configuration.RenderCollision &&
                currentRoom.ActiveMeshHeader.DLs.Count > 0 && currentRoom.ActiveMeshHeader.DLs[0].Triangles.Count > 0)
            {
                if (currentRoom.ActiveMeshHeader.DLs[0].Triangles[0].IsMoveable == moveable)
                {
                    foreach (var dlex in currentRoom.ActiveMeshHeader.DLs)
                        pickobjs.AddRange(dlex.Triangles);
                }
            }

            /* Rooms */
            if (allMeshHeaders != null && currentRoom == null && !Configuration.RenderCollision && allMeshHeaders.Count > 0 && allMeshHeaders[0].IsMoveable == moveable)
                pickobjs.AddRange(allMeshHeaders);

            /* Room actors */
            if (currentRoom != null && currentRoom.ActiveRoomActorData != null && Configuration.RenderRoomActors && currentRoom.ActiveRoomActorData.ActorList.Count > 0 &&
                currentRoom.ActiveRoomActorData.ActorList[0].IsMoveable == moveable)
                pickobjs.AddRange(currentRoom.ActiveRoomActorData.ActorList);

            /* Spawn points */
            if (currentScene.GetActiveSpawnPointData() != null && Configuration.RenderSpawnPoints && currentScene.GetActiveSpawnPointData().ActorList.Count > 0 &&
                currentScene.GetActiveSpawnPointData().ActorList[0].IsMoveable == moveable)
                pickobjs.AddRange(currentScene.GetActiveSpawnPointData().ActorList);

            /* Transition actors */
            if (currentScene.GetActiveTransitionData() != null && Configuration.RenderTransitions && currentScene.GetActiveTransitionData().ActorList.Count > 0 &&
                currentScene.GetActiveTransitionData().ActorList[0].IsMoveable == moveable)
                pickobjs.AddRange(currentScene.GetActiveTransitionData().ActorList);

            /* Waypoints */
            if (activePathHeader != null && activePathHeader.Points != null && Configuration.RenderPathWaypoints && activePathHeader.Points.Count > 0 &&
                activePathHeader.Points[0].IsMoveable == moveable)
                pickobjs.AddRange(activePathHeader.Points);

            /* Waterboxes */
            if (currentScene.GetActiveCollision() != null && Configuration.RenderWaterboxes && currentScene.GetActiveCollision().Waterboxes.Count > 0 &&
                currentScene.GetActiveCollision().Waterboxes[0].IsMoveable == moveable)
                pickobjs.AddRange(currentScene.GetActiveCollision().Waterboxes);

            /* Collision polygons */
            if (currentScene.GetActiveCollision() != null && Configuration.RenderCollision && currentScene.GetActiveCollision().Polygons.Count > 0 &&
                currentScene.GetActiveCollision().Polygons[0].IsMoveable == moveable)
                pickobjs.AddRange(currentScene.GetActiveCollision().Polygons);

            if ((picked = DoPicking(x, y, pickobjs)) != null)
            {
                /* Wrong mode? */
                if (picked.IsMoveable != moveable) return null;

                /* What's been picked...? */
                if (picked is Waypoint)
                {
                    dgvPathWaypoints.ClearSelection();
                    var row = dgvPathWaypoints.Rows.OfType<DataGridViewRow>().FirstOrDefault(xx => xx.DataBoundItem == picked as Waypoint);
                    if (row == null) return null;
                    row.Cells["X"].Selected = true;
                    tabControl1.SelectTab(tpWaypoints);
                }
                else if (picked is Actors.Entry)
                {
                    var actor = (picked as Actors.Entry);

                    if (actor.IsSpawnPoint)
                    {
                        cbSpawnPoints.SelectedItem = actor;
                        tabControl1.SelectTab(tpSpawnPoints);
                    }
                    else if (actor.IsTransitionActor)
                    {
                        cbTransitions.SelectedItem = actor;
                        tabControl1.SelectTab(tpTransitions);
                    }
                    else
                    {
                        cbActors.SelectedItem = actor;
                        tabControl1.SelectTab(tpRoomActors);
                    }
                }
                else if (picked is Collision.Polygon)
                {
                    currentCollisionPolygon = (picked as Collision.Polygon);

                    cbCollisionPolys.SelectedItem = currentCollisionPolygon;
                    tabControl1.SelectTab(tpCollision);
                }
                else if (picked is Collision.Waterbox)
                {
                    currentWaterbox = (picked as Collision.Waterbox);

                    cbWaterboxes.SelectedItem = currentWaterbox;
                    tabControl1.SelectTab(tpWaterboxes);
                }
                else if (picked is MeshHeader)
                {
                    tvScenes.SelectedNode = tvScenes.FlattenTree().FirstOrDefault(xx =>
                        xx.Tag == ((picked as MeshHeader).Parent as RoomInfoClass) &&
                        (xx.Parent.Tag as HeaderPair).SceneHeader.Number == currentScene.GetCurrentSceneHeader().Number);
                }
                else if (picked is DisplayListEx.Triangle)
                {
                    if (currentRoomTriangle != picked)
                    {
                        if (currentRoomTriangle != null) currentRoomTriangle.SelectedVertex = null;

                        currentRoomTriangle = (picked as DisplayListEx.Triangle);
                        currentRoomVertex = null;
                    }
                }
                else if (picked is SimpleF3DEX2.Vertex)
                {
                    currentRoomTriangle.SelectedVertex = currentRoomVertex = (picked as SimpleF3DEX2.Vertex);
                }
            }

            return picked;
        }

        private IPickableObject DoPicking(int x, int y, List<IPickableObject> objlist)
        {
            /* It's MAGIC! I fucking hate picking and shit. */
            GL.PushAttrib(AttribMask.AllAttribBits);
            GL.Disable(EnableCap.Texture2D);
            GL.Disable(EnableCap.Lighting);
            GL.Disable(EnableCap.Fog);
            GL.Enable(EnableCap.Blend);
            if (supportsGenProgramsARB) GL.Disable((EnableCap)All.FragmentProgram);
            if (supportsCreateShader) GL.UseProgram(0);
            GL.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);

            RenderInit(customGLControl.ClientRectangle, Color.Black);
            foreach (var obj in objlist)
            {
                if (obj is Collision.Polygon || obj is DisplayListEx.Triangle)
                    GL.Enable(EnableCap.CullFace);
                else
                    GL.Disable(EnableCap.CullFace);

                obj.Render(PickableObjectRenderType.Picking);
            }

            GL.PopAttrib();

            var pixel = new byte[4];
            var viewport = new int[4];

            GL.GetInteger(GetPName.Viewport, viewport);
            GL.ReadPixels(x, viewport[3] - y, 1, 1, PixelFormat.Rgba, PixelType.UnsignedByte, pixel);
            var argb = (uint)((pixel[3] << 24) | (pixel[0] << 16) | (pixel[1] << 8) | pixel[2]);

            return objlist.FirstOrDefault(xx => (xx.PickColor.ToArgb() & 0xFFFFFF) == (int)(argb & 0xFFFFFF));
        }

        private void customGLControl_MouseDown(object sender, MouseEventArgs e)
        {
            camera.ButtonsDown |= e.Button;

            switch (currentToolMode)
            {
                case ToolModes.Camera:
                    {
                        /* Camera only */
                        if (Convert.ToBoolean(camera.ButtonsDown & MouseButtons.Left))
                            camera.MouseCenter(new Vector2d(e.X, e.Y));
                        break;
                    }

                case ToolModes.MoveableObjs:
                case ToolModes.StaticObjs:
                    {
                        /* Object picking */
                        if (Convert.ToBoolean(camera.ButtonsDown & MouseButtons.Left) || Convert.ToBoolean(camera.ButtonsDown & MouseButtons.Middle))
                        {
                            pickedObject = TryPickObject(e.X, e.Y, (currentToolMode == ToolModes.MoveableObjs));
                            if (pickedObject == null)
                            {
                                /* No pick? Camera */
                                camera.MouseCenter(new Vector2d(e.X, e.Y));
                            }
                            else
                            {
                                /* Object found */
                                pickObjLastPosition = pickObjPosition = new Vector2d(e.X, e.Y);
                                pickObjDisplacement = Vector2d.Zero;
                                ((Control)sender).Focus();

                                /* Mark GLDLs as dirty? */
                                collisionDirty = (pickedObject is Collision.Polygon);
                                waterboxesDirty = (pickedObject is Collision.Waterbox);

                                /* Static object? Camera */
                                if (currentToolMode == ToolModes.StaticObjs)
                                {
                                    camera.MouseCenter(new Vector2d(e.X, e.Y));
                                    /*if (e.Clicks == 2 && currentRoomVertex != null)
                                    {
                                        EditVertexColor(currentRoomVertex);
                                    }*/
                                }
                            }
                        }
                        else if (Convert.ToBoolean(camera.ButtonsDown & MouseButtons.Right))
                        {
                            pickedObject = TryPickObject(e.X, e.Y, (currentToolMode == ToolModes.MoveableObjs));
                            if (pickedObject != null)
                            {
                                if (currentToolMode == ToolModes.MoveableObjs)
                                {
                                    if (pickedObject is Actors.Entry)
                                    {
                                        var ac = (pickedObject as Actors.Entry);
                                        /* Determine what menu entries should be enabled */
                                        xAxisToolStripMenuItem.Enabled = !(ac.Definition.Items.FirstOrDefault(x => x.Usage == Usages.RotationX) == null);
                                        yAxisToolStripMenuItem.Enabled = !(ac.Definition.Items.FirstOrDefault(x => x.Usage == Usages.RotationY) == null);
                                        zAxisToolStripMenuItem.Enabled = !(ac.Definition.Items.FirstOrDefault(x => x.Usage == Usages.RotationZ) == null);
                                        rotateToolStripMenuItem.Enabled = (xAxisToolStripMenuItem.Enabled || yAxisToolStripMenuItem.Enabled || zAxisToolStripMenuItem.Enabled);
                                    }
                                    else
                                        rotateToolStripMenuItem.Enabled = false;

                                    cmsMoveableObjectEdit.Show(((Control)sender).PointToScreen(e.Location));
                                }
                                else if (currentToolMode == ToolModes.StaticObjs)
                                {
                                    if (pickedObject is SimpleF3DEX2.Vertex)
                                    {
                                        cmsVertexEdit.Show(((Control)sender).PointToScreen(e.Location));
                                    }
                                }
                            }
                        }
                        break;
                    }
            }
        }

        private void customGLControl_MouseUp(object sender, MouseEventArgs e)
        {
            camera.ButtonsDown &= ~e.Button;
        }

        private void customGLControl_MouseMove(object sender, MouseEventArgs e)
        {
            switch (currentToolMode)
            {
                case ToolModes.Camera:
                    {
                        if (Convert.ToBoolean(e.Button & MouseButtons.Left))
                            camera.MouseMove(new Vector2d(e.X, e.Y));
                        break;
                    }

                case ToolModes.MoveableObjs:
                    {
                        if (!Convert.ToBoolean(e.Button & MouseButtons.Left) && !Convert.ToBoolean(e.Button & MouseButtons.Middle)) break;

                        if (pickedObject == null)
                            camera.MouseMove(new Vector2d(e.X, e.Y));
                        else
                        {
                            // TODO  make this not shitty; try to get the "new method" to work with anything that's not at (0,0,0)

                            /* Speed modifiers */
                            var movemod = 3.0;
                            if (keysDown[(ushort)Keys.Space]) movemod = 8.0;
                            else if (keysDown[(ushort)Keys.ShiftKey]) movemod = 1.0;

                            /* Determine mouse position and displacement */
                            pickObjPosition = new Vector2d(e.X, e.Y);
                            pickObjDisplacement = ((pickObjPosition - pickObjLastPosition) * movemod);

                            /* No displacement? Exit */
                            if (pickObjDisplacement == Vector2d.Zero) return;

                            /* Calculate camera rotation */
                            var CamXRotd = camera.Rot.X * (Math.PI / 180);
                            var CamYRotd = camera.Rot.Y * (Math.PI / 180);

                            /* WARNING: Cam position stuff below is "I dunno why it works, but it does!" */
                            var objpos = pickedObject.Position;

                            if (Convert.ToBoolean(e.Button & MouseButtons.Middle) || (Convert.ToBoolean(e.Button & MouseButtons.Left) && keysDown[(ushort)Keys.ControlKey]))
                            {
                                /* Middle mouse button OR left button + Ctrl -> move forward/backward */
                                objpos.X += ((Math.Sin(CamYRotd) * -pickObjDisplacement.Y));
                                objpos.Z -= ((Math.Cos(CamYRotd) * -pickObjDisplacement.Y));

                                camera.Pos.X -= ((Math.Sin(CamYRotd) * (-pickObjDisplacement.Y * camera.CameraCoeff * camera.Sensitivity) / 1.25));
                                camera.Pos.Z += ((Math.Cos(CamYRotd) * (-pickObjDisplacement.Y * camera.CameraCoeff * camera.Sensitivity) / 1.25));
                            }
                            else if (Convert.ToBoolean(e.Button & MouseButtons.Left))
                            {
                                /* Left mouse button -> move up/down/left/right */
                                objpos.X += ((Math.Cos(CamYRotd) * pickObjDisplacement.X));
                                objpos.Y -= (pickObjDisplacement.Y);
                                objpos.Z += ((Math.Sin(CamYRotd) * pickObjDisplacement.X));

                                camera.Pos.X -= ((Math.Cos(CamYRotd) * pickObjDisplacement.X)) * 0.02;
                                camera.Pos.Y += (pickObjDisplacement.Y) * 0.02;
                                camera.Pos.Z -= ((Math.Sin(CamYRotd) * pickObjDisplacement.X)) * 0.02;
                            }

                            /* Round away decimal places (mainly for waypoints) */
                            objpos.X = Math.Round(objpos.X, 0);
                            objpos.Y = Math.Round(objpos.Y, 0);
                            objpos.Z = Math.Round(objpos.Z, 0);
                            pickedObject.Position = objpos;

                            /* Refresh GUI according to type of picked object */
                            if (pickedObject is Waypoint)
                            {
                                foreach (DataGridViewCell cell in dgvPathWaypoints.SelectedCells)
                                {
                                    for (var i = 0; i < dgvPathWaypoints.ColumnCount; i++) dgvPathWaypoints.UpdateCellValue(i, cell.RowIndex);
                                }
                            }
                            else if (pickedObject is Actors.Entry)
                            {
                                var actor = (pickedObject as Actors.Entry);

                                if (actor.IsSpawnPoint)
                                    XmlActorDefinitionReader.RefreshActorPositionRotation(actor, tlpExSpawnPoints);
                                else if (actor.IsTransitionActor)
                                    XmlActorDefinitionReader.RefreshActorPositionRotation(actor, tlpExTransitions);
                                else
                                    XmlActorDefinitionReader.RefreshActorPositionRotation(actor, tlpExRoomActors);
                            }
                            else if (pickedObject is Collision.Waterbox)
                            {
                                waterboxesDirty = true;
                                RefreshWaterboxControls();
                            }

                            pickObjLastPosition = pickObjPosition;

                            ((Control)sender).Focus();
                        }
                        break;
                    }

                case ToolModes.StaticObjs:
                    {
                        if (Convert.ToBoolean(e.Button & MouseButtons.Left)/* && PickedObject == null*/)
                            camera.MouseMove(new Vector2d(e.X, e.Y));
                        break;
                    }
            }
        }

        private void customGLControl_KeyDown(object sender, KeyEventArgs e)
        {
            keysDown[(ushort)e.KeyValue] = true;
        }

        private void customGLControl_KeyUp(object sender, KeyEventArgs e)
        {
            keysDown[(ushort)e.KeyValue] = false;
        }

        private void customGLControl_Leave(object sender, EventArgs e)
        {
            keysDown.Fill(new bool[] { false });
        }

        private void EditVertexColor(SimpleF3DEX2.Vertex vertex)
        {
            var cdlg = new ColorPickerDialog(Color.FromArgb(vertex.Colors[3], vertex.Colors[0], vertex.Colors[1], vertex.Colors[2]));

            if (cdlg.ShowDialog() == DialogResult.OK)
            {
                vertex.Colors[0] = cdlg.Color.R;
                vertex.Colors[1] = cdlg.Color.G;
                vertex.Colors[2] = cdlg.Color.B;
                vertex.Colors[3] = cdlg.Color.A;

                // KLUDGE! Write to local room data HERE for rendering, write to ROM in SimpleF3DEX2.Vertex, the vertex.Store(...) below
                currentRoom.Data[(vertex.Address & 0xFFFFFF) + 12] = vertex.Colors[0];
                currentRoom.Data[(vertex.Address & 0xFFFFFF) + 13] = vertex.Colors[1];
                currentRoom.Data[(vertex.Address & 0xFFFFFF) + 14] = vertex.Colors[2];
                currentRoom.Data[(vertex.Address & 0xFFFFFF) + 15] = vertex.Colors[3];

                vertex.Store(individualFileMode ? null : _baseRom.Data, (int)currentRoom.Start);

                displayListsDirty = true;
            }
        }

        private void tabControl1_Selecting(object sender, TabControlCancelEventArgs e)
        {
            collisionDirty = (e.Action == TabControlAction.Selecting && e.TabPage == tpCollision || lastTabPage == tpCollision);
            waterboxesDirty = (e.Action == TabControlAction.Selecting && e.TabPage == tpWaterboxes || lastTabPage == tpWaterboxes);

            lastTabPage = e.TabPage;
        }

        private void nudSceneMetaReverb_ValueChanged(object sender, EventArgs e)
        {
            if (currentScene != null && currentScene.GetActiveSettingsSoundScene() != null) currentScene.GetActiveSettingsSoundScene().Reverb = (byte)((NumericUpDown)sender).Value;
        }

        private void nudSceneMetaNightSFX_ValueChanged(object sender, EventArgs e)
        {
            if (currentScene != null && currentScene.GetActiveSettingsSoundScene() != null) currentScene.GetActiveSettingsSoundScene().NightSfxId = (byte)((NumericUpDown)sender).Value;
        }

        private void RefreshRoomActorList()
        {
            var actorlist = new List<Actors.Entry>();
            actorlist.Add(new Actors.Entry());
            actorlist.AddRange(currentRoom.ActiveRoomActorData.ActorList);

            roomActorComboBinding = new BindingSource();
            roomActorComboBinding.DataSource = actorlist;
            cbActors.DataSource = roomActorComboBinding;
            cbActors.DisplayMember = "Description";
            cbActors.Enabled = true;
        }

        private void RefreshTransitionList()
        {
            var actorlist = new List<Actors.Entry>();
            actorlist.Add(new Actors.Entry());
            actorlist.AddRange(currentScene.GetActiveTransitionData().ActorList);

            transitionComboBinding = new BindingSource();
            transitionComboBinding.DataSource = actorlist;
            cbTransitions.DataSource = transitionComboBinding;
            cbTransitions.DisplayMember = "Description";
            cbTransitions.Enabled = true;
        }

        private void RefreshSpawnPointList()
        {
            var actorlist = new List<Actors.Entry>();
            actorlist.Add(new Actors.Entry());
            actorlist.AddRange(currentScene.GetActiveSpawnPointData().ActorList);

            spawnPointComboBinding = new BindingSource();
            spawnPointComboBinding.DataSource = actorlist;
            cbSpawnPoints.DataSource = spawnPointComboBinding;
            cbSpawnPoints.DisplayMember = "Description";
            cbSpawnPoints.Enabled = true;
        }

        private void cbActors_SelectedIndexChanged(object sender, EventArgs e)
        {
            var ac = ((ComboBox)sender).SelectedItem as Actors.Entry;
            pickedObject = (ac as IPickableObject);

            XmlActorDefinitionReader.CreateActorEditingControls(ac, tlpExRoomActors, () =>
            {
                var idx = ((ComboBox)sender).SelectedIndex;
                RefreshRoomActorList();
                ((ComboBox)sender).SelectedIndex = idx;
                SelectActorNumberControl(tlpExRoomActors);
            }, individual: individualFileMode);
        }

        private void cbTransitions_SelectedIndexChanged(object sender, EventArgs e)
        {
            var ac = ((ComboBox)sender).SelectedItem as Actors.Entry;
            pickedObject = (ac as IPickableObject);

            Rooms rooms = null;
            if (currentScene != null && currentScene.GetCurrentSceneHeader() != null)
                rooms = currentScene.GetCurrentSceneHeader().Commands.FirstOrDefault(x => x.Command == CommandTypeIDs.Rooms) as Rooms;

            XmlActorDefinitionReader.CreateActorEditingControls(ac, tlpExTransitions, () =>
            {
                var idx = ((ComboBox)sender).SelectedIndex;
                RefreshTransitionList();
                ((ComboBox)sender).SelectedIndex = idx;
                SelectActorNumberControl(tlpExTransitions);
            }, (rooms != null ? rooms.RoomInformation : null), individualFileMode);
        }

        private void cbSpawnPoints_SelectedIndexChanged(object sender, EventArgs e)
        {
            var ac = ((ComboBox)sender).SelectedItem as Actors.Entry;
            pickedObject = (ac as IPickableObject);

            XmlActorDefinitionReader.CreateActorEditingControls(ac, tlpExSpawnPoints, () =>
            {
                var idx = ((ComboBox)sender).SelectedIndex;
                RefreshSpawnPointList();
                ((ComboBox)sender).SelectedIndex = idx;
                SelectActorNumberControl(tlpExSpawnPoints);
            }, individual: individualFileMode);
        }

        private void SelectActorNumberControl(TableLayoutPanel tlp)
        {
            var ctrl = tlp.Controls.Find("ActorNumber", false).FirstOrDefault();
            if (ctrl != null && ctrl is TextBox)
            {
                var txt = (ctrl as TextBox);
                txt.SelectionStart = txt.Text.Length;
                txt.Select();
            }
        }

        private void cbPathHeaders_SelectionChangeCommitted(object sender, EventArgs e)
        {
            activePathHeader = (((ComboBox)sender).SelectedItem as PathHeader);
        }

        private void dgvPathWaypoints_RowPostPaint(object sender, DataGridViewRowPostPaintEventArgs e)
        {
            using (var b = new SolidBrush(((DataGridView)sender).RowHeadersDefaultCellStyle.ForeColor))
            {
                e.Graphics.DrawString((e.RowIndex + 1).ToString(), e.InheritedRowStyle.Font, b, e.RowBounds.Location.X + 18, e.RowBounds.Location.Y + 4);
            }
        }

        private void dgvPathWaypoints_SelectionChanged(object sender, EventArgs e)
        {
            var selwp = (dgvPathWaypoints.SelectedCells.Count != 0 ? dgvPathWaypoints.SelectedCells[0].OwningRow.DataBoundItem as Waypoint : null);
            if (selwp == null) return;
            pickedObject = (selwp as IPickableObject);
            collisionDirty = true;
        }

        private void RefreshCollisionPolyAndTypeLists()
        {
            /* Type list */
            var typelist = new List<Collision.PolygonType>();
            typelist.Add(new Collision.PolygonType());
            typelist.AddRange(currentScene.GetActiveCollision().PolygonTypes);

            colPolyTypeDataBinding = new BindingSource();
            colPolyTypeDataBinding.DataSource = typelist;
            cbCollisionPolyTypes.DataSource = colPolyTypeDataBinding;
            cbCollisionPolyTypes.DisplayMember = "Description";
            cbCollisionPolyTypes.Enabled = true;

            txtColPolyRawData.Enabled = true;
            cbColPolyGroundTypes.DataSource = Collision.PolygonType.GroundTypes;
            cbColPolyGroundTypes.DisplayMember = "Description";
            cbColPolyGroundTypes.Enabled = true;
            //TODO more editing stuff

            /* Poly list */
            var polylist = new List<Collision.Polygon>();
            polylist.Add(new Collision.Polygon());
            polylist.AddRange(currentScene.GetActiveCollision().Polygons);

            collisionPolyDataBinding = new BindingSource();
            collisionPolyDataBinding.DataSource = polylist;
            cbCollisionPolys.SelectedIndex = -1;
            cbCollisionPolys.DataSource = collisionPolyDataBinding;
            cbCollisionPolys.DisplayMember = "Description";
            cbCollisionPolys.Enabled = true;

            nudColPolyType.Minimum = 0;
            nudColPolyType.Maximum = (currentScene.GetActiveCollision().PolygonTypes.Count - 1);
            nudColPolyType.Enabled = true;
        }

        private void cbCollisionPolys_SelectedIndexChanged(object sender, EventArgs e)
        {
            currentCollisionPolygon = (((ComboBox)sender).SelectedItem as Collision.Polygon);
            if (currentCollisionPolygon == null) return;

            pickedObject = (currentCollisionPolygon as IPickableObject);
            collisionDirty = true;

            lblColPolyType.Visible = nudColPolyType.Visible = btnJumpToPolyType.Visible = !currentCollisionPolygon.IsDummy;
            if (!currentCollisionPolygon.IsDummy)
            {
                nudColPolyType.Value = currentCollisionPolygon.PolygonType;
                //TODO more here
            }
        }

        private void nudColPolyType_ValueChanged(object sender, EventArgs e)
        {
            currentCollisionPolygon.PolygonType = (ushort)((NumericUpDown)sender).Value;
            collisionPolyDataBinding.ResetCurrentItem();
        }

        private void btnJumpToPolyType_Click(object sender, EventArgs e)
        {
            if (cbCollisionPolyTypes.Items.Count > 0)
                cbCollisionPolyTypes.SelectedItem = (colPolyTypeDataBinding.List as List<Collision.PolygonType>).FirstOrDefault(x => x.Number == currentCollisionPolygon.PolygonType);
        }

        private void cbCollisionPolyTypes_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (((ComboBox)sender).SelectedItem == null) return;

            currentColPolygonType = (((ComboBox)sender).SelectedItem as Collision.PolygonType);

            busy = true;
            RefreshColPolyTypeControls();
            busy = false;
        }

        private void RefreshColPolyTypeControls()
        {
            txtColPolyRawData.Text = $"0x{currentColPolygonType.Raw:X16}";
            lblColPolyRawData.Visible = txtColPolyRawData.Visible = !currentColPolygonType.IsDummy;
            cbColPolyGroundTypes.SelectedItem = Collision.PolygonType.GroundTypes.FirstOrDefault(x => x.Value == currentColPolygonType.GroundTypeID);
            lblColPolyGroundType.Visible = cbColPolyGroundTypes.Visible = !currentColPolygonType.IsDummy;

            if (!busy) colPolyTypeDataBinding.ResetCurrentItem();

            collisionDirty = true;
        }

        private void txtColPolyRawData_TextChanged(object sender, EventArgs e)
        {
            var txt = (sender as TextBox);
            if (!txt.ContainsFocus) return;

            var ns = (txt.Text.StartsWith("0x") ? System.Globalization.NumberStyles.HexNumber : System.Globalization.NumberStyles.Integer);
            var valstr = (ns == System.Globalization.NumberStyles.HexNumber ? txt.Text.Substring(2) : txt.Text);
            var newval = ulong.Parse(valstr, ns);

            currentColPolygonType.Raw = newval;
            RefreshColPolyTypeControls();
        }

        private void cbColPolyGroundTypes_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!(sender as ComboBox).ContainsFocus) return;

            currentColPolygonType.GroundTypeID = (((ComboBox)sender).SelectedItem as Collision.PolygonType.GroundType).Value;
            RefreshColPolyTypeControls();
        }

        #region Waterboxes

        private void RefreshWaterboxControls()
        {
            if (tlpExWaterboxes.Visible = (currentWaterbox != null && !currentWaterbox.IsDummy))
            {
                tlpExWaterboxes.SuspendLayout();

                busy = true;

                txtWaterboxPositionX.Text = $"{currentWaterbox.Position.X}";
                txtWaterboxPositionY.Text = $"{currentWaterbox.Position.Y}";
                txtWaterboxPositionZ.Text = $"{currentWaterbox.Position.Z}";
                txtWaterboxSizeX.Text = $"{currentWaterbox.SizeXZ.X}";
                txtWaterboxSizeZ.Text = $"{currentWaterbox.SizeXZ.Y}";
                txtWaterboxProperties.Text = $"0x{currentWaterbox.Properties:X}";

                if (roomsForWaterboxSelection != null && roomsForWaterboxSelection.Count > 0)
                {
                    cbWaterboxRoom.DataSource = roomsForWaterboxSelection;
                    cbWaterboxRoom.DisplayMember = "Description";
                    cbWaterboxRoom.SelectedItem = roomsForWaterboxSelection.FirstOrDefault(x => x.Value == currentWaterbox.RoomNumber);
                }

                busy = false;

                tlpExWaterboxes.ResumeLayout();
            }

            waterboxesDirty = true;
        }

        private void cbWaterboxes_SelectedIndexChanged(object sender, EventArgs e)
        {
            currentWaterbox = (((ComboBox)sender).SelectedItem as Collision.Waterbox);
            if (currentWaterbox != null)
            {
                pickedObject = (currentWaterbox as IPickableObject);
                waterboxesDirty = true;

                txtWaterboxPositionX.Enabled = txtWaterboxPositionY.Enabled = txtWaterboxPositionZ.Enabled = txtWaterboxSizeX.Enabled = txtWaterboxSizeZ.Enabled = txtWaterboxProperties.Enabled
                    = !currentWaterbox.IsDummy;
            }
            RefreshWaterboxControls();
        }

        private void ModifyCurrentWaterbox()
        {
            if (busy) return;

            try
            {
                currentWaterbox.Position = new Vector3d(double.Parse(txtWaterboxPositionX.Text), double.Parse(txtWaterboxPositionY.Text), double.Parse(txtWaterboxPositionZ.Text));
                currentWaterbox.SizeXZ = new Vector2d(double.Parse(txtWaterboxSizeX.Text), double.Parse(txtWaterboxSizeZ.Text));
                currentWaterbox.RoomNumber = (ushort)(cbWaterboxRoom.SelectedItem as Option).Value;

                if (txtWaterboxProperties.Text.StartsWith("0x"))
                    currentWaterbox.Properties = ushort.Parse(txtWaterboxProperties.Text.Substring(2), System.Globalization.NumberStyles.HexNumber);
                else
                    currentWaterbox.Properties = ushort.Parse(txtWaterboxProperties.Text);

                waterboxesDirty = true;
            }
            catch (FormatException)
            {
                System.Media.SystemSounds.Hand.Play();
            }
        }

        private void txtWaterboxPositionX_TextChanged(object sender, EventArgs e)
        {
            ModifyCurrentWaterbox();
        }

        private void txtWaterboxPositionY_TextChanged(object sender, EventArgs e)
        {
            ModifyCurrentWaterbox();
        }

        private void txtWaterboxPositionZ_TextChanged(object sender, EventArgs e)
        {
            ModifyCurrentWaterbox();
        }

        private void txtWaterboxSizeX_TextChanged(object sender, EventArgs e)
        {
            ModifyCurrentWaterbox();
        }

        private void txtWaterboxSizeZ_TextChanged(object sender, EventArgs e)
        {
            ModifyCurrentWaterbox();
        }

        private void cbWaterboxRoom_SelectedIndexChanged(object sender, EventArgs e)
        {
            ModifyCurrentWaterbox();
        }

        private void txtWaterboxProperties_TextChanged(object sender, EventArgs e)
        {
            ModifyCurrentWaterbox();
        }

        #endregion

        private void bsiToolMode_Click(object sender, EventArgs e)
        {
            currentToolMode++;
        }

        private void deselectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (pickedObject == null) return;

            if (pickedObject is Actors.Entry)
            {
                var ac = (pickedObject as Actors.Entry);
                if (ac.IsTransitionActor)
                    cbTransitions.SelectedIndex = 0;
                else if (ac.IsSpawnPoint)
                    cbSpawnPoints.SelectedIndex = 0;
                else
                    cbActors.SelectedIndex = 0;
            }
            else if (pickedObject is Waypoint)
            {
                dgvPathWaypoints.ClearSelection();
            }
            else if (pickedObject is Collision.Waterbox)
            {
                cbWaterboxes.SelectedIndex = 0;
            }

            pickedObject = null;
        }

        private void RotatePickedObject(Vector3d rot)
        {
            if (pickedObject == null) return;

            if (pickedObject is Actors.Entry)
            {
                var actor = (pickedObject as Actors.Entry);
                actor.Rotation = Vector3d.Add(actor.Rotation, rot);

                if (actor.IsSpawnPoint)
                    XmlActorDefinitionReader.RefreshActorPositionRotation(actor, tlpExSpawnPoints);
                else if (actor.IsTransitionActor)
                    XmlActorDefinitionReader.RefreshActorPositionRotation(actor, tlpExTransitions);
                else
                    XmlActorDefinitionReader.RefreshActorPositionRotation(actor, tlpExRoomActors);
            }
        }

        private void xPlus45DegreesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            RotatePickedObject(new Vector3d(8192.0, 0.0, 0.0));
        }

        private void xMinus45DegreesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            RotatePickedObject(new Vector3d(-8192.0, 0.0, 0.0));
        }

        private void yPlus45DegreesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            RotatePickedObject(new Vector3d(0.0, 8192.0, 0.0));
        }

        private void yMinus45DegreesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            RotatePickedObject(new Vector3d(0.0, -8192.0, 0.0));
        }

        private void zPlus45DegreesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            RotatePickedObject(new Vector3d(0.0, 0.0, 8192.0));
        }

        private void zMinus45DegreesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            RotatePickedObject(new Vector3d(0.0, 0.0, -8192.0));
        }

        private void changeColorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (currentRoomVertex != null) EditVertexColor(currentRoomVertex);
        }

        private void propertiesToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            if (currentRoomVertex == null) return;

            var vertexInfo = new StringBuilder();
            vertexInfo.AppendFormat("Vertex at address 0x{0:X8}:\n\n", currentRoomVertex.Address);
            vertexInfo.AppendFormat("Position: {0}\n", currentRoomVertex.Position);
            vertexInfo.AppendFormat("Texture Coordinates: {0}\n", currentRoomVertex.TexCoord);
            vertexInfo.AppendFormat("Colors: ({0}, {1}, {2}, {3})\n", currentRoomVertex.Colors[0], currentRoomVertex.Colors[1], currentRoomVertex.Colors[2], currentRoomVertex.Colors[3]);
            vertexInfo.AppendFormat("Normals: ({0}, {1}, {2})\n", currentRoomVertex.Normals[0], currentRoomVertex.Normals[1], currentRoomVertex.Normals[2]);
            
          

            MessageBox.Show(vertexInfo.ToString(), "Vertex Properties", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void bsiCamCoords_Click(object sender, EventArgs e)
        {
            camera.Reset();
        }

        #region Menu events

        private void resetCameraPositionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            camera.Reset();
        }

        private void enableTexturesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Configuration.RenderTextures = ((ToolStripMenuItem)sender).Checked;
            displayListsDirty = true;
        }

        private void enableVSyncToolStripMenuItem_Click(object sender, EventArgs e)
        {
            customGLControl.VSync = Configuration.OglvSync = ((ToolStripMenuItem)sender).Checked;
        }

        private void enableAntiAliasingToolStripMenuItem_Click(object sender, EventArgs e)
        {
            /* Determine anti-aliasing status */
            if (Configuration.EnableAntiAliasing == ((ToolStripMenuItem)sender).Checked)
            {
                var samples = 0;
                GL.GetInteger(GetPName.MaxSamples, out samples);
                Configuration.AntiAliasingSamples = samples;
            }
            else
                Configuration.AntiAliasingSamples = 0;

            if (MessageBox.Show(
                    $"{(Configuration.EnableAntiAliasing ? "En" : "Dis")}abling anti-aliasing requires restarting SceneNavi.\n\nDo you want to restart the program now?",
                "Question", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                Configuration.IsRestarting = true;
                Application.Restart();
            }
        }

        private void enableMipmapsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Configuration.EnableMipmaps = ((ToolStripMenuItem)sender).Checked;

            if (_baseRom == null || _baseRom.Scenes == null) return;

            /* Destroy, destroy! Kill all the display lists! ...or should I say "Exterminate!"? Then again, I'm not a Doctor Who fan... */
            foreach (var sh in _baseRom.Scenes.SelectMany(x => x.GetSceneHeaders()))
            {
                var rooms = (sh.Commands.FirstOrDefault(x => x.Command == CommandTypeIDs.Rooms)) as Rooms;
                if (rooms == null) continue;

                foreach (var rh in rooms.RoomInformation.SelectMany(x => x.Headers))
                {
                    var mh = (rh.Commands.FirstOrDefault(x => x.Command == CommandTypeIDs.MeshHeader)) as MeshHeader;
                    if (mh != null) mh.DestroyDisplayLists();
                }
            }

            _baseRom.Renderer.ResetTextureCache();

            displayListsDirty = true;
        }

        private void renderCollisionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Configuration.RenderCollision = ((ToolStripMenuItem)sender).Checked;
            if (Configuration.RenderCollision) collisionDirty = true;
        }

        private void whiteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Configuration.RenderCollisionAsWhite = ((Controls.ToolStripRadioButtonMenuItem)sender).Checked;
            collisionDirty = true;
        }

        private void typebasedToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Configuration.RenderCollisionAsWhite = !((Controls.ToolStripRadioButtonMenuItem)sender).Checked;
            collisionDirty = true;
        }

        private void renderRoomActorsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Configuration.RenderRoomActors = ((ToolStripMenuItem)sender).Checked;
        }

        private void renderSpawnPointsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Configuration.RenderSpawnPoints = ((ToolStripMenuItem)sender).Checked;
        }

        private void renderTransitionsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Configuration.RenderTransitions = ((ToolStripMenuItem)sender).Checked;
        }

        private void renderPathWaypointsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Configuration.RenderPathWaypoints = ((ToolStripMenuItem)sender).Checked;
        }

        private void renderWaterboxesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Configuration.RenderWaterboxes = ((ToolStripMenuItem)sender).Checked;
        }

        private void linkAllWaypointsInPathToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Configuration.LinkAllWPinPath = ((ToolStripMenuItem)sender).Checked;
        }

        private void showWaterboxesPerRoomToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Configuration.ShowWaterboxesPerRoom = ((ToolStripMenuItem)sender).Checked;
        }

        private void rOMInformationToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var info = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "{0} ({1}, v1.{2}), {3} MB ({4} Mbit)\n{5}\nCreated by {6}, built on {7:F}\n\nCode file at 0x{8:X} - 0x{9:X} ({10})\n- DMA table address: 0x{11:X}\n- File name table address: {12}\n" +
                "- Scene table address: {13}\n- Actor table address: {14}\n- Object table address: {15}\n- Entrance table address: {16}",
                _baseRom.Title, _baseRom.GameId, _baseRom.Version, (_baseRom.Size / 0x100000), (_baseRom.Size / 0x20000), (_baseRom.HasZ64TablesHack ? "(uses 'z64tables' extended tables)\n" : ""),
                _baseRom.Creator, _baseRom.BuildDate, _baseRom.Code.PStart, (_baseRom.Code.IsCompressed ? _baseRom.Code.PEnd : _baseRom.Code.VEnd),
                (_baseRom.Code.IsCompressed ? "compressed" : "uncompressed"), _baseRom.DmaTableAddress, (_baseRom.HasFileNameTable ? ("0x" + _baseRom.FileNameTableAddress.ToString("X")) : "none"),
                (_baseRom.HasZ64TablesHack ? ("0x" + _baseRom.SceneTableAddress.ToString("X") + " (in ROM)") : ("0x" + _baseRom.SceneTableAddress.ToString("X"))),
                (_baseRom.HasZ64TablesHack ? ("0x" + _baseRom.ActorTableAddress.ToString("X") + " (in ROM)") : ("0x" + _baseRom.ActorTableAddress.ToString("X"))),
                (_baseRom.HasZ64TablesHack ? ("0x" + _baseRom.ObjectTableAddress.ToString("X") + " (in ROM)") : ("0x" + _baseRom.ObjectTableAddress.ToString("X"))),
                (_baseRom.HasZ64TablesHack ? ("0x" + _baseRom.EntranceTableAddress.ToString("X") + " (in ROM)") : ("0x" + _baseRom.EntranceTableAddress.ToString("X"))));

            MessageBox.Show(info, "ROM Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void editDataTablesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            new TableEditorForm(_baseRom).ShowDialog();
        }

        private void editAreaTitleCardToolStripMenuItem_Click(object sender, EventArgs e)
        {
            new TitleCardForm(_baseRom, currentScene as SceneTableEntryOcarina,null).ShowDialog();
        }

        private void checkForUpdateToolStripMenuItem_Click(object sender, EventArgs e)
        {
            new UpdateCheckDialog().ShowDialog();
        }

        private void openGLInformationToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var oglInfoString = new StringBuilder();

            oglInfoString.AppendFormat("Vendor: {0}\n", Initialization.VendorString);
            oglInfoString.AppendFormat("Renderer: {0}\n", Initialization.RendererString);
            oglInfoString.AppendFormat("Version: {0}\n", Initialization.VersionString);
            oglInfoString.AppendFormat("Shading Language Version: {0}\n", Initialization.ShadingLanguageVersionString);
            oglInfoString.AppendLine();

            oglInfoString.AppendFormat("Max Texture Units: {0}\n", Initialization.GetInteger(GetPName.MaxTextureUnits));
            oglInfoString.AppendFormat("Max Texture Size: {0}\n", Initialization.GetInteger(GetPName.MaxTextureSize));
            oglInfoString.AppendLine();

            oglInfoString.AppendFormat("{0} OpenGL extension(s) supported.\n", Initialization.SupportedExtensions.Length);
            oglInfoString.AppendLine();

            oglInfoString.AppendLine("Status of requested extensions:");

            foreach (var extension in allRequiredOglExtensions) oglInfoString.AppendFormat("* {0}\t{1}\n", extension.PadRight(40), Initialization.CheckForExtension(extension) ? "supported" : "not supported");

            MessageBox.Show(oglInfoString.ToString(), "OpenGL Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var linkerTimestamp = AssemblyHelpers.RetrieveLinkerTimestamp();

            var buildString =
                $"(Build: {linkerTimestamp.ToString("MM/dd/yyyy HH:mm:ss UTCzzz", System.Globalization.CultureInfo.InvariantCulture)})";
            var yearString = (linkerTimestamp.Year == 2013 ? "2013" : $"2013-{linkerTimestamp:yyyy}");

            MessageBox.Show(
                $"{Program.AppNameVer} {buildString}\n\nScene/room actor editor for The Legend of Zelda: Ocarina of Time\n\nWritten {yearString} by xdaniel / http://magicstone.de/dzd/",
                $"About {Application.ProductName}", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        #endregion
    }
}
