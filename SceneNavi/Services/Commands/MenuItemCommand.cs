using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Threading;
using MediatR;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using SceneNavi.Forms;
using SceneNavi.Forms.MainForm;
using SceneNavi.HeaderCommands;
using SceneNavi.ROMHandler;
using SceneNavi.RomHandlers;
using SceneNavi.Utilities.OpenGLHelpers;

namespace SceneNavi.Services.Commands
{

    public class AboutCommand : INotification
    {

    }

    public class AboutCommandHandler : INotificationHandler<AboutCommand>
    {
        public Task Handle(AboutCommand notification, CancellationToken cancellationToken)
        {
            var linkerTimestamp = AssemblyHelpers.RetrieveLinkerTimestamp();

            var buildString =
                $"(Build: {linkerTimestamp.ToString("MM/dd/yyyy HH:mm:ss UTCzzz", System.Globalization.CultureInfo.InvariantCulture)})";
            var yearString = (linkerTimestamp.Year == 2013 ? "2013" : $"2013-{linkerTimestamp:yyyy}");

//            MessageBox.Show(
//                $"{Program.AppNameVer} {buildString}\n\nScene/room actor editor for The Legend of Zelda: Ocarina of Time\n\nWritten {yearString} by xdaniel / http://magicstone.de/dzd/",
//                $"About {Application.ProductName}", MessageBoxButtons.OK, MessageBoxIcon.Information);
           
            return Task.CompletedTask;
        }
    }



    public class RotationCommand : INotification
    {
        public WeakReference<MainForm> MainForm { get; set; }
        public Vector3d Rotation { get; set; }
    }

    public class MessageBoxCommand : INotification
    { 
        public WeakReference<MainForm> MainForm { get; set; }
    }

    public class MessageBoxRequest :  IRequest<Unit>
    {
        public WeakReference<MainForm> MainForm { get; set; }
    }



    public class RotationCommandHandler : INotificationHandler<RotationCommand>
    {
        public Task Handle(RotationCommand notification, CancellationToken cancellationToken)
        {
            MessageBox.Show("this worked", "Vertex Properties", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return Task.CompletedTask;
        }
    }



    public class EnableMipMapsCommand : INotification
    {
        public WeakReference<object> SenderReference { get; set; }
        public WeakReference<BaseRomHandler> BaseRomReference { get; set; }
        public bool DisplayListsDirtyReference { get; set; }
    }


    public class EnableMipMapsCommandHandler : INotificationHandler<EnableMipMapsCommand>
    {
        private readonly IMainFormConfig _mainFormConfig;

        public EnableMipMapsCommandHandler(IMainFormConfig mainFormConfig)
        {
            _mainFormConfig = mainFormConfig;
        }

        public Task Handle(EnableMipMapsCommand notification, CancellationToken cancellationToken)
        {
           
            notification.SenderReference.TryGetTarget(out var senderTarget);
            notification.BaseRomReference.TryGetTarget(out var baseRomReference);

            Configuration.EnableMipmaps = ((ToolStripMenuItem) senderTarget).Checked;

            if (baseRomReference == null || baseRomReference.Scenes == null) return Task.CompletedTask;

            /* Destroy, destroy! Kill all the display lists! ...or should I say "Exterminate!"? Then again, I'm not a Doctor Who fan... */
            foreach (var sh in baseRomReference.Scenes.SelectMany(x => x.GetSceneHeaders()))
            {
                var rooms = (sh.Commands.FirstOrDefault(x => x.Command == CommandTypeIDs.Rooms)) as Rooms;
                if (rooms == null) continue;

                foreach (var rh in rooms.RoomInformation.SelectMany(x => x.Headers))
                {
                    var mh = (rh.Commands.FirstOrDefault(x => x.Command == CommandTypeIDs.MeshHeader)) as MeshHeader;
                    mh?.DestroyDisplayLists();
                }
            }

            baseRomReference.Renderer.ResetTextureCache();

            _mainFormConfig.DisplayListsDirty = true;

            return Task.CompletedTask;
        }
    }





