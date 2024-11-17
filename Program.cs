using System.Diagnostics;
using System.IO;
using System.Xml.Linq;
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

        private static uint FindCertificateChainEndOffset(Stream fileStream)
        {
            using BinaryReader reader = new(fileStream, System.Text.Encoding.Default, true);
            List<byte[]> Signatures = [];
            uint LastOffset = 0;

            for (uint i = 0; i < fileStream.Length - 6; i++)
            {
                fileStream.Seek(i, SeekOrigin.Begin);

                ushort offset0 = reader.ReadUInt16();
                short offset1 = (short)((reader.ReadByte() << 8) | reader.ReadByte());
                ushort offset2 = reader.ReadUInt16();

                if (offset0 == 0x8230 && offset1 >= 0 && offset2 == 0x8230 || offset0 == 0x8231 && offset1 >= 0 && offset2 == 0x8230)
                {
                    int CertificateSize = offset1 + 4; // Header Size is 4

                    bool IsCertificatePartOfExistingChain = LastOffset == 0 || LastOffset == i;
                    if (!IsCertificatePartOfExistingChain)
                    {
                        Debug.WriteLine("Chain broke right here: " + Signatures.Count);
                        break;
                    }

                    LastOffset = i + (uint)CertificateSize;

                    Console.WriteLine($"Certificate at offset {i:X8} with size {CertificateSize:X8}");

                    fileStream.Seek(i, SeekOrigin.Begin);
                    Signatures.Add(reader.ReadBytes(CertificateSize));
                }
            }

            return LastOffset;
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
                uint Version = reader.ReadUInt32();
                ushort EmbeddedDriverCount = reader.ReadUInt16();
                ushort PayloadItemCount = reader.ReadUInt16();

                Console.WriteLine($"Version: {Version}");

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

                    // 1
                    // 2
                    // 3
                    Console.WriteLine($"iVersion: {iVersion}");

                    Console.WriteLine($"UpdateImage at offset {stream.Position:X8} with size {UpdateImageSize + 0x8:X8}");

                    byte[] UpdateImage = reader.ReadBytes((int)UpdateImageSize + 0x8);
                    byte[] UpdateVendorCode = reader.ReadBytes((int)UpdateVendorCodeSize);

                    using Stream UpdateImageStream = new MemoryStream(UpdateImage);
                    uint certSize = FindCertificateChainEndOffset(UpdateImageStream);
                    UpdateImageStream.Seek(0, SeekOrigin.Begin);

                    using BinaryReader UpdateImageReader = new(UpdateImageStream);

                    uint FirmwareVolumeOffset = certSize;
                    int FirmwareVolumeSize = (int)(UpdateImageSize - certSize);

                    if (iVersion >= 3)
                    {
                        FirmwareVolumeSize += 0x8;
                    }

                    Console.WriteLine($"FirmwareVolume at offset {FirmwareVolumeOffset:X8} with size {FirmwareVolumeSize:X8}");

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

            Dictionary<Guid, string> GuidNameMapping = [];

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

                    Console.WriteLine($"Old Capsule Partition Descriptor Format: {oldVer}");

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

                        string NewFileName = FileName;

                        int i = 1;
                        while (GuidNameMapping.Any(x => x.Value == NewFileName))
                        {
                            NewFileName = $"{FileName} ({++i})";
                        }

                        Console.WriteLine($"Found {NewFileName} at offset {stream.Position:X8} with size {element.Length:X8}");

                        GuidNameMapping.Add(EFI_GUID, NewFileName);
                    }
                }
            }

            foreach (EFI efi2 in uefi.EFIs)
            {
                if (GuidNameMapping.TryGetValue(efi2.Guid, out string FileName))
                {
                    byte[] buffer = efi2.SectionElements![0].DecompressedImage!;

                    string filedst = Path.Combine(outputDirectory, $"{FileName}.bin");

                    if (!Directory.Exists(Path.GetDirectoryName(filedst)))
                    {
                        _ = Directory.CreateDirectory(Path.GetDirectoryName(filedst)!);
                    }

                    File.WriteAllBytes(filedst, buffer);
                }
            }
        }
    }
}