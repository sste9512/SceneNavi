using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml;
using System.Reflection;
using System.Windows.Forms;
using System.Drawing;
using SceneNavi.HeaderCommands;
using SceneNavi.Models;
using SceneNavi.Utilities.OpenGLHelpers;

namespace SceneNavi
{
    public partial class XmlActorDefinitionReader
    {
        private Version ProgramVersion { get; set; }
        public List<Definition> Definitions { get; private set; }

        public XmlActorDefinitionReader(string definitionDirectory)
        {
            Definitions = new List<Definition>();

            var path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? throw new FileFormatException(), definitionDirectory);
            if (Directory.Exists(path) == false) return;

            ProgramVersion = new Version();

            var xmlFiles = Directory.EnumerateFiles(path, "*.xml").ToList();
            foreach (var fileName in xmlFiles)
            {
                Definition ndef = null;
                Item nitem = null;
                Option nopt = null;
                DisplayList displaydl = null;
                DisplayList pickdl = null;

                var xml = new XmlTextReader(fileName);
                while (xml.Read())
                {
                    if (xml.NodeType == XmlNodeType.Element)
                    {
                        if (xml.Name == "ActorDatabase")
                        {
                            while (xml.MoveToNextAttribute())
                            {
                                if (xml.Name == "ProgramVersion")
                                {
                                    ProgramVersion = Version.Parse(xml.Value);
                                    if (ProgramVersion != Version.Parse(Application.ProductVersion)) ThrowVersionError();
                                }
                            }
                        }
                        else if (xml.Name == "Definition")
                        {
                            if (ProgramVersion == new Version()) ThrowVersionError();

                            ndef = new Definition {Items = new List<Item>()};

                            while (xml.MoveToNextAttribute())
                            {
                                switch (xml.Name)
                                {
                                    case "Number":
                                        if (xml.Value.StartsWith("0x") == true)
                                            ndef.Number = ushort.Parse(xml.Value.Substring(2), System.Globalization.NumberStyles.HexNumber);
                                        else
                                            ndef.Number = ushort.Parse(xml.Value);
                                        break;
                                    case "IsDefault":
                                        ndef.IsDefault = (DefaultTypes)Enum.Parse(typeof(DefaultTypes), xml.Value);
                                        break;
                                    case "DisplayModel":
                                        displaydl = StockObjects.GetDisplayList(xml.Value);
                                        if (displaydl != null) ndef.DisplayModel = displaydl;
                                        break;
                                    case "PickModel":
                                        pickdl = StockObjects.GetDisplayList(xml.Value);
                                        if (pickdl != null) ndef.PickModel = pickdl;
                                        break;
                                    case "FrontOffset":
                                        ndef.FrontOffset = double.Parse(xml.Value, System.Globalization.CultureInfo.InvariantCulture);
                                        break;
                                }
                            }
                        }
                        else if (xml.Name == "Item")
                        {
                            nitem = new Item();
                            while (xml.MoveToNextAttribute())
                            {
                                switch (xml.Name)
                                {
                                    case "Index":
                                        nitem.Index = int.Parse(xml.Value);
                                        break;
                                    case "ValueType":
                                        nitem.ValueType = FindTypeInCurrentAssemblies(xml.Value);
                                        break;
                                    case "DisplayStyle":
                                        nitem.DisplayStyle = (DisplayStyles)Enum.Parse(typeof(DisplayStyles), xml.Value);
                                        break;
                                    case "Usage":
                                        nitem.Usage = (Usages)Enum.Parse(typeof(Usages), xml.Value);
                                        break;
                                    case "Description":
                                        nitem.Description = xml.Value;
                                        break;
                                    case "Mask":
                                        if (xml.Value.StartsWith("0x") == true)
                                            nitem.Mask = UInt64.Parse(xml.Value.Substring(2), System.Globalization.NumberStyles.HexNumber);
                                        else
                                            nitem.Mask = UInt64.Parse(xml.Value);
                                        break;
                                    case "ControlType":
                                        nitem.ControlType = FindTypeInCurrentAssemblies(xml.Value);
                                        break;
                                }
                            }
                            ndef.Items.Add(nitem);
                        }
                        else if (xml.Name == "Option")
                        {
                            nopt = new Option();
                            while (xml.MoveToNextAttribute())
                            {
                                switch (xml.Name)
                                {
                                    case "Value":
                                        if (xml.Value.StartsWith("0x") == true)
                                            nopt.Value = UInt64.Parse(xml.Value.Substring(2), System.Globalization.NumberStyles.HexNumber);
                                        else
                                            nopt.Value = UInt64.Parse(xml.Value);
                                        break;
                                    case "Description":
                                        nopt.Description = xml.Value;
                                        break;
                                }
                            }
                            nitem.Options.Add(nopt);
                        }
                    }
                    else if (xml.NodeType == XmlNodeType.EndElement)
                    {
                        if (xml.Name != "Definition") continue;
                       
                        if (displaydl != null && pickdl == null) ndef.PickModel = displaydl;

                        Definitions.Add(ndef);
                    }
                }
            }
        }