    public class CheckForUpdateCommand : INotification
    {
        public WeakReference<MainForm> MainForm { get; set; }
    }

    public class CheckForUpdateCommandHandler : INotificationHandler<CheckForUpdateCommand>
    {
        public Task Handle(CheckForUpdateCommand notification, CancellationToken cancellationToken)
        {
            new UpdateCheckDialog().ShowDialog();
            return Task.CompletedTask;
        }
    }








    public class RomInformationCommand : INotification
    {
        public WeakReference<BaseRomHandler> BaseRom { get; set; }
    }

    public class RomInformationCommandHandler : INotificationHandler<RomInformationCommand>
    {

        public RomInformationCommandHandler()
        {
            
        }
        
        public Task Handle(RomInformationCommand notification, CancellationToken cancellationToken)
        {
            notification.BaseRom.TryGetTarget(out var target);

            var info = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "{0} ({1}, v1.{2}), {3} MB ({4} Mbit)\n{5}\nCreated by {6}, built on {7:F}\n\nCode file at 0x{8:X} - 0x{9:X} ({10})\n- DMA table address: 0x{11:X}\n- File name table address: {12}\n" +
                "- Scene table address: {13}\n- Actor table address: {14}\n- Object table address: {15}\n- Entrance table address: {16}",
                target.Title, target.GameId, target.Version, (target.Size / 0x100000),
                (target.Size / 0x20000), (target.HasZ64TablesHack ? "(uses 'z64tables' extended tables)\n" : ""),
                target.Creator, target.BuildDate, target.Code.PStart,
                (target.Code.IsCompressed ? target.Code.PEnd : target.Code.VEnd),
                (target.Code.IsCompressed ? "compressed" : "uncompressed"), target.DmaTableAddress,
                (target.HasFileNameTable ? ("0x" + target.FileNameTableAddress.ToString("X")) : "none"),
                (target.HasZ64TablesHack
                    ? ("0x" + target.SceneTableAddress.ToString("X") + " (in ROM)")
                    : ("0x" + target.SceneTableAddress.ToString("X"))),
                (target.HasZ64TablesHack
                    ? ("0x" + target.ActorTableAddress.ToString("X") + " (in ROM)")
                    : ("0x" + target.ActorTableAddress.ToString("X"))),
                (target.HasZ64TablesHack
                    ? ("0x" + target.ObjectTableAddress.ToString("X") + " (in ROM)")
                    : ("0x" + target.ObjectTableAddress.ToString("X"))),
                (target.HasZ64TablesHack
                    ? ("0x" + target.EntranceTableAddress.ToString("X") + " (in ROM)")
                    : ("0x" + target.EntranceTableAddress.ToString("X"))));

            MessageBox.Show(info, "ROM Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
           
            
            return Task.CompletedTask;
        }
    }






    public class MenuItemCommand : INotification
    {
        public WeakReference<MainForm> MainForm { get; set; }
    }

    public class MenuItemOpenGlInformationCommandHandler : INotificationHandler<MenuItemCommand>
    {
        public Task Handle(MenuItemCommand notification, CancellationToken cancellationToken)
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

            oglInfoString.AppendFormat("{0} OpenGL extension(s) supported.\n",
                Initialization.SupportedExtensions.Length);
            oglInfoString.AppendLine();

            oglInfoString.AppendLine("Status of requested extensions:");

            foreach (var extension in MainFormConstants.AllRequiredOglExtensions)
                oglInfoString.AppendFormat("* {0}\t{1}\n", extension.PadRight(40),
                    Initialization.CheckForExtension(extension) ? "supported" : "not supported");

            MessageBox.Show(oglInfoString.ToString(), "OpenGL Information", MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            return Task.CompletedTask;
        }
    }
}