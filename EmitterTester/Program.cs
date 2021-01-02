using System;
using SceneNavi.Framework.Client.Dependencies.Providers;

namespace EmitterTester
{
    internal class Program
    {
        public static void Main(string[] args)
        {
            var stop = new ReactiveStopwatch();
            stop.Subscribe(x =>
            {
                Console.WriteLine(x);
                if (x == 60)
                {
                    stop.Restart();
                }
            });
            stop.Start();
            
        }
    }
}