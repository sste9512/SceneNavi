using System.Collections.Generic;

namespace SceneNavi.Framework.Client.Dependencies.Interfaces
{
    public interface IMemento<TState>
    {
        IDictionary<string,TState> States { get; set; }
        void AddState(string message, TState state);
        void Undo();
        void Redo();
    }
}
