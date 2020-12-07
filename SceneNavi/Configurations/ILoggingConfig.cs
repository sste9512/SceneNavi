using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SceneNavi.Configurations
{
    interface ILoggingConfig
    {
         string LoggingLocation { get; set; }
         IList<string> Messages { get; set; }
    }
}
