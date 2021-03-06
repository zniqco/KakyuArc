// #define USE_SORT_ORDER
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace KakyuArc
{
    public class Program
    {
        private const string HeaderFileName = "__header";
        private const string FooterFileName = "__footer";
        private const string PaddingFileName = "__padding";

        public static int Main(string[] args)
        {
            if (args.Length < 2)
                return Error($"Usage: {AppDomain.CurrentDomain.FriendlyName} <-u|-p> path [output-path]");

            var mode = args[0];
            var path = args[1];

            switch (mode)
            {
                case "-u":
                    var outputDirectory = args.Length >= 3 ? args[2] : Path.Combine(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path));

                    if (!Directory.Exists(outputDirectory))
                        Directory.CreateDirectory(outputDirectory);

                    using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read))
                    using (var reader = new BinaryReader(stream))
                    {
                        // Header
                        var header = reader.ReadBytes(16); // "KAKYUSEI.ARC\x1Aelf"

                        // List files
                        var files = new List<FileData>();

                        while (true)
                        {
                            var name = reader.ReadStringEncrypted(12);
                            var sizeHigh = reader.ReadUInt16Encrypted();
                            var positionLow = reader.ReadUInt16Encrypted();
                            var positionHigh = reader.ReadUInt16Encrypted();
                            var sizeLow = reader.ReadUInt16Encrypted();
                            var size = (sizeHigh << 16) | sizeLow;
                            var position = (positionHigh << 16) | positionLow;

                            files.Add(new FileData(name, position, size));

                            if (size + position >= stream.Length - 20) // 20-bytes padding
                                break;
                        }

                        // Padding (Optional)
                        var padding = default(byte[]);
                        var firstFile = files.OrderBy(x => x.Position).FirstOrDefault();
                        var paddingSize = firstFile.Position - stream.Position;

                        if (paddingSize > 0)
                            padding = reader.ReadBytes((int)paddingSize);

                        // Write files
                        for (var i = 0; i < files.Count; ++i)
                        {
                            var file = files[i];

                            stream.Position = file.Position;

                            var bytes = reader.ReadBytes(file.Size);
                            var extension = Path.GetExtension(file.Name);

                            switch (extension.ToUpper())
                            {
                                case ".MES":
                                    bytes = ElfLZSS.Decompress(bytes);

                                    break;
                            }

                            File.WriteAllBytes(Path.Combine(outputDirectory, $"{Path.GetFileNameWithoutExtension(file.Name)}@{i}{extension}"), bytes);
                        }

                        // Footer
                        var footer = reader.ReadBytes(20);

                        File.WriteAllBytes(Path.Combine(outputDirectory, HeaderFileName), header);
                        File.WriteAllBytes(Path.Combine(outputDirectory, FooterFileName), footer);

                        if (padding != null)
                            File.WriteAllBytes(Path.Combine(outputDirectory, PaddingFileName), padding);
                    }

                    break;

                case "-p":
                    var outputPath = args.Length >= 3 ? args[2] : path + ".ARC";
                    var inputFilePaths =
#if USE_SORT_ORDER
                        Directory.GetFiles(path)
                        .Where(x => IsPackableFile(x))
                        .Select(x => GetPackOrderByFileName(x))
                        .OrderBy(x => x.Order)
                        .ThenBy(x => x.Name)
                        .Select(x => x.Name)
                        .ToArray();
#else
                        Directory.GetFiles(path);
#endif

                    using (var stream = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
                    using (var writer = new BinaryWriter(stream))
                    {
                        // Header
                        writer.Write(File.ReadAllBytes(Path.Combine(path, HeaderFileName)));

                        var currentPosition = stream.Position + inputFilePaths.Length * 20;

                        // Padding (Optional)
                        var paddingPath = Path.Combine(path, PaddingFileName);

                        if (File.Exists(paddingPath))
                        {
                            var padding = File.ReadAllBytes(paddingPath);
                            var previousPosition = stream.Position;

                            stream.Position = currentPosition;

                            writer.Write(padding);

                            stream.Position = previousPosition;

                            currentPosition += padding.Length;
                        }

                        // Files
                        foreach (var inputFilePath in inputFilePaths)
                        {
                            // Contents
                            var bytes = File.ReadAllBytes(inputFilePath);
                            var extension = Path.GetExtension(inputFilePath);
                            var previousPosition = stream.Position;

                            switch (extension.ToUpper())
                            {
                                case ".MES":
                                    bytes = ElfLZSS.Compress(bytes);

                                    break;
                            }

                            stream.Position = currentPosition;

                            writer.Write(bytes);

                            stream.Position = previousPosition;

                            // Meta
                            var name = Path.GetFileName(inputFilePath);
                            var size = bytes.Length;
                            var position = currentPosition;

                            if (name.Contains("@"))
                                name = name.Substring(0, name.IndexOf("@")) + Path.GetExtension(name);

                            writer.WriteStringEncrypted(name, 12);
                            writer.WriteUInt16Encrypted((ushort)((size >> 16) & 0xFFFF));
                            writer.WriteUInt16Encrypted((ushort)(position & 0xFFFF));
                            writer.WriteUInt16Encrypted((ushort)((position >> 16) & 0xFFFF));
                            writer.WriteUInt16Encrypted((ushort)(size & 0xFFFF));

                            currentPosition += size;
                        }

                        // Footer
                        stream.Position = currentPosition;

                        writer.Write(File.ReadAllBytes(Path.Combine(path, FooterFileName)));
                    }

                    break;

                default:
                    return Error($"Unknown parameter: {args[0]}");
            }

            return 0;
        }

#if USE_SORT_ORDER
        private static OrderData GetPackOrderByFileName(string path)
        {
            // "name@order.ext"
            var order = int.MaxValue;
            var fileName = Path.GetFileNameWithoutExtension(path);

            if (fileName.Contains("@"))
            {
                var seperatorPosition = fileName.IndexOf("@");
                var orderString = fileName.Substring(seperatorPosition + 1);

                if (int.TryParse(orderString, out var o))
                    order = o;
            }

            return new OrderData(path, order);
        }
#endif

        private static bool IsPackableFile(string path)
        {
            var fileName = Path.GetFileName(path);

            switch (fileName)
            {
                case HeaderFileName:
                case FooterFileName:
                case PaddingFileName:
                    return false;                
            }

            return true;
        }

        private static int Error(string message)
        {
            Console.WriteLine(message);

            return -1;
        }
    }
}
