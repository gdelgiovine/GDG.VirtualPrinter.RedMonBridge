namespace GDG.VirtualPrinter.Core;

using System;
using System.IO;
using System.Runtime.InteropServices;

/// <summary>
/// Converts OpenXPS (.oxps) to Microsoft XPS (.xps) using the XPS Object Model
/// already present in Windows. No native helper DLL and no XpsConverter.exe.
/// </summary>
public static class XpsFormatConverter
{
    // CLSID_XpsOMObjectFactory
    private static readonly Guid ClsidXpsOmObjectFactory =
        new("E974D26D-3D9B-4D47-88CC-3872F2DC3585");

    // IID_IXpsOMObjectFactory1
    private static readonly Guid IidXpsOmObjectFactory1 =
        new("0A91B617-D612-4181-BF7C-BE5824E9CC8F");

    private const uint CLSCTX_INPROC_SERVER = 0x1;
    private const uint FILE_ATTRIBUTE_NORMAL = 0x80;

    private const int S_OK = 0;
    private const int S_FALSE = 1;
    private const int RPC_E_CHANGED_MODE = unchecked((int)0x80010106);

    // IXpsOMObjectFactory1 inherits IXpsOMObjectFactory.
    // CreatePackageFromFile1 is vtable slot 48 (IUnknown starts at slot 0).
    private const int FactoryCreatePackageFromFile1Slot = 48;

    // IXpsOMPackage1 inherits IXpsOMPackage.
    // WriteToFile1 is vtable slot 14.
    private const int PackageWriteToFile1Slot = 14;

    public static void ConvertOxpsToXps(string inputFile, string outputFile)
    {
        if (string.IsNullOrWhiteSpace(inputFile))
            throw new ArgumentException("Input file is required.", nameof(inputFile));

        if (string.IsNullOrWhiteSpace(outputFile))
            throw new ArgumentException("Output file is required.", nameof(outputFile));

        if (!File.Exists(inputFile))
            throw new FileNotFoundException("OXPS source file not found.", inputFile);

        string? outputDirectory = Path.GetDirectoryName(outputFile);
        if (!string.IsNullOrWhiteSpace(outputDirectory))
            Directory.CreateDirectory(outputDirectory);

        if (File.Exists(outputFile))
            File.Delete(outputFile);

        int initHr = CoInitializeEx(IntPtr.Zero, COINIT.COINIT_MULTITHREADED);
        bool mustUninitialize = initHr == S_OK || initHr == S_FALSE;

        if (initHr < 0 && initHr != RPC_E_CHANGED_MODE)
            Marshal.ThrowExceptionForHR(initHr);

        IntPtr factory = IntPtr.Zero;
        IntPtr package = IntPtr.Zero;

        try
        {
            Guid clsid = ClsidXpsOmObjectFactory;
            Guid iid = IidXpsOmObjectFactory1;

            int hr = CoCreateInstance(
                ref clsid,
                IntPtr.Zero,
                CLSCTX_INPROC_SERVER,
                ref iid,
                out factory);

            if (hr < 0)
                Marshal.ThrowExceptionForHR(hr);

            if (factory == IntPtr.Zero)
                throw new InvalidOperationException(
                    "Windows did not return IXpsOMObjectFactory1.");

            var createPackage = GetVTableDelegate<CreatePackageFromFile1Delegate>(
                factory,
                FactoryCreatePackageFromFile1Slot);

            hr = createPackage(
                factory,
                inputFile,
                false, // reuseObjects = FALSE
                out package);

            if (hr < 0)
                Marshal.ThrowExceptionForHR(hr);

            if (package == IntPtr.Zero)
                throw new InvalidOperationException(
                    "Windows did not create an IXpsOMPackage1 instance.");

            var writeToFile = GetVTableDelegate<WriteToFile1Delegate>(
                package,
                PackageWriteToFile1Slot);

            hr = writeToFile(
                package,
                outputFile,
                IntPtr.Zero,
                FILE_ATTRIBUTE_NORMAL,
                false, // optimizeMarkupSize = FALSE
                XpsDocumentType.Xps);

            if (hr < 0)
                Marshal.ThrowExceptionForHR(hr);

            if (!File.Exists(outputFile))
                throw new IOException(
                    "XPS Object Model completed without creating the XPS file.");
        }
        finally
        {
            if (package != IntPtr.Zero)
                Marshal.Release(package);

            if (factory != IntPtr.Zero)
                Marshal.Release(factory);

            if (mustUninitialize)
                CoUninitialize();
        }
    }

    private static T GetVTableDelegate<T>(IntPtr comInterface, int slot)
        where T : Delegate
    {
        IntPtr vtable = Marshal.ReadIntPtr(comInterface);
        if (vtable == IntPtr.Zero)
            throw new InvalidOperationException("Invalid COM vtable.");

        IntPtr method = Marshal.ReadIntPtr(vtable, checked(slot * IntPtr.Size));
        if (method == IntPtr.Zero)
            throw new InvalidOperationException($"COM method slot {slot} is null.");

        return Marshal.GetDelegateForFunctionPointer<T>(method);
    }

    private enum XpsDocumentType : int
    {
        Unspecified = 1,
        Xps = 2,
        OpenXps = 3
    }

    [Flags]
    private enum COINIT : uint
    {
        COINIT_MULTITHREADED = 0x0
    }

    [UnmanagedFunctionPointer(
        CallingConvention.StdCall,
        CharSet = CharSet.Unicode)]
    private delegate int CreatePackageFromFile1Delegate(
        IntPtr @this,
        [MarshalAs(UnmanagedType.LPWStr)] string filename,
        [MarshalAs(UnmanagedType.Bool)] bool reuseObjects,
        out IntPtr package);

    [UnmanagedFunctionPointer(
        CallingConvention.StdCall,
        CharSet = CharSet.Unicode)]
    private delegate int WriteToFile1Delegate(
        IntPtr @this,
        [MarshalAs(UnmanagedType.LPWStr)] string fileName,
        IntPtr securityAttributes,
        uint flagsAndAttributes,
        [MarshalAs(UnmanagedType.Bool)] bool optimizeMarkupSize,
        XpsDocumentType documentType);

    [DllImport("ole32.dll")]
    private static extern int CoInitializeEx(
        IntPtr pvReserved,
        COINIT dwCoInit);

    [DllImport("ole32.dll")]
    private static extern void CoUninitialize();

    [DllImport("ole32.dll", ExactSpelling = true)]
    private static extern int CoCreateInstance(
        ref Guid rclsid,
        IntPtr pUnkOuter,
        uint dwClsContext,
        ref Guid riid,
        out IntPtr ppv);
}
