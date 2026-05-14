namespace AccountingETL.Core.Exceptions;

/// <summary>
/// Base exception for all ETL pipeline errors.
/// </summary>
public class EtlException : Exception
{
    public EtlException(string message) : base(message) { }
    public EtlException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>
/// Thrown when a source adapter cannot read data.
/// </summary>
public class SourceException : EtlException
{
    public string SourceName { get; }

    public SourceException(string sourceName, string message) : base(message)
    {
        SourceName = sourceName;
    }

    public SourceException(string sourceName, string message, Exception inner)
        : base(message, inner)
    {
        SourceName = sourceName;
    }
}

/// <summary>
/// Thrown when a target adapter cannot write data.
/// </summary>
public class TargetException : EtlException
{
    public string TargetName { get; }

    public TargetException(string targetName, string message) : base(message)
    {
        TargetName = targetName;
    }

    public TargetException(string targetName, string message, Exception inner)
        : base(message, inner)
    {
        TargetName = targetName;
    }
}

/// <summary>
/// Thrown when field mapping cannot be resolved.
/// </summary>
public class MappingException : EtlException
{
    public MappingException(string message) : base(message) { }
    public MappingException(string message, Exception inner) : base(message, inner) { }
}
