#!/bin/bash
set -e

PG_PASSWORD="Expr3ss_ETL_2026!@#"

echo "=== Installing PostgreSQL..."
sudo apt-get update -qq && sudo apt-get install -y -qq postgresql postgresql-client

echo "=== Configuring PostgreSQL to accept remote connections..."

# Allow connections from any IP
sudo sed -i "s/^#listen_addresses = 'localhost'/listen_addresses = '*'/" /etc/postgresql/14/main/postgresql.conf
sudo sed -i "s/^listen_addresses = 'localhost'/listen_addresses = '*'/" /etc/postgresql/14/main/postgresql.conf

# Allow password auth from any IP
sudo sed -i '/^# IPv4 local connections/i host    all             all             0.0.0.0/0               md5' /etc/postgresql/14/main/pg_hba.conf

echo "=== Restarting PostgreSQL..."
sudo systemctl restart postgresql

echo "=== Setting up database and user..."
sudo -u postgres psql <<EOSQL
-- Set password for postgres user
ALTER USER postgres WITH PASSWORD '${PG_PASSWORD}';

-- Create staging schema
CREATE SCHEMA IF NOT EXISTS express_staging;
GRANT ALL ON SCHEMA express_staging TO postgres;

-- Create tables
CREATE TABLE IF NOT EXISTS express_staging.customers (
    express_code    VARCHAR(20) PRIMARY KEY,
    name            VARCHAR(200),
    address         TEXT,
    tel             VARCHAR(50),
    tax_id          VARCHAR(20),
    credit_day      INT,
    updated_at      TIMESTAMP DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS express_staging.suppliers (
    express_code    VARCHAR(20) PRIMARY KEY,
    name            VARCHAR(200),
    address         TEXT,
    tel             VARCHAR(50),
    tax_id          VARCHAR(20),
    credit_day      INT,
    updated_at      TIMESTAMP DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS express_staging.items (
    item_code       VARCHAR(50) PRIMARY KEY,
    item_name       VARCHAR(200),
    unit            VARCHAR(20),
    sale_price      NUMERIC(15,2),
    cost_price      NUMERIC(15,2),
    updated_at      TIMESTAMP DEFAULT NOW()
);

-- Grant permissions
GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA express_staging TO postgres;
GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA express_staging TO postgres;
EOSQL

echo "=== Opening firewall..."
sudo ufw allow 5432/tcp 2>/dev/null || echo "ufw not installed, skipping"

echo ""
echo "=========================================="
echo "  PostgreSQL Setup Complete!"
echo "=========================================="
echo ""
echo "  Host:     $(hostname -I | awk '{print $1}')"
echo "  Port:     5432"
echo "  Database: postgres"
echo "  User:     postgres"
echo "  Password: ${PG_PASSWORD}"
echo ""
echo "  Schema:   express_staging"
echo "=========================================="
echo ""
echo "  On Windows client, use these settings:"
echo "    PG Host:    $(hostname -I | awk '{print $1}')"
echo "    PG Port:    5432"
echo "    PG Database: postgres"
echo "    PG User:    postgres"
echo "    PG Password: ${PG_PASSWORD}"
echo "=========================================="
