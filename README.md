# AccountingETL

Accounting data ETL system — sync data from **any accounting program** to **any target** (PostgreSQL, ERP, API).

Built with **Hexagonal Architecture** (Ports & Adapters) so new accounting programs can be added without changing the core.

## Architecture

```
┌─────────────────────────────────────────────┐
│              core/ (Domain)                  │
│  - ISourceAdapter  (port)                    │
│  - ITargetAdapter  (port)                    │
│  - IFieldMapper    (port)                    │
│  - IEtlPipeline    (port)                    │
│  - EntityType, Record, EtlConfig, ...        │
└──────────┬──────────────────────┬────────────┘
           │                      │
    ┌──────▼──────┐        ┌──────▼──────┐
    │  adapters/  │        │  adapters/  │
    ├─────────────┤        ├─────────────┤
    │  express/   │        │ postgresql/ │
    │   (DBF)     │        │  (COPY)     │
    │  sap-b1/    │        │  erpnext/   │
    │   (ODBC)    │        │  (REST API) │
    │  csv/       │        │  mysql/     │
    │  ...        │        │  ...        │
    └──────┬──────┘        └──────┬──────┘
           │                      │
    ┌──────▼──────────────────────▼──────┐
    │              app/                   │
    │  - WinForms GUI (MainForm)          │
    │  - Settings, History, Service       │
    │  - Composition Root (DI setup)      │
    └─────────────────────────────────────┘
```

### Project Structure

| Project | Path | Purpose |
|---------|------|---------|
| **AccountingETL.Core** | `core/` | Domain models + ports (interfaces). Zero external dependencies. |
| **AccountingETL.Adapters.Express** | `adapters/express/` | Source adapter for Express Accounting (DBF files) |
| **AccountingETL.Adapters.PostgreSQL** | `adapters/postgresql/` | Target adapter using PostgreSQL COPY protocol |
| **AccountingETL.Adapters.ErpNext** | `adapters/erpnext/` | Target adapter using ERPNext REST API |
| **AccountingETL.App** | `app/` | Desktop UI (WinForms), composition root, Windows Service |

### Key Interfaces

```csharp
// Read data from any accounting program
interface ISourceAdapter {
    string SourceName { get; }
    IReadOnlyList<EntityType> SupportedEntities { get; }
    IAsyncEnumerable<Record> ReadAsync(EntityType entity, DateTimeOffset? cutoff, CancellationToken ct);
    Task<bool> ValidateConnectionAsync(CancellationToken ct);
}

// Write data to any target system
interface ITargetAdapter {
    string TargetName { get; }
    IReadOnlyList<EntityType> SupportedEntities { get; }
    Task EnsureSchemaAsync(CancellationToken ct);
    Task<SyncCounts> UpsertAsync(EntityType entity, IEnumerable<Record> records, string keyField, CancellationToken ct);
    Task<ISet<string>> GetExistingKeysAsync(EntityType entity, string keyField, CancellationToken ct);
    Task WriteSyncLogAsync(SyncLog log, CancellationToken ct);
    Task<IReadOnlyList<SyncLog>> ReadSyncHistoryAsync(int limit, CancellationToken ct);
    Task<bool> ValidateConnectionAsync(CancellationToken ct);
}

// Map source field names to canonical field names
interface IFieldMapper {
    Record Map(EntityType entity, Record sourceRecord);
    string GetSourceField(EntityType entity, string canonicalField);
    void SetSourceField(EntityType entity, string canonicalField, string sourceField);
}
```

### Supported Entity Types

| Entity | Description | Key Field |
|--------|-------------|-----------|
| `Customer` | Master data - customers | `code` |
| `Supplier` | Master data - suppliers | `code` |
| `Item` | Master data - items/products | `code` |
| `ArInvoice` | Sales invoice headers | `invoice_no` |
| `ArInvoiceLine` | Sales invoice line items | `invoice_no` + `line_no` |
| `ApInvoice` | Purchase invoice headers | `invoice_no` |
| `ApInvoiceLine` | Purchase invoice line items | `invoice_no` + `line_no` |
| `GlTransaction` | General ledger entries | `doc_no` + `account_code` |

## Quick Start

### Prerequisites
- .NET 8.0 SDK
- Windows (for running the GUI app) — build works on any OS

### Build
```bash
dotnet build
```

