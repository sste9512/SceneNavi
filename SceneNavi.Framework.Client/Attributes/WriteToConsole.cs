using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using MethodDecorator.Fody.Interfaces;

namespace SceneNavi.Framework.Client.Attributes
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Assembly | AttributeTargets.Module)]
    public class WriteToConsole : Attribute, IMethodDecorator
    {
        private MethodBase _methodBase;
        private object _instance;
        private object[] _args;

        public void Init(object instance, MethodBase method, object[] args)
        {
            _methodBase = method;
            _instance = instance;
            _args = args;
            
            Console.WriteLine(instance.GetType());
            Console.WriteLine(method.GetType());
        }

        public void OnEntry()
        {
            Console.WriteLine(_methodBase.Name);
            Console.WriteLine("Executing -> " + _methodBase.Name);
            foreach (var arg in _args)
            {
                Console.WriteLine("With params " + arg.GetType() + " : "  + arg);
            }
            
        }

        public void OnExit()
        {
            Console.WriteLine(_methodBase.Name);
            Console.WriteLine("Finish -> " + _methodBase.Name);
        }

        public void OnException(Exception exception)
        {
            Console.WriteLine(_methodBase.Name);
            Console.WriteLine("Error Occurred -> " + _methodBase.Name + " -> " + exception.Message);
        }
    }
}
