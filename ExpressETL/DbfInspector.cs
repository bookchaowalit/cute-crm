using System.Text;

namespace ExpressETL;

/// <summary>
/// เครื่องมือตรวจสอบโครงสร้างไฟล์ .DBF
/// </summary>
public class DbfInspector
{
    public static void Inspect(string filePath)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var encoding = Encoding.GetEncoding("tis-620");

        Console.WriteLine($"\n=== Inspecting: {Path.GetFileName(filePath)} ===");
        Console.WriteLine($"Full path: {filePath}");
        Console.WriteLine($"Exists: {File.Exists(filePath)}");

        if (!File.Exists(filePath))
        {
            Console.WriteLine("⚠️  FILE NOT FOUND\n");
            return;
        }

        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var br = new BinaryReader(fs);

        // Header
        fs.Seek(0, SeekOrigin.Begin);
        byte version = br.ReadByte();
        byte year = br.ReadByte();
        byte month = br.ReadByte();
        byte day = br.ReadByte();
        int recordCount = br.ReadInt32();
        short headerSize = br.ReadInt16();
        short recordSize = br.ReadInt16();

        Console.WriteLine($"Version: 0x{version:X2}");
        Console.WriteLine($"Last updated: {year + 1900:D4}-{month:D2}-{day:D2}");
        Console.WriteLine($"Record count: {recordCount}");
        Console.WriteLine($"Header size: {headerSize}");
        Console.WriteLine($"Record size: {recordSize}");

        // Fields
        var fields = new List<(string name, int offset, int length, byte type)>();
        int fieldOffset = 1;
        int pos = 32;

        Console.WriteLine("\n--- Fields ---");
        Console.WriteLine($"{"#",-4} {"Name",-15} {"Type",-6} {"Offset",-8} {"Length",-8}");
        Console.WriteLine(new string('-', 45));

        while (true)
        {
            fs.Seek(pos, SeekOrigin.Begin);
            byte term = br.ReadByte();
            if (term == 0x0D) break;

            fs.Seek(pos, SeekOrigin.Begin);
            byte[] nameBytes = br.ReadBytes(11);
            string fieldName = encoding.GetString(nameBytes).TrimEnd('\0').Trim();
            byte fieldType = br.ReadByte();
            int foVal = br.ReadInt32();
            byte fl = br.ReadByte();
            byte dc = br.ReadByte();

            string typeName = fieldType switch
            {
                0x43 => "C", 0x4E => "N", 0x44 => "D", 0x4C => "L",
                0x4D => "M", 0x46 => "F", 0x42 => "B", 0x49 => "I",
                _ => $"0x{fieldType:X2}"
            };

            Console.WriteLine($"{fields.Count,-4} {fieldName,-15} {typeName,-6} {fieldOffset,-8} {fl,-8}");
            fields.Add((fieldName, fieldOffset, fl, fieldType));
            fieldOffset += fl;

            pos += 32;
        }

        Console.WriteLine($"Total fields: {fields.Count}");

        // Sample first 3 records
        if (recordCount > 0)
        {
            Console.WriteLine($"\n--- Sample Records (first {Math.Min(3, recordCount)}) ---");
            for (int i = 0; i < Math.Min(3, recordCount); i++)
            {
                fs.Seek(headerSize, SeekOrigin.Begin);
                byte deletedFlag = br.ReadByte();
                if (deletedFlag == 0x2A) continue;

                var record = new Dictionary<string, string>();
                foreach (var (name, offset, length, _) in fields)
                {
                    fs.Seek(headerSize + 1 + i * recordSize + offset, SeekOrigin.Begin);
                    byte[] valBytes = br.ReadBytes(length);
                    record[name] = encoding.GetString(valBytes).TrimEnd('\0').Trim();
                }

                Console.WriteLine($"\nRecord #{i + 1}:");
                foreach (var (k, v) in record)
                {
                    Console.WriteLine($"  {k} = \"{v}\"");
                }
            }
        }

        Console.WriteLine();
    }

    /// <summary>
    /// List all DBF files in a directory
    /// </summary>
    public static void ListFiles(string dirPath)
    {
        Console.WriteLine($"\n=== Files in: {dirPath} ===");
        Console.WriteLine($"Exists: {Directory.Exists(dirPath)}");

        if (!Directory.Exists(dirPath)) return;

        var dbfFiles = Directory.GetFiles(dirPath, "*.DBF", SearchOption.TopDirectoryOnly);
        Console.WriteLine($"Found {dbfFiles.Length} .DBF files:\n");
        foreach (var f in dbfFiles.OrderBy(x => x))
        {
            var fi = new FileInfo(f);
            Console.WriteLine($"  {fi.Name,-20} {fi.Length,12:N0} bytes");
        }
    }
}
