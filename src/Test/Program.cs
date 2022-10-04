using System;
using X11;

namespace Test
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");

            XWindowManager windowManager = new XWindowManager();
            windowManager.Open(null);
            windowManager.GetFocusedWindow();
        }
    }
}