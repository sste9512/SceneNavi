using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using SceneNavi.ROMHandler.Interfaces;
using SceneNavi.SimpleF3DEX2;

namespace SceneNavi.ROMHandler
{
 

    public class BaseRomHandler : IRomHandler
    {
        public ByteOrder DetectedByteOrder { get; private set; }

        public string Filename { get; private set; }
        public byte[] Data { get; private set; }

        public string Title { get; private set; }
        public string GameId { get; private set; }
        public byte Version { get; private set; }
        public int Size => Data.Length;

        public bool HasZ64TablesHack { get; private set; }
        public bool IsMajora { get; private set; }

        public string Creator { get; private set; }
        public string BuildDateString { get; private set; }
        public DateTime BuildDate => DateTime.ParseExact(BuildDateString, "yy-MM-dd HH:mm:ss", null);

        public int DmaTableAddress { get; private set; }
        public List<DmaTableEntry> Files { get; private set; }

        public int FileNameTableAddress { get; private set; }
        public bool HasFileNameTable => FileNameTableAddress != -1;

        public DmaTableEntry Code { get; private set; }
        public byte[] CodeData { get; private set; }

        public List<ISceneTableEntry> Scenes { get; private set; }
        public int SceneTableAddress { get; private set; }
        public AutoCompleteStringCollection SceneNameAcStrings { get; private set; }

        public List<ActorTableEntry> Actors { get; private set; }
        public int ActorTableAddress { get; private set; }

        public List<ObjectTableEntry> Objects { get; private set; }
        public int ObjectTableAddress { get; private set; }
        public ushort ObjectCount { get; private set; }
        public AutoCompleteStringCollection ObjectNameAcStrings { get; private set; }

        public List<EntranceTableEntry> Entrances { get; private set; }
        public int EntranceTableAddress { get; private set; }

        public Hashtable SegmentMapping { get; set; }

        public F3DEX2Interpreter Renderer { get; private set; }


        // This seems to be a basic xml reader, not for game files but for project xml objects
        public XmlActorDefinitionReader XmlActorDefReader { get; private set; }


        public XMLHashTableReader XmlActorNames { get; private set; }
        public XMLHashTableReader XmlObjectNames { get; private set; }
        public XMLHashTableReader XmlSongNames { get; private set; }

        public XMLHashTableReader XmlSceneNames { get; private set; }
        public XMLHashTableReader XmlRoomNames { get; private set; }
        public XMLHashTableReader XmlStageDescriptions { get; private set; }

        public bool Loaded { get; private set; }

        public static int GetTerminatedString(byte[] bytes, int index, out string str)
        {
            var nullidx = Array.FindIndex(bytes, index, (x) => x == 0);

            if (nullidx >= 0) str = Encoding.ASCII.GetString(bytes, index, nullidx - index);
            else
                str = Encoding.ASCII.GetString(bytes, index, bytes.Length - index);

            var nextidx = Array.FindIndex(bytes, nullidx, (x) => x != 0);

            return nextidx;
        }

        public BaseRomHandler()
        {
            
        }
        
