using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace SceneNavi
{
    public interface INavigation
    {
        
        void Move<T>() where T : Form;
        T ShowModal<T>() where T : Form, new();
    }
}