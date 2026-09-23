/**
 * Steamless - Copyright (c) 2015 - 2024 atom0s [atom0s@live.com]
 *
 * This work is licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International License.
 * To view a copy of this license, visit http://creativecommons.org/licenses/by-nc-nd/4.0/ or send a letter to
 * Creative Commons, PO Box 1866, Mountain View, CA 94042, USA.
 *
 * By using Steamless, you agree to the above license and its terms.
 *
 *      Attribution - You must give appropriate credit, provide a link to the license and indicate if changes were
 *                    made. You must do so in any reasonable manner, but not in any way that suggests the licensor
 *                    endorses you or your use.
 *
 *   Non-Commercial - You may not use the material (Steamless) for commercial purposes.
 *
 *   No-Derivatives - If you remix, transform, or build upon the material (Steamless), you may not distribute the
 *                    modified material. You are, however, allowed to submit the modified works back to the original
 *                    Steamless project in attempt to have it added to the original project.
 *
 * You may not apply legal terms or technological measures that legally restrict others
 * from doing anything the license permits.
 *
 * No warranties are given.
 */

namespace Steamless.Unpacker.Variant21.x86.Classes
{
    using System.Runtime.InteropServices;

    /// <summary>
    /// SteamStub DRM Variant 2.1 Header (Standard)
    ///
    /// This is the standard variant which includes the LoadLibraryW field.
    /// The D0 variant (SteamStub32Var21Header_D0Variant) omits LoadLibraryW,
    /// making it 4 bytes smaller. Selected when (structSize / 4) != 0xD0.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct SteamStub32Var21Header : ISteamStub32Var21Header
    {
        // Import addresses used by the stub; each is zero when the stub does not import that API.
        public uint XorKey; // The base XOR key, if defined, to unpack the file with.
        public uint GetModuleHandleA; // The address of GetModuleHandleA. (If set.)
        public uint GetModuleHandleW; // The address of GetModuleHandleW. (If set.)
        public uint GetProcAddress; // The address of GetProcAddress. (If set.)
        public uint LoadLibraryA; // The address of LoadLibraryA. (If set.)
        public uint LoadLibraryW; // The address of LoadLibraryW. (If set.)
        public uint BindSectionVirtualAddress; // The virtual address to the .bind section.
        public uint BindStartFunctionSize; // The size of the start function from the .bind section.
        public uint PayloadKeyMatch; // Matches the first 4 bytes of the payload data.
        public uint PayloadDataVirtualAddress; // The virtual address to the payload data.
        public uint PayloadDataSize; // The size of the payload data.
        public uint SteamAppID; // The steam application id of the packed file.
        public uint Unknown0001; // Unknown

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 0x08)]
        public byte[] SteamAppIDString; // The SteamAppID of the packed file, in string format.

       // Offsets into the payload data; the stored values are the VA and size of the SteamDRMP.dll data.
        public uint SteamDRMPDllVirtualAddress;
        public uint SteamDRMPDllSize; // The offset inside of the payload data holding the size of the SteamDRMP.dll file data.
        public uint XTeaKeys; // Offset into the payload data where the XTEA key array begins.

        // The 'StubData' field is dynamically sized based on the stub version and used options, so it
        // cannot be marshalled with a fixed size; the caller slices the remainder of the header instead.

        uint ISteamStub32Var21Header.PayloadDataVirtualAddress => PayloadDataVirtualAddress;
        uint ISteamStub32Var21Header.PayloadDataSize => PayloadDataSize;
        uint ISteamStub32Var21Header.SteamDRMPDllVirtualAddress => SteamDRMPDllVirtualAddress;
        uint ISteamStub32Var21Header.SteamDRMPDllSize => SteamDRMPDllSize;
        uint ISteamStub32Var21Header.XTeaKeys => XTeaKeys;
    }

    /// <summary>
    /// SteamStub DRM Variant 2.1 Header (Header Size: 0xD0 Variant)
    ///
    /// Identical to SteamStub32Var21Header except it omits the LoadLibraryW
    /// field (4 bytes smaller). Selected when (structSize / 4) == 0xD0.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct SteamStub32Var21Header_D0Variant : ISteamStub32Var21Header
    {
        // Import addresses used by the stub; each is zero when the stub does not import that API.
        public uint XorKey; // The base XOR key, if defined, to unpack the file with.
        public uint GetModuleHandleA; // The address of GetModuleHandleA. (If set.)
        public uint GetModuleHandleW; // The address of GetModuleHandleW. (If set.)
        public uint GetProcAddress; // The address of GetProcAddress. (If set.)
        public uint LoadLibraryA; // The address of LoadLibraryA. (If set.)
        public uint BindSectionVirtualAddress; // The virtual address to the .bind section.
        public uint BindStartFunctionSize; // The size of the start function from the .bind section.
        public uint PayloadKeyMatch; // Matches the first 4 bytes of the payload data.
        public uint PayloadDataVirtualAddress; // The virtual address to the payload data.
        public uint PayloadDataSize; // The size of the payload data.
        public uint SteamAppID; // The steam application id of the packed file.
        public uint Unknown0000; // Unknown

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 0x08)]
        public byte[] SteamAppIDString; // The SteamAppID of the packed file, in string format.

        // Offsets into the payload data; the stored values are the VA and size of the SteamDRMP.dll data.
        public uint SteamDRMPDllVirtualAddress;
        public uint SteamDRMPDllSize; // The offset inside of the payload data holding the size of the SteamDRMP.dll file data.
        public uint XTeaKeys; // Offset into the payload data where the XTEA key array begins.

        // The 'StubData' field is dynamically sized based on the stub version and used options, so it
        // cannot be marshalled with a fixed size; the caller slices the remainder of the header instead.

        uint ISteamStub32Var21Header.PayloadDataVirtualAddress => PayloadDataVirtualAddress;
        uint ISteamStub32Var21Header.PayloadDataSize => PayloadDataSize;
        uint ISteamStub32Var21Header.SteamDRMPDllVirtualAddress => SteamDRMPDllVirtualAddress;
        uint ISteamStub32Var21Header.SteamDRMPDllSize => SteamDRMPDllSize;
        uint ISteamStub32Var21Header.XTeaKeys => XTeaKeys;
    }
}
