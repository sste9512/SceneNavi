using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SceneNavi.Dependencies
{
    public interface IMemento<TState>
    {
        IDictionary<string,TState> States { get; set; }
        void AddState(string message, TState state);
        void Undo();
        void Redo();
    }
}
