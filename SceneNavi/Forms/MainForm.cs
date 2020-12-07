using MediatR;
using NLog;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using SceneNavi.Configurations;
using SceneNavi.Forms;
using SceneNavi.HeaderCommands;
using SceneNavi.Models;
using SceneNavi.ROMHandler;
using SceneNavi.ROMHandler.Interfaces;
using SceneNavi.Services.Commands;
using SceneNavi.Utilities.OpenGLHelpers;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using SceneNavi.Dependencies.Interfaces;

namespace SceneNavi
{
    /*
     * As usual, my GUI code is a mess! :D
     * There's some useful stuff in here, like the OpenGL picking code, but overall this is probably the least interesting part of the program...
     * ...like, excluding constants and enums or something anyway.
     */
    public class MainFormState
    {
        RoomInfoClass CurrentRoom { get; set; }
        public List<MeshHeader> AllMeshHeaders { get; set; }
        Collision.Polygon CurrentCollisionPolygon { get; set; }
        Collision.PolygonType CurrentColPolygonType { get; set; }
        Collision.Waterbox CurrentWaterBox { get; set; }
        DisplayListEx.Triangle CurrentRoomTriangle { get; set; }
        SimpleF3DEX2.Vertex CurrentRoomVertex { get; set; }
        EnvironmentSettings.Entry CurrentEnvSettings { get; set; }


        IPickableObject PickedObject { get; set; }
        Vector2d PickObjDisplacement { get; set; }
        Vector2d PickObjLastPosition { get; set; }
        Vector2d PickObjPosition { get; set; }


        TabPage LastTabPage { get; set; }


        bool Ready { get; set; }
        bool Busy { get; set; }

        bool[] _keysDown = new bool[ushort.MaxValue];
        ToolModes InternalToolMode { get; set; }


//        ToolModes CurrentToolMode
//        {
//            get => InternalToolMode;
//            set
//            {
//                Configuration.LastToolMode = InternalToolMode = (Enum.IsDefined(typeof(ToolModes), value)
//                    ? InternalToolMode = value
//                    : InternalToolMode = ToolModes.Camera);
//                
//                bsiToolMode.Text = MainFormConstants.ToolModeNametable[InternalToolMode][0];
//            
//                if (mouseModeToolStripMenuItem.DropDownItems.Count > 0)
//                {
//                    (mouseModeToolStripMenuItem.DropDownItems[(int) InternalToolMode] as
//                        Controls.ToolStripRadioButtonMenuItem).Checked = true;
//                }
//            }
//        }
    }

    public partial class MainForm : Form
    {
        //dependencies
        CombinerTypes _internalCombinerType;

        // dependencies
        CombinerTypes CurrentCombinerType
        {
            get => _internalCombinerType;
            set
            {
                Configuration.CombinerType = _internalCombinerType = (Enum.IsDefined(typeof(CombinerTypes), value)
                    ? _internalCombinerType = value
                    : _internalCombinerType = CombinerTypes.None);
                _baseRom?.Renderer.InitCombiner();
                _displayListsDirty = true;
            }
        }


        // Dependency
        BaseRomHandler _baseRom;


        // data??
        Dictionary<byte, string> _bgms;


        // Dependency
        ISceneTableEntry _currentScene;

        // state
        RoomInfoClass _currentRoom;

        //state
        List<MeshHeader> _allMeshHeaders;

        // state
        Collision.Polygon _currentCollisionPolygon;
        Collision.PolygonType _currentColPolygonType;
        Collision.Waterbox _currentWaterbox;
        DisplayListEx.Triangle _currentRoomTriangle;
        SimpleF3DEX2.Vertex _currentRoomVertex;
        EnvironmentSettings.Entry _currentEnvSettings;

        // state
        bool _displayListsDirty, _collisionDirty, _waterboxesDirty;

        // state
        IPickableObject _pickedObject;
        Vector2d _pickObjDisplacement, _pickObjLastPosition, _pickObjPosition;

        // state
        TabPage _lastTabPage;

        //state
        bool _ready, _busy;
        bool[] _keysDown = new bool[ushort.MaxValue];

        // state
        ToolModes _internalToolMode;

        //state
        ToolModes CurrentToolMode
        {
            get => _internalToolMode;
            set
            {
                Configuration.LastToolMode = _internalToolMode = (Enum.IsDefined(typeof(ToolModes), value)
                    ? _internalToolMode = value
                    : _internalToolMode = ToolModes.Camera);
                bsiToolMode.Text = MainFormConstants.ToolModeNametable[_internalToolMode][0];
                if (mouseModeToolStripMenuItem.DropDownItems.Count > 0)
                {
                    (mouseModeToolStripMenuItem.DropDownItems[(int) _internalToolMode] as
                        Controls.ToolStripRadioButtonMenuItem).Checked = true;
                }
            }
        }

        // Dependencies
        ISceneTableEntry _tempScene;

        // Dependencies
        Rooms _tempRooms;

        // weird but works?
        PathHeader ActivePathHeader
        {
            get => (cbPathHeaders.SelectedItem as PathHeader);
            set => RefreshPathWaypoints();
        }


        //  Dependency?
        BindingSource _roomActorComboBinding,
            _transitionComboBinding,
            _spawnPointComboBinding,
            _collisionPolyDataBinding,
            _colPolyTypeDataBinding,
            _waypointPathComboDataBinding,
            _waterboxComboDataBinding;


        // dependency
        DisplayList _collisionDl, _waterboxDl;
        List<Option> _roomsForWaterboxSelection;


        private readonly IMediator _mediator;
        private readonly IMainFormConfig _mainFormConfig;
        private readonly IBaseConfig _baseConfig;
        private readonly INavigation _navigation;
        private readonly ILogger _logger;
        private readonly ITextPrinter _textPrinter;
        private readonly ICamera _camera;
        private readonly IFpsMonitor _fpsMonitor;
        private readonly IRomHandler _romHandler;
        private readonly IGraphicsRenderingSettings _graphicsRenderingSettings;
        private readonly IViewPortRenderSettings _viewPortRenderSettings;


        public MainForm(IMediator mediator,
            IMainFormConfig mainFormConfig,
            IBaseConfig baseConfig,
            INavigation navigation,
            ILogger logger,
            ITextPrinter textPrinter,
            ICamera camera,
            IFpsMonitor fpsMonitor,
            IRomHandler romHandler,
            IGraphicsRenderingSettings graphicsRenderingSettings,
            IViewPortRenderSettings viewPortRenderSettings)
        {
            InitializeComponent();

            _mediator = mediator;
            _mainFormConfig = mainFormConfig;
            _baseConfig = baseConfig;
            _navigation = navigation;
            _logger = logger;
            _textPrinter = textPrinter;
            _camera = camera;
            _fpsMonitor = fpsMonitor;
            _romHandler = romHandler;
            _graphicsRenderingSettings = graphicsRenderingSettings;
            _viewPortRenderSettings = viewPortRenderSettings;
        }

        protected override void OnLoad(EventArgs e)
        {
            Application.Idle += Application_Idle;
            Application.ApplicationExit += Application_ApplicationExit;
            Program.Status.MessageChanged += StatusMsg_OnStatusMessageChanged;

            


            dgvObjects.DoubleBuffered(_mainFormConfig.ObjectsDoubleBuffered);
            dgvPathWaypoints.DoubleBuffered(_mainFormConfig.PathWayPointsDoubleBuffered);
            SetFormTitle();

            base.OnLoad(e);
        }

        private void StatusMsg_OnStatusMessageChanged(object sender, StatusMessageHandler.MessageChangedEventArgs e)
        {
            tsslStatus.Text = e.Message;
            statusStrip1.Invoke((MethodInvoker) (() => statusStrip1.Update()));
        }

        private void SetFormTitle()
        {
            var filenamePart = ((_baseRom != null && _baseRom.Loaded)
                ? $" - [{Path.GetFileName(_baseRom.Filename)}]"
                : string.Empty);
            var scenePart =
                (_mainFormConfig.IndividualFileMode
                    ? $" ({Path.GetFileName(Configuration.LastSceneFile)})"
                    : string.Empty);
            Text = string.Concat(Program.AppNameVer, filenamePart, scenePart);
        }

        private void Application_Idle(object sender, EventArgs e)
        {
            if (_ready)
            {
                _camera.KeyUpdate(_keysDown);
                _glRendererControl.Invalidate();

                bsiCamCoords.Text = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "Cam X: {0:00.000}, Y: {1:00.000}, Z: {2:00.000}", _camera.GetCurrentPosition().X,
                    _camera.GetCurrentPosition().Y, _camera.GetCurrentPosition().Z);
            }
        }

        private void Application_ApplicationExit(object sender, EventArgs e)
        {
            _textPrinter?.Dispose();
            _camera?.Dispose();
            _fpsMonitor?.Dispose();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            ResetCurrentData();
        }

