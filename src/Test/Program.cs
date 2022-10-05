using System;
using X11;

namespace Test
{
    internal class Program
    {
        static void Main(string[] args)
        {
            XWindowManager windowManager = new XWindowManager();
            windowManager.Open(null);
            while (true)
            {
                Thread.Sleep(1000);
                windowManager.GetFocusedWindow();
            }
        }
    }
}