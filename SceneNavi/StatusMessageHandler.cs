using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using SceneNavi.ROMHandler;

namespace SceneNavi
{
    public class StatusMessageHandler
    {
        
        public event MessageChangedEvent MessageChanged;

        string _lastMessage;
        
        public delegate void MessageChangedEvent(object sender, MessageChangedEventArgs e);

        public class MessageChangedEventArgs : EventArgs
        {
            public string Message { get; set; }
           
        }
        
        public string Message
        {
            get => _lastMessage;
            set
            {
                _lastMessage = value;
                var ev = MessageChanged;
                ev?.Invoke(this, new MessageChangedEventArgs() {Message = _lastMessage});
            }
        }
    }
}