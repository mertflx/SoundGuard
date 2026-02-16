using System.Runtime.InteropServices;

namespace SoundGuard.Interop;

/// <summary>
/// SetupAPI ve CfgMgr32 P/Invoke tanımlamaları.
/// Bluetooth cihaz servislerini yönetmek için gerekli Windows API'leri.
/// </summary>
internal static partial class NativeMethods
{
    // ──────────────────────────────────────────────
    //  SetupAPI DLL İmportları
    // ──────────────────────────────────────────────

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr SetupDiGetClassDevs(
        ref Guid classGuid,
        string? enumerator,
        IntPtr hwndParent,
        uint flags);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetupDiEnumDeviceInfo(
        IntPtr deviceInfoSet,
        uint memberIndex,
        ref SP_DEVINFO_DATA deviceInfoData);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetupDiGetDeviceRegistryProperty(
        IntPtr deviceInfoSet,
        ref SP_DEVINFO_DATA deviceInfoData,
        uint property,
        out uint propertyRegDataType,
        byte[]? propertyBuffer,
        uint propertyBufferSize,
        out uint requiredSize);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetupDiSetClassInstallParams(
        IntPtr deviceInfoSet,
        ref SP_DEVINFO_DATA deviceInfoData,
        ref SP_PROPCHANGE_PARAMS classInstallParams,
        uint classInstallParamsSize);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetupDiCallClassInstaller(
        uint installFunction,
        IntPtr deviceInfoSet,
        ref SP_DEVINFO_DATA deviceInfoData);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetupDiGetDeviceInstanceId(
        IntPtr deviceInfoSet,
        ref SP_DEVINFO_DATA deviceInfoData,
        char[] deviceInstanceId,
        uint deviceInstanceIdSize,
        out uint requiredSize);

    // ──────────────────────────────────────────────
    //  CfgMgr32 DLL İmportları
    // ──────────────────────────────────────────────

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    public static extern uint CM_Get_DevNode_Status(
        out uint status,
        out uint problemNumber,
        uint devInst,
        uint flags);

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    public static extern uint CM_Locate_DevNode(
        out uint devInst,
        string deviceInstanceId,
        uint flags);

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    public static extern uint CM_Enable_DevNode(
        uint devInst,
        uint flags);

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    public static extern uint CM_Disable_DevNode(
        uint devInst,
        uint flags);

    // ──────────────────────────────────────────────
    //  Sabitler (Constants)
    // ──────────────────────────────────────────────

    // SetupDiGetClassDevs flags
    public const uint DIGCF_PRESENT = 0x00000002;
    public const uint DIGCF_ALLCLASSES = 0x00000004;
    public const uint DIGCF_PROFILE = 0x00000008;

    // Device registry property codes
    public const uint SPDRP_DEVICEDESC = 0x00000000;
    public const uint SPDRP_HARDWAREID = 0x00000001;
    public const uint SPDRP_FRIENDLYNAME = 0x0000000C;
    public const uint SPDRP_ENUMERATOR_NAME = 0x00000016;

    // Class installer function codes
    public const uint DIF_PROPERTYCHANGE = 0x00000012;

    // Property change state codes
    public const uint DICS_ENABLE = 0x00000001;
    public const uint DICS_DISABLE = 0x00000002;

    // Scope
    public const uint DICS_FLAG_GLOBAL = 0x00000001;

    // CM_Get_DevNode_Status flags
    public const uint DN_STARTED = 0x00000008;
    public const uint DN_DISABLEABLE = 0x00002000;

    // CM_Locate_DevNode flags
    public const uint CM_LOCATE_DEVNODE_NORMAL = 0x00000000;
    public const uint CM_LOCATE_DEVNODE_PHANTOM = 0x00000001;

    public const uint CR_SUCCESS = 0x00000000;

    public static readonly IntPtr INVALID_HANDLE_VALUE = new(-1);

    // ──────────────────────────────────────────────
    //  Yapılar (Structs)
    // ──────────────────────────────────────────────

    [StructLayout(LayoutKind.Sequential)]
    public struct SP_DEVINFO_DATA
    {
        public uint cbSize;
        public Guid ClassGuid;
        public uint DevInst;
        public IntPtr Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SP_CLASSINSTALL_HEADER
    {
        public uint cbSize;
        public uint InstallFunction;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SP_PROPCHANGE_PARAMS
    {
        public SP_CLASSINSTALL_HEADER ClassInstallHeader;
        public uint StateChange;
        public uint Scope;
        public uint HwProfile;
    }
}
