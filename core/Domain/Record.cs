namespace AccountingETL.Core.Domain;

/// <summary>
/// A generic key-value data record with case-insensitive keys.
/// This is the canonical data shape flowing through the ETL pipeline.
/// </summary>
public class Record : Dictionary<string, object?>
{
    public Record() : base(StringComparer.OrdinalIgnoreCase) { }

    public Record(int capacity) : base(capacity, StringComparer.OrdinalIgnoreCase) { }

    public Record(IDictionary<string, object?> dictionary)
        : base(dictionary, StringComparer.OrdinalIgnoreCase) { }

    /// <summary>
    /// Get value with type conversion. Returns default if null or missing.
    /// </summary>
    public T? Get<T>(string key, T? defaultValue = default)
    {
        if (!TryGetValue(key, out var value) || value is null)
            return defaultValue;

        if (value is T typed)
            return typed;

        // Try common conversions
        return (T?)Convert.ChangeType(value, typeof(T));
    }
}
