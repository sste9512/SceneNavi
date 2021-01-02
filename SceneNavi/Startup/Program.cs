using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Windows.Forms;
using System.Threading;
using Autofac;
using MyNamespace;
using SceneNavi.ROMHandler;
using SceneNavi.ROMHandler.Interfaces;
using SceneNavi.Startup;

namespace SceneNavi
{
    static class Program
    {

        [STAThread]
        static void Main()
        {
            var appHost = new ApplicationHost<Startup.Startup>()
                   .Run();

        }
    }
}