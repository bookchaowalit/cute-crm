using System.Text;
using AccountingETL.Core.Domain;

namespace AccountingETL.Adapters.Express;

/// <summary>
/// Reads .DBF files (dBase/FoxPro format) directly without third-party libraries.
/// Supports configurable encoding (default tis-620 for Thai).
/// </summary>
public class DbfReader
{
    private readonly Encoding _encoding;

    public DbfReader(string encodingName = "tis-620")
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        try
        {
            _encoding = Encoding.GetEncoding(encodingName);
        }
        catch
        {
            _encoding = Encoding.GetEncoding("tis-620");
        }
    }

    /// <summary>
    /// Read all records from a DBF file.
    /// </summary>
    public List<Record> Read(string filePath)
    {
        var results = new List<Record>();

        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var br = new BinaryReader(fs);

        // === Header (32 bytes) ===
        fs.Seek(0, SeekOrigin.Begin);
        byte version = br.ReadByte();
        byte year = br.ReadByte();
        byte month = br.ReadByte();
        byte day = br.ReadByte();
        int recordCount = br.ReadInt32();
        short headerSize = br.ReadInt16();
        short recordSize = br.ReadInt16();

        // === Field descriptors (32 bytes each, terminated by 0x0D) ===
        var rawFields = new List<(string name, int storedOffset, int declaredLength, int decimalCount)>();
        int pos = 32;

        while (true)
        {
            fs.Seek(pos, SeekOrigin.Begin);
            byte term = br.ReadByte();
            if (term == 0x0D) break;

            fs.Seek(pos, SeekOrigin.Begin);
            byte[] nameBytes = br.ReadBytes(11);
            string fieldName = _encoding.GetString(nameBytes).TrimEnd('\0').Trim();
            byte fieldType = br.ReadByte();

            int fieldOffsetVal = br.ReadInt32();
            byte fieldLength = br.ReadByte();
            byte decimalCount = br.ReadByte();

            rawFields.Add((fieldName, fieldOffsetVal, fieldLength, decimalCount));

            pos += 32;
        }

        // VFP/Express DBF stores the actual record offset in descriptor bytes 12-15.
        // Declared field lengths are 1 byte wider than actual, causing boundary shift per field.
        // Use stored offsets and compute effective lengths from offset differences.
        var fields = new List<(string name, int offset, int length, int decimalCount)>();
        bool useStoredOffsets = rawFields.Count > 0 && rawFields[0].storedOffset == 1;

        if (useStoredOffsets)
        {
            for (int j = 0; j < rawFields.Count; j++)
            {
                int effectiveLen = j < rawFields.Count - 1
                    ? rawFields[j + 1].storedOffset - rawFields[j].storedOffset
                    : rawFields[j].declaredLength;
                fields.Add((rawFields[j].name, rawFields[j].storedOffset, effectiveLen, rawFields[j].decimalCount));
            }
        }
        else
        {
            // dBASE III compatibility: stored offsets are 0, fall back to accumulation
            int fieldOffset = 1;
            foreach (var (name, _, declaredLength, dec) in rawFields)
            {
                fields.Add((name, fieldOffset, declaredLength, dec));
                fieldOffset += declaredLength;
            }
        }

        // === Read records ===
        fs.Seek(headerSize, SeekOrigin.Begin);

        for (int i = 0; i < recordCount; i++)
        {
            byte deletedFlag = br.ReadByte();
            if (deletedFlag == 0x2A) continue; // deleted record

            var record = new Record();
            foreach (var (name, offset, length, dec) in fields)
            {
                // offset already accounts for the deletion flag byte at position 0
                fs.Seek(headerSize + i * recordSize + offset, SeekOrigin.Begin);
                byte[] valBytes = br.ReadBytes(length);
                string valStr = _encoding.GetString(valBytes).TrimEnd('\0').Trim();

                record[name] = ParseValue(valStr, dec);
            }
            results.Add(record);
        }

        return results;
    }

    /// <summary>
    /// Read only the header info from a DBF file (fast — doesn't read records).
    /// Used for auto-discovery.
    /// </summary>
    public TableInfo? GetTableInfo(string filePath)
    {
        if (!File.Exists(filePath)) return null;

        try
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var br = new BinaryReader(fs);

            fs.Seek(0, SeekOrigin.Begin);
            br.ReadByte(); // version
            br.ReadByte(); // year
            br.ReadByte(); // month
            br.ReadByte(); // day
            int recordCount = br.ReadInt32();
            short headerSize = br.ReadInt16();
            short recordSize = br.ReadInt16();

            var rawFields = new List<(string name, char type, int storedOffset, int declaredLength, int decimals)>();
            int pos = 32;

            while (true)
            {
                fs.Seek(pos, SeekOrigin.Begin);
                byte term = br.ReadByte();
                if (term == 0x0D) break;

                fs.Seek(pos, SeekOrigin.Begin);
                byte[] nameBytes = br.ReadBytes(11);
                string fieldName = _encoding.GetString(nameBytes).TrimEnd('\0').Trim();
                byte fieldType = br.ReadByte();

                int storedOffset = br.ReadInt32();
                byte fieldLength = br.ReadByte();
                byte decimalCount = br.ReadByte();

                rawFields.Add((fieldName, (char)fieldType, storedOffset, fieldLength, decimalCount));
                pos += 32;
            }

            bool useStoredOffsets = rawFields.Count > 0 && rawFields[0].storedOffset == 1;
            var fields = new List<SourceFieldInfo>();
            for (int j = 0; j < rawFields.Count; j++)
            {
                int effectiveLen = useStoredOffsets && j < rawFields.Count - 1
                    ? rawFields[j + 1].storedOffset - rawFields[j].storedOffset
                    : rawFields[j].declaredLength;
                fields.Add(new SourceFieldInfo(
                    Name: rawFields[j].name,
                    DbType: rawFields[j].type.ToString(),
                    Length: effectiveLen,
                    Decimals: rawFields[j].decimals));
            }

            return new TableInfo(
                Fields: fields,
                RecordCount: recordCount);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Header-only info from a DBF file.
    /// </summary>
    public record TableInfo(IReadOnlyList<SourceFieldInfo> Fields, int RecordCount);

    private static object? ParseValue(string val, int decimalCount)
    {
        if (string.IsNullOrWhiteSpace(val)) return "";

        if (decimalCount > 0)
        {
            if (decimal.TryParse(val, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out decimal d))
                return d;
        }
        else if (int.TryParse(val, out int n))
        {
            return n;
        }
        else if (decimal.TryParse(val, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out decimal d))
        {
            return d;
        }

        return val;
    }
}
