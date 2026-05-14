using AccountingETL.Core.Domain;
using CoreRecord = AccountingETL.Core.Domain.Record;
using AccountingETL.Adapters.Express;

using Xunit;

namespace AccountingETL.Adapters.Express.Tests;

public class ExpressFieldMapperTests
{
    [Fact]
    public void Map_Customer_ShouldMapExpressFieldsToCanonical()
    {
        var mapper = new ExpressFieldMapper();
        var source = new CoreRecord
        {
            ["CUSTCODE"] = "C001", ["CUSTNAME"] = "Test Customer",
            ["TAXID"] = "0123456789012", ["ADDRESS"] = "123 Main St", ["TEL"] = "02-123-4567"
        };

        var result = mapper.Map(EntityType.Customer, source);

        Assert.Equal("C001", result["code"]);
        Assert.Equal("Test Customer", result["name"]);
        Assert.Equal("0123456789012", result["tax_id"]);
        Assert.Equal("02-123-4567", result["phone"]);
    }

    [Fact]
    public void Map_Supplier_ShouldMapExpressFieldsToCanonical()
    {
        var mapper = new ExpressFieldMapper();
        var source = new CoreRecord { ["VENDCODE"] = "V001", ["VENDNAME"] = "Test Supplier", ["TAXID"] = "0987654321098" };
        var result = mapper.Map(EntityType.Supplier, source);

        Assert.Equal("V001", result["code"]);
        Assert.Equal("Test Supplier", result["name"]);
        Assert.Equal("0987654321098", result["tax_id"]);
    }

    [Fact]
    public void Map_Item_ShouldMapExpressFieldsToCanonical()
    {
        var mapper = new ExpressFieldMapper();
        var source = new CoreRecord { ["ITEMCODE"] = "I001", ["ITEMNAME"] = "Test Item", ["COSTPRICE"] = 100.50m, ["SALEPRICE"] = 150.00m, ["UNIT"] = "PCS" };
        var result = mapper.Map(EntityType.Item, source);

        Assert.Equal("I001", result["code"]);
        Assert.Equal("Test Item", result["name"]);
        Assert.Equal(100.50m, result["cost_price"]);
        Assert.Equal(150.00m, result["sale_price"]);
        Assert.Equal("PCS", result["unit"]);
    }

    [Fact]
    public void Map_ARInvoice_ShouldMapExpressFieldsToCanonical()
    {
        var mapper = new ExpressFieldMapper();
        var source = new CoreRecord { ["INVNO"] = "AR-001", ["CUSTCODE"] = "C001", ["INVDATE"] = "2024-01-15", ["TOTALAMT"] = 1000m, ["VATAMT"] = 70m, ["NETAMT"] = 1070m };
        var result = mapper.Map(EntityType.ArInvoice, source);

        Assert.Equal("AR-001", result["invoice_no"]);
        Assert.Equal("C001", result["customer_code"]);
        Assert.Equal(1000m, result["total_amount"]);
        Assert.Equal(70m, result["tax_amount"]);
        Assert.Equal(1070m, result["net_amount"]);
    }

    [Fact]
    public void Map_GLTransaction_ShouldMapExpressFieldsToCanonical()
    {
        var mapper = new ExpressFieldMapper();
        var source = new CoreRecord { ["DOCNO"] = "GL-001", ["GLDATE"] = "2024-01-15", ["ACCODE"] = "1100", ["ACNAME"] = "Cash", ["DEBIT"] = 500m, ["CREDIT"] = 0m };
        var result = mapper.Map(EntityType.GlTransaction, source);

        Assert.Equal("GL-001", result["doc_no"]);
        Assert.Equal("1100", result["account_code"]);
        Assert.Equal("Cash", result["description"]);
        Assert.Equal(500m, result["debit_amount"]);
        Assert.Equal(0m, result["credit_amount"]);
    }

    [Fact]
    public void Map_WithCustomOverrides_ShouldUseCustomMappings()
    {
        var customMappings = new Dictionary<EntityType, Dictionary<string, string>>
        {
            [EntityType.Customer] = new Dictionary<string, string> { ["code"] = "CUSTOMER_ID", ["name"] = "CUSTOMER_NAME" }
        };
        var mapper = new ExpressFieldMapper(customMappings);
        var source = new CoreRecord { ["CUSTOMER_ID"] = "C999", ["CUSTOMER_NAME"] = "Custom Mapped" };
        var result = mapper.Map(EntityType.Customer, source);

        Assert.Equal("C999", result["code"]);
        Assert.Equal("Custom Mapped", result["name"]);
    }

    [Fact]
    public void GetSourceField_ShouldReturnExpressFieldName()
    {
        var mapper = new ExpressFieldMapper();
        Assert.Equal("CUSTCODE", mapper.GetSourceField(EntityType.Customer, "code"));
        Assert.Equal("VENDCODE", mapper.GetSourceField(EntityType.Supplier, "code"));
        Assert.Equal("SALEPRICE", mapper.GetSourceField(EntityType.Item, "sale_price"));
    }

    [Fact]
    public void SetSourceField_ShouldAllowCustomMapping()
    {
        var mapper = new ExpressFieldMapper();
        mapper.SetSourceField(EntityType.Customer, "code", "NEW_CUST_CODE");
        Assert.Equal("NEW_CUST_CODE", mapper.GetSourceField(EntityType.Customer, "code"));
    }

    [Fact]
    public void Map_PassThroughUnmappedFields()
    {
        var mapper = new ExpressFieldMapper();
        var source = new CoreRecord { ["CUSTCODE"] = "C001", ["CUSTNAME"] = "Test", ["EXTRA_FIELD"] = "extra_value" };
        var result = mapper.Map(EntityType.Customer, source);
        Assert.Equal("extra_value", result["EXTRA_FIELD"]);
    }
}
