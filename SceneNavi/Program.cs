using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.Threading;
using Autofac;
using SceneNavi.ROMHandler;
using SceneNavi.ROMHandler.Interfaces;
using SceneNavi.Startup;

namespace SceneNavi
{
    static class Program
    {
        public static string AppNameVer = Application.ProductName + " " +
                                          VersionManagement.CreateVersionString(Application.ProductVersion);

        public static StatusMessageHandler Status = new StatusMessageHandler();
        public static bool IsHinting = false;


        /* Mutex & general app restart stuff from http://stackoverflow.com/a/9056664 */

        [STAThread]
        static void Main()
        {
            Mutex runOnce = null;


            Console.Write("Initialising");

            if (Configuration.IsRestarting)
            {
                Configuration.IsRestarting = false;
                Thread.Sleep(3000);
            }

            try
            {
                runOnce = new Mutex(true, "SOME_MUTEX_NAME");

                if (!runOnce.WaitOne(TimeSpan.Zero)) return;
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(Di.Resolve<Form>(nameof(MainForm)));
               // Application.Idle += Application_Idle;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Critical error occured: " + ex.GetType().FullName + " - " + ex.Message + "\nTarget site: " +
                    ex.TargetSite, "Exception", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
            finally
            {
                runOnce?.Close();
            }
        }
    }
}