### Run (GUI)
```bash
cd app/bin/Debug/net8.0-windows/win-x64
.\AccountingETL.exe
```

### Run as Windows Service
```bash
# Install
.\AccountingETL.exe /install

# Uninstall
.\AccountingETL.exe /uninstall
```

### Publish Single File
```bash
dotnet publish app/AccountingETL.App.csproj -c Release -r win-x64 --self-contained
```

## Adding a New Adapter

### Step 1: Create a new adapter project

```bash
mkdir adapters/sap-b1
cd adapters/sap-b1
```

Create `AccountingETL.Adapters.SapB1.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RootNamespace>AccountingETL.Adapters.SapB1</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\core\AccountingETL.Core.csproj" />
  </ItemGroup>
</Project>
```

### Step 2: Implement ISourceAdapter

```csharp
using AccountingETL.Core.Domain;
using AccountingETL.Core.Ports;

namespace AccountingETL.Adapters.SapB1;

public class SapB1SourceAdapter : ISourceAdapter
{
    private readonly string _connectionString;

    public string SourceName => "SAP Business One";

    public IReadOnlyList<EntityType> SupportedEntities { get; } = new[]
    {
        EntityType.Customer,
        EntityType.Supplier,
        EntityType.Item,
        // ... add more as needed
    };

    public SapB1SourceAdapter(string connectionString)
    {
        _connectionString = connectionString;
    }

    public Task<bool> ValidateConnectionAsync(CancellationToken ct = default)
    {
        // Test ODBC connection
        // ...
    }

    public async IAsyncEnumerable<Record> ReadAsync(
        EntityType entity,
        DateTimeOffset? cutoff,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        // Read from SAP B1 via DI API or Service Layer
        // Map to Record with canonical field names
        // yield return each record
    }
}
```

### Step 3: Implement IFieldMapper (if needed)

```csharp
public class SapB1FieldMapper : IFieldMapper
{
    public Record Map(EntityType entity, Record sourceRecord)
    {
        var result = new Record();
        // Map SAP B1 field names → canonical names
        // e.g., result["code"] = sourceRecord["CardCode"];
        return result;
    }

    public string GetSourceField(EntityType entity, string canonicalField)
    {
        // Return SAP B1 field name for canonical field
    }

    public void SetSourceField(EntityType entity, string canonicalField, string sourceField)
    {
        // Allow customization
    }
}
```

### Step 4: Wire it up in the App

In `app/MainForm.cs` → `LoadConfig()`:
```csharp
// For a new source:
var source = new SapB1SourceAdapter(connectionString);
```

In `app/Program.cs` → `RunAsService()`:
```csharp
services.AddSingleton<ISourceAdapter>(sp =>
    new SapB1SourceAdapter(connectionString));
```

### Step 5: Add to solution

```bash
dotnet sln add adapters/sap-b1/AccountingETL.Adapters.SapB1.csproj
```

## Entity Schema

Canonical field definitions are in `core/Domain/EntitySchema.cs`. Each entity type has:
- **Field names** (canonical)
- **Field types** (string, decimal, datetime, boolean, etc.)
- **Key field** (used for upsert matching)

## How Upsert Works

1. **Read** records from source adapter
2. **Transform** via field mapper (source fields → canonical fields)
3. **Query** existing keys from target
4. **Split** into new vs existing records
5. **Insert** new records via bulk COPY
6. **Update** existing records via COPY to temp table + `UPDATE ... FROM`

## Configuration

Config is stored in `config.json` (encrypted by default):
```json
{
  "DbfPath": "C:\\ExpressD\\dat",
  "PgHost": "localhost",
  "PgPort": 5432,
  "PgDb": "accounting_etl",
  "PgUser": "postgres",
  "PgPass": "...",
  "IntervalHours": 24,
  "DbfEncoding": "tis-620",
  "LineToken": "...",
  "NotifyOnSuccess": true,
  "NotifyOnFailure": true,
  "MinimizeToTray": true,
  "SyncToErpNext": false,
  "ErpNextUrl": "",
  "ErpNextApiKey": "",
  "ErpNextApiSecret": ""
}
```

## Notifications

LINE Notify integration — send success/failure notifications:
```json
{
  "LineToken": "your-line-notify-token",
  "NotifyOnSuccess": true,
  "NotifyOnFailure": true
}
```

## License

MIT
