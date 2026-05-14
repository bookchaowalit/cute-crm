namespace AccountingETL.Core.Domain;

/// <summary>
/// Defines the canonical schema for each entity type — what fields exist and their types.
/// Adapters use this to map source columns to target columns.
/// </summary>
public static class EntitySchema
{
    private static readonly Dictionary<EntityType, IReadOnlyDictionary<string, string>> _schemas = new()
    {
        [EntityType.Customer] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["code"] = "string",
            ["name"] = "string",
            ["address"] = "string",
            ["tax_id"] = "string",
            ["phone"] = "string",
            ["email"] = "string",
            ["credit_limit"] = "decimal",
            ["discount"] = "decimal",
            ["is_active"] = "boolean",
        },
        [EntityType.Supplier] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["code"] = "string",
            ["name"] = "string",
            ["address"] = "string",
            ["tax_id"] = "string",
            ["phone"] = "string",
            ["email"] = "string",
            ["payment_terms"] = "string",
            ["is_active"] = "boolean",
        },
        [EntityType.Item] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["code"] = "string",
            ["name"] = "string",
            ["unit"] = "string",
            ["cost_price"] = "decimal",
            ["sale_price"] = "decimal",
            ["stock_qty"] = "decimal",
            ["is_active"] = "boolean",
        },
        [EntityType.ArInvoice] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["invoice_no"] = "string",
            ["customer_code"] = "string",
            ["invoice_date"] = "datetime",
            ["due_date"] = "datetime",
            ["total_amount"] = "decimal",
            ["tax_amount"] = "decimal",
            ["discount_amount"] = "decimal",
            ["net_amount"] = "decimal",
            ["is_void"] = "boolean",
        },
        [EntityType.ArInvoiceLine] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["invoice_no"] = "string",
            ["line_no"] = "integer",
            ["item_code"] = "string",
            ["quantity"] = "decimal",
            ["unit_price"] = "decimal",
            ["amount"] = "decimal",
        },
        [EntityType.ApInvoice] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["invoice_no"] = "string",
            ["supplier_code"] = "string",
            ["invoice_date"] = "datetime",
            ["due_date"] = "datetime",
            ["total_amount"] = "decimal",
            ["tax_amount"] = "decimal",
            ["discount_amount"] = "decimal",
            ["net_amount"] = "decimal",
            ["is_void"] = "boolean",
        },
        [EntityType.ApInvoiceLine] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["invoice_no"] = "string",
            ["line_no"] = "integer",
            ["item_code"] = "string",
            ["quantity"] = "decimal",
            ["unit_price"] = "decimal",
            ["amount"] = "decimal",
        },
        [EntityType.GlTransaction] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["doc_no"] = "string",
            ["doc_date"] = "datetime",
            ["account_code"] = "string",
            ["description"] = "string",
            ["debit_amount"] = "decimal",
            ["credit_amount"] = "decimal",
        },
    };

    /// <summary>
    /// Get the canonical field definitions for an entity type.
    /// </summary>
    public static IReadOnlyDictionary<string, string> GetFields(EntityType entity)
    {
        if (_schemas.TryGetValue(entity, out var fields))
            return fields;
        throw new KeyNotFoundException($"No schema defined for entity type: {entity}");
    }

    /// <summary>
    /// Get the key field for an entity (used for upsert matching).
    /// </summary>
    public static string GetKeyField(EntityType entity) => entity switch
    {
        EntityType.Customer => "code",
        EntityType.Supplier => "code",
        EntityType.Item => "code",
        EntityType.ArInvoice => "invoice_no",
        EntityType.ArInvoiceLine => "invoice_no",
        EntityType.ApInvoice => "invoice_no",
        EntityType.ApInvoiceLine => "invoice_no",
        EntityType.GlTransaction => "doc_no",
        _ => throw new ArgumentException($"Unknown entity type: {entity}", nameof(entity))
    };
}
