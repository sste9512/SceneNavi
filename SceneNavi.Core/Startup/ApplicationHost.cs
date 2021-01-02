using System;
using System.Threading;
using System.Windows.Forms;

namespace SceneNavi
{
    // Logic for the application inner contents to be run, alot like an asp.net core project
    public class ApplicationHost<T>
    {
        private T Tenant { get; set; }

        
        public static string AppNameVer = Application.ProductName + " " +
                                          VersionManagement.CreateVersionString(Application.ProductVersion);

        public static StatusMessageHandler Status = new StatusMessageHandler();
        public static bool IsHinting = false;
        


        /* Mutex & general app restart stuff from http://stackoverflow.com/a/9056664 */
        
        public ApplicationHost<T> Run()
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

                if (!runOnce.WaitOne(TimeSpan.Zero)) return this;
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


            return this;
        }
    }
}