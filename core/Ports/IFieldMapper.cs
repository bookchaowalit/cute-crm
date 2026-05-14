using AccountingETL.Core.Domain;

namespace AccountingETL.Core.Ports;

/// <summary>
/// Port for mapping source field names to canonical target field names.
/// Each accounting program has different column names — this adapter normalizes them.
/// </summary>
public interface IFieldMapper
{
    /// <summary>
    /// Maps a raw source record to a canonical record using configured field mappings.
    /// </summary>
    Record Map(EntityType entity, Record sourceRecord);

    /// <summary>
    /// Get the source field name for a canonical field in a given entity.
    /// </summary>
    string GetSourceField(EntityType entity, string canonicalField);

    /// <summary>
    /// Set the source field name for a canonical field in a given entity.
    /// </summary>
    void SetSourceField(EntityType entity, string canonicalField, string sourceField);
}
