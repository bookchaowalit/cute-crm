namespace AccountingETL.Core.Domain;

/// <summary>
/// Canonical entity types that any accounting system should support.
/// New entity types can be added here as the domain expands.
/// </summary>
public enum EntityType
{
    Customer,
    Supplier,
    Item,
    ArInvoice,        // Accounts Receivable Invoice (Sales)
    ArInvoiceLine,    // AR Invoice line items
    ApInvoice,        // Accounts Payable Invoice (Purchase)
    ApInvoiceLine,    // AP Invoice line items
    GlTransaction,    // General Ledger Transaction
}
