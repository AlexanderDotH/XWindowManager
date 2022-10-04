using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace X11
{
    public class XWindowManager : IXWindowManager
    {
        private SafeHandle _display;

        public void Open(string displayName)
        {
            _display = new XDisplayHandle(Native.XOpenDisplay(displayName), true);
            if (_display.IsInvalid)
            {
                throw new XWindowException($"Could not open display: {displayName}");
            }
        }

        public void Close()
        {
            if (!_display.IsInvalid)
            {
                _display.Dispose();
            }
        }

        public bool TryGetXWindows(out List<XWindowInfo> windows)
        {
            ThrowIfNotOpened();
            return TryGetXWindows(_display, out windows);
        }

        public void Dispose()
        {
            Close();
        }

        private void ThrowIfNotOpened()
        {
            if (_display.IsInvalid)
            {
                throw new XWindowException(
                    "Display is not defined. Before call Open method before invoke the method.");
            }
        }

        private static bool TryGetXWindows(SafeHandle display, out List<XWindowInfo> windows)
        {
            windows = new List<XWindowInfo>();

            using (var clientList = GetClientList(display, out var clientListSize))
            {
                if (clientList.IsInvalid)
                {
                    return false;
                }

                for (var i = 0; i < (int) clientListSize; i++)
                {
                    var win = Marshal.ReadIntPtr(clientList.DangerousGetHandle(), i * IntPtr.Size);
                    var wmClass = ParseWmClass(GetXWindowClass(display, win));
                    var windowTitle = GetWindowTitle(display, win);
                    var pid = GetPid(display, win);
                    var clientMachine = GetClientMachine(display, win);
                    Native.XGetGeometry(display, win, out var junkRoot, out var junkX, out var junkY, out var width,
                        out var height, out var borderWidth, out var depth);
                    windows.Add(new XWindowInfo
                    {
                        Id = win,
                        WmClass = wmClass,
                        WmName = windowTitle,
                        WmPid = pid,
                        WmClientMachine = clientMachine,
                        Geometry = new Geometry
                        {
                            X = junkX,
                            Y = junkY,
                            Width = width,
                            Height = height,
                            BorderWidth = borderWidth,
                            Depth = depth
                        }
                    });
                }
            }

            return true;
        }

        public void GetFocusedWindow()
        {
            var xaPropName = Native.XInternAtom(this._display, "_NET_ACTIVE_WINDOW", false);
            Console.WriteLine(xaPropName);
            Debug.WriteLine(xaPropName);
        }
        
        private static WmClass ParseWmClass(string xWindowClass)
        {
            var classes = xWindowClass
                .Split('\0')
                .Where(_ => !string.IsNullOrWhiteSpace(_))
                .ToArray();
            var instance = classes.Length > 0 ? classes[0] : string.Empty;
            var @class = classes.Length > 1 ? classes[1] : string.Empty;

            return new WmClass {InstanceName = instance, ClassName = @class};
        }

        private static string GetXWindowClass(SafeHandle display, IntPtr win) =>
            GetPropertyString(display, win, "WM_CLASS") ?? string.Empty;

        private static string GetClientMachine(SafeHandle display, IntPtr win) =>
            GetPropertyString(display, win, "WM_CLIENT_MACHINE");

        private static string GetPropertyString(SafeHandle display, IntPtr win, string propName,
            ulong propType = (ulong) Native.XAtom.XA_STRING)
        {
            using (var handle = GetProperty(display, win, propType, propName, out var size))
            {
                return GetString(handle, size);
            }
        }

        private static ulong GetPropertyNumber(SafeHandle display, IntPtr win, string propName)
        {
            using (var handle = GetProperty(display, win, Native.XAtom.XA_CARDINAL, propName, out _))
            {
                return handle.IsInvalid ? default(ulong) : Marshal.PtrToStructure<ulong>(handle.DangerousGetHandle());
            }
        }

        private static string GetString(SafeHandle handle, ulong size)
        {
            return handle.IsInvalid ? default(string) : Marshal.PtrToStringAnsi(handle.DangerousGetHandle(), (int) size);
        }

        private static string GetWindowTitle(SafeHandle display, IntPtr win)
        {
            var netWmName = GetPropertyString(display, win, "_NET_WM_NAME",
                Native.XInternAtom(display, "UTF8_STRING", false));
            var wmName = GetPropertyString(display, win, "WM_NAME");
            return netWmName ?? wmName;
        }

        private static ulong GetPid(SafeHandle display, IntPtr win) => GetPropertyNumber(display, win, "_NET_WM_PID");

        private static SafeHandle GetClientList(SafeHandle display, out ulong size)
        {
            SafeHandle clientList;

            if ((clientList = GetProperty(display, Native.XDefaultRootWindow(display),
                Native.XAtom.XA_WINDOW, "_NET_CLIENT_LIST", out size)).IsInvalid)
            {
                if ((clientList = GetProperty(display, Native.XDefaultRootWindow(display),
                    Native.XAtom.XA_CARDINAL, "_WIN_CLIENT_LIST", out size)).IsInvalid)
                {
                    return new XPropertyHandle(IntPtr.Zero, false);
                }
            }

            return clientList;
        }
        
        private static SafeHandle GetProperty(SafeHandle display, IntPtr win,
            Native.XAtom xaPropType, string propName, out ulong size) =>
            GetProperty(display, win, (ulong) xaPropType, propName, out size);

        private static SafeHandle GetProperty(SafeHandle display, IntPtr win, ulong xaPropType, string propName,
            out ulong size)
        {
            size = 0;

            var xaPropName = Native.XInternAtom(display, propName, false);

            if (Native.XGetWindowProperty(display, win, xaPropName, 0,
                    4096 / 4, false, xaPropType, out var actualTypeReturn, out var actualFormatReturn,
                    out var nItemsReturn, out var bytesAfterReturn, out var propReturn) != 0)
            {
                return new XPropertyHandle(IntPtr.Zero, false);
            }

            if (actualTypeReturn != xaPropType)
            {
                return new XPropertyHandle(IntPtr.Zero, false);
            }

            size = nItemsReturn;
            return new XPropertyHandle(propReturn, false);
        }
    }
}
