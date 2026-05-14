using System.Text;

namespace ExpressETL;

/// <summary>
/// อ่านไฟล์ .DBF (dBase/FoxPro format) โดยตรง ไม่ต้องใช้ library เพิ่มเติม
/// </summary>
public class DbfReader
{
    private readonly Encoding _encoding;

    public DbfReader(string encodingName = "tis-620")
    {
        // Register Windows code pages encoding provider for non-UTF encodings like tis-620
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        _encoding = Encoding.GetEncoding(encodingName);
    }

    public List<Dictionary<string, object>> Read(string filePath)
    {
        var results = new List<Dictionary<string, object>>();

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
        var fields = new List<(string name, int offset, int length, int decimalCount)>();
        int fieldOffset = 1; // 1 byte for deleted flag
        int pos = 32;

        while (true)
        {
            fs.Seek(pos, SeekOrigin.Begin);
            byte term = br.ReadByte();
            if (term == 0x0D) break;

            fs.Seek(pos, SeekOrigin.Begin);
            byte[] nameBytes = br.ReadBytes(11);
            string fieldName = _encoding.GetString(nameBytes).TrimEnd('\0').Trim();
            br.ReadByte(); // field type

            int fieldOffsetVal = br.ReadInt32();
            byte fieldLength = br.ReadByte();
            byte decimalCount = br.ReadByte();

            // ใช้ offset ที่คำนวณเอง (fieldOffsetVal ในบางไฟล์ไม่ถูกต้อง)
            fields.Add((fieldName, fieldOffset, fieldLength, decimalCount));
            fieldOffset += fieldLength;

            pos += 32;
        }

        // === Read records ===
        fs.Seek(headerSize, SeekOrigin.Begin);

        for (int i = 0; i < recordCount; i++)
        {
            byte deletedFlag = br.ReadByte();
            if (deletedFlag == 0x2A) continue; // deleted record

            var record = new Dictionary<string, object>();
            foreach (var (name, offset, length, dec) in fields)
            {
                fs.Seek(headerSize + 1 + i * recordSize + offset, SeekOrigin.Begin);
                byte[] valBytes = br.ReadBytes(length);
                string valStr = _encoding.GetString(valBytes).TrimEnd('\0').Trim();

                // Parse type
                record[name] = ParseValue(valStr, dec);
            }
            results.Add(record);
        }

        return results;
    }

    private object ParseValue(string val, int decimalCount)
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
