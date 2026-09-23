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

namespace Steamless.Unpacker.Variant21.x86
{
    using API;
    using API.Crypto;
    using API.Events;
    using API.Extensions;
    using API.Model;
    using API.PE32;
    using API.Services;
    using Classes;
    using Iced.Intel;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.InteropServices;
    using System.Security.Cryptography;

    [SteamlessApiVersion(1, 0)]
    public class Main : SteamlessPlugin
    {
        private const int AesIvSize = 16;
        
        /// <summary>
        /// Internal logging service instance.
        /// </summary>
        private LoggingService m_LoggingService;

        /// <summary>
        /// Gets the author of this plugin.
        /// </summary>
        public override string Author => "atom0s";

        /// <summary>
        /// Gets the name of this plugin.
        /// </summary>
        public override string Name => "SteamStub Variant 2.1 Unpacker (x86)";

        /// <summary>
        /// Gets the description of this plugin.
        /// </summary>
        public override string Description => "Unpacker for the 32bit SteamStub variant 2.1.";

        /// <summary>
        /// Gets the version of this plugin.
        /// </summary>
        public override Version Version => Assembly.GetExecutingAssembly().GetName().Version;

        /// <summary>
        /// Internal wrapper to log a message.
        /// </summary>
        /// <param name="msg"></param>
        /// <param name="type"></param>
        private void Log(string msg, LogMessageType type)
        {
            this.m_LoggingService.OnAddLogMessage(this, new LogMessageEventArgs(msg, type));
        }

        /// <summary>
        /// Initialize function called when this plugin is first loaded.
        /// </summary>
        /// <param name="logService"></param>
        /// <returns></returns>
        public override bool Initialize(LoggingService logService)
        {
            this.m_LoggingService = logService;
            return true;
        }