        private void SettingsGuiInit()
        {
            /* Read settings */

            enableTexturesToolStripMenuItem.Checked = _viewPortRenderSettings.RenderTextures;
            renderCollisionToolStripMenuItem.Checked = _viewPortRenderSettings.RenderCollision;
            whiteToolStripMenuItem.Checked = _viewPortRenderSettings.RenderCollisionAsWhite;
            typebasedToolStripMenuItem.Checked = !whiteToolStripMenuItem.Checked;
            renderRoomActorsToolStripMenuItem.Checked = _viewPortRenderSettings.RenderRoomActors;
            renderSpawnPointsToolStripMenuItem.Checked = _viewPortRenderSettings.RenderSpawnPoints;
            renderTransitionsToolStripMenuItem.Checked = _viewPortRenderSettings.RenderTransitions;
            renderPathWaypointsToolStripMenuItem.Checked = _viewPortRenderSettings.RenderPathWayPoints;
            linkAllWaypointsInPathToolStripMenuItem.Checked = _baseConfig.LinkAllWPinPath;
            renderWaterboxesToolStripMenuItem.Checked = _viewPortRenderSettings.RenderWaterBoxes;
            showWaterboxesPerRoomToolStripMenuItem.Checked = _viewPortRenderSettings.ShowWaterBoxesPerRoom;


            //            
            //            enableTexturesToolStripMenuItem.Checked = Configuration.RenderTextures;
            //            renderCollisionToolStripMenuItem.Checked = Configuration.RenderCollision;
            //
            //            whiteToolStripMenuItem.Checked = Configuration.RenderCollisionAsWhite;
            //            typebasedToolStripMenuItem.Checked = !whiteToolStripMenuItem.Checked;
            //
            //            renderRoomActorsToolStripMenuItem.Checked = Configuration.RenderRoomActors;
            //            renderSpawnPointsToolStripMenuItem.Checked = Configuration.RenderSpawnPoints;
            //            renderTransitionsToolStripMenuItem.Checked = Configuration.RenderTransitions;
            //
            //            renderPathWaypointsToolStripMenuItem.Checked = Configuration.RenderPathWayPoints;
            //            linkAllWaypointsInPathToolStripMenuItem.Checked = Configuration.LinkAllWPinPath;
            //
            //            renderWaterboxesToolStripMenuItem.Checked = Configuration.RenderWaterBoxes;
            //
            //            showWaterboxesPerRoomToolStripMenuItem.Checked = Configuration.ShowWaterBoxesPerRoom;
            //
            //            enableVSyncToolStripMenuItem.Checked = _glRendererControl.VSync = Configuration.OglVSync;
            //            enableAntiAliasingToolStripMenuItem.Checked = Configuration.EnableAntiAliasing;
            //            enableMipmapsToolStripMenuItem.Checked = Configuration.EnableMipmaps;
            //
            //            CurrentToolMode = Configuration.LastToolMode;
            //            CurrentCombinerType = Configuration.CombinerType;


            /* Create tool mode menu */
            var i = 0;
            foreach (var keyValuePair in MainFormConstants.ToolModeNametable)
            {
                var toolStripMenuItem = new Controls.ToolStripRadioButtonMenuItem(keyValuePair.Value[0])
                {
                    Tag = keyValuePair.Key,
                    CheckOnClick = true,
                    HelpText = keyValuePair.Value[1]
                };


                if (CurrentToolMode == keyValuePair.Key) toolStripMenuItem.Checked = true;

                toolStripMenuItem.Click += (s, ex) =>
                {
                    var tag = ((ToolStripMenuItem) s).Tag;
                    if (tag is ToolModes) CurrentToolMode = ((ToolModes) tag);
                };

                mouseModeToolStripMenuItem.DropDownItems.Add(toolStripMenuItem);
                i++;
            }


            /* Create combiner type menu */
            i = 0;
            foreach (var keyValuePair in MainFormConstants.CombinerTypeNametable)
            {
                var toolStripMenuItem = new Controls.ToolStripRadioButtonMenuItem(keyValuePair.Value[0])
                {
                    Tag = keyValuePair.Key,
                    CheckOnClick = true,
                    HelpText = keyValuePair.Value[1]
                };

                if (CurrentCombinerType == keyValuePair.Key) toolStripMenuItem.Checked = true;

                toolStripMenuItem.Click += (s, ex) =>
                {
                    var tag = ((ToolStripMenuItem) s).Tag;
                    if (tag is CombinerTypes) CurrentCombinerType = ((CombinerTypes) tag);
                };

                combinerTypeToolStripMenuItem.DropDownItems.Add(toolStripMenuItem);
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

            if (!_mainFormConfig.IndividualFileMode)
            {
                root = new TreeNode($"{_baseRom.Title} ({_baseRom.GameId}, v1.{_baseRom.Version}; {_baseRom.Scenes.Count} scenes)") {Tag = _baseRom};
                foreach (var ste in _baseRom.Scenes)
                {
                    var scene = new TreeNode($"{ste.GetName()} (0x{ste.GetSceneStartAddress():X})") {Tag = ste};

                    if (ste.GetSceneHeaders().Count != 0)
                    {
                        var rooms = ste.GetSceneHeaders()[0].Commands
                            .FirstOrDefault(x => x.Command == CommandTypeIDs.Rooms) as Rooms;
                        if (rooms == null) continue;

                        foreach (var shead in ste.GetSceneHeaders())
                        {
                            var rhs = new List<HeaderLoader>();
                            foreach (var ric in rooms.RoomInformation)
                                if (ric.Headers.Count != 0)
                                    rhs.Add(ric.Headers[shead.Number]);

                            var hp = new HeaderPair(shead, rhs);

                            var de = new System.Collections.DictionaryEntry();
                            foreach (System.Collections.DictionaryEntry d in _baseRom.XmlStageDescriptions.Names)
                            {
                                var sk = d.Key as StageKey;
                                if (sk.SceneAddress == ste.GetSceneStartAddress() &&
                                    sk.HeaderNumber == hp.SceneHeader.Number)
                                {
                                    de = d;
                                    hp.Description = (string) de.Value;
                                    break;
                                }
                            }

                            var sheadnode =
                                new TreeNode((de.Value == null ? $"Stage #{shead.Number}" : (string) de.Value))
                                    {Tag = hp};
                            foreach (var ric in rooms.RoomInformation)
                            {
                                var room = new TreeNode($"{ric.Description} (0x{ric.Start:X})") {Tag = ric};
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
                root = new TreeNode(_tempScene.GetName()) {Tag = _tempScene};
                var rooms = _tempScene.GetSceneHeaders()[0].Commands
                    .FirstOrDefault(x => x.Command == CommandTypeIDs.Rooms) as Rooms;

                TreeNode nodeToSelect = null;
                if (rooms != null)
                {
                    foreach (var shead in _tempScene.GetSceneHeaders())
                    {
                        var rhs = new List<HeaderLoader>();
                        foreach (var ric in rooms.RoomInformation)
                            if (ric.Headers.Count != 0)
                                rhs.Add(ric.Headers[shead.Number]);

                        var hp = new HeaderPair(shead, rhs);


                        var sheadnode = new TreeNode($"Stage #{shead.Number}") {Tag = hp};


                        foreach (var ric in rooms.RoomInformation)
                        {
                            var room = new TreeNode($"{ric.Description} (0x{ric.Start:X})") {Tag = ric};
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

            _bgms = new Dictionary<byte, string>();
            foreach (System.Collections.DictionaryEntry de in _baseRom.XmlSongNames.Names)
                _bgms.Add((byte) de.Key, (string) de.Value);
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

                _mainFormConfig.IndividualFileMode = false;
                _displayListsDirty = _collisionDirty = _waterboxesDirty = true;

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

                editDataTablesToolStripMenuItem.Enabled = saveToolStripMenuItem.Enabled =
                    openSceneToolStripMenuItem.Enabled = rOMInformationToolStripMenuItem.Enabled =
                        _glRendererControl.Enabled = _baseRom.Loaded;
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

                if ((_tempScene = (!_baseRom.IsMajora
                        ? new SceneTableEntryOcarina(_baseRom, ofdOpenScene.FileName)
                        : (ISceneTableEntry) new SceneTableEntryMajora(_baseRom, ofdOpenScene.FileName))) != null)
                {
                    if (ofdOpenRoom.ShowDialog() != DialogResult.OK) return;

                    Configuration.LastRoomFile = ofdOpenRoom.FileName;

                    _mainFormConfig.IndividualFileMode = true;
                    _displayListsDirty = _collisionDirty = _waterboxesDirty = true;

                    ResetCurrentData(true);
                    _tempScene.ReadScene((_tempRooms = new Rooms(_baseRom, _tempScene, ofdOpenRoom.FileName)));
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
            _mainFormConfig.IndividualFileMode = false;
            _displayListsDirty = _collisionDirty = _waterboxesDirty = true;

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
            if (_mainFormConfig.IndividualFileMode)
            {
                if (_tempRooms.RoomInformation.Count != 1)
                    throw new Exception("Zero or more than one individual room file loaded; this should not happen!");

                ParseStoreHeaders(_tempScene.GetSceneHeaders(), _tempScene.GetData(), 0);
                ParseStoreHeaders(_tempRooms.RoomInformation[0].Headers, _tempRooms.RoomInformation[0].Data, 0);

                var bwScene = new BinaryWriter(File.Open(Configuration.LastSceneFile, FileMode.Open,
                    FileAccess.ReadWrite, FileShare.ReadWrite));
                bwScene.Write(_tempScene.GetData());
                bwScene.Close();

                var bwRoom = new BinaryWriter(File.Open(Configuration.LastRoomFile, FileMode.Open, FileAccess.ReadWrite,
                    FileShare.ReadWrite));
                bwRoom.Write(_tempRooms.RoomInformation[0].Data);
                bwRoom.Close();
            }
            else
            {
                /* Store scene table entries & scenes */
                foreach (var ste in _baseRom.Scenes)
                {
                    ste.SaveTableEntry();
                    ParseStoreHeaders(ste.GetSceneHeaders(), _baseRom.Data, (int) ste.GetSceneStartAddress());
                }

                /* Store entrance table entries */
                foreach (var ete in _baseRom.Entrances) ete.SaveTableEntry();

                /* Copy code data */
                Buffer.BlockCopy(_baseRom.CodeData, 0, _baseRom.Data, (int) _baseRom.Code.PStart,
                    _baseRom.CodeData.Length);

                /* Write to file */
                var bw = new BinaryWriter(File.Open(_baseRom.Filename, FileMode.Open, FileAccess.ReadWrite,
                    FileShare.ReadWrite));
                bw.Write(_baseRom.Data);
                bw.Close();
            }
        }

        private void ParseStoreHeaders(List<HeaderLoader> headers, byte[] databuf, int baseadr)
        {
            foreach (var hl in headers)
            {
                /* Fetch and parse room headers first */
                if (!_mainFormConfig.IndividualFileMode)
                {
                    var rooms = (hl.Commands.FirstOrDefault(x => x.Command == CommandTypeIDs.Rooms) as Rooms);
                    if (rooms != null)
                    {
                        foreach (var ric in rooms.RoomInformation)
                            ParseStoreHeaders(ric.Headers, databuf, (int) ric.Start);
                    }
                }

                /* Now store all storeable commands */
                foreach (IStoreable hc in hl.Commands.Where(x => x is IStoreable))
                    hc.Store(databuf, baseadr);
            }
        }

        private void ResetCurrentData(bool norefresh = false)
        {
            _currentScene = null;
            _currentRoom = null;
            _currentRoomTriangle = null;
            _currentRoomVertex = null;
            _currentEnvSettings = null;

            if (!norefresh) RefreshCurrentData();
        }

        private void CreateStatusString()
        {
            var infostrs = new List<string>();

            if (_currentScene != null)
            {
                if (_currentRoom == null)
                {
                    infostrs.Add($"{_currentScene.GetName()}");

                    var rooms = (_currentScene.GetCurrentSceneHeader().Commands
                        .FirstOrDefault(x => x.Command == CommandTypeIDs.Rooms) as Rooms);
                    if (rooms != null)
                        infostrs.Add(
                            $"{rooms.RoomInformation.Count} room{(rooms.RoomInformation.Count != 1 ? "s" : "")}");
                }
                else if (_currentRoom != null)
                {
                    infostrs.Add($"{_currentScene.GetName()}, {_currentRoom.Description}");
                }
            }
            else
            {
                infostrs.Add(
                    $"Ready{((Configuration.ShownIntelWarning || Configuration.ShownExtensionWarning) ? " (limited combiner)" : string.Empty)}");
                if (_baseRom != null && _baseRom.Scenes != null)
                    infostrs.Add(
                        $"{_baseRom.Title} ({_baseRom.GameId}, v1.{_baseRom.Version}; {_baseRom.Scenes.Count} scenes)");
            }

            if (_currentRoom != null && _currentRoom.ActiveRoomActorData != null)
            {
                infostrs.Add(
                    $"{_currentRoom.ActiveRoomActorData.ActorList.Count} room actor{(_currentRoom.ActiveRoomActorData.ActorList.Count != 1 ? "s" : "")}");
            }

            if (_currentScene != null && _currentScene.GetActiveTransitionData() != null && _currentRoom == null)
            {
                infostrs.Add(
                    $"{_currentScene.GetActiveTransitionData().ActorList.Count} transition actor{(_currentScene.GetActiveTransitionData().ActorList.Count != 1 ? "s" : "")}");
            }

            if (_currentScene != null && _currentScene.GetActiveSpawnPointData() != null && _currentRoom == null)
            {
                infostrs.Add(
                    $"{_currentScene.GetActiveSpawnPointData().ActorList.Count} spawn point{(_currentScene.GetActiveSpawnPointData().ActorList.Count != 1 ? "s" : "")}");
            }

            if (_currentRoom != null && _currentRoom.ActiveObjects != null)
            {
                infostrs.Add(
                    $"{_currentRoom.ActiveObjects.ObjectList.Count} object{(_currentRoom.ActiveObjects.ObjectList.Count != 1 ? "s" : "")}");
            }

            if (_currentScene != null && _currentScene.GetActiveWaypoints() != null && _currentRoom == null)
            {
                infostrs.Add(
                    $"{_currentScene.GetActiveWaypoints().Paths.Count} path{(_currentScene.GetActiveWaypoints().Paths.Count != 1 ? "s" : "")}");
            }

            Program.Status.Message = string.Join("; ", infostrs);
        }

        private void RefreshCurrentData()
        {
            CreateStatusString();

            if (_currentScene != null)
            {
                if (!_baseRom.IsMajora)
                {
                    var steOcarina = (_currentScene as SceneTableEntryOcarina);
                    editAreaTitleCardToolStripMenuItem.Enabled =
                        (!_baseRom.IsMajora && steOcarina.LabelStartAddress != 0 && steOcarina.LabelEndAddress != 0);
                }

                var rooms = (_currentScene.GetCurrentSceneHeader().Commands
                    .FirstOrDefault(x => x.Command == CommandTypeIDs.Rooms) as Rooms);
                if (rooms != null)
                {
                    _roomsForWaterboxSelection = new List<Option>();
                    _roomsForWaterboxSelection.Add(new Option() {Description = "(All Rooms)", Value = 0x3F});
                    foreach (var ric in rooms.RoomInformation)
                        _roomsForWaterboxSelection.Add(new Option()
                            {Description = ric.Description, Value = ric.Number});
                }

                if (_currentRoom == null)
                {
                    _baseRom.SegmentMapping.Remove((byte) 0x02);
                    _baseRom.SegmentMapping.Add((byte) 0x02, _currentScene.GetData());

                    _allMeshHeaders = new List<MeshHeader>();

                    if (rooms != null)
                    {
                        foreach (var hl in rooms.RoomInformation.SelectMany(x => x.Headers))
                            _allMeshHeaders.Add(
                                hl.Commands.FirstOrDefault(x => x.Command == CommandTypeIDs.MeshHeader) as MeshHeader);
                    }

                    _allMeshHeaders = _allMeshHeaders.Distinct().ToList();
                }
                else if (_currentRoom != null)
                {
                    _baseRom.SegmentMapping.Remove((byte) 0x02);
                    _baseRom.SegmentMapping.Remove((byte) 0x03);
                    _baseRom.SegmentMapping.Add((byte) 0x02, _currentScene.GetData());
                    _baseRom.SegmentMapping.Add((byte) 0x03, _currentRoom.Data);
                }
            }
            else
            {
                editAreaTitleCardToolStripMenuItem.Enabled = false;
            }

            if (_currentRoom != null && _currentRoom.ActiveRoomActorData != null)
            {
                RefreshRoomActorList();
            }
            else
            {
                cbActors.Enabled = false;
                cbActors.DataSource = null;
            }

            if (_currentScene != null && _currentScene.GetActiveTransitionData() != null)
            {
                RefreshTransitionList();
            }
            else
            {
                cbTransitions.Enabled = false;
                cbTransitions.DataSource = null;
            }

            if (_currentScene != null && _currentScene.GetActiveSpawnPointData() != null)
            {
                RefreshSpawnPointList();
            }
            else
            {
                cbSpawnPoints.Enabled = false;
                cbSpawnPoints.DataSource = null;
            }

            if (_currentScene != null && _currentScene.GetActiveSpecialObjs() != null)
            {
                cbSpecialObjs.Enabled = true;
                cbSpecialObjs.DisplayMember = "Name";
                cbSpecialObjs.ValueMember = "ObjectNumber";
                cbSpecialObjs.DataSource = new BindingSource() {DataSource = SpecialObjects.Types};
                cbSpecialObjs.DataBindings.Clear();
                cbSpecialObjs.DataBindings.Add("SelectedValue", _currentScene.GetActiveSpecialObjs(),
                    "SelectedSpecialObjects");
            }
            else
            {
                cbSpecialObjs.Enabled = false;
                cbSpecialObjs.DataSource = null;
                cbSpecialObjs.DataBindings.Clear();
            }

            if (_currentRoom != null && _currentRoom.ActiveObjects != null)
            {
                dgvObjects.Enabled = true;
                dgvObjects.DataSource = new BindingSource() {DataSource = _currentRoom.ActiveObjects.ObjectList};
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

            if (_currentScene != null && _currentScene.GetActiveWaypoints() != null)
            {
                RefreshWaypointPathList(_currentScene.GetActiveWaypoints());
            }
            else
            {
                cbPathHeaders.Enabled = false;
                cbPathHeaders.DataSource = null;
            }

            if (_currentScene != null && _currentScene.GetActiveCollision() != null)
            {
                RefreshCollisionPolyAndTypeLists();
            }
            else
            {
                cbCollisionPolys.Enabled = cbCollisionPolyTypes.Enabled = txtColPolyRawData.Enabled =
                    nudColPolyType.Enabled = cbColPolyGroundTypes.Enabled = false;
                cbCollisionPolys.DataSource = cbCollisionPolyTypes.DataSource = cbColPolyGroundTypes.DataSource = null;
                txtColPolyRawData.Text = string.Empty;
            }

            if (_currentScene != null && _currentScene.GetActiveCollision() != null &&
                _currentScene.GetActiveCollision().Waterboxes.Count > 0)
            {
                var wblist = new List<Collision.Waterbox>
                {
                    new Collision.Waterbox()
                };
                wblist.AddRange(_currentScene.GetActiveCollision().Waterboxes);

                _waterboxComboDataBinding = new BindingSource();
                _waterboxComboDataBinding.DataSource = wblist;
                cbWaterboxes.DataSource = _waterboxComboDataBinding;
                cbWaterboxes.DisplayMember = "Description";
                cbWaterboxes.Enabled = true;
            }
            else
            {
                cbWaterboxes.Enabled = tlpExWaterboxes.Visible = false;
                cbWaterboxes.DataSource = null;
            }

            RefreshPathWaypoints();

            if (_currentScene != null && _currentScene.GetActiveSettingsSoundScene() != null)
            {
                cbSceneMetaBGM.Enabled = true;
                cbSceneMetaBGM.ValueMember = "Key";
                cbSceneMetaBGM.DisplayMember = "Value";
                cbSceneMetaBGM.DataSource = new BindingSource() {DataSource = _bgms.OrderBy(x => x.Key).ToList()};
                cbSceneMetaBGM.DataBindings.Clear();
                cbSceneMetaBGM.DataBindings.Add("SelectedValue", _currentScene.GetActiveSettingsSoundScene(),
                    "TrackID");
                nudSceneMetaReverb.Value = _currentScene.GetActiveSettingsSoundScene().Reverb;
                nudSceneMetaNightSFX.Value = _currentScene.GetActiveSettingsSoundScene().NightSfxId;
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

            _collisionDirty = true;
            _waterboxesDirty = true;
        }

        private void RefreshWaypointPathList(Waypoints wp)
        {
            if (wp == null) return;

            var pathlist = new List<PathHeader> {new PathHeader()};
            pathlist.AddRange(wp.Paths);

            _waypointPathComboDataBinding = new BindingSource {DataSource = pathlist};
            cbPathHeaders.DataSource = _waypointPathComboDataBinding;
            cbPathHeaders.DisplayMember = "Description";
            cbPathHeaders.Enabled = true;
        }

        private void RefreshPathWaypoints()
        {
            if (ActivePathHeader != null && ActivePathHeader.Points != null)
            {
                dgvPathWaypoints.Enabled = true;
                dgvPathWaypoints.DataSource = new BindingSource() {DataSource = ActivePathHeader.Points};
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
                    if (ushort.TryParse((ishex ? str.Substring(2) : str),
                        (ishex
                            ? System.Globalization.NumberStyles.AllowHexSpecifier
                            : System.Globalization.NumberStyles.None),
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
                if (_currentScene != (e.Node.Tag as ISceneTableEntry))
                {
                    _currentScene = (e.Node.Tag as ISceneTableEntry);
                    _currentScene.SetCurrentSceneHeader(_currentScene.GetSceneHeaders()[0]);
                    _currentEnvSettings = _currentScene.GetActiveEnvSettings().EnvSettingList.First();
                }

                _currentRoom = null;
                _currentRoomTriangle = null;
                _currentRoomVertex = null;
            }
            else if (e.Node.Tag is HeaderPair)
            {
                var hp = (e.Node.Tag as HeaderPair);

                if (hp.SceneHeader.Parent != _currentScene) _currentScene = (hp.SceneHeader.Parent as ISceneTableEntry);
                _currentScene.SetCurrentSceneHeader(hp.SceneHeader);
                _currentEnvSettings = _currentScene.GetActiveEnvSettings().EnvSettingList.First();

                _currentRoom = null;
                _currentRoomTriangle = null;
                _currentRoomVertex = null;
            }
            else if (e.Node.Tag is RoomInfoClass)
            {
                var hp = (e.Node.Parent.Tag as HeaderPair);

                if (hp.SceneHeader.Parent != _currentScene) _currentScene = (hp.SceneHeader.Parent as ISceneTableEntry);
                _currentScene.SetCurrentSceneHeader(hp.SceneHeader);
                _currentEnvSettings = _currentScene.GetActiveEnvSettings().EnvSettingList.First();

                _currentRoom = (e.Node.Tag as RoomInfoClass);
                if (hp.SceneHeader.Number < _currentRoom.Headers.Count)
                    _currentRoom.CurrentRoomHeader = _currentRoom.Headers[hp.SceneHeader.Number];

                _currentRoomTriangle = null;
                _currentRoomVertex = null;
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

                var node = tree.GetNodeAt(pt);
                if (node != null)
                {
                    if (node.Bounds.Contains(pt))
                    {
                        tree.SelectedNode = node;
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
            _mainFormConfig.SupportsCreateShader = Initialization.SupportsFunction("glCreateShader");
            _mainFormConfig.SupportsGenProgramsArb = Initialization.SupportsFunction("glGenProgramsARB");

            var extErrorMessages = new StringBuilder();
            var extMissAll = new List<string>();

            var extMissGeneral = Initialization.CheckForExtensions(MainFormConstants.RequiredOglExtensionsGeneral);
            extMissAll.AddRange(extMissGeneral);
            if (extMissGeneral.Contains("GL_ARB_multisample"))
            {
                enableAntiAliasingToolStripMenuItem.Checked = Configuration.EnableAntiAliasing = false;
                enableAntiAliasingToolStripMenuItem.Enabled = false;
                extErrorMessages.AppendLine("Multisampling is not supported. Anti-aliasing support has been disabled.");
            }

            var extMissCombinerGeneral =
                Initialization.CheckForExtensions(MainFormConstants.RequiredOglExtensionsCombinerGeneral);
            extMissAll.AddRange(extMissCombinerGeneral);
            if (extMissCombinerGeneral.Contains("GL_ARB_multitexture"))
            {
                DisableCombiner(true, true);
                extErrorMessages.AppendLine(
                    "Multitexturing is not supported. Combiner emulation has been disabled and correct graphics rendering cannot be guaranteed.");
            }
            else
            {
                var extMissArbCombiner =
                    Initialization.CheckForExtensions(MainFormConstants.RequiredOglExtensionsArbCombiner);
                extMissAll.AddRange(extMissArbCombiner);
                if (extMissArbCombiner.Count > 0 || !_mainFormConfig.SupportsGenProgramsArb)
                {
                    extErrorMessages.AppendLine(
                        "ARB Fragment Programs are not supported. ARB Assembly Combiner has been disabled.");
                }

                var extMissGlslCombiner =
                    Initialization.CheckForExtensions(MainFormConstants.RequiredOglExtensionsGlslCombiner);
                extMissAll.AddRange(extMissGlslCombiner);
                if (extMissGlslCombiner.Count > 0)
                {
                    extErrorMessages.AppendLine(
                        "OpenGL Shading Language is not supported. GLSL Combiner has been disabled.");
                }

                DisableCombiner((extMissArbCombiner.Count > 0 || !_mainFormConfig.SupportsGenProgramsArb),
                    (extMissGlslCombiner.Count > 0));
            }

            if (extMissAll.Count > 0 || !_mainFormConfig.SupportsGenProgramsArb)
            {
                if (!Configuration.ShownExtensionWarning)
                {
                    Configuration.ShownExtensionWarning = true;

                    var sb = new StringBuilder();

                    if (extMissAll.Count > 0)
                    {
                        sb.AppendFormat("The following OpenGL Extension{0} not supported by your hardware:\n",
                            ((extMissAll.Count - 1) > 0 ? "s are" : " is"));
                        sb.AppendLine();
                        foreach (var str in extMissAll) sb.AppendFormat("* {0}\n", str);
                        sb.AppendLine();
                    }

                    if (!_mainFormConfig.SupportsGenProgramsArb)
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
            if ((arb && CurrentCombinerType == CombinerTypes.ArbCombiner) ||
                (glsl && CurrentCombinerType == CombinerTypes.GlslCombiner))
                CurrentCombinerType = CombinerTypes.None;

            foreach (ToolStripMenuItem tsmi in combinerTypeToolStripMenuItem.DropDownItems)
            {
                if (tsmi.Tag is CombinerTypes &&
                    ((((CombinerTypes) tsmi.Tag) == CombinerTypes.ArbCombiner && arb) ||
                     (((CombinerTypes) tsmi.Tag) == CombinerTypes.GlslCombiner && glsl)))
                {
                    tsmi.Enabled = false;
                    tsmi.Checked = false;
                }
            }
        }

        private void customGLControl_Load(object sender, EventArgs e)
        {
            SettingsGuiInit();

            StartupExtensionChecks();

            Initialization.SetDefaults();

            //            _gl = new TextPrinter(new Font("Verdana", 9.0f, FontStyle.Bold));
            //            _camera = new Camera();
            //            _fpsMonitor = new FPSMonitor();

            _graphicsRenderingSettings.OglSceneScale = 0.02;

            _ready = true;
        }

        private void customGLControl_Paint(object sender, PaintEventArgs e)
        {
            if (!_ready) return;

            try
            {
                _fpsMonitor.Update();

                RenderInit(((GLControl) sender).ClientRectangle, Color.LightBlue);

                if (_baseRom != null && _baseRom.Loaded)
                {
                    /* Scene/rooms */
                    RenderScene();

                    /* Prepare for actors */
                    GL.PushAttrib(AttribMask.AllAttribBits);
                    GL.Disable(EnableCap.Texture2D);
                    GL.Disable(EnableCap.Lighting);
                    if (_mainFormConfig.SupportsGenProgramsArb) GL.Disable((EnableCap) All.FragmentProgram);
                    if (_mainFormConfig.SupportsCreateShader) GL.UseProgram(0);
                    {
                        /* Room actors */
                        if (Configuration.RenderRoomActors && _currentRoom != null &&
                            _currentRoom.ActiveRoomActorData != null)
                            foreach (var ac in _currentRoom.ActiveRoomActorData.ActorList)
                                ac.Render(ac == (cbActors.SelectedItem as Actors.Entry) &&
                                          cbActors.Visible
                                    ? PickableObjectRenderType.Selected
                                    : PickableObjectRenderType.Normal);

                        /* Spawn points */
                        if (Configuration.RenderSpawnPoints && _currentScene != null &&
                            _currentScene.GetActiveSpawnPointData() != null)
                            foreach (var ac in _currentScene.GetActiveSpawnPointData().ActorList)
                                ac.Render(ac == (cbSpawnPoints.SelectedItem as Actors.Entry) &&
                                          cbSpawnPoints.Visible
                                    ? PickableObjectRenderType.Selected
                                    : PickableObjectRenderType.Normal);

                        /* Transitions */
                        if (Configuration.RenderTransitions && _currentScene != null &&
                            _currentScene.GetActiveTransitionData() != null)
                            foreach (var ac in _currentScene.GetActiveTransitionData().ActorList)
                                ac.Render(ac == (cbTransitions.SelectedItem as Actors.Entry) &&
                                          cbTransitions.Visible
                                    ? PickableObjectRenderType.Selected
                                    : PickableObjectRenderType.Normal);

                        /* Path waypoints */
                        if (Configuration.RenderPathWaypoints && ActivePathHeader != null &&
                            ActivePathHeader.Points != null)
                        {
                            /* Link waypoints? */
                            if (Configuration.LinkAllWPinPath)
                            {
                                GL.LineWidth(4.0f);
                                GL.Color3(0.25, 0.5, 1.0);

                                GL.Begin(PrimitiveType.LineStrip);
                                foreach (var wp in ActivePathHeader.Points) GL.Vertex3(wp.X, wp.Y, wp.Z);
                                GL.End();
                            }

                            var selwp = (dgvPathWaypoints.SelectedCells.Count != 0
                                ? dgvPathWaypoints.SelectedCells[0].OwningRow.DataBoundItem as Waypoint
                                : null);
                            foreach (var wp in ActivePathHeader.Points)
                                wp.Render(wp == selwp && cbPathHeaders.Visible
                                    ? PickableObjectRenderType.Selected
                                    : PickableObjectRenderType.Normal);
                        }
                    }
                    GL.PopAttrib();

                    /* Collision */
                    if (Configuration.RenderCollision && _currentScene != null &&
                        _currentScene.GetActiveCollision() != null)
                    {
                        if (!_collisionDirty && _collisionDl != null)
                        {
                            _collisionDl.Render();
                        }
                        else
                        {
                            _collisionDirty = false;

                            if (_collisionDl != null) _collisionDl.Dispose();
                            _collisionDl = new DisplayList(ListMode.CompileAndExecute);

                            GL.PushAttrib(AttribMask.AllAttribBits);
                            GL.Disable(EnableCap.Texture2D);
                            GL.Disable(EnableCap.Lighting);
                            if (_mainFormConfig.SupportsGenProgramsArb) GL.Disable((EnableCap) All.FragmentProgram);
                            if (_mainFormConfig.SupportsCreateShader) GL.UseProgram(0);
                            GL.DepthRange(0.0, 0.99999);

                            if (Configuration.RenderCollisionAsWhite) GL.Color4(1.0, 1.0, 1.0, 0.5);

                            GL.Begin(PrimitiveType.Triangles);
                            foreach (var poly in _currentScene.GetActiveCollision().Polygons)
                            {
                                if (poly == _currentCollisionPolygon && cbCollisionPolys.Visible)
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
                            foreach (var poly in _currentScene.GetActiveCollision().Polygons)
                                poly.Render(PickableObjectRenderType.NoColor);
                            GL.End();

                            GL.PopAttrib();

                            _collisionDl.End();
                        }
                    }

                    /* Waterboxes */
                    if (Configuration.RenderWaterboxes && _currentScene != null &&
                        _currentScene.GetActiveCollision() != null)
                    {
                        if (!_waterboxesDirty && _waterboxDl != null)
                        {
                            _waterboxDl.Render();
                        }
                        else
                        {
                            _waterboxesDirty = false;

                            if (_waterboxDl != null) _waterboxDl.Dispose();
                            _waterboxDl = new DisplayList(ListMode.CompileAndExecute);

                            GL.PushAttrib(AttribMask.AllAttribBits);
                            GL.Disable(EnableCap.Texture2D);
                            GL.Disable(EnableCap.Lighting);
                            if (_mainFormConfig.SupportsGenProgramsArb) GL.Disable((EnableCap) All.FragmentProgram);
                            if (_mainFormConfig.SupportsCreateShader) GL.UseProgram(0);
                            GL.Disable(EnableCap.CullFace);

                            GL.Begin(PrimitiveType.Quads);
                            foreach (var wb in _currentScene.GetActiveCollision().Waterboxes)
                            {
                                var alpha = ((Configuration.ShowWaterboxesPerRoom && _currentRoom != null &&
                                              (wb.RoomNumber != _currentRoom.Number && wb.RoomNumber != 0x3F))
                                    ? 0.1
                                    : 0.5);

                                if (wb == _currentWaterbox && cbWaterboxes.Visible)
                                    GL.Color4(0.5, 1.0, 0.5, alpha);
                                else
                                    GL.Color4(0.0, 0.5, 1.0, alpha);

                                wb.Render(PickableObjectRenderType.Normal);
                            }

                            GL.End();

                            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);
                            GL.LineWidth(2.0f);
                            GL.Begin(PrimitiveType.Quads);
                            foreach (var wb in _currentScene.GetActiveCollision().Waterboxes)
                            {
                                var alpha = ((Configuration.ShowWaterboxesPerRoom && _currentRoom != null &&
                                              (wb.RoomNumber != _currentRoom.Number && wb.RoomNumber != 0x3F))
                                    ? 0.1
                                    : 0.5);
                                GL.Color4(0.0, 0.0, 0.0, alpha);
                                wb.Render(PickableObjectRenderType.Normal);
                            }

                            GL.End();

                            GL.Enable(EnableCap.CullFace);
                            GL.PopAttrib();

                            GL.Color4(Color.White);

                            _waterboxDl.End();
                        }
                    }

                    /* Render selected room triangle overlay */
                    if (_currentRoomTriangle != null && !Configuration.RenderCollision)
                    {
                        _currentRoomTriangle.Render(PickableObjectRenderType.Normal);
                    }

                    /* 2D text overlay */
                    RenderTextOverlay();
                }

                ((GLControl) sender).SwapBuffers();
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
            Initialization.CreateViewportAndProjection(Initialization.ProjectionTypes.Perspective, rect, 0.001f,
                _currentEnvSettings?.DrawDistance / 50.0f ?? 300.0f);
            _camera.RenderPosition();
            GL.Scale(_graphicsRenderingSettings.OglSceneScale, _graphicsRenderingSettings.OglSceneScale, _graphicsRenderingSettings.OglSceneScale);
        }

        private void RenderScene()
        {
            GL.PushAttrib(AttribMask.AllAttribBits);

            if (_currentScene != null && _currentEnvSettings != null) _currentEnvSettings.CreateLighting();

            if (_currentRoom != null && _currentRoom.ActiveMeshHeader != null)
            {
                /* Render single room */
                RenderMeshHeader(_currentRoom.ActiveMeshHeader);
                _displayListsDirty = false;
            }
            else if (_currentScene != null && _currentScene.GetCurrentSceneHeader() != null)
            {
                /* Render all rooms */
                foreach (var ric in
                    (_currentScene.GetCurrentSceneHeader().Commands
                        .FirstOrDefault(x => x.Command == CommandTypeIDs.Rooms) as Rooms).RoomInformation)
                {
                    _baseRom.SegmentMapping.Remove((byte) 0x02);
                    _baseRom.SegmentMapping.Remove((byte) 0x03);
                    _baseRom.SegmentMapping.Add((byte) 0x02, (ric.Parent as ISceneTableEntry).GetData());
                    _baseRom.SegmentMapping.Add((byte) 0x03, ric.Data);

                    if (ric.Headers.Count == 0) continue;

                    var mh =
                        (ric.Headers[0].Commands
                            .FirstOrDefault(x => x.Command == CommandTypeIDs.MeshHeader) as MeshHeader);
                    if (mh == null) continue;

                    RenderMeshHeader(mh);
                }

                _displayListsDirty = false;
            }

            GL.PopAttrib();
        }

        private void RenderMeshHeader(MeshHeader mh)
        {
            if (mh.DLs == null || _displayListsDirty || mh.CachedWithTextures != Configuration.RenderTextures ||
                mh.CachedWithCombinerType != Configuration.CombinerType)
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
            _textPrinter.Begin(_glRendererControl);
            if (!Configuration.OglvSync)
                _textPrinter.Print(
                    string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:0.00} FPS", _fpsMonitor.Value),
                    new Vector2d(10.0, 10.0), Color.FromArgb(128, Color.Black));
            _textPrinter.Flush();
        }

        private IPickableObject TryPickObject(int x, int y, bool moveable)
        {
            if (_currentScene == null) return null;

            IPickableObject picked = null;
            var pickobjs = new List<IPickableObject>();

            /* Room model triangle vertices */
            if (!Configuration.RenderCollision && _currentRoomTriangle != null)
                pickobjs.AddRange(_currentRoomTriangle.Vertices);

            /* Room model triangles */
            if (_currentRoom != null && _currentRoom.ActiveMeshHeader != null && !Configuration.RenderCollision &&
                _currentRoom.ActiveMeshHeader.DLs.Count > 0 && _currentRoom.ActiveMeshHeader.DLs[0].Triangles.Count > 0)
            {
                if (_currentRoom.ActiveMeshHeader.DLs[0].Triangles[0].IsMoveable == moveable)
                {
                    foreach (var dlex in _currentRoom.ActiveMeshHeader.DLs)
                        pickobjs.AddRange(dlex.Triangles);
                }
            }

            /* Rooms */
            if (_allMeshHeaders != null && _currentRoom == null && !Configuration.RenderCollision &&
                _allMeshHeaders.Count > 0 && _allMeshHeaders[0].IsMoveable == moveable)
                pickobjs.AddRange(_allMeshHeaders);

            /* Room actors */
            if (_currentRoom != null && _currentRoom.ActiveRoomActorData != null && Configuration.RenderRoomActors &&
                _currentRoom.ActiveRoomActorData.ActorList.Count > 0 &&
                _currentRoom.ActiveRoomActorData.ActorList[0].IsMoveable == moveable)
                pickobjs.AddRange(_currentRoom.ActiveRoomActorData.ActorList);

            /* Spawn points */
            if (_currentScene.GetActiveSpawnPointData() != null && Configuration.RenderSpawnPoints &&
                _currentScene.GetActiveSpawnPointData().ActorList.Count > 0 &&
                _currentScene.GetActiveSpawnPointData().ActorList[0].IsMoveable == moveable)
                pickobjs.AddRange(_currentScene.GetActiveSpawnPointData().ActorList);

            /* Transition actors */
            if (_currentScene.GetActiveTransitionData() != null && Configuration.RenderTransitions &&
                _currentScene.GetActiveTransitionData().ActorList.Count > 0 &&
                _currentScene.GetActiveTransitionData().ActorList[0].IsMoveable == moveable)
                pickobjs.AddRange(_currentScene.GetActiveTransitionData().ActorList);

            /* Waypoints */
            if (ActivePathHeader != null && ActivePathHeader.Points != null && Configuration.RenderPathWaypoints &&
                ActivePathHeader.Points.Count > 0 &&
                ActivePathHeader.Points[0].IsMoveable == moveable)
                pickobjs.AddRange(ActivePathHeader.Points);

            /* Waterboxes */
            if (_currentScene.GetActiveCollision() != null && Configuration.RenderWaterboxes &&
                _currentScene.GetActiveCollision().Waterboxes.Count > 0 &&
                _currentScene.GetActiveCollision().Waterboxes[0].IsMoveable == moveable)
                pickobjs.AddRange(_currentScene.GetActiveCollision().Waterboxes);

            /* Collision polygons */
            if (_currentScene.GetActiveCollision() != null && Configuration.RenderCollision &&
                _currentScene.GetActiveCollision().Polygons.Count > 0 &&
                _currentScene.GetActiveCollision().Polygons[0].IsMoveable == moveable)
                pickobjs.AddRange(_currentScene.GetActiveCollision().Polygons);

            if ((picked = DoPicking(x, y, pickobjs)) != null)
            {
                /* Wrong mode? */
                if (picked.IsMoveable != moveable) return null;

                /* What's been picked...? */
                if (picked is Waypoint)
                {
                    dgvPathWaypoints.ClearSelection();
                    var row = dgvPathWaypoints.Rows.OfType<DataGridViewRow>()
                        .FirstOrDefault(xx => xx.DataBoundItem == picked as Waypoint);
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
                    _currentCollisionPolygon = (picked as Collision.Polygon);

                    cbCollisionPolys.SelectedItem = _currentCollisionPolygon;
                    tabControl1.SelectTab(tpCollision);
                }
                else if (picked is Collision.Waterbox)
                {
                    _currentWaterbox = (picked as Collision.Waterbox);

                    cbWaterboxes.SelectedItem = _currentWaterbox;
                    tabControl1.SelectTab(tpWaterboxes);
                }
                else if (picked is MeshHeader)
                {
                    tvScenes.SelectedNode = tvScenes.FlattenTree().FirstOrDefault(xx =>
                        xx.Tag == ((picked as MeshHeader).Parent as RoomInfoClass) &&
                        (xx.Parent.Tag as HeaderPair).SceneHeader.Number ==
                        _currentScene.GetCurrentSceneHeader().Number);
                }
                else if (picked is DisplayListEx.Triangle)
                {
                    if (_currentRoomTriangle != picked)
                    {
                        if (_currentRoomTriangle != null) _currentRoomTriangle.SelectedVertex = null;

                        _currentRoomTriangle = (picked as DisplayListEx.Triangle);
                        _currentRoomVertex = null;
                    }
                }
                else if (picked is SimpleF3DEX2.Vertex)
                {
                    _currentRoomTriangle.SelectedVertex = _currentRoomVertex = (picked as SimpleF3DEX2.Vertex);
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
            if (_mainFormConfig.SupportsGenProgramsArb) GL.Disable((EnableCap) All.FragmentProgram);
            if (_mainFormConfig.SupportsCreateShader) GL.UseProgram(0);
            GL.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);

            RenderInit(_glRendererControl.ClientRectangle, Color.Black);
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
            var argb = (uint) ((pixel[3] << 24) | (pixel[0] << 16) | (pixel[1] << 8) | pixel[2]);

            return objlist.FirstOrDefault(xx => (xx.PickColor.ToArgb() & 0xFFFFFF) == (int) (argb & 0xFFFFFF));
        }

        private void customGLControl_MouseDown(object sender, MouseEventArgs e)
        {
//            _camera.ButtonsDown |= e.Button;
//
//            switch (CurrentToolMode)
//            {
//                case ToolModes.Camera:
//                {
//                    /* Camera only */
//                    if (Convert.ToBoolean(_camera.ButtonsDown & MouseButtons.Left))
//                        _camera.MouseCenter(new Vector2d(e.X, e.Y));
//                    break;
//                }
//
//                case ToolModes.MoveableObjs:
//                case ToolModes.StaticObjs:
//                {
//                    /* Object picking */
//                    if (Convert.ToBoolean(_camera.ButtonsDown & MouseButtons.Left) ||
//                        Convert.ToBoolean(_camera.ButtonsDown & MouseButtons.Middle))
//                    {
//                        _pickedObject = TryPickObject(e.X, e.Y, (CurrentToolMode == ToolModes.MoveableObjs));
//                        if (_pickedObject == null)
//                        {
//                            /* No pick? Camera */
//                            _camera.MouseCenter(new Vector2d(e.X, e.Y));
//                        }
//                        else
//                        {
//                            /* Object found */
//                            _pickObjLastPosition = _pickObjPosition = new Vector2d(e.X, e.Y);
//                            _pickObjDisplacement = Vector2d.Zero;
//                            ((Control) sender).Focus();
//
//                            /* Mark GLDLs as dirty? */
//                            _collisionDirty = (_pickedObject is Collision.Polygon);
//                            _waterboxesDirty = (_pickedObject is Collision.Waterbox);
//
//                            /* Static object? Camera */
//                            if (CurrentToolMode == ToolModes.StaticObjs)
//                            {
//                                _camera.MouseCenter(new Vector2d(e.X, e.Y));
//                                /*if (e.Clicks == 2 && currentRoomVertex != null)
//                                {
//                                    EditVertexColor(currentRoomVertex);
//                                }*/
//                            }
//                        }
//                    }
//                    else if (Convert.ToBoolean(_camera.ButtonsDown & MouseButtons.Right))
//                    {
//                        _pickedObject = TryPickObject(e.X, e.Y, (CurrentToolMode == ToolModes.MoveableObjs));
//                        if (_pickedObject != null)
//                        {
//                            if (CurrentToolMode == ToolModes.MoveableObjs)
//                            {
//                                if (_pickedObject is Actors.Entry)
//                                {
//                                    var ac = (_pickedObject as Actors.Entry);
//                                    /* Determine what menu entries should be enabled */
//                                    xAxisToolStripMenuItem.Enabled =
//                                        !(ac.Definition.Items.FirstOrDefault(x => x.Usage == Usages.RotationX) == null);
//                                    yAxisToolStripMenuItem.Enabled =
//                                        !(ac.Definition.Items.FirstOrDefault(x => x.Usage == Usages.RotationY) == null);
//                                    zAxisToolStripMenuItem.Enabled =
//                                        !(ac.Definition.Items.FirstOrDefault(x => x.Usage == Usages.RotationZ) == null);
//                                    rotateToolStripMenuItem.Enabled =
//                                        (xAxisToolStripMenuItem.Enabled || yAxisToolStripMenuItem.Enabled ||
//                                         zAxisToolStripMenuItem.Enabled);
//                                }
//                                else
//                                    rotateToolStripMenuItem.Enabled = false;
//
//                                cmsMoveableObjectEdit.Show(((Control) sender).PointToScreen(e.Location));
//                            }
//                            else if (CurrentToolMode == ToolModes.StaticObjs)
//                            {
//                                if (_pickedObject is SimpleF3DEX2.Vertex)
//                                {
//                                    cmsVertexEdit.Show(((Control) sender).PointToScreen(e.Location));
//                                }
//                            }
//                        }
//                    }
//
//                    break;
//                }
//            }
        }

        private void customGLControl_MouseUp(object sender, MouseEventArgs e)
        {
           // _camera.ButtonsDown &= ~e.Button;
        }

        private void customGLControl_MouseMove(object sender, MouseEventArgs e)
        {
//            switch (CurrentToolMode)
//            {
//                case ToolModes.Camera:
//                {
//                    if (Convert.ToBoolean(e.Button & MouseButtons.Left))
//                        _camera.MouseMove(new Vector2d(e.X, e.Y));
//                    break;
//                }
//
//                case ToolModes.MoveableObjs:
//                {
//                    if (!Convert.ToBoolean(e.Button & MouseButtons.Left) &&
//                        !Convert.ToBoolean(e.Button & MouseButtons.Middle)) break;
//
//                    if (_pickedObject == null)
//                        _camera.MouseMove(new Vector2d(e.X, e.Y));
//                    else
//                    {
//                        // TODO  make this not shitty; try to get the "new method" to work with anything that's not at (0,0,0)
//
//                        /* Speed modifiers */
//                        var movemod = 3.0;
//                        if (_keysDown[(ushort) Keys.Space]) movemod = 8.0;
//                        else if (_keysDown[(ushort) Keys.ShiftKey]) movemod = 1.0;
//
//                        /* Determine mouse position and displacement */
//                        _pickObjPosition = new Vector2d(e.X, e.Y);
//                        _pickObjDisplacement = ((_pickObjPosition - _pickObjLastPosition) * movemod);
//
//                        /* No displacement? Exit */
//                        if (_pickObjDisplacement == Vector2d.Zero) return;
//
//                        /* Calculate camera rotation */
//                        var camXRotd = _camera.GetCurrentPosition().X * (Math.PI / 180);
//                        var camYRotd = _camera.GetCurrentRotation().Y * (Math.PI / 180);
//
//                        /* WARNING: Cam position stuff below is "I dunno why it works, but it does!" */
//                        var objpos = _pickedObject.Position;
//
//                        if (Convert.ToBoolean(e.Button & MouseButtons.Middle) ||
//                            (Convert.ToBoolean(e.Button & MouseButtons.Left) && _keysDown[(ushort) Keys.ControlKey]))
//                        {
//                            /* Middle mouse button OR left button + Ctrl -> move forward/backward */
//                            objpos.X += ((Math.Sin(camYRotd) * -_pickObjDisplacement.Y));
//                            objpos.Z -= ((Math.Cos(camYRotd) * -_pickObjDisplacement.Y));
//
//                            _camera.TransformPosition((x, y, z) =>
//                            {
//                                x -= ((Math.Sin(camYRotd) *
//                                       (-_pickObjDisplacement.Y * _camera.CameraCoeff * _camera.Sensitivity) /
//                                       1.25));
//                                z += ((Math.Cos(camYRotd) *
//                                       (-_pickObjDisplacement.Y * _camera.CameraCoeff * _camera.Sensitivity) /
//                                       1.25));
//                            });
//                        }
//                        else if (Convert.ToBoolean(e.Button & MouseButtons.Left))
//                        {
//                            /* Left mouse button -> move up/down/left/right */
//                            objpos.X += ((Math.Cos(camYRotd) * _pickObjDisplacement.X));
//                            objpos.Y -= (_pickObjDisplacement.Y);
//                            objpos.Z += ((Math.Sin(camYRotd) * _pickObjDisplacement.X));
//
//
//                            _camera.TransformPosition((x, y, z) =>
//                            {
//                                x -= ((Math.Cos(camYRotd) * _pickObjDisplacement.X)) * 0.02;
//                                y += (_pickObjDisplacement.Y) * 0.02;
//                                z -= ((Math.Sin(camYRotd) * _pickObjDisplacement.X)) * 0.02;
//                            });
//                           
//                        }
//
//                        /* Round away decimal places (mainly for waypoints) */
//                        objpos.X = Math.Round(objpos.X, 0);
//                        objpos.Y = Math.Round(objpos.Y, 0);
//                        objpos.Z = Math.Round(objpos.Z, 0);
//                        _pickedObject.Position = objpos;
//
//                        /* Refresh GUI according to type of picked object */
//                        if (_pickedObject is Waypoint)
//                        {
//                            foreach (DataGridViewCell cell in dgvPathWaypoints.SelectedCells)
//                            {
//                                for (var i = 0; i < dgvPathWaypoints.ColumnCount; i++)
//                                    dgvPathWaypoints.UpdateCellValue(i, cell.RowIndex);
//                            }
//                        }
//                        else if (_pickedObject is Actors.Entry)
//                        {
//                            var actor = (_pickedObject as Actors.Entry);
//
//                            if (actor.IsSpawnPoint)
//                                XmlActorDefinitionReader.RefreshActorPositionRotation(actor, tlpExSpawnPoints);
//                            else if (actor.IsTransitionActor)
//                                XmlActorDefinitionReader.RefreshActorPositionRotation(actor, tlpExTransitions);
//                            else
//                                XmlActorDefinitionReader.RefreshActorPositionRotation(actor, tlpExRoomActors);
//                        }
//                        else if (_pickedObject is Collision.Waterbox)
//                        {
//                            _waterboxesDirty = true;
//                            RefreshWaterboxControls();
//                        }
//
//                        _pickObjLastPosition = _pickObjPosition;
//
//                        ((Control) sender).Focus();
//                    }
//
//                    break;
//                }
//
//                case ToolModes.StaticObjs:
//                {
//                    if (Convert.ToBoolean(e.Button & MouseButtons.Left) /* && PickedObject == null*/)
//                        _camera.MouseMove(new Vector2d(e.X, e.Y));
//                    break;
//                }
//            }
        }

        private void customGLControl_KeyDown(object sender, KeyEventArgs e)
        {
            _keysDown[(ushort) e.KeyValue] = true;
        }

        private void customGLControl_KeyUp(object sender, KeyEventArgs e)
        {
            _keysDown[(ushort) e.KeyValue] = false;
        }

        private void customGLControl_Leave(object sender, EventArgs e)
        {
            _keysDown.Fill(new bool[] {false});
        }

        private void EditVertexColor(SimpleF3DEX2.Vertex vertex)
        {
            var cdlg = new ColorPickerDialog(Color.FromArgb(vertex.Colors[3], vertex.Colors[0], vertex.Colors[1],
                vertex.Colors[2]));

            if (cdlg.ShowDialog() == DialogResult.OK)
            {
                vertex.Colors[0] = cdlg.Color.R;
                vertex.Colors[1] = cdlg.Color.G;
                vertex.Colors[2] = cdlg.Color.B;
                vertex.Colors[3] = cdlg.Color.A;

                // KLUDGE! Write to local room data HERE for rendering, write to ROM in SimpleF3DEX2.Vertex, the vertex.Store(...) below
                _currentRoom.Data[(vertex.Address & 0xFFFFFF) + 12] = vertex.Colors[0];
                _currentRoom.Data[(vertex.Address & 0xFFFFFF) + 13] = vertex.Colors[1];
                _currentRoom.Data[(vertex.Address & 0xFFFFFF) + 14] = vertex.Colors[2];
                _currentRoom.Data[(vertex.Address & 0xFFFFFF) + 15] = vertex.Colors[3];

                vertex.Store(_mainFormConfig.IndividualFileMode ? null : _baseRom.Data, (int) _currentRoom.Start);

                _displayListsDirty = true;
            }
        }

        private void tabControl1_Selecting(object sender, TabControlCancelEventArgs e)
        {
            _collisionDirty = (e.Action == TabControlAction.Selecting && e.TabPage == tpCollision ||
                               _lastTabPage == tpCollision);
            _waterboxesDirty = (e.Action == TabControlAction.Selecting && e.TabPage == tpWaterboxes ||
                                _lastTabPage == tpWaterboxes);

            _lastTabPage = e.TabPage;
        }

        private void nudSceneMetaReverb_ValueChanged(object sender, EventArgs e)
        {
            if (_currentScene != null && _currentScene.GetActiveSettingsSoundScene() != null)
                _currentScene.GetActiveSettingsSoundScene().Reverb = (byte) ((NumericUpDown) sender).Value;
        }

        private void nudSceneMetaNightSFX_ValueChanged(object sender, EventArgs e)
        {
            if (_currentScene != null && _currentScene.GetActiveSettingsSoundScene() != null)
                _currentScene.GetActiveSettingsSoundScene().NightSfxId = (byte) ((NumericUpDown) sender).Value;
        }

        private void RefreshRoomActorList()
        {
            var actorlist = new List<Actors.Entry>();
            actorlist.Add(new Actors.Entry());
            actorlist.AddRange(_currentRoom.ActiveRoomActorData.ActorList);

            _roomActorComboBinding = new BindingSource();
            _roomActorComboBinding.DataSource = actorlist;
            cbActors.DataSource = _roomActorComboBinding;
            cbActors.DisplayMember = "Description";
            cbActors.Enabled = true;
        }

        private void RefreshTransitionList()
        {
            var actorlist = new List<Actors.Entry>();
            actorlist.Add(new Actors.Entry());
            actorlist.AddRange(_currentScene.GetActiveTransitionData().ActorList);

            _transitionComboBinding = new BindingSource();
            _transitionComboBinding.DataSource = actorlist;
            cbTransitions.DataSource = _transitionComboBinding;
            cbTransitions.DisplayMember = "Description";
            cbTransitions.Enabled = true;
        }

        private void RefreshSpawnPointList()
        {
            var actorlist = new List<Actors.Entry>();
            actorlist.Add(new Actors.Entry());
            actorlist.AddRange(_currentScene.GetActiveSpawnPointData().ActorList);

            _spawnPointComboBinding = new BindingSource();
            _spawnPointComboBinding.DataSource = actorlist;
            cbSpawnPoints.DataSource = _spawnPointComboBinding;
            cbSpawnPoints.DisplayMember = "Description";
            cbSpawnPoints.Enabled = true;
        }

        private void cbActors_SelectedIndexChanged(object sender, EventArgs e)
        {
            var ac = ((ComboBox) sender).SelectedItem as Actors.Entry;
            _pickedObject = (ac as IPickableObject);

            XmlActorDefinitionReader.CreateActorEditingControls(ac, tlpExRoomActors, () =>
            {
                var idx = ((ComboBox) sender).SelectedIndex;
                RefreshRoomActorList();
                ((ComboBox) sender).SelectedIndex = idx;
                SelectActorNumberControl(tlpExRoomActors);
            }, individual: _mainFormConfig.IndividualFileMode);
        }

        private void cbTransitions_SelectedIndexChanged(object sender, EventArgs e)
        {
            var ac = ((ComboBox) sender).SelectedItem as Actors.Entry;
            _pickedObject = (ac as IPickableObject);

            Rooms rooms = null;
            if (_currentScene != null && _currentScene.GetCurrentSceneHeader() != null)
                rooms = _currentScene.GetCurrentSceneHeader().Commands
                    .FirstOrDefault(x => x.Command == CommandTypeIDs.Rooms) as Rooms;

            XmlActorDefinitionReader.CreateActorEditingControls(ac, tlpExTransitions, () =>
            {
                var idx = ((ComboBox) sender).SelectedIndex;
                RefreshTransitionList();
                ((ComboBox) sender).SelectedIndex = idx;
                SelectActorNumberControl(tlpExTransitions);
            }, (rooms != null ? rooms.RoomInformation : null), _mainFormConfig.IndividualFileMode);
        }

        private void cbSpawnPoints_SelectedIndexChanged(object sender, EventArgs e)
        {
            var ac = ((ComboBox) sender).SelectedItem as Actors.Entry;
            _pickedObject = (ac as IPickableObject);

            XmlActorDefinitionReader.CreateActorEditingControls(ac, tlpExSpawnPoints, () =>
            {
                var idx = ((ComboBox) sender).SelectedIndex;
                RefreshSpawnPointList();
                ((ComboBox) sender).SelectedIndex = idx;
                SelectActorNumberControl(tlpExSpawnPoints);
            }, individual: _mainFormConfig.IndividualFileMode);
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
            ActivePathHeader = (((ComboBox) sender).SelectedItem as PathHeader);
        }

        private void dgvPathWaypoints_RowPostPaint(object sender, DataGridViewRowPostPaintEventArgs e)
        {
            using (var b = new SolidBrush(((DataGridView) sender).RowHeadersDefaultCellStyle.ForeColor))
            {
                e.Graphics.DrawString((e.RowIndex + 1).ToString(), e.InheritedRowStyle.Font, b,
                    e.RowBounds.Location.X + 18, e.RowBounds.Location.Y + 4);
            }
        }

        private void dgvPathWaypoints_SelectionChanged(object sender, EventArgs e)
        {
            var selwp = (dgvPathWaypoints.SelectedCells.Count != 0
                ? dgvPathWaypoints.SelectedCells[0].OwningRow.DataBoundItem as Waypoint
                : null);
            if (selwp == null) return;
            _pickedObject = selwp;
            _collisionDirty = true;
        }

        private void RefreshCollisionPolyAndTypeLists()
        {
            /* Type list */
            var typelist = new List<Collision.PolygonType> {new Collision.PolygonType()};
            typelist.AddRange(_currentScene.GetActiveCollision().PolygonTypes);

            _colPolyTypeDataBinding = new BindingSource {DataSource = typelist};
            cbCollisionPolyTypes.DataSource = _colPolyTypeDataBinding;
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
            polylist.AddRange(_currentScene.GetActiveCollision().Polygons);

            _collisionPolyDataBinding = new BindingSource();
            _collisionPolyDataBinding.DataSource = polylist;
            cbCollisionPolys.SelectedIndex = -1;
            cbCollisionPolys.DataSource = _collisionPolyDataBinding;
            cbCollisionPolys.DisplayMember = "Description";
            cbCollisionPolys.Enabled = true;

            nudColPolyType.Minimum = 0;
            nudColPolyType.Maximum = (_currentScene.GetActiveCollision().PolygonTypes.Count - 1);
            nudColPolyType.Enabled = true;
        }

        private void cbCollisionPolys_SelectedIndexChanged(object sender, EventArgs e)
        {
            _currentCollisionPolygon = (((ComboBox) sender).SelectedItem as Collision.Polygon);
            if (_currentCollisionPolygon == null) return;

            _pickedObject = (_currentCollisionPolygon as IPickableObject);
            _collisionDirty = true;

            lblColPolyType.Visible =
                nudColPolyType.Visible = btnJumpToPolyType.Visible = !_currentCollisionPolygon.IsDummy;
            if (!_currentCollisionPolygon.IsDummy)
            {
                nudColPolyType.Value = _currentCollisionPolygon.PolygonType;
                //TODO more here
            }
        }

        private void nudColPolyType_ValueChanged(object sender, EventArgs e)
        {
            _currentCollisionPolygon.PolygonType = (ushort) ((NumericUpDown) sender).Value;
            _collisionPolyDataBinding.ResetCurrentItem();
        }

        private void btnJumpToPolyType_Click(object sender, EventArgs e)
        {
            if (cbCollisionPolyTypes.Items.Count > 0)
                cbCollisionPolyTypes.SelectedItem =
                    (_colPolyTypeDataBinding.List as List<Collision.PolygonType>).FirstOrDefault(x =>
                        x.Number == _currentCollisionPolygon.PolygonType);
        }

        private void cbCollisionPolyTypes_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (((ComboBox) sender).SelectedItem == null) return;

            _currentColPolygonType = (((ComboBox) sender).SelectedItem as Collision.PolygonType);

            _busy = true;
            RefreshColPolyTypeControls();
            _busy = false;
        }

        private void RefreshColPolyTypeControls()
        {
            txtColPolyRawData.Text = $"0x{_currentColPolygonType.Raw:X16}";
            lblColPolyRawData.Visible = txtColPolyRawData.Visible = !_currentColPolygonType.IsDummy;
            cbColPolyGroundTypes.SelectedItem =
                Collision.PolygonType.GroundTypes.FirstOrDefault(x => x.Value == _currentColPolygonType.GroundTypeID);
            lblColPolyGroundType.Visible = cbColPolyGroundTypes.Visible = !_currentColPolygonType.IsDummy;

            if (!_busy) _colPolyTypeDataBinding.ResetCurrentItem();

            _collisionDirty = true;
        }

        private void txtColPolyRawData_TextChanged(object sender, EventArgs e)
        {
            var txt = (sender as TextBox);
            if (!txt.ContainsFocus) return;

            var ns = (txt.Text.StartsWith("0x")
                ? System.Globalization.NumberStyles.HexNumber
                : System.Globalization.NumberStyles.Integer);
            var valstr = (ns == System.Globalization.NumberStyles.HexNumber ? txt.Text.Substring(2) : txt.Text);
            var newval = ulong.Parse(valstr, ns);

            _currentColPolygonType.Raw = newval;
            RefreshColPolyTypeControls();
        }

        private void cbColPolyGroundTypes_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!(sender as ComboBox).ContainsFocus) return;

            _currentColPolygonType.GroundTypeID =
                (((ComboBox) sender).SelectedItem as Collision.PolygonType.GroundType).Value;
            RefreshColPolyTypeControls();
        }

        #region Waterboxes

        private void RefreshWaterboxControls()
        {
            if (tlpExWaterboxes.Visible = (_currentWaterbox != null && !_currentWaterbox.IsDummy))
            {
                tlpExWaterboxes.SuspendLayout();

                _busy = true;

                txtWaterboxPositionX.Text = $"{_currentWaterbox.Position.X}";
                txtWaterboxPositionY.Text = $"{_currentWaterbox.Position.Y}";
                txtWaterboxPositionZ.Text = $"{_currentWaterbox.Position.Z}";
                txtWaterboxSizeX.Text = $"{_currentWaterbox.SizeXZ.X}";
                txtWaterboxSizeZ.Text = $"{_currentWaterbox.SizeXZ.Y}";
                txtWaterboxProperties.Text = $"0x{_currentWaterbox.Properties:X}";

                if (_roomsForWaterboxSelection != null && _roomsForWaterboxSelection.Count > 0)
                {
                    cbWaterboxRoom.DataSource = _roomsForWaterboxSelection;
                    cbWaterboxRoom.DisplayMember = "Description";
                    cbWaterboxRoom.SelectedItem =
                        _roomsForWaterboxSelection.FirstOrDefault(x => x.Value == _currentWaterbox.RoomNumber);
                }

                _busy = false;

                tlpExWaterboxes.ResumeLayout();
            }

            _waterboxesDirty = true;
        }

        private void cbWaterboxes_SelectedIndexChanged(object sender, EventArgs e)
        {
            _currentWaterbox = (((ComboBox) sender).SelectedItem as Collision.Waterbox);
            if (_currentWaterbox != null)
            {
                _pickedObject = (_currentWaterbox as IPickableObject);
                _waterboxesDirty = true;

                txtWaterboxPositionX.Enabled = txtWaterboxPositionY.Enabled = txtWaterboxPositionZ.Enabled =
                    txtWaterboxSizeX.Enabled = txtWaterboxSizeZ.Enabled = txtWaterboxProperties.Enabled
                        = !_currentWaterbox.IsDummy;
            }

            RefreshWaterboxControls();
        }

        private void ModifyCurrentWaterbox()
        {
            if (_busy) return;

            try
            {
                _currentWaterbox.Position = new Vector3d(double.Parse(txtWaterboxPositionX.Text),
                    double.Parse(txtWaterboxPositionY.Text), double.Parse(txtWaterboxPositionZ.Text));
                _currentWaterbox.SizeXZ =
                    new Vector2d(double.Parse(txtWaterboxSizeX.Text), double.Parse(txtWaterboxSizeZ.Text));
                _currentWaterbox.RoomNumber = (ushort) (cbWaterboxRoom.SelectedItem as Option).Value;

                if (txtWaterboxProperties.Text.StartsWith("0x"))
                    _currentWaterbox.Properties = ushort.Parse(txtWaterboxProperties.Text.Substring(2),
                        System.Globalization.NumberStyles.HexNumber);
                else
                    _currentWaterbox.Properties = ushort.Parse(txtWaterboxProperties.Text);

                _waterboxesDirty = true;
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
            CurrentToolMode++;
        }

        private void deselectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_pickedObject == null) return;

            if (_pickedObject is Actors.Entry)
            {
                var ac = (_pickedObject as Actors.Entry);
                if (ac.IsTransitionActor)
                    cbTransitions.SelectedIndex = 0;
                else if (ac.IsSpawnPoint)
                    cbSpawnPoints.SelectedIndex = 0;
                else
                    cbActors.SelectedIndex = 0;
            }
            else if (_pickedObject is Waypoint)
            {
                dgvPathWaypoints.ClearSelection();
            }
            else if (_pickedObject is Collision.Waterbox)
            {
                cbWaterboxes.SelectedIndex = 0;
            }

            _pickedObject = null;
        }

        public void RotatePickedObject(Vector3d rot)
        {
            if (_pickedObject == null) return;

            if (_pickedObject is Actors.Entry)
            {
                var actor = (_pickedObject as Actors.Entry);
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
            // RotatePickedObject(new Vector3d(8192.0, 0.0, 0.0));

            _mediator.Publish(new RotationCommand
            {
                MainForm = new WeakReference<MainForm>(this),
                Rotation = new Vector3d(8192.0, 0.0, 0.0)
            });
        }

        private void xMinus45DegreesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //RotatePickedObject(new Vector3d(-8192.0, 0.0, 0.0));

            _mediator.Publish(new RotationCommand
            {
                MainForm = new WeakReference<MainForm>(this),
                Rotation = new Vector3d(-8192.0, 0.0, 0.0)
            });
        }

        private void yPlus45DegreesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // RotatePickedObject(new Vector3d(0.0, 8192.0, 0.0));

            _mediator.Publish(new RotationCommand
            {
                MainForm = new WeakReference<MainForm>(this),
                Rotation = new Vector3d(0.0, 8192.0, 0.0)
            });
        }

        private void yMinus45DegreesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // RotatePickedObject(new Vector3d(0.0, -8192.0, 0.0));

            _mediator.Publish(new RotationCommand
            {
                MainForm = new WeakReference<MainForm>(this),
                Rotation = new Vector3d(0.0, -8192.0, 0.0)
            });
        }

        private void zPlus45DegreesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //RotatePickedObject(new Vector3d(0.0, 0.0, 8192.0));

            _mediator.Publish(new RotationCommand
            {
                MainForm = new WeakReference<MainForm>(this),
                Rotation = new Vector3d(0.0, 0.0, 8192.0)
            });
        }

        private void zMinus45DegreesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //RotatePickedObject(new Vector3d(0.0, 0.0, -8192.0));

            _mediator.Publish(new RotationCommand
            {
                MainForm = new WeakReference<MainForm>(this),
                Rotation = new Vector3d(0.0, 0.0, -8192.0)
            });
        }

        private void changeColorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_currentRoomVertex != null) EditVertexColor(_currentRoomVertex);
        }

        private void propertiesToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            if (_currentRoomVertex == null) return;

            var vertexInfo = new StringBuilder();
            vertexInfo.AppendFormat("Vertex at address 0x{0:X8}:\n\n", _currentRoomVertex.Address);
            vertexInfo.AppendFormat("RenderPosition: {0}\n", _currentRoomVertex.Position);
            vertexInfo.AppendFormat("Texture Coordinates: {0}\n", _currentRoomVertex.TexCoord);
            vertexInfo.AppendFormat("Colors: ({0}, {1}, {2}, {3})\n", _currentRoomVertex.Colors[0],
                _currentRoomVertex.Colors[1], _currentRoomVertex.Colors[2], _currentRoomVertex.Colors[3]);
            vertexInfo.AppendFormat("Normals: ({0}, {1}, {2})\n", _currentRoomVertex.Normals[0],
                _currentRoomVertex.Normals[1], _currentRoomVertex.Normals[2]);


            MessageBox.Show(vertexInfo.ToString(), "Vertex Properties", MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void bsiCamCoords_Click(object sender, EventArgs e)
        {
            _camera.Reset();
        }

        #region Menu events

        private void resetCameraPositionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            _camera.Reset();
        }

        private void enableTexturesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Configuration.RenderTextures = ((ToolStripMenuItem) sender).Checked;
            _displayListsDirty = true;
        }

        private void enableVSyncToolStripMenuItem_Click(object sender, EventArgs e)
        {
            _glRendererControl.VSync = Configuration.OglvSync = ((ToolStripMenuItem) sender).Checked;
        }

        private void enableAntiAliasingToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Make into config
            /* Determine anti-aliasing status */
            if (Configuration.EnableAntiAliasing == ((ToolStripMenuItem) sender).Checked)
            {
                GL.GetInteger(GetPName.MaxSamples, out var samples);
                Configuration.AntiAliasingSamples = samples;
            }
            else
            {
                // make this a config
                Configuration.AntiAliasingSamples = 0;
            }


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
            _mediator.Publish(new EnableMipMapsCommand()
            {
                SenderReference = new WeakReference<object>(sender),
                BaseRomReference = new WeakReference<BaseRomHandler>(_baseRom),
                DisplayListsDirtyReference = _displayListsDirty
            });
        }

        private void renderCollisionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // TODO make config
            Configuration.RenderCollision = ((ToolStripMenuItem) sender).Checked;
            if (Configuration.RenderCollision) _collisionDirty = true;
        }

        private void whiteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // TODO make config
            Configuration.RenderCollisionAsWhite = ((Controls.ToolStripRadioButtonMenuItem) sender).Checked;
            _collisionDirty = true;
        }

        private void typebasedToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Configuration.RenderCollisionAsWhite = !((Controls.ToolStripRadioButtonMenuItem) sender).Checked;
            _collisionDirty = true;
        }

        private void renderRoomActorsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // TODO make config
            Configuration.RenderRoomActors = ((ToolStripMenuItem) sender).Checked;
        }

        private void renderSpawnPointsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // TODO make config
            Configuration.RenderSpawnPoints = ((ToolStripMenuItem) sender).Checked;
        }

        private void renderTransitionsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // TODO make config
            Configuration.RenderTransitions = ((ToolStripMenuItem) sender).Checked;
        }

        private void renderPathWaypointsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // TODO make config
            Configuration.RenderPathWaypoints = ((ToolStripMenuItem) sender).Checked;
        }

        private void renderWaterboxesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Configuration.RenderWaterboxes = ((ToolStripMenuItem) sender).Checked;
        }

        private void linkAllWaypointsInPathToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Configuration.LinkAllWPinPath = ((ToolStripMenuItem) sender).Checked;
        }

        private void showWaterboxesPerRoomToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Configuration.ShowWaterboxesPerRoom = ((ToolStripMenuItem) sender).Checked;
        }

        private async void rOMInformationToolStripMenuItem_Click(object sender, EventArgs e)
        {
            await _mediator.Publish(new RomInformationCommand());
        }

        private void editDataTablesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //_navigation.ShowModal<TableEditorForm>();
            new TableEditorForm(_baseRom).ShowDialog();
        }

        private void editAreaTitleCardToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //  new TitleCardForm(_baseRom, _currentScene as SceneTableEntryOcarina, null).ShowDialog();
        }

        private async void checkForUpdateToolStripMenuItem_Click(object sender, EventArgs e)
        {
            await _mediator.Publish(new CheckForUpdateCommand());
        }

        private async void openGLInformationToolStripMenuItem_Click(object sender, EventArgs e)
        {
            await _mediator.Publish(new MenuItemCommand());
        }

        private async void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            await _mediator.Publish(new AboutCommand());
        }

        #endregion
    }
}