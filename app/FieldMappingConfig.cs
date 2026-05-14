namespace AccountingETL.App;

using AccountingETL.Core.Domain;

/// <summary>
/// เก็บการ mapping ชื่อ field จาก DBF → DB column
/// </summary>
public class FieldMappingConfig
{
    // Customer fields
    public string CustomerCodeField { get; set; } = "CUSTCODE";
    public string CustomerNameField { get; set; } = "CUSTNAME";
    public string CustomerTaxIdField { get; set; } = "TAXID";
    public string CustomerAddressField { get; set; } = "ADDRESS";
    public string CustomerTelField { get; set; } = "TEL";
    public string CustomerCreditDayField { get; set; } = "CREDITDAY";

    // Supplier fields
    public string SupplierCodeField { get; set; } = "VENDCODE";
    public string SupplierNameField { get; set; } = "VENDNAME";
    public string SupplierTaxIdField { get; set; } = "TAXID";
    public string SupplierAddressField { get; set; } = "ADDRESS";
    public string SupplierTelField { get; set; } = "TEL";
    public string SupplierCreditDayField { get; set; } = "CREDITDAY";

    // Item fields
    public string ItemCodeField { get; set; } = "ITEMCODE";
    public string ItemNameField { get; set; } = "ITEMNAME";
    public string ItemSalePriceField { get; set; } = "SALEPRICE";
    public string ItemCostPriceField { get; set; } = "COSTPRICE";
    public string ItemUnitField { get; set; } = "UNIT";

    /// <summary>
    /// Convert to dictionary format for ExpressFieldMapper.
    /// Maps canonical field names to Express-specific source field names.
    /// </summary>
    public Dictionary<EntityType, Dictionary<string, string>> ToDictionary()
    {
        return new Dictionary<EntityType, Dictionary<string, string>>
        {
            [EntityType.Customer] = new Dictionary<string, string>
            {
                ["code"] = CustomerCodeField,
                ["name"] = CustomerNameField,
                ["tax_id"] = CustomerTaxIdField,
                ["address"] = CustomerAddressField,
                ["phone"] = CustomerTelField,
            },
            [EntityType.Supplier] = new Dictionary<string, string>
            {
                ["code"] = SupplierCodeField,
                ["name"] = SupplierNameField,
                ["tax_id"] = SupplierTaxIdField,
                ["address"] = SupplierAddressField,
                ["phone"] = SupplierTelField,
            },
            [EntityType.Item] = new Dictionary<string, string>
            {
                ["code"] = ItemCodeField,
                ["name"] = ItemNameField,
                ["sale_price"] = ItemSalePriceField,
                ["cost_price"] = ItemCostPriceField,
                ["unit"] = ItemUnitField,
            },
        };
    }
}
