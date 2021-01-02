using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using SceneNavi.ROMHandler;
using SceneNavi.ROMHandler.Interfaces;
using SceneNavi.SimpleF3DEX2;
using SceneNavi.Utilities;

namespace SceneNavi.RomHandlers
{
    

    public class RomContext
    {
        public RomContext()
        {
        }

        public RomContext(int dmaTableAddress, string filename, int dataSize)
        {
            DmaTableAddress = dmaTableAddress;
            FileNameTableAddress = dmaTableAddress;
            SceneTableAddress = dmaTableAddress;
            Filename = filename;
            SegmentMapping = new Hashtable();
            Data = new byte[dataSize];
        }

        public ByteOrder DetectedByteOrder { get;  set; }

        // Material dimensions
        public string Filename { get;  set; }
        
        
        
        
        public byte[] Data { get;  set; }
        public string Title { get;  set; }
        public string GameId { get;  set; }
        public byte Version { get;  set; }
        public int Size => Data.Length;
        public bool HasZ64TablesHack { get; set; }
        public bool IsMajora { get;  set; }
        public string Creator { get;  set; }
        public string BuildDateString { get;  set; }
        public DateTime BuildDate => DateTime.ParseExact(BuildDateString, "yy-MM-dd HH:mm:ss", null);
        public int DmaTableAddress { get;  set; }
        public List<DmaTableEntry> Files { get; set; }
        public int FileNameTableAddress { get; set; }
        public bool HasFileNameTable => FileNameTableAddress != -1;
        public DmaTableEntry Code { get;set; }
        public byte[] CodeData { get; set; }
        public List<ISceneTableEntry> Scenes { get; set; }
        public int SceneTableAddress { get; set; }
        public AutoCompleteStringCollection SceneNameAcStrings { get; set; }
        public List<ActorTableEntry> Actors { get; set; }
        public int ActorTableAddress { get; set; }
        public List<ObjectTableEntry> Objects { get; set; }
        public int ObjectTableAddress { get; set; }
        public ushort ObjectCount { get; set; }
        public AutoCompleteStringCollection ObjectNameAcStrings { get;set; }
        public List<EntranceTableEntry> Entrances { get; set; }
        public int EntranceTableAddress { get; set; }
        public Hashtable SegmentMapping { get; set; }
        

        // these can be removed after proper setup in the architecture
        public XmlActorDefinitionReader XmlActorDefReader { get;  set; }
        public XmlHashTableReader XmlActorNames { get;  set; }
        public XmlHashTableReader XmlObjectNames { get; set; }
        public XmlHashTableReader XmlSongNames { get; set; }
        public XmlHashTableReader XmlSceneNames { get;  set; }
        public XmlHashTableReader XmlRoomNames { get;  set; }
        public XmlHashTableReader XmlStageDescriptions { get; set; }
    }

    public class BaseRomHandler : IRomHandler
    {





        public F3Dex2Interpreter Renderer { get; set; }
        public bool Loaded { get; private set; }

        public RomContext Rom { get; set; }


        public BaseRomHandler()
        {
            Rom = new RomContext();
            Renderer = new F3Dex2Interpreter(this);

        }