        private void ThrowVersionError()
        {
            throw new XmlActorDefinitionReaderException(
                $"Program version mismatch; expected {Application.ProductVersion}, found {ProgramVersion}. Please make sure your XML folder is up-to-date.");
        }

        public static void RefreshActorPositionRotation(Actors.Entry ac, Controls.TableLayoutPanelEx tlpex)
        {
            foreach (Control ctrl in tlpex.Controls)
            {
                if (ctrl is TextBox && ctrl.Tag is Item)
                {
                    var item = (ctrl.Tag as Item);
                    if (item.Usage == Usages.PositionX || item.Usage == Usages.PositionY || item.Usage == Usages.PositionZ ||
                        item.Usage == Usages.RotationX || item.Usage == Usages.RotationY || item.Usage == Usages.RotationZ)
                    {
                        var fstr = "{0}";
                        switch (item.DisplayStyle)
                        {
                            case DisplayStyles.Hexadecimal: fstr = "0x{0:X}"; break;
                            case DisplayStyles.Decimal: fstr = "{0:D}"; break;
                        }
                        var val = GetValueFromActor(item, ac);
                        item.ControlType.GetProperty("Text")?.SetValue(ctrl, string.Format(fstr, val), null);
                    }
                }
            }
        }

        public static void CreateActorEditingControls(Actors.Entry ac, Controls.TableLayoutPanelEx tlpex, Action numberchanged, object tag = null, bool individual = false)
        {
            //TODO TODO TODO: more value types, more control types, etc, etc!!!

            /* No proper actor entry given? */
            if (ac == null || ac.Definition == null)
            {
                tlpex.Controls.Clear();
                return;
            }

            /* Get current definition */
            var def = ac.Definition;

            /* No definition given? */
            if (def == null) return;

            /* Begin layout creation */
            tlpex.SuspendLayout();
            tlpex.Controls.Clear();

            /* Create description label */
            var desc = new Label()
            {
                Text = (ac.InternalName == string.Empty ? ac.Name : string.Format("{0} ({1})", ac.Name, ac.InternalName)),
                TextAlign = ContentAlignment.MiddleLeft,
                Dock = DockStyle.Fill
            };
            tlpex.Controls.Add(desc, 0, 0);
            tlpex.SetColumnSpan(desc, 2);

            /* Parse items */
            for (var i = 0; i < def.Items.Count; i++)
            {
                /* Get current item */
                var item = def.Items[i];

                /* UGLY HACK -> for room number in transition actor with individual file mode... */
                if (item.Usage == Usages.NextRoomBack || item.Usage == Usages.NextRoomFront)
                    item.ControlType = (individual ? typeof(TextBox) : typeof(ComboBox));

                /* Get value, create control */
                var val = GetValueFromActor(item, ac);
                var ctrl = Activator.CreateInstance(item.ControlType);

                /* First ControlType check; ex. is label needed? */
                if (item.ControlType == typeof(CheckBox))
                {
                    /* Add control alone */
                    tlpex.Controls.Add(ctrl as Control, 0, (i + 1));
                }
                else
                {
                    /* Add label and control */
                    var lbl = new Label() { Text = $"{item.Description}:", TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill };
                    tlpex.Controls.Add(lbl, 0, (i + 1));
                    tlpex.Controls.Add(ctrl as Control ?? throw new NullReferenceException(), 1, (i + 1));
                }

                /* Set control properties */
                item.ControlType.GetProperty("Dock")?.SetValue(ctrl, DockStyle.Fill, null);
                item.ControlType.GetProperty("Tag")?.SetValue(ctrl, item, null);
                item.ControlType.GetProperty("Name")?.SetValue(ctrl, item.Usage.ToString(), null);

                /* ControlType-specific settings */
                if (item.ControlType == typeof(ComboBox))
                {
                    /* Set ComboBox */
                    item.ControlType.GetProperty("DropDownStyle")?.SetValue(ctrl, ComboBoxStyle.DropDownList, null);
                    item.ControlType.GetProperty("DisplayMember")?.SetValue(ctrl, "Description", null);

                    if (!individual && (item.Usage == Usages.NextRoomBack || item.Usage == Usages.NextRoomFront) && (tag is List<RoomInfoClass>))
                    {
                        /* Item usage is room number in transition actor; get room listing from function tag */
                        item.Options = new List<Option>();
                        foreach (var ric in (tag as List<RoomInfoClass>))
                            item.Options.Add(new Option() { Description = ric.Description, Value = ric.Number });
                    }

                    if (item.Options.Count > 0)
                    {
                        item.ControlType.GetProperty("DataSource")?.SetValue(ctrl, item.Options, null);
                        item.ControlType.GetProperty("SelectedItem")?.SetValue(ctrl, item.Options.Find(x => x.Value == (Convert.ToUInt64(val) & item.Mask)), null);
                        (ctrl as ComboBox).SelectedIndexChanged += new EventHandler((s, ex) =>
                        {
                            SetValueInActor(item, ac, ((Option)((ComboBox)s).SelectedItem).Value);
                        });
                    }
                }
                else if (item.ControlType == typeof(CheckBox))
                {
                    /* Set CheckBox */
                    item.ControlType.GetProperty("Checked")?.SetValue(ctrl, Convert.ToBoolean(val), null);
                    item.ControlType.GetProperty("Text")?.SetValue(ctrl, item.Description, null);
                    tlpex.SetColumnSpan(ctrl as Control, 2);
                    (ctrl as CheckBox).CheckedChanged += new EventHandler((s, ex) =>
                    {
                        ChangeBitInActor(item, ac, item.Mask, ((CheckBox)s).Checked);
                    });
                }
                else
                {
                    /* Fallback */
                    if (item.ControlType.GetProperty("Text") != null)
                    {
                        var fstr = "{0}";
                        switch (item.DisplayStyle)
                        {
                            case DisplayStyles.Hexadecimal: fstr = "0x{0:X}"; break;
                            case DisplayStyles.Decimal: fstr = "{0:D}"; break;
                        }
                        item.ControlType.GetProperty("Text")?.SetValue(ctrl, string.Format(fstr, val), null);
                        (ctrl as Control).TextChanged += new EventHandler((s, ex) =>
                        {
                            var newval = Activator.CreateInstance(item.ValueType);
                            var mi = item.ValueType.GetMethod("Parse", new Type[] { typeof(string), typeof(System.Globalization.NumberStyles) });
                            if (mi != null)
                            {
                                /* Determine NumberStyle to use */
                                var ns =
                                    (item.DisplayStyle == DisplayStyles.Hexadecimal ? System.Globalization.NumberStyles.HexNumber : System.Globalization.NumberStyles.Integer);

                                /* Hex number; is text long enough? */
                                if (ns == System.Globalization.NumberStyles.HexNumber && ((Control)s).Text.Length < 2) return;

                                /* Get value string, depending on NumberStyle */
                                var valstr = (ns == System.Globalization.NumberStyles.HexNumber ? ((Control)s).Text.Substring(2) : ((Control)s).Text);

                                /* Proper value string found? */
                                if (valstr != null && valstr != "")
                                {
                                    try
                                    {
                                        /* Invoke Parse function and get parsed value */
                                        newval = mi.Invoke(newval, new object[] { valstr, ns });

                                        /* Set new value in actor; if usage is ActorNumber, also do callback */
                                        SetValueInActor(item, ac, newval);
                                        if (item.Usage == Usages.ActorNumber && numberchanged != null) numberchanged();
                                    }
                                    catch (TargetInvocationException tiex)
                                    {
                                        if (tiex.InnerException is FormatException)
                                        {
                                            /* Ignore; happens with ex. malformed hex numbers (i.e. "0xx0") */
                                        }
                                    }
                                }
                            }
                        });
                    }
                }
            }

            /* Done */
            tlpex.ResumeLayout();
        }

