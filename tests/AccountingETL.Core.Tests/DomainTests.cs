using AccountingETL.Core.Domain;
using Xunit;

namespace AccountingETL.Core.Tests;

public class EntitySchemaTests
{
    [Theory]
    [InlineData(EntityType.Customer, "code")]
    [InlineData(EntityType.Supplier, "code")]
    [InlineData(EntityType.Item, "code")]
    [InlineData(EntityType.ArInvoice, "invoice_no")]
    [InlineData(EntityType.ArInvoiceLine, "invoice_no")]
    [InlineData(EntityType.ApInvoice, "invoice_no")]
    [InlineData(EntityType.ApInvoiceLine, "invoice_no")]
    [InlineData(EntityType.GlTransaction, "doc_no")]
    public void GetKeyField_ShouldReturnCorrectKey(EntityType entity, string expectedKey)
    {
        Assert.Equal(expectedKey, EntitySchema.GetKeyField(entity));
    }

    [Theory]
    [InlineData(EntityType.Customer)]
    [InlineData(EntityType.Supplier)]
    [InlineData(EntityType.Item)]
    [InlineData(EntityType.ArInvoice)]
    [InlineData(EntityType.ArInvoiceLine)]
    [InlineData(EntityType.ApInvoice)]
    [InlineData(EntityType.ApInvoiceLine)]
    [InlineData(EntityType.GlTransaction)]
    public void GetFields_ShouldReturnNonEmptyFields(EntityType entity)
    {
        var fields = EntitySchema.GetFields(entity);
        Assert.NotEmpty(fields);
        Assert.True(fields.ContainsKey("code") || fields.ContainsKey("invoice_no") || fields.ContainsKey("doc_no"));
    }

    [Fact]
    public void GetFields_Customer_ShouldHaveExpectedFields()
    {
        var fields = EntitySchema.GetFields(EntityType.Customer);
        var expected = new[] { "code", "name", "address", "tax_id", "phone", "email", "credit_limit", "discount", "is_active" };
        foreach (var f in expected)
            Assert.True(fields.ContainsKey(f), $"Missing field: {f}");
    }

    [Fact]
    public void GetFields_Item_ShouldHaveExpectedFields()
    {
        var fields = EntitySchema.GetFields(EntityType.Item);
        var expected = new[] { "code", "name", "unit", "cost_price", "sale_price", "stock_qty", "is_active" };
        foreach (var f in expected)
            Assert.True(fields.ContainsKey(f), $"Missing field: {f}");
    }

    [Fact]
    public void GetFields_UnknownEntity_ShouldThrow()
    {
        Assert.Throws<KeyNotFoundException>(() => EntitySchema.GetFields((EntityType)999));
    }
}

public class SyncCountsTests
{
    [Fact]
    public void Zero_ShouldReturnAllZeros()
    {
        Assert.Equal(0, SyncCounts.Zero.Inserted);
        Assert.Equal(0, SyncCounts.Zero.Updated);
        Assert.Equal(0, SyncCounts.Zero.Skipped);
    }

    [Fact]
    public void Total_ShouldSumAllCounts()
    {
        var counts = new SyncCounts(10, 5, 3);
        Assert.Equal(18, counts.Total);
    }

    [Fact]
    public void Add_ShouldCombineCounts()
    {
        var a = new SyncCounts(10, 5, 2);
        var b = new SyncCounts(3, 1, 1);
        var result = a.Add(b);

        Assert.Equal(13, result.Inserted);
        Assert.Equal(6, result.Updated);
        Assert.Equal(3, result.Skipped);
    }
}

public class SyncResultTests
{
    [Fact]
    public void SyncResult_ShouldCaptureAllFields()
    {
        var result = new SyncResult(
            Entity: EntityType.Customer,
            Counts: new SyncCounts(5, 3, 1),
            Duration: TimeSpan.FromSeconds(2),
            Status: "success",
            ErrorMessage: null);

        Assert.Equal(EntityType.Customer, result.Entity);
        Assert.Equal(5, result.Counts.Inserted);
        Assert.Equal(3, result.Counts.Updated);
        Assert.Equal(TimeSpan.FromSeconds(2), result.Duration);
        Assert.Equal("success", result.Status);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void SyncResult_FailedStatus_ShouldHaveErrorMessage()
    {
        var result = new SyncResult(
            EntityType.Item, SyncCounts.Zero, TimeSpan.Zero, "failed", "Connection refused");

        Assert.Equal("failed", result.Status);
        Assert.Equal("Connection refused", result.ErrorMessage);
    }
}

public class EtlConfigTests
{
    [Fact]
    public void BuildConnectionString_ShouldReturnValidString()
    {
        var config = new EtlConfig
        {
            TargetHost = "localhost",
            TargetPort = 5432,
            TargetDatabase = "testdb",
            TargetUser = "admin",
            TargetPassword = "secret"
        };

        var connStr = config.BuildConnectionString();
        Assert.Contains("Host=localhost", connStr);
        Assert.Contains("Port=5432", connStr);
        Assert.Contains("Database=testdb", connStr);
        Assert.Contains("Username=admin", connStr);
        Assert.Contains("Password=secret", connStr);
    }

    [Fact]
    public void DefaultValues_ShouldBeReasonable()
    {
        var config = new EtlConfig();
        Assert.Equal("localhost", config.TargetHost);
        Assert.Equal(5432, config.TargetPort);
        Assert.Equal("accounting_etl", config.TargetDatabase);
        Assert.Equal(24, config.IntervalHours);
        Assert.Equal("tis-620", config.SourceEncoding);
    }
}

public class SyncLogTests
{
    [Fact]
    public void SyncLog_ShouldCaptureAllFields()
    {
        var now = DateTimeOffset.Now;
        var log = new SyncLog(
            SyncTime: now, Entity: EntityType.Customer,
            Inserted: 10, Updated: 5, Skipped: 2,
            Duration: TimeSpan.FromMilliseconds(1500), Status: "success");

        Assert.Equal(now, log.SyncTime);
        Assert.Equal(EntityType.Customer, log.Entity);
        Assert.Equal(10, log.Inserted);
        Assert.Equal(5, log.Updated);
        Assert.Equal(TimeSpan.FromMilliseconds(1500), log.Duration);
        Assert.Equal("success", log.Status);
        Assert.Null(log.ErrorMessage);
    }

    [Fact]
    public void SyncLog_WithError_ShouldHaveErrorMessage()
    {
        var log = new SyncLog(
            DateTimeOffset.Now, EntityType.Item, 0, 0, 0, TimeSpan.Zero,
            "failed", "Timeout connecting to database");

        Assert.Equal("Timeout connecting to database", log.ErrorMessage);
    }
}
