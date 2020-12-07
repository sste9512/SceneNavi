﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Windows.Forms;
using System.Drawing.Imaging;
using System.ComponentModel;
using System.Drawing;

namespace SceneNavi
{
    public static class ExtensionMethods
    {
        // http://stackoverflow.com/a/3588137
        public static void UiThread(this Control @this, Action code)
        {
            if (@this.InvokeRequired)
            {
                @this.BeginInvoke(code);
            }
            else
            {
                code.Invoke();
            }
        }

        public static void DoubleBuffered(this Control ctrl, bool setting)
        {
            var ctrlType = ctrl.GetType();
            var pi = ctrlType.GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);
            if (pi != null) pi.SetValue(ctrl, setting, null);
        }

        public static byte Scale(this byte val, byte min, byte max, byte minScale, byte maxScale)
        {
            return (byte)(minScale + (float)(val - min) / (max - min) * (maxScale - minScale));
        }

        public static float Scale(this float val, float min, float max, float minScale, float maxScale)
        {
            return (minScale + (float)(val - min) / (max - min) * (maxScale - minScale));
        }

        public static T Clamp<T>(this T val, T min, T max) where T : IComparable<T>
        {
            if (val.CompareTo(min) < 0) return min;
            else if (val.CompareTo(max) > 0) return max;
            else return val;
        }

        public static void Init<T>(this T[] array, T defaultValue)
        {
            if (array == null)
                return;

            for (var i = 0; i < array.Length; i++)
            {
                array[i] = defaultValue;
            }
        }

        public static void Fill<T>(this T[] array, T[] data)
        {
            if (array == null)
                return;

            for (var i = 0; i < array.Length; i += data.Length)
            {
                for (var j = 0; j < data.Length; j++)
                {
                    try
                    {
                        array[i + j] = data[j];
                    }
                    catch
                    {
                        return;
                    }
                }
            }
        }

        public static IEnumerable<TreeNode> FlattenTree(this TreeView tv)
        {
            return FlattenTree(tv.Nodes);
        }

        public static IEnumerable<TreeNode> FlattenTree(this TreeNodeCollection coll)
        {
            return coll.Cast<TreeNode>().Concat(coll.Cast<TreeNode>().SelectMany(x => FlattenTree(x.Nodes)));
        }

        public static IEnumerable<ToolStripMenuItem> FlattenHintMenu(this MenuStrip menu)
        {
            //return FlattenMenu(menu.Items);
            return null;
        }

//        public static IEnumerable<ToolStripHintMenuItem> FlattenMenu(this ToolStripItemCollection coll)
//        {
//            return coll.OfType<ToolStripHintMenuItem>().Concat(coll.OfType<ToolStripHintMenuItem>().SelectMany(x => FlattenMenu(x.DropDownItems)));
//        }

        public static void SetCommonImageFilter(this FileDialog fileDialog)
        {
            fileDialog.SetCommonImageFilter(null);
        }

        public static void SetCommonImageFilter(this FileDialog fileDialog, string defaultExtension)
        {
            var codecs = ImageCodecInfo.GetImageEncoders().ToList();
            var imageExtensions = string.Join(";", codecs.Select(ici => ici.FilenameExtension));
            var separateFilters = new List<string>();
            foreach (var codec in codecs) separateFilters.Add(string.Format("{0} Files ({1})|{1}", codec.FormatDescription, codec.FilenameExtension.ToLowerInvariant()));
            fileDialog.Filter = string.Format("{0}|Image Files ({1})|{1}|All Files (*.*)|*.*", string.Join("|", separateFilters), imageExtensions.ToLowerInvariant());
            if (defaultExtension != null) fileDialog.FilterIndex = (codecs.IndexOf(codecs.FirstOrDefault(x => x.FormatDescription.ToLowerInvariant().Contains(defaultExtension.ToLowerInvariant()))) + 1);
            else fileDialog.FilterIndex = (codecs.Count + 1);
        }

        public static void SwapRGBAToBGRA(this byte[] buffer)
        {
            for (var i = 0; i < buffer.Length; i += 4)
            {
                var red = buffer[i];
                buffer[i] = buffer[i + 2];
                buffer[i + 2] = red;
            }
        }

        public static string GetDescription(this Type objectType, string field)
        {
            var propertyDescriptor = TypeDescriptor.GetProperties(objectType)[field];
            if (propertyDescriptor == null) return field;

            var attributes = propertyDescriptor.Attributes;
            var description = (DescriptionAttribute)attributes[typeof(DescriptionAttribute)];

            if (description.Description == string.Empty) return field;

            return description.Description;
        }

        public static SizeF MeasureString(this string s, Font font, StringFormat strFormat)
        {
            SizeF result;
            using (var image = new Bitmap(1, 1))
            {
                using (var g = Graphics.FromImage(image))
                {
                    result = g.MeasureString(s, font, int.MaxValue, strFormat);
                }
            }

            return result;
        }
    }
}