        /// <summary>
        /// Processing function called when a file is being unpacked. Allows plugins to check the file
        /// and see if it can handle the file for its intended purpose.
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        public override bool CanProcessFile(string file)
        {
            try
            {
                // Load the file..
                var f = new Pe32File(file);
                if (!f.Parse() || f.IsFile64Bit() || !f.HasSection(".bind"))
                    return false;

                // Obtain the bind section data..
                var bind = f.GetSectionData(".bind");

                // Look for the SteamStub v2.x unpacker prologue signature.
                return Pe32Helpers.FindPattern(bind, "53 51 52 56 57 55 8B EC 81 EC 00 10 00 00 C7") != -1;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Processing function called to allow the plugin to process the file.
        /// </summary>
        /// <param name="file"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        public override bool ProcessFile(string file, SteamlessOptions options)
        {
            // Initialize the class members..
            this.Options = options;
            this.CodeSectionData = null;
            this.CodeSectionIndex = -1;
            this.PayloadData = null;
            this.SteamDrmpData = null;
            this.SteamDrmpOffsets = new List<int>();
            this.UseFallbackDrmpOffsets = false;
            this.XorKey = 0;

            // Parse the file..
            this.File = new Pe32File(file);
            if (!this.File.Parse())
                return false;

            // Announce we are being unpacked with this packer..
            this.Log("File is packed with SteamStub Variant 2.1!", LogMessageType.Information);

            this.Log("Step 1 - Read, disassemble and decode the SteamStub DRM header.", LogMessageType.Information);
            if (!this.Step1())
                return false;

            this.Log("Step 2 - Read, decode and process the payload data.", LogMessageType.Information);
            if (!this.Step2())
                return false;

            this.Log("Step 3 - Read, decode and dump the SteamDRMP.dll file.", LogMessageType.Information);
            if (!this.Step3())
                return false;

            this.Log("Step 4 - Scan, dump and pull needed offsets from within the SteamDRMP.dll file.", LogMessageType.Information);
            if (!this.Step4())
                return false;

            this.Log("Step 5 - Read, decrypt and process the main code section.", LogMessageType.Information);
            if (!this.Step5())
                return false;

            this.Log("Step 6 - Rebuild and save the unpacked file.", LogMessageType.Information);
            if (!this.Step6())
                return false;

            if (this.Options.RecalculateFileChecksum)
            {
                this.Log("Step 7 - Rebuild unpacked file checksum.", LogMessageType.Information);
                if (!this.Step7())
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Step #1
        /// 
        /// Read, disassemble and decode the SteamStub DRM header.
        /// </summary>
        /// <returns></returns>
        private bool Step1()
        {
            /**
             * Note: This version of the stub has a variable length header due to how it builds the 
             * header information. When the stub is generated, the header has additional string data
             * that can be dynamically built based on the various options of the protection being used
             * and other needed API imports. Inside of the stub header, this field is 'StubData'.
             */

            // Obtain the file entry offset..
            var fileOffset = this.File.GetFileOffsetFromRva(this.File.NtHeaders.OptionalHeader.AddressOfEntryPoint);

            // Stub header magic; marks the start of the DRM header preceding the entry point.
            if (fileOffset < 4 || BitConverter.ToUInt32(this.File.FileData, (int)fileOffset - 4) != 0xC0DEC0DE)
                return false;

            // Disassemble the file to locate the needed DRM information..
            if (!this.DisassembleFile(out var structOffset, out var structSize, out var structXorKey))
                return false;

            // Obtain the DRM header data..
            var headerData = new byte[structSize];
            Array.Copy(this.File.FileData, this.File.GetFileOffsetFromRva(structOffset), headerData, 0, structSize);

            // Xor decode the header data..
            this.XorKey = SteamStubHelpers.SteamXor(ref headerData, (uint)headerData.Length, structXorKey);

            // Select the D0 variant (omits LoadLibraryW) when the header is exactly 0xD0 dwords.
            if ((structSize / 4) == 0xD0)
            {
                this.StubHeader = Pe32Helpers.GetStructure<SteamStub32Var21Header_D0Variant>(headerData);
                this.StubData = headerData.Skip(Marshal.SizeOf(typeof(SteamStub32Var21Header_D0Variant))).ToArray();
            }
            else
            {
                this.StubHeader = Pe32Helpers.GetStructure<SteamStub32Var21Header>(headerData);
                this.StubData = headerData.Skip(Marshal.SizeOf(typeof(SteamStub32Var21Header))).ToArray();
            }

            return true;
        }

        /// <summary>
        /// Step #2
        /// 
        /// Read, decode and process the payload data.
        /// </summary>
        /// <returns></returns>
        private bool Step2()
        {
            // Obtain the payload address and size..
            var payloadAddr = this.File.GetFileOffsetFromRva(this.File.GetRvaFromVa(this.StubHeader.PayloadDataVirtualAddress));
            var payloadData = new byte[this.StubHeader.PayloadDataSize];
            Array.Copy(this.File.FileData, payloadAddr, payloadData, 0, this.StubHeader.PayloadDataSize);

            // Decode the payload data..
            this.XorKey = SteamStubHelpers.SteamXor(ref payloadData, this.StubHeader.PayloadDataSize, this.XorKey);
            this.PayloadData = payloadData;

            try
            {
                if (this.Options.DumpPayloadToDisk)
                {
                    System.IO.File.WriteAllBytes(this.File.FilePath + ".payload", payloadData);
                    this.Log(" --> Saved payload to disk!", LogMessageType.Debug);
                }
            }
            catch (Exception)
            {
                // Do nothing here since it doesn't matter if this fails..
            }

            return true;
        }

        /// <summary>
        /// Step #3
        /// 
        /// Read, decode and dump the SteamDRMP.dll file.
        /// </summary>
        /// <returns></returns>
        private bool Step3()
        {
            this.Log(" --> File has SteamDRMP.dll file!", LogMessageType.Debug);

            try
            {
                // Obtain the SteamDRMP.dll file address and data..
                var drmpAddr = this.File.GetFileOffsetFromRva(this.File.GetRvaFromVa(BitConverter.ToUInt32(this.PayloadData, (int)this.StubHeader.SteamDRMPDllVirtualAddress)));
                var drmpSize = BitConverter.ToUInt32(this.PayloadData, (int)this.StubHeader.SteamDRMPDllSize);
                var drmpData = new byte[drmpSize];
                Array.Copy(this.File.FileData, drmpAddr, drmpData, 0, drmpSize);

                // Obtain the XTea encryption keys..
                var xteyKeys = new uint[(this.PayloadData.Length - this.StubHeader.XTeaKeys) / 4];
                for (var x = 0; x < (this.PayloadData.Length - this.StubHeader.XTeaKeys) / 4; x++)
                    xteyKeys[x] = BitConverter.ToUInt32(this.PayloadData, (int)this.StubHeader.XTeaKeys + (x * 4));

                // Decrypt the file data..
                SteamStubHelpers.SteamDrmpDecryptPass1(ref drmpData, drmpSize, xteyKeys);
                this.SteamDrmpData = drmpData;

                try
                {
                    if (this.Options.DumpSteamDrmpToDisk)
                    {
                        var basePath = Path.GetDirectoryName(this.File.FilePath) ?? string.Empty;
                        System.IO.File.WriteAllBytes(Path.Combine(basePath, "SteamDRMP.dll"), drmpData);
                        this.Log(" --> Saved SteamDRMP.dll to disk!", LogMessageType.Debug);
                    }
                }
                catch (Exception)
                {
                    // Do nothing here since it doesn't matter if this fails..
                }

                return true;
            }
            catch (Exception)
            {
                this.Log(" --> Error trying to decrypt the files SteamDRMP.dll data!", LogMessageType.Error);
                return false;
            }
        }

        private bool ValidateSteamDrmpOffsets(List<int> offsets)
        {
            if (offsets.Count != 8)
                return false;

            // Always validate the flags offset since it is used for encryption detection..
            if (offsets[0] < 0 || offsets[0] + 4 > this.PayloadData.Length)
                return false;

            // Validate the OEP and code section virtual address offsets since they are consumed in later steps..
            if (offsets[2] < 0 || offsets[2] + 4 > this.PayloadData.Length)
                return false;
            if (offsets[3] < 0 || offsets[3] + 4 > this.PayloadData.Length)
                return false;

            var flags = BitConverter.ToUInt32(this.PayloadData, offsets[0]);
            if ((flags & (uint)DrmFlags.NoEncryption) != (uint)DrmFlags.NoEncryption)
            {
                // File is encrypted � validate encryption-related offsets..
                if (offsets[4] < 0 || offsets[4] + 4 > this.PayloadData.Length)
                    return false;
                if (offsets[5] < 0 || offsets[5] + 32 > this.PayloadData.Length)
                    return false;
                if (offsets[6] < 0 || offsets[6] + 16 > this.PayloadData.Length)
                    return false;
                if (offsets[7] < 0 || offsets[7] + 16 > this.PayloadData.Length)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Step #4
        /// 
        /// Scan, dump and pull needed offsets from within the SteamDRMP.dll file.
        /// </summary>
        /// <returns></returns>
        private bool Step4()
        {
            var patterns = new List<(string Pattern, bool IsFallback)>
                {
                    ("8B ?? ?? ?? ?? ?? 89 ?? ?? ?? ?? ?? 8B ?? ?? ?? ?? ?? 89 ?? ?? ?? ?? ?? 8B ?? ?? ?? ?? ?? 89 ?? ?? ?? ?? ?? 8B ?? ?? ?? ?? ?? 89 ?? ?? ?? ?? ?? 8B ?? ?? ?? ?? ?? 89 ?? ?? ?? ?? ?? 8D ?? ?? ?? ?? ?? 05", false),
                    ("8B ?? ?? ?? ?? ?? 89 ?? ?? ?? ?? ?? 8B ?? ?? ?? ?? ?? 89 ?? ?? ?? ?? ?? 8B ?? ?? ?? ?? ?? 89 ?? ?? ?? ?? ?? 8B ?? ?? ?? ?? ?? 89 ?? ?? ?? ?? ?? 8B", false),
                    ("8B ?? ?? ?? ?? ?? 89 ?? ?? ?? ?? ?? 8B ?? ?? ?? ?? ?? A3 ?? ?? ?? ?? 8B ?? ?? ?? ?? ?? A3 ?? ?? ?? ?? 8B ?? ?? ?? ?? ?? A3 ?? ?? ?? ?? 8B", true)
                };

            foreach (var (pattern, isFallback) in patterns)
            {
                var drmpOffset = Pe32Helpers.FindPattern(this.SteamDrmpData, pattern);
                if (drmpOffset == -1)
                    continue;

                // Ensure there is enough data remaining for offset extraction..
                var copySize = Math.Min(1024, this.SteamDrmpData.Length - (int)drmpOffset);
                if (copySize < 76)
                    continue;

                var drmpOffsetData = new byte[copySize];
                Array.Copy(this.SteamDrmpData, drmpOffset, drmpOffsetData, 0, copySize);

                // Try with the known hardcoded offsets first..
                foreach (var useFallback in new[] { false, true })
                {
                    this.UseFallbackDrmpOffsets = useFallback;
                    var drmpOffsets = this.GetSteamDrmpOffsets(drmpOffsetData);
                    if (drmpOffsets.Count != 8)
                        continue;

                    if (this.ValidateSteamDrmpOffsets(drmpOffsets))
                    {
                        this.SteamDrmpOffsets = drmpOffsets;
                        return true;
                    }
                }

                // Hardcoded offsets failed � try the dynamic disassembler method on this data block..
                var dynOffsets = this.GetSteamDrmpOffsetsDynamic(drmpOffsetData);
                if (dynOffsets.Count == 8 && this.ValidateSteamDrmpOffsets(dynOffsets))
                {
                    this.Log($" --> Using dynamic offset extraction.", LogMessageType.Debug);
                    this.SteamDrmpOffsets = dynOffsets;
                    return true;
                }

                // Hardcoded and dynamic both failed for this pattern � try scanning for the correct layout..
                this.Log($" --> Scanning for correct offset layout in DRMP data block...", LogMessageType.Debug);
                var foundOffsets = this.ScanSteamDrmpOffsets(this.SteamDrmpData, drmpOffset);
                if (foundOffsets != null)
                {
                    this.Log($" --> Found valid offsets via scan.", LogMessageType.Debug);
                    this.SteamDrmpOffsets = foundOffsets;
                    return true;
                }

                this.Log($" --> Pattern matched but could not find valid offsets, trying next pattern.", LogMessageType.Debug);
            }

            return false;
        }

        private List<int> ScanSteamDrmpOffsets(byte[] steamDrmpData, long scanOffset)
        {
            var copySize = Math.Min(1024, steamDrmpData.Length - (int)scanOffset);
            if (copySize <= 0)
                return null;

            var data = new byte[copySize];
            Array.Copy(steamDrmpData, scanOffset, data, 0, copySize);

            var payloadLimit = this.PayloadData.Length;

            for (int start = 0; start < data.Length - 28; start += 2)
            {
                var vals = new List<int>();
                for (int j = 0; j < 6; j++)
                    vals.Add(BitConverter.ToInt32(data, start + j * 4));

                // vals[6] is the IV offset (used to compute vals[7])
                var ivOffset = BitConverter.ToInt32(data, start + 6 * 4);
                vals.Add(ivOffset);
                vals.Add(ivOffset + 16);

                if (vals[0] < 0 || vals[0] + 4 > payloadLimit)
                    continue;

                var flags = BitConverter.ToUInt32(this.PayloadData, vals[0]);
                var noEnc = (flags & (uint)DrmFlags.NoEncryption) == (uint)DrmFlags.NoEncryption;

                if (vals[3] < 0 || vals[3] + 4 > payloadLimit)
                    continue;

                if (!noEnc)
                {
                    if (vals[4] < 0 || vals[4] + 4 > payloadLimit) continue;
                    if (vals[5] < 0 || vals[5] + 32 > payloadLimit) continue;
                    if (vals[6] < 0 || vals[6] + 16 > payloadLimit) continue;
                    if (vals[7] < 0 || vals[7] + 16 > payloadLimit) continue;

                    if (vals[5] + 32 > vals[6]) continue;
                    if (vals[6] + 16 != vals[7]) continue;
                }

                this.Log($" --> Found valid offset layout at byte offset {start} in data block!", LogMessageType.Debug);
                return vals;
            }

            return null;
        }

        /// <summary>
        /// Step #5
        /// 
        /// Read, decrypt and process the main code section.
        /// </summary>
        /// <returns></returns>
        private bool Step5()
        {
            // Stash the .bind bounds first; they are needed later to repair pointers into the removed section.
            {
                var bindSection = this.File.GetSection(".bind");
                if (bindSection.IsValid)
                {
                    this.BindSectionRva = bindSection.VirtualAddress;
                    this.BindSectionSize = bindSection.VirtualSize;
                }
            }

            if (!this.Options.KeepBindSection)
            {
                // Obtain the .bind section..
                var bindSection = this.File.GetSection(".bind");
                if (!bindSection.IsValid)
                    return false;

                // Remove the section..
                this.File.RemoveSection(bindSection);

                // Decrease the header section count..
                var ntHeaders = this.File.NtHeaders;
                ntHeaders.FileHeader.NumberOfSections--;
                this.File.NtHeaders = ntHeaders;

                this.Log(" --> .bind section was removed from the file.", LogMessageType.Debug);
            }
            else
                this.Log(" --> .bind section was kept in the file.", LogMessageType.Debug);

            byte[] codeSectionData;

            // Obtain the main code section (typically .text)..
            NativeApi32.ImageSectionHeader32 mainSection;
            if (this.SteamDrmpOffsets[3] != 0)
            {
                mainSection = this.File.GetOwnerSection(this.File.GetRvaFromVa(BitConverter.ToUInt32(this.PayloadData, this.SteamDrmpOffsets[3])));
                if (mainSection.PointerToRawData == 0 || mainSection.SizeOfRawData == 0)
                    return false;
            }
            else
            {
                // Fallback: the code section VA offset is 0 (some SteamDRMP variants
                // do not store this field). Use the OEP to locate the code section
                // since the original entry point always resides inside it.
                var oepVa = BitConverter.ToUInt32(this.PayloadData, this.SteamDrmpOffsets[2]);
                mainSection = this.File.GetOwnerSection(this.File.GetRvaFromVa(oepVa));
                if (mainSection.PointerToRawData == 0 || mainSection.SizeOfRawData == 0)
                    return false;

                this.Log($" --> Code section VA offset was 0; resolved via OEP 0x{oepVa:X8}.", LogMessageType.Debug);
            }

            this.Log($" --> {mainSection.SectionName} linked as main code section.", LogMessageType.Debug);

            // Save the code section index for later use..
            this.CodeSectionIndex = this.File.GetSectionIndex(mainSection);

            uint encryptedSize = 0;

            // Determine if we are using encryption on the section..
            var flags = BitConverter.ToUInt32(this.PayloadData, this.SteamDrmpOffsets[0]);
            if ((flags & (uint)DrmFlags.NoEncryption) == (uint)DrmFlags.NoEncryption)
            {
                this.Log($" --> {mainSection.SectionName} section is not encrypted.", LogMessageType.Debug);

                // No encryption was used, just read the original data..
                codeSectionData = new byte[mainSection.SizeOfRawData];
                Array.Copy(this.File.FileData, this.File.GetFileOffsetFromRva(mainSection.VirtualAddress), codeSectionData, 0, mainSection.SizeOfRawData);
            }
            else
            {
                this.Log($" --> {mainSection.SectionName} section is encrypted.", LogMessageType.Debug);

                try
                {
                    // Encryption was used, obtain the encryption information..
                    var aesKey = new byte[32];
                    Buffer.BlockCopy(this.PayloadData, this.SteamDrmpOffsets[5], aesKey, 0, 32);
                    var aesIv = new byte[AesIvSize];
                    Buffer.BlockCopy(this.PayloadData, this.SteamDrmpOffsets[6], aesIv, 0, AesIvSize);
                    var codeStolen = new byte[AesIvSize];
                    Buffer.BlockCopy(this.PayloadData, this.SteamDrmpOffsets[7], codeStolen, 0, AesIvSize);
                    encryptedSize = BitConverter.ToUInt32(this.PayloadData, this.SteamDrmpOffsets[4]);

                    if (aesKey.Length != 32 || aesIv.Length != AesIvSize || codeStolen.Length != AesIvSize)
                    {
                        this.Log($" --> Invalid encryption offsets (key={aesKey.Length}, iv={aesIv.Length}, stolen={codeStolen.Length}, payloadLen={this.PayloadData.Length}, useFallback={this.UseFallbackDrmpOffsets})", LogMessageType.Warning);
                        return false;
                    }

                    // Restore the stolen data then read the rest of the section data..
                    codeSectionData = new byte[encryptedSize + codeStolen.Length];
                    Array.Copy(codeStolen, 0, codeSectionData, 0, codeStolen.Length);
                    Array.Copy(this.File.FileData, this.File.GetFileOffsetFromRva(mainSection.VirtualAddress), codeSectionData, codeStolen.Length, encryptedSize);

                    // Decrypt the code section..
                    var aes = new AesHelper(aesKey, aesIv);
                    using (aes)
                    {
                        aes.RebuildIv(aesIv);
                        codeSectionData = aes.Decrypt(codeSectionData, CipherMode.CBC, PaddingMode.None);
                    }
                }
                catch (Exception)
                {
                    this.Log(" --> Error trying to decrypt the files code section data!", LogMessageType.Error);
                    return false;
                }
            }

            if (this.CodeSectionIndex < 0)
            {
                this.Log(" --> Error: could not resolve code section index!", LogMessageType.Error);
                return false;
            }

            // Merge the code section data..
            var sectionData = this.File.SectionData[this.CodeSectionIndex];
            Array.Copy(codeSectionData, sectionData, codeSectionData.Length);
            this.CodeSectionData = sectionData;

            return true;
        }

        /// <summary>
        /// Step #6
        /// 
        /// Rebuild and save the unpacked file.
        /// </summary>
        /// <returns></returns>
        private bool Step6()
        {
            FileStream fStream = null;

            try
            {
                // Zero the DosStubData if desired..
                if (this.Options.ZeroDosStubData && this.File.DosStubSize > 0)
                    this.File.DosStubData = Enumerable.Repeat((byte)0, (int)this.File.DosStubSize).ToArray();

                // Rebuild the file sections..
                this.File.RebuildSections(this.Options.DontRealignSections == false);

                // Open the unpacked file for writing..
                var unpackedPath = this.File.FilePath + ".unpacked.exe";
                fStream = new FileStream(unpackedPath, FileMode.Create, FileAccess.ReadWrite);

                // Write the DOS header to the file..
                fStream.WriteBytes(Pe32Helpers.GetStructureBytes(this.File.DosHeader));

                // Write the DOS stub to the file..
                if (this.File.DosStubSize > 0)
                    fStream.WriteBytes(this.File.DosStubData);

                // Update the NT headers..
                var ntHeaders = this.File.NtHeaders;
                var lastSection = this.File.Sections[this.File.Sections.Count - 1];
                var originalEntry = BitConverter.ToUInt32(this.PayloadData, this.SteamDrmpOffsets[2]);
                ntHeaders.OptionalHeader.AddressOfEntryPoint = this.File.GetRvaFromVa(originalEntry);
                ntHeaders.OptionalHeader.CheckSum = 0;
                ntHeaders.OptionalHeader.SizeOfImage = this.File.GetAlignment(lastSection.VirtualAddress + lastSection.VirtualSize, this.File.NtHeaders.OptionalHeader.SectionAlignment);

                // Fix the import table entry if it points into the removed .bind section..
                if (!this.Options.KeepBindSection && this.BindSectionSize > 0)
                {
                    var importTable = ntHeaders.OptionalHeader.ImportTable;
                    if (importTable.VirtualAddress >= this.BindSectionRva && importTable.VirtualAddress < this.BindSectionRva + this.BindSectionSize)
                    {
                        var rdataSection = this.File.GetSection(".rdata");
                        if (rdataSection.IsValid)
                        {
                            var rdataData = this.File.GetSectionData(".rdata");
                            var rdataEnd = rdataSection.VirtualAddress + rdataSection.VirtualSize;
                            var importRva = Pe32Helpers.FindImportDescriptorInRdata(rdataData, rdataSection.VirtualAddress);
                            if (importRva > 0)
                            {
                                importTable.VirtualAddress = importRva;
                                ntHeaders.OptionalHeader.ImportTable = importTable;
                                this.Log($" --> Fixed import table pointer to RVA 0x{importRva:X8}", LogMessageType.Debug);
                            }
                        }
                    }
                }

                // Fix the certificate table entry if a certificate exists and the file layout has changed..
                if (!this.Options.KeepBindSection && this.BindSectionSize > 0)
                {
                    var certTable = ntHeaders.OptionalHeader.CertificateTable;
                    if (certTable.VirtualAddress > 0 && certTable.Size > 0)
                    {
                        // The security entry uses a file offset (not RVA). Update it to the current overlay position.
                        var lastSectionRaw = this.File.Sections[this.File.Sections.Count - 1];
                        var overlayStart = lastSectionRaw.PointerToRawData + lastSectionRaw.SizeOfRawData;
                        certTable.VirtualAddress = overlayStart;
                        ntHeaders.OptionalHeader.CertificateTable = certTable;
                        this.Log($" --> Fixed certificate table pointer to file offset 0x{overlayStart:X8}", LogMessageType.Debug);
                    }
                }

                this.File.NtHeaders = ntHeaders;

                // Write the NT headers to the file..
                fStream.WriteBytes(Pe32Helpers.GetStructureBytes(ntHeaders));

                // Write the sections to the file..
                for (var x = 0; x < this.File.Sections.Count; x++)
                {
                    var section = this.File.Sections[x];
                    var sectionData = this.File.SectionData[x];

                    // Write the section header to the file..
                    fStream.WriteBytes(Pe32Helpers.GetStructureBytes(section));

                    // Set the file pointer to the sections raw data..
                    var sectionOffset = fStream.Position;
                    fStream.Position = section.PointerToRawData;

                    // Write the sections raw data..
                    var sectionIndex = this.File.Sections.IndexOf(section);
                    if (sectionIndex == this.CodeSectionIndex)
                        fStream.WriteBytes(this.CodeSectionData ?? sectionData);
                    else
                        fStream.WriteBytes(sectionData);

                    // Reset the file offset..
                    fStream.Position = sectionOffset;
                }

                // Set the stream to the end of the file..
                fStream.Position = fStream.Length;

                // Write the overlay data if it exists..
                if (this.File.OverlayData != null)
                    fStream.WriteBytes(this.File.OverlayData);

                this.Log(" --> Unpacked file saved to disk!", LogMessageType.Success);
                this.Log($" --> File Saved As: {unpackedPath}", LogMessageType.Success);

                return true;
            }
            catch (Exception)
            {
                this.Log(" --> Error trying to save unpacked file!", LogMessageType.Error);
                return false;
            }
            finally
            {
                fStream?.Dispose();
            }
        }

        /// <summary>
        /// Step #7
        /// 
        /// Recalculate the file checksum.
        /// </summary>
        /// <returns></returns>
        private bool Step7()
        {
            var unpackedPath = this.File.FilePath + ".unpacked.exe";
            if (!Pe32Helpers.UpdateFileChecksum(unpackedPath))
            {
                this.Log(" --> Error trying to recalculate unpacked file checksum!", LogMessageType.Error);
                return false;
            }

            this.Log(" --> Unpacked file updated with new checksum!", LogMessageType.Success);
            return true;

        }

        /// <summary>
        /// Disassembles the file to locate the needed DRM header information.
        /// </summary>
        /// <param name="offset"></param>
        /// <param name="size"></param>
        /// <param name="xorKey"></param>
        /// <returns></returns>
        private bool DisassembleFile(out uint offset, out uint size, out uint xorKey)
        {
            // Prepare our needed variables..
            uint structOffset = 0;
            uint structSize = 0;
            uint structXorKey = 0;

            // Determine the entry offset of the file..
            var entryOffset = this.File.GetFileOffsetFromRva(this.File.NtHeaders.OptionalHeader.AddressOfEntryPoint);

            try
            {
                var reader = new ByteArrayCodeReader(this.File.FileData, (int)entryOffset, Math.Min(4096, this.File.FileData.Length - (int)entryOffset));
                var decoder = Decoder.Create(32, reader);
                decoder.IP = (ulong)entryOffset;
                var endRip = decoder.IP + 4096;

                while (decoder.IP < endRip && reader.CanReadByte)
                {
                    var inst = decoder.Decode();

                    // Looks for: mov dword ptr [value], immediate
                    if (inst.Mnemonic == Mnemonic.Mov && inst.Op0Kind == OpKind.Memory && IsImmediate32(inst.Op1Kind))
                    {
                        if (structOffset == 0)
                            structOffset = inst.Immediate32 - this.File.NtHeaders.OptionalHeader.ImageBase;
                        else
                            structXorKey = inst.Immediate32;
                    }

                    // Looks for: mov reg, immediate
                    if (inst.Mnemonic == Mnemonic.Mov && inst.Op0Kind == OpKind.Register && IsImmediate32(inst.Op1Kind))
                        structSize = inst.Immediate32 * 4;

                    if (structOffset > 0 && structSize > 0 && structXorKey > 0)
                    {
                        offset = structOffset;
                        size = structSize;
                        xorKey = structXorKey;
                        return true;
                    }
                }

                offset = size = xorKey = 0;
                return false;
            }
            catch (Exception)
            {
                offset = size = xorKey = 0;
                return false;
            }
        }

        /// <summary>
        /// Obtains the needed DRM offsets from the SteamDRMP.dll file.
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        private List<int> GetSteamDrmpOffsets(byte[] data)
        {
            var offset0 = 2; // Flags
            var offset1 = 14; // Steam App Id
            var offset2 = this.UseFallbackDrmpOffsets ? 25 : 26; // OEP
            var offset3 = this.UseFallbackDrmpOffsets ? 36 : 38; // Code Section Virtual Address
            var offset4 = this.UseFallbackDrmpOffsets ? 47 : 50; // Code Section Virtual Size (Encrypted Size)
            var offset5 = this.UseFallbackDrmpOffsets ? 61 : 62; // Code Section AES Key
            var offset6 = this.UseFallbackDrmpOffsets ? 72 : 67; // Code Section AES Iv

            var offsets = new List<int>
                {
                    BitConverter.ToInt32(data, offset0), // ... 0 - Flags
                    BitConverter.ToInt32(data, offset1), // ... 1 - Steam App Id
                    BitConverter.ToInt32(data, offset2), // ... 2 - OEP
                    BitConverter.ToInt32(data, offset3), // ... 3 - Code Section Virtual Address
                    BitConverter.ToInt32(data, offset4), // ... 4 - Code Section Virtual Size (Encrypted Size)
                    BitConverter.ToInt32(data, offset5) // .... 5 - Code Section AES Key
                };

            var aesIvOffset = BitConverter.ToInt32(data, offset6);
            offsets.Add(aesIvOffset); // ................. 6 - Code Section AES Iv
            offsets.Add(aesIvOffset + 16); // ............ 7 - Code Section Stolen Bytes

            return offsets;
        }

        /// <summary>
        /// Obtains the needed DRM offsets from the SteamDRMP.dll file. (Dynamically via disassembling.)
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        private List<int> GetSteamDrmpOffsetsDynamic(byte[] data)
        {
            var offsets = new List<int>();
            var count = 0;

            /**
             * Assumed order of the offset values:
             * - Flags (mov)
             * - SteamAppId (mov)
             * - OEP (mov)
             * - Code Section VA (mov)
             * - Code Section Size (mov)
             * - Code Section AES Key (lea)
             * - Code Section AES IV (offset from above lea)
             * - Stolen Bytes (add)
             */

            try
            {
                var skipMov = false;
                var reader = new ByteArrayCodeReader(data);
                var decoder = Decoder.Create(32, reader);
                var endRip = decoder.IP + (uint)data.Length;

                while (decoder.IP < endRip && reader.CanReadByte)
                {
                    if (count >= 8)
                        break;

                    var inst = decoder.Decode();

                    // ex: mov eax, [eax+1234]
                    if (!skipMov && inst.Mnemonic == Mnemonic.Mov && inst.Op0Kind == OpKind.Register && inst.Op1Kind == OpKind.Memory)
                    {
                        count++;
                        offsets.Add((int)inst.MemoryDisplacement32);
                    }

                    // ex: lea eax, [eax+1234]
                    if (inst.Code == Code.Lea_r32_m)
                    {
                        if (inst.Op0Kind == OpKind.Register && inst.Op1Kind == OpKind.Memory)
                        {
                            count += 2;
                            offsets.Add((int)inst.MemoryDisplacement32);
                            offsets.Add((int)inst.MemoryDisplacement32 + 16);

                            /**
                             * Some v2 compiled files have the order of the last offset (add inst) after a mov which loads
                             * GetModuleHandleA's address into a register. In order to skip that from being read as an offset
                             * we need this small workaround..
                             */
                            skipMov = true;
                        }
                    }

                    // ex: add eax, 1234
                    if (inst.Mnemonic == Mnemonic.Add && inst.Op0Kind == OpKind.Register && IsImmediate32(inst.Op1Kind))
                    {
                        count++;
                        offsets.Add((int)inst.Immediate32);
                    }
                }

                return offsets;
            }
            catch (Exception)
            {
                return new List<int>();
            }
        }

        /// <summary>
        /// Gets or sets the Steamless options this file was requested to process with.
        /// </summary>
        private SteamlessOptions Options { get; set; }

        /// <summary>
        /// Gets or sets the file being processed.
        /// </summary>
        private Pe32File File { get; set; }

        /// <summary>
        /// Gets or sets the current xor key being used against the file data.
        /// </summary>
        private uint XorKey { get; set; }

        /// <summary>
        /// Gets or sets the DRM stub header.
        /// </summary>
        private ISteamStub32Var21Header StubHeader { get; set; }

        /// <summary>
        /// Gets or sets the dynamic field 'StubData' from the header.
        /// </summary>
        private byte[] StubData { get; set; }

        /// <summary>
        /// Gets or sets the payload data.
        /// </summary>
        private byte[] PayloadData { get; set; }

        /// <summary>
        /// Gets or sets the SteamDRMP.dll data.
        /// </summary>
        private byte[] SteamDrmpData { get; set; }

        /// <summary>
        /// Gets or sets the list of SteamDRMP.dll offsets.
        /// </summary>
        private List<int> SteamDrmpOffsets { get; set; }

        /// <summary>
        /// Gets or sets if the offsets should be read using fallback values.
        /// </summary>
        private bool UseFallbackDrmpOffsets { get; set; }

        /// <summary>
        /// Gets or sets the index of the code section.
        /// </summary>
        private int CodeSectionIndex { get; set; }

        /// <summary>
        /// Gets or sets the decrypted code section data.
        /// </summary>
        private byte[] CodeSectionData { get; set; }

        private uint BindSectionRva { get; set; }

        private uint BindSectionSize { get; set; }

        private static bool IsImmediate32(OpKind kind) =>
            kind == OpKind.Immediate32 || kind == OpKind.Immediate8to32;
    }
}