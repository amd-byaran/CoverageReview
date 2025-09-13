using System;
using System.Runtime.InteropServices;
using System.Text;

namespace CoverageAnalyzerGUI.Interop
{
    /// <summary>
    /// P/Invoke wrapper for FunctionalCoverageParsers.dll
    /// </summary>
    public static class FunctionalParserWrapper
    {
        private const string DLL_NAME = "FunctionalCoverageParsers.dll";

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr ParseFunctionalFile([MarshalAs(UnmanagedType.LPStr)] string filePath);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern void FreeFunctionalData(IntPtr data);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern int GetFunctionalCount(IntPtr data);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr GetFunctionalName(IntPtr data, int index);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr GetFunctionalDescription(IntPtr data, int index);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern int GetFunctionalStatus(IntPtr data, int index);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern double GetFunctionalWeight(IntPtr data, int index);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr GetFunctionalCategory(IntPtr data, int index);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern long GetFunctionalTimestamp(IntPtr data, int index);

        // Helper method to convert IntPtr string to managed string
        public static string PtrToString(IntPtr ptr)
        {
            if (ptr == IntPtr.Zero)
                return string.Empty;
            
            return Marshal.PtrToStringAnsi(ptr) ?? string.Empty;
        }
    }
}