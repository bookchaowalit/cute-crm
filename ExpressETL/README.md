# ExpressETL — คู่มือ Build & Deploy

## สิ่งที่เปลี่ยนจาก Python → C#

| | Python (PyInstaller) | C# (.NET 8) |
|---|---|---|
| ขนาดไฟล์ | ~40-50 MB | ~10-15 MB |
| Start time | 1-3 วินาที | <1 วินาที |
| Memory | ~100 MB | ~30 MB |
| GUI | tkinter (เก่า) | WinForms (native) |
| Crash handling | Silent crash | MessageBox + log |

## บน VM Windows 11 — ครั้งเดียว

### 1. ติดตั้ง .NET 8 SDK
ดาวน์โหลด: https://dotnet.microsoft.com/download/dotnet/8.0
เลือก **SDK** (ไม่ใช่ Runtime อย่างเดียว)

### 2. Clone โปรเจกต์
```powershell
git clone <repo-url>
cd cute-crm/ExpressETL
```

### 3. Restore packages + Build
```powershell
dotnet restore
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeAllContentForSelfExtract=true
```

### 4. ไฟล์ที่ได้
```
bin\Release\net8.0-windows\win-x64\publish\ExpressETL.exe
```
ไฟล์เดียว ~10-15 MB — copy ให้ user ได้เลย

## สำหรับ Developer — แก้โค้ดแล้ว Build ใหม่

```powershell
cd cute-crm/ExpressETL
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

## สิ่งที่ user ต้องทำ
1. Double-click `ExpressETL.exe`
2. หน้าต่าง Settings เปิดอัตโนมัติ (ครั้งแรก)
3. กรอก PostgreSQL host, user, pass
4. กด "Test Connection" → ✓
5. กด "Save & Close"
6. หน้าต่างหลักแสดง status — ETL รันอัตโนมัติทุก X ชั่วโมง

## Schema ที่ต้องมีใน PostgreSQL
```sql
CREATE SCHEMA express_staging;

CREATE TABLE express_staging.customers (
    express_code VARCHAR(20) PRIMARY KEY,
    name VARCHAR(200),
    address TEXT,
    tel VARCHAR(50),
    tax_id VARCHAR(20),
    credit_day INT,
    updated_at TIMESTAMP DEFAULT NOW()
);

CREATE TABLE express_staging.suppliers (
    express_code VARCHAR(20) PRIMARY KEY,
    name VARCHAR(200),
    address TEXT,
    tel VARCHAR(50),
    tax_id VARCHAR(20),
    credit_day INT,
    updated_at TIMESTAMP DEFAULT NOW()
);

CREATE TABLE express_staging.items (
    item_code VARCHAR(50) PRIMARY KEY,
    item_name VARCHAR(200),
    unit VARCHAR(20),
    sale_price NUMERIC(15,2),
    cost_price NUMERIC(15,2),
    updated_at TIMESTAMP DEFAULT NOW()
);
```

## ไฟล์ DBF ที่อ่าน
| ไฟล์ | Doctype | Field ที่ใช้ |
|---|---|---|
| `ARMAS.DBF` | Customer | CUSTCODE, CUSTNAME, TAXID, ADDRESS, TEL, CREDITDAY |
| `APMAS.DBF` | Supplier | VENDCODE, VENDNAME, TAXID, ADDRESS, TEL, CREDITDAY |
| `ICMAS.DBF` | Item | ITEMCODE, ITEMNAME, SALEPRICE, COSTPRICE, UNIT |

> ⚠️ ชื่อ field อาจต่างจากเวอร์ชัน Express — ถ้าชื่อไม่ตรง ต้องแก้ใน `EtlService.cs`