        public BaseRomHandler(string fileName)
        {
#if !DEBUG
            try
#endif
            {
                reload:
                DmaTableAddress = FileNameTableAddress = SceneTableAddress = -1;

                Filename = fileName;

                /* Initialize segment and rendering systems */
                SegmentMapping = new Hashtable();
                Renderer = new F3DEX2Interpreter(this);

                /* Read ROM */
                var binaryReader =
                    new BinaryReader(File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.Read));
                if (binaryReader.BaseStream.Length < RomConstants.MinRomSize)
                    throw new RomHandlerException(
                        $"File size is less than {(RomConstants.MinRomSize / 0x100000)}MB; ROM appears to be invalid.");
                Data = new byte[binaryReader.BaseStream.Length];
                binaryReader.Read(Data, 0, (int) binaryReader.BaseStream.Length);
                binaryReader.Close();

                /* Detect byte order */
                DetectByteOrder();

                if (DetectedByteOrder != ByteOrder.BigEndian)
                {
                    if (MessageBox.Show(
                            "The ROM file you have selected uses an incompatible byte order, and needs to be converted to Big Endian format to be used." +
                            Environment.NewLine + Environment.NewLine +
                            "Convert the ROM now? (You will be asked for the target filename; the converted ROM will also be reloaded.)",
                            "Byte Order Warning", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    {
                        /* Ask for new filename */
                        var fnnew = GUIHelpers.ShowSaveFileDialog(
                            "Nintendo 64 ROMs (*.z64;*.bin)|*.z64;*.bin|All Files (*.*)|*.*");
                        if (fnnew != string.Empty)
                        {
                            fileName = fnnew;

                            /* Perform byte order conversion */
                            var datanew = new byte[Data.Length];
                            byte[] conv = null;
                            for (int i = 0, j = 0; i < Data.Length; i += 4, j += 4)
                            {
                                if (DetectedByteOrder == ByteOrder.MiddleEndian)
                                    conv = new byte[4] {Data[i + 1], Data[i], Data[i + 3], Data[i + 2]};
                                else if (DetectedByteOrder == ByteOrder.LittleEndian)
                                    conv = new byte[4] {Data[i + 3], Data[i + 2], Data[i + 1], Data[i]};
                                Buffer.BlockCopy(conv, 0, datanew, j, conv.Length);
                            }

                            /* Save converted ROM, then reload it */
                            var bw = new BinaryWriter(File.Create(fileName));
                            bw.Write(datanew);
                            bw.Close();

                            goto reload;
                        }
                    }
                    else
                    {
                        /* Wrong byte order, no conversion performed */
                        throw new ByteOrderException(
                            $"Incompatible byte order {DetectedByteOrder} detected; ROM cannot be used.");
                    }
                }
                else
                {
                    /* Read header */
                    ReadRomHeader();

                    /* Create XML actor definition reader */
                    XmlActorDefReader =
                        new XmlActorDefinitionReader(Path.Combine("XML", "ActorDefinitions", GameId.Substring(1, 2)));

                    if (XmlActorDefReader.Definitions.Count > 0)
                    {
                        /* Create remaining XML-related objects */
                        XmlActorNames =
                            new XMLHashTableReader(Path.Combine("XML", "GameDataGeneric", GameId.Substring(1, 2)),
                                "ActorNames.xml");
                        XmlObjectNames =
                            new XMLHashTableReader(Path.Combine("XML", "GameDataGeneric", GameId.Substring(1, 2)),
                                "ObjectNames.xml");
                        XmlSongNames =
                            new XMLHashTableReader(Path.Combine("XML", "GameDataGeneric", GameId.Substring(1, 2)),
                                "SongNames.xml");

                        XmlSceneNames = new XMLHashTableReader(Path.Combine("XML", "GameDataSpecific",
                            $"{GameId}{Version:X1}"), "SceneNames.xml");
                        XmlRoomNames = new XMLHashTableReader(Path.Combine("XML", "GameDataSpecific",
                            $"{GameId}{Version:X1}"), "RoomNames.xml");
                        XmlStageDescriptions = new XMLHashTableReader(Path.Combine("XML", "GameDataSpecific",
                            $"{GameId}{Version:X1}"), "StageDescriptions.xml");

                        /* Determine if ROM uses z64tables hack */
                        HasZ64TablesHack = (Version == 15 &&
                                            Endian.SwapUInt32(BitConverter.ToUInt32(Data, 0x1238)) != 0x0C00084C);

                        /* Find and read build information, DMA table, etc. */
                        FindBuildInfo();
                        FindFileNameTable();
                        ReadDmaTable();
                        ReadFileNameTable();

                        /* Try to identify files */
                        foreach (var dmaTableEntry in Files) dmaTableEntry.Identify(this);

                        /* Find the code file */
                        FindCodeFile();

                        /* Find other Zelda-specific stuff */
                        FindActorTable();
                        FindObjectTable();
                        FindSceneTable();
                        ReadEntranceTable();

                        /* Some sanity checking & exception handling*/
                        if (Scenes == null || Scenes.Count == 0)
                            throw new RomHandlerException("No valid scenes could be recognized in the ROM.");

                        /* Done */
                        Loaded = true;
                    }
                }
            }
#if !DEBUG
            catch (Exception ex)
            {
                Loaded = false;
                if (MessageBox.Show(ex.Message + "\n\n" + "Show detailed information?", "Exception", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                {
                    MessageBox.Show(ex.ToString(), "Exception Details", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
#endif
        }

        public bool IsAddressSupported(uint address)
        {
            if (address >> 24 != 0x80)
            {
                if ((address >> 24) > 0x0F || SegmentMapping[(byte) (address >> 24)] == null) return false;
                if ((address & 0xFFFFFF) > ((byte[]) SegmentMapping[(byte) (address >> 24)]).Length &&
                    ((byte[]) SegmentMapping[(byte) (address >> 24)]).Length != 0) return false;
            }
            else
                return false;

            return true;
        }

        public void DetectByteOrder()
        {
            if (Data[0x0] == 0x80 && Data[0x8] == 0x80) DetectedByteOrder = ByteOrder.BigEndian;
            else if (Data[0x1] == 0x80 && Data[0x9] == 0x80) DetectedByteOrder = ByteOrder.MiddleEndian;
            else if (Data[0x3] == 0x80 && Data[0xB] == 0x80) DetectedByteOrder = ByteOrder.LittleEndian;
        }

        public void ReadRomHeader()
        {
            Title = Encoding.ASCII.GetString(Data, 0x20, 0x14).TrimEnd(new char[] {'\0', ' '});
            GameId = Encoding.ASCII.GetString(Data, 0x3B, 0x4).TrimEnd(new char[] {'\0'});
            Version = Data[0x3F];

            //TODO : Move to majora rom handler
            IsMajora = (Title.Contains("MAJORA") || Title.Contains("MUJURA"));
        }

        public void FindBuildInfo()
        {
            for (var i = 0; i < RomConstants.MinRomSize; i++)
            {
                if (Encoding.ASCII.GetString(Data, i, 4) != "@srd") continue;


                i -= (i % 8);
                var tmp = string.Empty;

                var next = GetTerminatedString(Data, i, out tmp);
                Creator = tmp;
                var next2 = GetTerminatedString(Data, next, out tmp);
                BuildDateString = tmp;

                next2 -= (next2 % 8);
                DmaTableAddress = next2;
                return;
            }

            if (DmaTableAddress == -1) throw new RomHandlerException("Could not find DMA table.");
        }

        public void FindFileNameTable()
        {
            for (var i = 0; i < RomConstants.MinRomSize; i += 4)
            {
                if (Encoding.ASCII.GetString(Data, i, 7) != "makerom") continue;

                FileNameTableAddress = i;
                return;
            }
        }

        public void ReadDmaTable()
        {
            Files = new List<DmaTableEntry>();

            var idx = 0;
            while (true)
            {
                if (DmaTableAddress + (idx * 0x10) > Data.Length)
                    throw new RomHandlerException("Went out of range while reading DMA table.");
                var ndma = new DmaTableEntry(this, idx);
                if (ndma.VStart == 0 && ndma.VEnd == 0 && ndma.PStart == 0) break;
                Files.Add(ndma);
                idx++;
            }
        }

        public void ReadFileNameTable()
        {
            if (FileNameTableAddress == -1) return;

            var nofs = FileNameTableAddress;
            foreach (var file in Files)
            {
                file.Name = Encoding.ASCII.GetString(Data, nofs, 50).TrimEnd('\0');
                var index = file.Name.IndexOf('\0');
                if (index >= 0) file.Name = file.Name.Remove(index);
                nofs += file.Name.Length;
                while (Data[nofs] == 0) nofs++;
            }
        }

        public void FindCodeFile()
        {
            Code = null;

            foreach (var dma in Files)
            {
                if (dma.IsValid == false) continue;

                var fdata = new byte[dma.VEnd - dma.VStart];
                if (dma.IsCompressed == true) throw new RomHandlerException("Compressed ROMs are not supported.");
                Array.Copy(Data, dma.PStart, fdata, 0, dma.VEnd - dma.VStart);

                for (var i = (fdata.Length - 8);
                    i > Math.Min((uint) (fdata.Length - RomConstants.CodeUcodeThreshold), fdata.Length);
                    i -= 8)
                {
                    if (Encoding.ASCII.GetString(fdata, i, 8) == "RSP Gfx ")
                    {
                        Code = dma;
                        CodeData = fdata;
                        return;
                    }
                }
            }

            if (Code == null) throw new RomHandlerException("Could not find code file.");
        }

        public void FindSceneTable()
        {
            Scenes = new List<ISceneTableEntry>();

            if (IsMajora || !HasZ64TablesHack)
            {
                //TODO : Move to majora rom handler
                var increment = (IsMajora ? 16 : 20);

                var dmaTableEntry = Files.OrderBy(x => x.VStart)
                    .FirstOrDefault(x => x.FileType == DmaTableEntry.FileTypes.Scene);

                for (var i = CodeData.Length - (increment * 2); i > 0; i -= 4)
                {
                    var entry = (!IsMajora
                        ? new SceneTableEntryOcarina(this, i, true)
                        : (ISceneTableEntry) new SceneTableEntryMajora(this, i, true));
                    if (entry.GetSceneStartAddress() == dmaTableEntry.VStart &&
                        entry.GetSceneEndAddress() == dmaTableEntry.VEnd)
                    {
                        SceneTableAddress = i;
                        break;
                    }
                }

                if (SceneTableAddress != -1)
                {
                    for (int i = SceneTableAddress, j = 0; i < CodeData.Length - (16 * 16); i += increment)
                    {
                        var sceneTableEntry = (!IsMajora
                            ? new SceneTableEntryOcarina(this, i, true)
                            : (ISceneTableEntry) new SceneTableEntryMajora(this, i, true));

                        if (!sceneTableEntry.IsValid() && !sceneTableEntry.IsAllZero()) break;

                        sceneTableEntry.SetNumber((ushort) j);
                        if (!sceneTableEntry.IsAllZero()) Scenes.Add(sceneTableEntry);
                        j++;
                    }
                }
            }
            else
            {
                SceneTableAddress = Endian.SwapInt32(BitConverter.ToInt32(Data, RomConstants.Z64TablesAdrOffset));
                var cnt = Endian.SwapInt32(BitConverter.ToInt32(Data, RomConstants.Z64TablesAdrOffset + 4));
                for (var i = 0; i < cnt; i++)
                {
                    Scenes.Add(new SceneTableEntryOcarina(this, SceneTableAddress + (i * 20), false));
                }
            }

            SceneNameAcStrings = new AutoCompleteStringCollection();
            foreach (var sceneTableEntry in Scenes)
            {
                sceneTableEntry.ReadScene();
                SceneNameAcStrings.Add(sceneTableEntry.GetName());
            }
        }

        public void FindActorTable()
        {
            Actors = new List<ActorTableEntry>();

            if (!HasZ64TablesHack)
            {
                var increment = 16;
                for (var i = 0; i < CodeData.Length - (16 * 16); i += increment)
                {
                    var act1 = new ActorTableEntry(this, i, true);
                    var act2 = new ActorTableEntry(this, i + 32, true);
                    var act3 = new ActorTableEntry(this, i + 64, true);
                    if (act1.IsComplete == false && act1.IsEmpty == false && act2.IsComplete == false &&
                        act2.IsEmpty == false && Actors.Count > 0) break;
                    if ((act1.IsValid != true || act1.IsIncomplete != true) ||
                        (act2.IsComplete != true && act2.IsEmpty != true) || Actors.Count != 0) continue;
                    ActorTableAddress = i;
                    break;
                }

                for (var i = ActorTableAddress; i < CodeData.Length - 32; i += 32)
                {
                    var nact = new ActorTableEntry(this, i, true);

                    if (nact.IsEmpty || nact.IsValid) Actors.Add(nact);
                    else break;
                }
            }
            else
            {
                ActorTableAddress = Endian.SwapInt32(BitConverter.ToInt32(Data, RomConstants.Z64TablesAdrOffset + 24));
                var cnt = Endian.SwapInt32(BitConverter.ToInt32(Data, RomConstants.Z64TablesAdrOffset + 28));
                for (var i = 0; i < cnt; i++)
                {
                    Actors.Add(new ActorTableEntry(this, ActorTableAddress + i * 32, false));
                }
            }
        }

        public void FindObjectTable()
        {
            Objects = new List<ObjectTableEntry>();
            ObjectTableAddress = 0;
            ObjectCount = 0;

            EntranceTableAddress = 0;

            if (!HasZ64TablesHack)
            {
                var inc = 8;
                for (var i = ActorTableAddress; i < CodeData.Length - (8 * 8); i += inc)
                {
                    ObjectCount = Endian.SwapUInt16(BitConverter.ToUInt16(CodeData, i - 2));
                    if (ObjectCount < 0x100 || ObjectCount > 0x300) continue;

                    var obj1 = new ObjectTableEntry(this, i, true);
                    var obj2 = new ObjectTableEntry(this, i + 8, true);
                    var obj3 = new ObjectTableEntry(this, i + 16, true);

                    if (obj1.IsEmpty == true && obj2.IsValid == true && obj3.IsValid == true && Objects.Count == 0)
                    {
                        ObjectTableAddress = i;
                        break;
                    }
                }

                if (ObjectTableAddress != 0 && ObjectCount != 0)
                {
                    int i, j = 0;
                    for (i = ObjectTableAddress; i < (ObjectTableAddress + (ObjectCount * 8)); i += 8)
                    {
                        Objects.Add(new ObjectTableEntry(this, i, true, (ushort) j));
                        j++;
                    }

                    //TODO : Move to majora rom handler
                    if (!IsMajora)
                        EntranceTableAddress = i + (i % 16);
                }
            }
            else
            {
                ObjectTableAddress = Endian.SwapInt32(BitConverter.ToInt32(Data, RomConstants.Z64TablesAdrOffset + 8));
                ObjectCount =
                    (ushort) Endian.SwapInt32(BitConverter.ToInt32(Data, RomConstants.Z64TablesAdrOffset + 12));
                for (var i = 0; i < ObjectCount; i++)
                {
                    Objects.Add(new ObjectTableEntry(this, ObjectTableAddress + i * 8, false, (ushort) i));
                }
            }

            // TODO: this shouldnt be here, it is a completed action that should have no knowledge of the ui
            ObjectNameAcStrings = new AutoCompleteStringCollection();
            foreach (var obj in Objects)
            {
                ObjectNameAcStrings.Add(obj.Name);
            }
        }

        public void ReadEntranceTable()
        {
            Entrances = new List<EntranceTableEntry>();

            if (!HasZ64TablesHack)
            {
                if (EntranceTableAddress == 0) return;

                int i = EntranceTableAddress, cnt = 0;
                while (i < SceneTableAddress)
                {
                    Entrances.Add(new EntranceTableEntry(CodeData, Data, i, true) {Number = (ushort) cnt++});
                    i += 4;
                }
            }
            else
            {
                EntranceTableAddress =
                    Endian.SwapInt32(BitConverter.ToInt32(Data, RomConstants.Z64TablesAdrOffset + 16));
                var cnt = Endian.SwapInt32(BitConverter.ToInt32(Data, RomConstants.Z64TablesAdrOffset + 20));
                for (var i = 0; i < cnt; i++)
                {
                    Entrances.Add(new EntranceTableEntry(CodeData, Data, EntranceTableAddress + i * 4, false)
                        {Number = (ushort) i});
                }
            }
        }
    }
}