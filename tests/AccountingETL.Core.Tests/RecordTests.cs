using CoreRecord = AccountingETL.Core.Domain.Record;
using Xunit;

namespace AccountingETL.Core.Tests;

public class RecordTests
{
    [Fact]
    public void Record_ShouldBeCaseInsensitive()
    {
        var record = new AccountingETL.Core.Domain.Record();
        record["Code"] = "C001";
        Assert.Equal("C001", record["CODE"]);
        Assert.Equal("C001", record["code"]);
    }

    [Fact]
    public void Record_GetWithDefault_ShouldReturnDefault_WhenKeyMissing()
    {
        var record = new AccountingETL.Core.Domain.Record();
        Assert.Equal("default", record.Get<string>("missing", "default"));
    }

    [Fact]
    public void Record_GetWithTypeConversion_ShouldConvertDecimal()
    {
        var record = new AccountingETL.Core.Domain.Record { ["price"] = 100.50m };
        Assert.Equal(100.50m, record.Get<decimal>("price"));
    }

    [Fact]
    public void Record_GetWithTypeConversion_ShouldConvertIntToDecimal()
    {
        var record = new AccountingETL.Core.Domain.Record { ["qty"] = 5 };
        Assert.Equal(5m, record.Get<decimal>("qty"));
    }

    [Fact]
    public void Record_Get_ShouldReturnNull_WhenValueIsNull()
    {
        var record = new AccountingETL.Core.Domain.Record { ["name"] = null };
        Assert.Equal("fallback", record.Get<string>("name", "fallback"));
    }

    [Fact]
    public void Record_CopyConstructor_ShouldPreserveCaseInsensitivity()
    {
        var source = new Dictionary<string, object?> { { "Key", "value" } };
        var record = new AccountingETL.Core.Domain.Record(source);
        Assert.Equal("value", record["key"]);
    }
}
