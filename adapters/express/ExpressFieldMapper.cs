using AccountingETL.Core.Domain;
using AccountingETL.Core.Ports;

namespace AccountingETL.Adapters.Express;

/// <summary>
/// Field mapper for Express Accounting — maps Express DBF column names to canonical field names.
/// </summary>
public class ExpressFieldMapper : IFieldMapper
{
    // Default Express field mappings
    private readonly Dictionary<EntityType, Dictionary<string, string>> _mappings;

    private static readonly Dictionary<EntityType, Dictionary<string, string>> _defaults = new()
    {
        [EntityType.Customer] = new Dictionary<string, string>
        {
            ["code"] = "CUSTCODE",
            ["name"] = "CUSTNAME",
            ["address"] = "ADDRESS",
            ["tax_id"] = "TAXID",
            ["phone"] = "TEL",
            ["credit_limit"] = "CREDITLIMIT",
            ["discount"] = "DISCOUNT",
        },
        [EntityType.Supplier] = new Dictionary<string, string>
        {
            ["code"] = "VENDCODE",
            ["name"] = "VENDNAME",
            ["address"] = "ADDRESS",
            ["tax_id"] = "TAXID",
            ["phone"] = "TEL",
            ["payment_terms"] = "CREDITDAY",
        },
        [EntityType.Item] = new Dictionary<string, string>
        {
            ["code"] = "ITEMCODE",
            ["name"] = "ITEMNAME",
            ["unit"] = "UNIT",
            ["cost_price"] = "COSTPRICE",
            ["sale_price"] = "SALEPRICE",
        },
        [EntityType.ArInvoice] = new Dictionary<string, string>
        {
            ["invoice_no"] = "INVNO",
            ["customer_code"] = "CUSTCODE",
            ["invoice_date"] = "INVDATE",
            ["total_amount"] = "TOTALAMT",
            ["tax_amount"] = "VATAMT",
            ["net_amount"] = "NETAMT",
        },
        [EntityType.ArInvoiceLine] = new Dictionary<string, string>
        {
            ["invoice_no"] = "INVNO",
            ["line_no"] = "LINENO",
            ["item_code"] = "ITEMCODE",
            ["quantity"] = "QTY",
            ["unit_price"] = "UNITPRICE",
            ["amount"] = "AMOUNT",
        },
        [EntityType.ApInvoice] = new Dictionary<string, string>
        {
            ["invoice_no"] = "INVNO",
            ["supplier_code"] = "VENDCODE",
            ["invoice_date"] = "INVDATE",
            ["total_amount"] = "TOTALAMT",
            ["tax_amount"] = "VATAMT",
            ["net_amount"] = "NETAMT",
        },
        [EntityType.ApInvoiceLine] = new Dictionary<string, string>
        {
            ["invoice_no"] = "INVNO",
            ["line_no"] = "LINENO",
            ["item_code"] = "ITEMCODE",
            ["quantity"] = "QTY",
            ["unit_price"] = "UNITPRICE",
            ["amount"] = "AMOUNT",
        },
        [EntityType.GlTransaction] = new Dictionary<string, string>
        {
            ["doc_no"] = "DOCNO",
            ["doc_date"] = "GLDATE",
            ["account_code"] = "ACCODE",
            ["description"] = "ACNAME",
            ["debit_amount"] = "DEBIT",
            ["credit_amount"] = "CREDIT",
        },
    };

    public ExpressFieldMapper(Dictionary<EntityType, Dictionary<string, string>>? customMappings = null)
    {
        // Clone defaults
        _mappings = new Dictionary<EntityType, Dictionary<string, string>>();
        foreach (var (entity, fields) in _defaults)
        {
            _mappings[entity] = new Dictionary<string, string>(fields, StringComparer.OrdinalIgnoreCase);
        }

        // Apply custom overrides
        if (customMappings != null)
        {
            foreach (var (entity, fields) in customMappings)
            {
                if (!_mappings.ContainsKey(entity))
                    _mappings[entity] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                foreach (var (canonical, source) in fields)
                {
                    _mappings[entity][canonical] = source;
                }
            }
        }
    }

    public Record Map(EntityType entity, Record sourceRecord)
    {
        if (!_mappings.TryGetValue(entity, out var mapping))
            return sourceRecord; // No mapping defined, return as-is

        var result = new Record();
        foreach (var (canonicalField, sourceField) in mapping)
        {
            if (sourceRecord.TryGetValue(sourceField, out var value))
            {
                result[canonicalField] = value;
            }
        }

        // Pass through any unmapped fields
        foreach (var (key, value) in sourceRecord)
        {
            if (!result.ContainsKey(key))
                result[key] = value;
        }

        return result;
    }

    public string GetSourceField(EntityType entity, string canonicalField)
    {
        if (_mappings.TryGetValue(entity, out var mapping) &&
            mapping.TryGetValue(canonicalField, out var sourceField))
        {
            return sourceField;
        }
        return canonicalField; // fallback
    }

    public void SetSourceField(EntityType entity, string canonicalField, string sourceField)
    {
        if (!_mappings.ContainsKey(entity))
            _mappings[entity] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        _mappings[entity][canonicalField] = sourceField;
    }
}