        private Type FindTypeInCurrentAssemblies(string name)
        {
            Type ntype = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                ntype = asm.GetType(name);
                if (ntype != null) break;
            }
            return ntype;
        }

        public static object GetValueFromActor(Item item, Actors.Entry ac)
        {
            if (item == null || ac == null) return null;

            object val = null;
            if (item.ValueType == typeof(Byte))
                val = (ac.RawData[item.Index] & (Byte)item.Mask);
            if (item.ValueType == typeof(UInt16))
                val = (Endian.SwapUInt16(BitConverter.ToUInt16(ac.RawData, item.Index)) & (UInt16)item.Mask);
            else if (item.ValueType == typeof(Int16))
                val = (Endian.SwapInt16(BitConverter.ToInt16(ac.RawData, item.Index)) & (Int16)item.Mask);

            return Convert.ChangeType(val, item.ValueType);
        }

        public static void SetValueInActor(Item item, Actors.Entry ac, object value)
        {
            if (item == null || ac == null || value == null) return;

            object oldval = null;

            if (item.ValueType == typeof(Byte))
            {
                ac.RawData[item.Index] = (byte)((ac.RawData[item.Index] & ~(Byte)item.Mask) | Convert.ToByte(value));
            }
            else if (item.ValueType == typeof(UInt16))
            {
                oldval = (UInt16)(Endian.SwapUInt16(BitConverter.ToUInt16(ac.RawData, item.Index)) & ~(UInt16)item.Mask);
                var newval = Endian.SwapUInt16((UInt16)(Convert.ToUInt16(oldval) | Convert.ToUInt16(value)));
                BitConverter.GetBytes(newval).CopyTo(ac.RawData, item.Index);
            }
            else if (item.ValueType == typeof(Int16))
            {
                oldval = (Int16)(Endian.SwapInt16(BitConverter.ToInt16(ac.RawData, item.Index)) & ~(Int16)item.Mask);
                var newval = Endian.SwapInt16((Int16)(Convert.ToInt16(oldval) | Convert.ToInt16(value)));
                BitConverter.GetBytes(newval).CopyTo(ac.RawData, item.Index);
            }

            if (item.Usage == Usages.ActorNumber) ac.RefreshVariables();
        }

        public static void ChangeBitInActor(Item item, Actors.Entry ac, object value, bool set)
        {
            if (item == null || ac == null || value == null) return;

            //TODO TODO TODO allow bit toggle in non-byte types??
            if (set == true)
            {
                if (item.ValueType == typeof(Byte))
                    ac.RawData[item.Index] |= (byte)(Convert.ToByte(value) & (Byte)item.Mask);
                else
                    throw new Exception("Cannot toggle bits in non-byte value");
            }
            else
            {
                if (item.ValueType == typeof(Byte))
                    ac.RawData[item.Index] &= (byte)~(Convert.ToByte(value) & (Byte)item.Mask);
                else
                    throw new Exception("Cannot toggle bits in non-byte value");
            }
        }
    }

    internal class XmlActorDefinitionReaderException : Exception
    {
        public XmlActorDefinitionReaderException(string errorMessage) : base(errorMessage) { }
        public XmlActorDefinitionReaderException(string errorMessage, Exception innerEx) : base(errorMessage, innerEx) { }
    };
}
