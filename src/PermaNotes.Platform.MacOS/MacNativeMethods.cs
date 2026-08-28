using System;
using System.Runtime.InteropServices;

namespace PermaNotes.Platform.MacOS
{
    internal static class MacNativeMethods
    {
        private const string ObjCLibrary = "/usr/lib/libobjc.A.dylib";
        private const string CoreGraphicsLibrary = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";

        [DllImport(ObjCLibrary)]
        public static extern IntPtr sel_registerName(string name);

        [DllImport(ObjCLibrary)]
        public static extern IntPtr objc_getClass(string name);

        [DllImport(ObjCLibrary)]
        public static extern IntPtr object_getClass(IntPtr obj);

        [DllImport(ObjCLibrary)]
        public static extern IntPtr class_getInstanceMethod(IntPtr cls, IntPtr sel);

        [DllImport(ObjCLibrary)]
        public static extern IntPtr method_getImplementation(IntPtr m);

        [DllImport(ObjCLibrary)]
        public static extern IntPtr class_replaceMethod(IntPtr cls, IntPtr name, IntPtr imp, string types);

        // objc_msgSend variants
        [DllImport(ObjCLibrary, EntryPoint = "objc_msgSend")]
        public static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector);

        [DllImport(ObjCLibrary, EntryPoint = "objc_msgSend")]
        public static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector, int arg1);

        [DllImport(ObjCLibrary, EntryPoint = "objc_msgSend")]
        public static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector, long arg1);

        [DllImport(ObjCLibrary, EntryPoint = "objc_msgSend")]
        public static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector, ulong arg1);

        [DllImport(ObjCLibrary, EntryPoint = "objc_msgSend")]
        public static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector, IntPtr arg1);

        [DllImport(ObjCLibrary, EntryPoint = "objc_msgSend")]
        public static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector, IntPtr arg1, IntPtr arg2, IntPtr arg3);

        [DllImport(ObjCLibrary, EntryPoint = "objc_msgSend")]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool objc_msgSend_bool(IntPtr receiver, IntPtr selector);

        [DllImport(ObjCLibrary, EntryPoint = "objc_msgSend")]
        public static extern void objc_msgSend_setBool(IntPtr receiver, IntPtr selector, [MarshalAs(UnmanagedType.I1)] bool arg1);

        // CoreGraphics window level lookup
        [DllImport(CoreGraphicsLibrary, EntryPoint = "CGWindowLevelForKey")]
        public static extern int CGWindowLevelForKey(int key);

        // Well-known CGWindowLevelKey constants
        public const int kCGDesktopWindowLevelKey = 2;
        public const int kCGNormalWindowLevelKey = 4;
        public const int kCGFloatingWindowLevelKey = 5;

        // NSWindowCollectionBehavior constants
        public const ulong NSWindowCollectionBehaviorCanJoinAllSpaces = 1UL << 0; // 1
        public const ulong NSWindowCollectionBehaviorStationary = 1UL << 4;       // 16
        public const ulong NSWindowCollectionBehaviorIgnoresCycle = 1UL << 6;     // 64
        public const ulong NSWindowCollectionBehaviorDefault = 0UL;

        public static int GetDesktopWindowLevel()
        {
            try
            {
                return CGWindowLevelForKey(kCGDesktopWindowLevelKey);
            }
            catch
            {
                return -1000; // standard Cocoa fallback
            }
        }

        public static int GetNormalWindowLevel()
        {
            try
            {
                return CGWindowLevelForKey(kCGNormalWindowLevelKey);
            }
            catch
            {
                return 0; // standard normal level
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct CGPoint
    {
        public double X;
        public double Y;

        public CGPoint(double x, double y)
        {
            X = x;
            Y = y;
        }
    }
}