        public BaseRomHandler(string fileName)
        {

#if !DEBUG
            try
#endif
            {
                reload:
                Renderer = new F3Dex2Interpreter(this);

                /* Initialize segment and rendering systems */

                /* Read ROM */
                var binaryReader =
                    new BinaryReader(File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.Read));
                if (binaryReader.BaseStream.Length < RomConstants.MinRomSize)
                    throw new RomHandlerException(
                        $"File size is less than {(RomConstants.MinRomSize / 0x100000)}MB; ROM appears to be invalid.");
                Rom = new RomContext(-1, fileName, (int)binaryReader.BaseStream.Length);
                binaryReader.Read(Rom.Data, 0, (int) binaryReader.BaseStream.Length);
                binaryReader.Close();

                /* Detect byte order */
                DetectByteOrder();

                if (Rom.DetectedByteOrder != ByteOrder.BigEndian)
                {
                    if (MessageBox.Show(
                            "The ROM file you have selected uses an incompatible byte order, and needs to be converted to Big Endian format to be used." +
                            Environment.NewLine + Environment.NewLine +
                            "Convert the ROM now? (You will be asked for the target filename; the converted ROM will also be reloaded.)",
                            "Byte Order Warning", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    {
                        /* Ask for new filename */
                        var fnnew = GuiHelpers.ShowSaveFileDialog(
                            "Nintendo 64 ROMs (*.z64;*.bin)|*.z64;*.bin|All Files (*.*)|*.*");
                        if (fnnew != string.Empty)
                        {
                            fileName = fnnew;

                            /* Perform byte order conversion */
                            var datanew = new byte[Rom.Data.Length];
                            byte[] conv = null;
                            for (int i = 0, j = 0; i < Rom.Data.Length; i += 4, j += 4)
                            {
                                if (Rom.DetectedByteOrder == ByteOrder.MiddleEndian)
                                    conv = new byte[4] {Rom.Data[i + 1], Rom.Data[i], Rom.Data[i + 3], Rom.Data[i + 2]};
                                else if (Rom.DetectedByteOrder == ByteOrder.LittleEndian)
                                    conv = new byte[4] {Rom.Data[i + 3], Rom.Data[i + 2], Rom.Data[i + 1], Rom.Data[i]};
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
                            $"Incompatible byte order {Rom.DetectedByteOrder} detected; ROM cannot be used.");
                    }
                }
                else
                {
                    /* Read header */
                    ReadRomHeader();

                    /* Create XML actor definition reader */

                    /*
                     *
                     *   this will dissappear once the xml files have been converted to json or ini
                     *   most of this is just xml reading and producing hardcoded values from the xmls, which can be done via dependency injection in config.net
                     *
                     */
                    Rom.XmlActorDefReader =
                        new XmlActorDefinitionReader(Path.Combine("XML", "ActorDefinitions", Rom.GameId.Substring(1, 2)));

                    if (Rom.XmlActorDefReader.Definitions.Count > 0)
                    {
                        /* Create remaining XML-related objects */
                        Rom.XmlActorNames =
                            new XmlHashTableReader(Path.Combine("XML", "GameDataGeneric", Rom.GameId.Substring(1, 2)),
                                "ActorNames.xml");
                        Rom.XmlObjectNames =
                            new XmlHashTableReader(Path.Combine("XML", "GameDataGeneric", Rom.GameId.Substring(1, 2)),
                                "ObjectNames.xml");
                        Rom.XmlSongNames =
                            new XmlHashTableReader(Path.Combine("XML", "GameDataGeneric", Rom.GameId.Substring(1, 2)),
                                "SongNames.xml");

                        Rom.XmlSceneNames = new XmlHashTableReader(Path.Combine("XML", "GameDataSpecific",
                            $"{Rom.GameId}{Rom.Version:X1}"), "SceneNames.xml");
                        Rom.XmlRoomNames = new XmlHashTableReader(Path.Combine("XML", "GameDataSpecific",
                            $"{Rom.GameId}{Rom.Version:X1}"), "RoomNames.xml");
                        Rom.XmlStageDescriptions = new XmlHashTableReader(Path.Combine("XML", "GameDataSpecific",
                            $"{Rom.GameId}{Rom.Version:X1}"), "StageDescriptions.xml");

                        /* Determine if ROM uses z64tables hack */
                        Rom.HasZ64TablesHack = (Rom.Version == 15 &&
                                            Endian.SwapUInt32(BitConverter.ToUInt32(Rom.Data, 0x1238)) != 0x0C00084C);

                        /* Find and read build information, DMA table, etc. */
                        FindBuildInfo();
                        FindFileNameTable();
                        ReadDmaTable();
                        ReadFileNameTable();

                        /* Try to identify files */
                        foreach (var dmaTableEntry in Rom.Files) dmaTableEntry.Identify(this);

                        /* Find the code file */
                        FindCodeFile();

                        /* Find other Zelda-specific stuff */
                        FindActorTable();
                        FindObjectTable();
                        FindSceneTable();
                        ReadEntranceTable();

                        /* Some sanity checking & exception handling*/
                        if (Rom.Scenes == null || Rom.Scenes.Count == 0)
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
                if ((address >> 24) > 0x0F || Rom.SegmentMapping[(byte) (address >> 24)] == null) return false;
                if ((address & 0xFFFFFF) > ((byte[]) Rom.SegmentMapping[(byte) (address >> 24)]).Length &&
                    ((byte[]) Rom.SegmentMapping[(byte) (address >> 24)]).Length != 0) return false;
            }
            else
                return false;

            return true;
        }

        public void DetectByteOrder()
        {
            if (Rom.Data[0x0] == 0x80 && Rom.Data[0x8] == 0x80)
                Rom.DetectedByteOrder = ByteOrder.BigEndian;
            else if (Rom.Data[0x1] == 0x80 && Rom.Data[0x9] == 0x80)
                Rom.DetectedByteOrder = ByteOrder.MiddleEndian;
            else if (Rom.Data[0x3] == 0x80 && Rom.Data[0xB] == 0x80) Rom.DetectedByteOrder = ByteOrder.LittleEndian;
        }

        public void ReadRomHeader()
        {
            Rom.Title = Encoding.ASCII.GetString(Rom.Data, 0x20, 0x14).TrimEnd(new char[] {'\0', ' '});
            Rom.GameId = Encoding.ASCII.GetString(Rom.Data, 0x3B, 0x4).TrimEnd(new char[] {'\0'});
            Rom.Version = Rom.Data[0x3F];

            //TODO : Move to majora rom handler
            Rom.IsMajora = (Rom.Title.Contains("MAJORA") || Rom.Title.Contains("MUJURA"));
        }

        public void FindBuildInfo()
        {
            for (var i = 0; i < RomConstants.MinRomSize; i++)
            {
                if (Encoding.ASCII.GetString(Rom.Data, i, 4) != "@srd") continue;


                i -= (i % 8);
                var tmp = string.Empty;

                var next = StringExtensions.GetTerminatedString(Rom.Data, i, out tmp);
                Rom.Creator = tmp;
                var next2 = StringExtensions.GetTerminatedString(Rom.Data, next, out tmp);
                Rom.BuildDateString = tmp;

                next2 -= (next2 % 8);
                Rom.DmaTableAddress = next2;
                return;
            }

            if (Rom.DmaTableAddress == -1) throw new RomHandlerException("Could not find DMA table.");
        }

        public void FindFileNameTable()
        {
            for (var i = 0; i < RomConstants.MinRomSize; i += 4)
            {
                if (Encoding.ASCII.GetString(Rom.Data, i, 7) != "makerom") continue;

                Rom.FileNameTableAddress = i;
                return;
            }
        }

        public void ReadDmaTable()
        {
            Rom.Files = new List<DmaTableEntry>();

            var idx = 0;
            while (true)
            {
                if (Rom.DmaTableAddress + (idx * 0x10) > Rom.Data.Length)
                    throw new RomHandlerException("Went out of range while reading DMA table.");
                var ndma = new DmaTableEntry(this, idx);
                if (ndma.VStart == 0 && ndma.VEnd == 0 && ndma.PStart == 0) break;
                Rom.Files.Add(ndma);
                idx++;
            }
        }

        public void ReadFileNameTable()
        {
            if (Rom.FileNameTableAddress == -1) return;

            var nofs = Rom.FileNameTableAddress;
            foreach (var file in Rom.Files)
            {
                file.Name = Encoding.ASCII.GetString(Rom.Data, nofs, 50).TrimEnd('\0');
                var index = file.Name.IndexOf('\0');
                if (index >= 0) file.Name = file.Name.Remove(index);
                nofs += file.Name.Length;
                while (Rom.Data[nofs] == 0) nofs++;
            }
        }

        public void FindCodeFile()
        {
            Rom.Code = null;

            foreach (var dma in Rom.Files)
            {
                if (dma.IsValid == false) continue;

                var fdata = new byte[dma.VEnd - dma.VStart];
                if (dma.IsCompressed == true) throw new RomHandlerException("Compressed ROMs are not supported.");
                Array.Copy(Rom.Data, dma.PStart, fdata, 0, dma.VEnd - dma.VStart);

                for (var i = (fdata.Length - 8);
                    i > Math.Min((uint) (fdata.Length - RomConstants.CodeUcodeThreshold), fdata.Length);
                    i -= 8)
                {
                    if (Encoding.ASCII.GetString(fdata, i, 8) == "RSP Gfx ")
                    {
                        Rom.Code = dma;
                        Rom.CodeData = fdata;
                        return;
                    }
                }
            }

            if (Rom.Code == null) throw new RomHandlerException("Could not find code file.");
        }

        public void FindSceneTable()
        {
            Rom.Scenes = new List<ISceneTableEntry>();

            if (Rom.IsMajora || !Rom.HasZ64TablesHack)
            {
                //TODO : Move to majora rom handler
                var increment = (Rom.IsMajora ? 16 : 20);

                var dmaTableEntry = Rom.Files.OrderBy(x => x.VStart)
                    .FirstOrDefault(x => x.FileType == DmaTableEntry.FileTypes.Scene);

                for (var i = Rom.CodeData.Length - (increment * 2); i > 0; i -= 4)
                {
                    var entry = (!Rom.IsMajora
                        ? new SceneTableEntryOcarina(this, i, true)
                        : (ISceneTableEntry) new SceneTableEntryMajora(this, i, true));
                    if (entry.GetSceneStartAddress() == dmaTableEntry.VStart &&
                        entry.GetSceneEndAddress() == dmaTableEntry.VEnd)
                    {
                        Rom.SceneTableAddress = i;
                        break;
                    }
                }

                if (Rom.SceneTableAddress != -1)
                {
                    for (int i = Rom.SceneTableAddress, j = 0; i < Rom.CodeData.Length - (16 * 16); i += increment)
                    {
                        var sceneTableEntry = (!Rom.IsMajora
                            ? new SceneTableEntryOcarina(this, i, true)
                            : (ISceneTableEntry) new SceneTableEntryMajora(this, i, true));

                        if (!sceneTableEntry.IsValid() && !sceneTableEntry.IsAllZero()) break;

                        sceneTableEntry.SetNumber((ushort) j);
                        if (!sceneTableEntry.IsAllZero()) Rom.Scenes.Add(sceneTableEntry);
                        j++;
                    }
                }
            }
            else
            {
                Rom.SceneTableAddress = Endian.SwapInt32(BitConverter.ToInt32(Rom.Data, RomConstants.Z64TablesAdrOffset));
                var cnt = Endian.SwapInt32(BitConverter.ToInt32(Rom.Data, RomConstants.Z64TablesAdrOffset + 4));
                for (var i = 0; i < cnt; i++)
                {
                    Rom.Scenes.Add(new SceneTableEntryOcarina(this, Rom.SceneTableAddress + (i * 20), false));
                }
            }

            Rom.SceneNameAcStrings = new AutoCompleteStringCollection();
            foreach (var sceneTableEntry in Rom.Scenes)
            {
                sceneTableEntry.ReadScene();
                Rom.SceneNameAcStrings.Add(sceneTableEntry.GetName());
            }
        }

        public void FindActorTable()
        {
            Rom.Actors = new List<ActorTableEntry>();

            if (!Rom.HasZ64TablesHack)
            {
                var increment = 16;
                for (var i = 0; i < Rom.CodeData.Length - (16 * 16); i += increment)
                {
                    var act1 = new ActorTableEntry(this, i, true);
                    var act2 = new ActorTableEntry(this, i + 32, true);
                    var act3 = new ActorTableEntry(this, i + 64, true);
                    if (act1.IsComplete == false && act1.IsEmpty == false && act2.IsComplete == false &&
                        act2.IsEmpty == false && Rom.Actors.Count > 0) break;
                    if ((act1.IsValid != true || act1.IsIncomplete != true) ||
                        (act2.IsComplete != true && act2.IsEmpty != true) || Rom.Actors.Count != 0) continue;
                    Rom.ActorTableAddress = i;
                    break;
                }

                for (var i = Rom.ActorTableAddress; i < Rom.CodeData.Length - 32; i += 32)
                {
                    var nact = new ActorTableEntry(this, i, true);

                    if (nact.IsEmpty || nact.IsValid)
                        Rom.Actors.Add(nact);
                    else break;
                }
            }
            else
            {
                Rom.ActorTableAddress = Endian.SwapInt32(BitConverter.ToInt32(Rom.Data, RomConstants.Z64TablesAdrOffset + 24));
                var cnt = Endian.SwapInt32(BitConverter.ToInt32(Rom.Data, RomConstants.Z64TablesAdrOffset + 28));
                for (var i = 0; i < cnt; i++)
                {
                    Rom.Actors.Add(new ActorTableEntry(this, Rom.ActorTableAddress + i * 32, false));
                }
            }
        }

        public void FindObjectTable()
        {
            Rom.Objects = new List<ObjectTableEntry>();
            Rom.ObjectTableAddress = 0;
            Rom.ObjectCount = 0;

            Rom.EntranceTableAddress = 0;

            if (!Rom.HasZ64TablesHack)
            {
                var inc = 8;
                for (var i = Rom.ActorTableAddress; i < Rom.CodeData.Length - (8 * 8); i += inc)
                {
                    Rom.ObjectCount = Endian.SwapUInt16(BitConverter.ToUInt16(Rom.CodeData, i - 2));
                    if (Rom.ObjectCount < 0x100 || Rom.ObjectCount > 0x300) continue;

                    var obj1 = new ObjectTableEntry(this, i, true);
                    var obj2 = new ObjectTableEntry(this, i + 8, true);
                    var obj3 = new ObjectTableEntry(this, i + 16, true);

                    if (obj1.IsEmpty == true && obj2.IsValid == true && obj3.IsValid == true && Rom.Objects.Count == 0)
                    {
                        Rom.ObjectTableAddress = i;
                        break;
                    }
                }

                if (Rom.ObjectTableAddress != 0 && Rom.ObjectCount != 0)
                {
                    int i, j = 0;
                    for (i = Rom.ObjectTableAddress; i < (Rom.ObjectTableAddress + (Rom.ObjectCount * 8)); i += 8)
                    {
                        Rom.Objects.Add(new ObjectTableEntry(this, i, true, (ushort) j));
                        j++;
                    }

                    //TODO : Move to majora rom handler
                    if (!Rom.IsMajora) Rom.EntranceTableAddress = i + (i % 16);
                }
            }
            else
            {
                Rom.ObjectTableAddress = Endian.SwapInt32(BitConverter.ToInt32(Rom.Data, RomConstants.Z64TablesAdrOffset + 8));
                Rom.ObjectCount =
                    (ushort) Endian.SwapInt32(BitConverter.ToInt32(Rom.Data, RomConstants.Z64TablesAdrOffset + 12));
                for (var i = 0; i < Rom.ObjectCount; i++)
                {
                    Rom.Objects.Add(new ObjectTableEntry(this, Rom.ObjectTableAddress + i * 8, false, (ushort) i));
                }
            }

            // TODO: this shouldnt be here, it is a completed action that should have no knowledge of the ui
            Rom.ObjectNameAcStrings = new AutoCompleteStringCollection();
            foreach (var obj in Rom.Objects)
            {
                Rom.ObjectNameAcStrings.Add(obj.Name);
            }
        }

        public void ReadEntranceTable()
        {
            Rom.Entrances = new List<EntranceTableEntry>();

            if (!Rom.HasZ64TablesHack)
            {
                if (Rom.EntranceTableAddress == 0) return;

                int i = Rom.EntranceTableAddress, cnt = 0;
                while (i < Rom.SceneTableAddress)
                {
                    Rom.Entrances.Add(new EntranceTableEntry(Rom.CodeData, Rom.Data, i, true) {Number = (ushort) cnt++});
                    i += 4;
                }
            }
            else
            {
                Rom.EntranceTableAddress =
                    Endian.SwapInt32(BitConverter.ToInt32(Rom.Data, RomConstants.Z64TablesAdrOffset + 16));
                var cnt = Endian.SwapInt32(BitConverter.ToInt32(Rom.Data, RomConstants.Z64TablesAdrOffset + 20));
                for (var i = 0; i < cnt; i++)
                {
                    Rom.Entrances.Add(new EntranceTableEntry(Rom.CodeData, Rom.Data, Rom.EntranceTableAddress + i * 4, false)
                        {Number = (ushort) i});
                }
            }
        }
    }
}