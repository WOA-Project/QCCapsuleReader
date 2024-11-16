using UEFIReader;

namespace QCCapsuleReader
{
    internal class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 2 && File.Exists(args[0]))
            {
                ParseCapsule(args[0], args[1]);
            }
            else
            {
                Console.WriteLine("Usage: <Path to Capsule image> <Output Directory>");
            }
        }

        public static Dictionary<Guid, string> PartitionTypeNameMapping = new()
        {
            { new("{0A85A45E-915F-49DB-8BD5-5337861F8082}"), "SBL1" },
            { new("{E7BF4F3F-7DC9-40C0-9DF2-CE2EC5CEACEF}"), "SBL2" },
            { new("{8BA7FEBE-AB44-411D-86E7-A6F2DE7E3F40}"), "SBL3" },
            { new("{8A8BD280-F35E-48E1-A891-BF0BB855831E}"), "RPM" },
            { new("{497FBC93-5784-4B8C-8F01-2AF50FB19239}"), "TZ" },
            { new("{F7A7DF2A-A845-4C17-94B8-85FD9CD022D7}"), "WINSECAPP" },
            { new("{31C5C241-D6CE-4E7A-B400-9C35571B2EA9}"), "UEFI" },
            { new("{642F3381-0327-4E0D-A7C1-A2C7D2C45812}"), "CSRT_ACPI" },
            { new("{044AF707-CDE8-4D15-B811-594BDABEB1FD}"), "DSDT_AML" },
            { new("{24FC010F-AA0F-4310-AC48-E259FBD07AB0}"), "FACP_ACPI" },
            { new("{E455D6FD-16A5-45D2-8E07-1F5C992ABE28}"), "FACS_ACPI" },
            { new("{CA6BECA3-CD6D-44F4-AFB2-65076B81AD54}"), "MADT_ACPI" },
            { new("{D298A7FE-C5CC-4C54-9CBC-9A59FA47F3AB}"), "TPM2_ACPI" },
            { new("{8E230A44-9617-40BC-B18D-256795E55526}"), "BGRT_ACPI" },
            { new("{D8E02C2D-9310-47E6-8B92-C7A3564C488A}"), "DBG2_ACPI" },
            { new("{F760AEEB-B172-4522-AFB2-ECCDC523B598}"), "FPDT_ACPI" },
            { new("{8AA7DEF2-4B2E-470C-BAE2-057CACA327DB}"), "logo1_ACPI" },
            { new("{C7340E65-0D5D-43D6-ABB7-39751D5EC8E7}"), "METADATA_GUID" },
            { new("{3998E865-A733-4812-97D7-4BC973EA3442}"), "OPM_PRIV_PROVISION" },
            { new("{01620DA3-F273-4401-9821-1D0E5169D8DA}"), "OPM_PUB_PROVISION" }
        };

        private static void ParseCapsuleLegacy(string file, string outputDirectory)
        {
            using Stream stream = File.OpenRead(file);
            using BinaryReader reader = new(stream);

            Guid CapsuleGuid = new(reader.ReadBytes(16));
            uint HeaderSize = reader.ReadUInt32();
            uint Flags = reader.ReadUInt32();
            uint CapsuleImageSize = reader.ReadUInt32();

            stream.Seek(HeaderSize, SeekOrigin.Begin);

            if (CapsuleGuid == new Guid("6DCBD5ED-E82D-4C44-BDA1-7194199AD92A")) // EFI_FIRMWARE_MANAGEMENT_CAPSULE_ID_GUID
            {
                // 1
                // 2
                // 3
                uint Version = reader.ReadUInt32();
                ushort EmbeddedDriverCount = reader.ReadUInt16();
                ushort PayloadItemCount = reader.ReadUInt16();

                List<ulong> EmbeddedDriverItemOffsetList = [];
                List<ulong> PayloadItemOffsetList = [];

                for (int i = 0; i < EmbeddedDriverCount; i++)
                {
                    ulong ItemOffset = reader.ReadUInt64();
                    EmbeddedDriverItemOffsetList.Add(ItemOffset);
                }

                for (int i = EmbeddedDriverCount; i < EmbeddedDriverCount + PayloadItemCount; i++)
                {
                    ulong ItemOffset = reader.ReadUInt64();
                    PayloadItemOffsetList.Add(ItemOffset);
                }

                foreach (ulong ItemOffset in PayloadItemOffsetList)
                {
                    stream.Seek((long)(HeaderSize + ItemOffset), SeekOrigin.Begin);

                    uint iVersion = reader.ReadUInt32();
                    Guid UpdateImageTypeId = new(reader.ReadBytes(16));
                    byte UpdateImageIndex = reader.ReadByte();
                    byte[] reserved_bytes = reader.ReadBytes(3);
                    uint UpdateImageSize = reader.ReadUInt32();
                    uint UpdateVendorCodeSize = reader.ReadUInt32();
                    ulong UpdateHardwareInstance = reader.ReadUInt64();

                    byte[] UpdateImage = reader.ReadBytes((int)UpdateImageSize + 0x8);
                    byte[] UpdateVendorCode = reader.ReadBytes((int)UpdateVendorCodeSize);

                    using Stream UpdateImageStream = new MemoryStream(UpdateImage);
                    using BinaryReader UpdateImageReader = new(UpdateImageStream);

                    UpdateImageStream.Seek(0x10, SeekOrigin.Begin);
                    uint certSize = 0xa55;//UpdateImageReader.ReadUInt32();

                    uint FirmwareVolumeOffset = certSize - 4 + 0x10 + 0xc;
                    int FirmwareVolumeSize = (int)(UpdateImageSize - certSize - 0x10) - 0x8;

                    UpdateImageStream.Seek(FirmwareVolumeOffset, SeekOrigin.Current);

                    byte[] FirmwareVolume = UpdateImageReader.ReadBytes(FirmwareVolumeSize);

                    ParseFirmareVolume(FirmwareVolume, outputDirectory);
                }
            }
        }

        private static void ParseCapsuleLegacy2(string file, string outputDirectory)
        {
            using Stream stream = File.OpenRead(file);
            using BinaryReader reader = new(stream);

            Guid CapsuleGuid = new(reader.ReadBytes(16));
            uint HeaderSize = reader.ReadUInt32();
            uint Flags = reader.ReadUInt32();
            uint CapsuleImageSize = reader.ReadUInt32();

            stream.Seek(HeaderSize, SeekOrigin.Begin);

            if (CapsuleGuid == new Guid("6DCBD5ED-E82D-4C44-BDA1-7194199AD92A")) // EFI_FIRMWARE_MANAGEMENT_CAPSULE_ID_GUID
            {
                // 1
                // 2
                // 3
                uint Version = reader.ReadUInt32();
                ushort EmbeddedDriverCount = reader.ReadUInt16();
                ushort PayloadItemCount = reader.ReadUInt16();

                List<ulong> EmbeddedDriverItemOffsetList = [];
                List<ulong> PayloadItemOffsetList = [];

                for (int i = 0; i < EmbeddedDriverCount; i++)
                {
                    ulong ItemOffset = reader.ReadUInt64();
                    EmbeddedDriverItemOffsetList.Add(ItemOffset);
                }

                for (int i = EmbeddedDriverCount; i < EmbeddedDriverCount + PayloadItemCount; i++)
                {
                    ulong ItemOffset = reader.ReadUInt64();
                    PayloadItemOffsetList.Add(ItemOffset);
                }

                foreach (ulong ItemOffset in PayloadItemOffsetList)
                {
                    stream.Seek((long)(HeaderSize + ItemOffset), SeekOrigin.Begin);

                    uint iVersion = reader.ReadUInt32();
                    Guid UpdateImageTypeId = new(reader.ReadBytes(16));
                    byte UpdateImageIndex = reader.ReadByte();
                    byte[] reserved_bytes = reader.ReadBytes(3);
                    uint UpdateImageSize = reader.ReadUInt32();
                    uint UpdateVendorCodeSize = reader.ReadUInt32();
                    ulong UpdateHardwareInstance = reader.ReadUInt64();

                    byte[] UpdateImage = reader.ReadBytes((int)UpdateImageSize + 0x8);
                    byte[] UpdateVendorCode = reader.ReadBytes((int)UpdateVendorCodeSize);

                    using Stream UpdateImageStream = new MemoryStream(UpdateImage);
                    using BinaryReader UpdateImageReader = new(UpdateImageStream);

                    UpdateImageStream.Seek(0x10, SeekOrigin.Begin);
                    uint certSize = 0xa55;//UpdateImageReader.ReadUInt32();

                    uint FirmwareVolumeOffset = certSize - 4 + 0x10 + 0xc;
                    int FirmwareVolumeSize = (int)(UpdateImageSize - certSize - 0x10) - 0x8;

                    UpdateImageStream.Seek(FirmwareVolumeOffset, SeekOrigin.Current);

                    byte[] FirmwareVolume = UpdateImageReader.ReadBytes(FirmwareVolumeSize);

                    ParseFirmareVolumeLegacy(FirmwareVolume, outputDirectory);
                }
            }
        }

        private static void ParseCapsule(string file, string outputDirectory)
        {
            using Stream stream = File.OpenRead(file);
            using BinaryReader reader = new(stream);

            Guid CapsuleGuid = new(reader.ReadBytes(16));
            uint HeaderSize = reader.ReadUInt32();
            uint Flags = reader.ReadUInt32();
            uint CapsuleImageSize = reader.ReadUInt32();

            stream.Seek(HeaderSize, SeekOrigin.Begin);

            if (CapsuleGuid == new Guid("6DCBD5ED-E82D-4C44-BDA1-7194199AD92A")) // EFI_FIRMWARE_MANAGEMENT_CAPSULE_ID_GUID
            {
                // 1
                // 2
                // 3
                uint Version = reader.ReadUInt32();
                ushort EmbeddedDriverCount = reader.ReadUInt16();
                ushort PayloadItemCount = reader.ReadUInt16();

                List<ulong> EmbeddedDriverItemOffsetList = [];
                List<ulong> PayloadItemOffsetList = [];

                for (int i = 0; i < EmbeddedDriverCount; i++)
                {
                    ulong ItemOffset = reader.ReadUInt64();
                    EmbeddedDriverItemOffsetList.Add(ItemOffset);
                }

                for (int i = EmbeddedDriverCount; i < EmbeddedDriverCount + PayloadItemCount; i++)
                {
                    ulong ItemOffset = reader.ReadUInt64();
                    PayloadItemOffsetList.Add(ItemOffset);
                }

                foreach (ulong ItemOffset in PayloadItemOffsetList)
                {
                    stream.Seek((long)(HeaderSize + ItemOffset), SeekOrigin.Begin);

                    uint iVersion = reader.ReadUInt32();
                    Guid UpdateImageTypeId = new(reader.ReadBytes(16));
                    byte UpdateImageIndex = reader.ReadByte();
                    byte[] reserved_bytes = reader.ReadBytes(3);
                    uint UpdateImageSize = reader.ReadUInt32();
                    uint UpdateVendorCodeSize = reader.ReadUInt32();
                    ulong UpdateHardwareInstance = reader.ReadUInt64();

                    byte[] UpdateImage = reader.ReadBytes((int)UpdateImageSize + 0x8);
                    byte[] UpdateVendorCode = reader.ReadBytes((int)UpdateVendorCodeSize);

                    using Stream UpdateImageStream = new MemoryStream(UpdateImage);
                    using BinaryReader UpdateImageReader = new(UpdateImageStream);

                    UpdateImageStream.Seek(0x10, SeekOrigin.Begin);
                    uint certSize = UpdateImageReader.ReadUInt32();

                    uint FirmwareVolumeOffset = certSize - 4 + 0x10;
                    int FirmwareVolumeSize = (int)(UpdateImageSize - certSize - 0x10);

                    UpdateImageStream.Seek(FirmwareVolumeOffset, SeekOrigin.Current);

                    byte[] FirmwareVolume = UpdateImageReader.ReadBytes(FirmwareVolumeSize);

                    ParseFirmareVolume(FirmwareVolume, outputDirectory);
                }
            }
        }

        private static void ParseFirmareVolume(byte[] FirmwareVolume, string outputDirectory)
        {
            UEFI uefi = new(FirmwareVolume);

            bool oldVer = false;

            foreach (EFI efi in uefi.EFIs)
            {
                if (efi.Guid == new Guid("C7340E65-0D5D-43D6-ABB7-39751D5EC8E7"))
                {
                    using Stream stream = new MemoryStream(efi.SectionElements![0].DecompressedImage!);
                    using BinaryReader reader = new(stream);

                    if ((stream.Length - 0x28) % 0x4D0 == 0)
                    {
                        oldVer = true;
                    }

                    if ((stream.Length - 0x28) % 0x51C == 0)
                    {
                        oldVer = false;
                    }

                    stream.Seek(0x28, SeekOrigin.Begin);

                    while (stream.Position != stream.Length)
                    {
                        byte[] element = reader.ReadBytes(oldVer ? 0x4D0 : 0x51C);

                        using Stream elementStream = new MemoryStream(element);
                        using BinaryReader elementReader = new(elementStream);

                        Guid EFI_GUID = new(elementReader.ReadBytes(16));
                        elementStream.Seek(16, SeekOrigin.Current);
                        byte[] NameBuffer = elementReader.ReadBytes(36 * 2);
                        string Name = System.Text.Encoding.Unicode.GetString(NameBuffer).Trim('\0');

                        string Name2 = "";
                        if (!oldVer)
                        {
                            elementStream.Seek(0x4D4, SeekOrigin.Begin);
                            byte[] Name2Buffer = elementReader.ReadBytes(36 * 2);
                            Name2 = System.Text.Encoding.Unicode.GetString(Name2Buffer).Trim('\0');
                        }

                        string FileName = (string.IsNullOrEmpty(Name2) || Name2.Contains('\0')) ? Name : $"{Name} {Name2}";

                        foreach (EFI efi2 in uefi.EFIs)
                        {
                            if (efi2.Guid == EFI_GUID)
                            {
                                byte[] buffer = efi2.SectionElements![0].DecompressedImage!;
                                /*byte[]? DecompressedImage = null;
                                try
                                {
                                    DecompressedImage = LZMA.Decompress(buffer, 0, (uint)buffer.Length);
                                }
                                catch { }*/

                                string filedst = Path.Combine(outputDirectory, $"{FileName}.bin");

                                if (!Directory.Exists(Path.GetDirectoryName(filedst)))
                                {
                                    _ = Directory.CreateDirectory(Path.GetDirectoryName(filedst)!);
                                }

                                File.WriteAllBytes(filedst, buffer);

                                /*if (DecompressedImage != null && DecompressedImage.Length != 0)
                                {
                                    filedst = Path.Combine(outputDirectory, $"{FileName}.unpacked.bin");
                                    File.WriteAllBytes(filedst, DecompressedImage);
                                }*/
                            }
                        }
                    }
                }
            }
        }

        private static void ParseFirmareVolumeLegacy(byte[] FirmwareVolume, string outputDirectory)
        {
            UEFI uefi = new(FirmwareVolume);

            bool oldVer = false;

            foreach (EFI efi in uefi.EFIs)
            {
                string FileName = efi.Guid.ToString();

                if (PartitionTypeNameMapping.ContainsKey(efi.Guid))
                {
                    FileName = PartitionTypeNameMapping[efi.Guid];
                }

                byte[] buffer = efi.SectionElements![0].DecompressedImage!;
                /*byte[]? DecompressedImage = null;
                try
                {
                    DecompressedImage = LZMA.Decompress(buffer, 0, (uint)buffer.Length);
                }
                catch { }*/

                string filedst = Path.Combine(outputDirectory, $"{FileName}.bin");

                if (!Directory.Exists(Path.GetDirectoryName(filedst)))
                {
                    _ = Directory.CreateDirectory(Path.GetDirectoryName(filedst)!);
                }

                File.WriteAllBytes(filedst, buffer);

                /*if (DecompressedImage != null && DecompressedImage.Length != 0)
                {
                    filedst = Path.Combine(outputDirectory, $"{FileName}.unpacked.bin");
                    File.WriteAllBytes(filedst, DecompressedImage);
                }*/
            }
        }

        private static void ParseFirmareVolumeLegacy2(byte[] FirmwareVolume, string outputDirectory)
        {
            UEFI uefi = new(FirmwareVolume);

            bool oldVer = false;

            foreach (EFI efi in uefi.EFIs)
            {
                string FileName = efi.Guid.ToString();

                if (PartitionTypeNameMapping.ContainsKey(efi.Guid))
                {
                    FileName = PartitionTypeNameMapping[efi.Guid];
                }

                byte[] buffer = efi.SectionElements![0].DecompressedImage!;
                /*byte[]? DecompressedImage = null;
                try
                {
                    DecompressedImage = LZMA.Decompress(buffer, 0, (uint)buffer.Length);
                }
                catch { }*/

                string filedst = Path.Combine(outputDirectory, $"{FileName}.bin");

                if (!Directory.Exists(Path.GetDirectoryName(filedst)))
                {
                    _ = Directory.CreateDirectory(Path.GetDirectoryName(filedst)!);
                }

                File.WriteAllBytes(filedst, buffer);

                /*if (DecompressedImage != null && DecompressedImage.Length != 0)
                {
                    filedst = Path.Combine(outputDirectory, $"{FileName}.unpacked.bin");
                    File.WriteAllBytes(filedst, DecompressedImage);
                }*/
            }
        }
    }
}