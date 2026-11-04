"""One-way Xuân Hòa export plan. This file does not connect unless both endpoints are set.

Refuses kitplatform, kitplatform_test, postgres, pharmacore, novixa_prod, and any destination
whose name matches the source. Does not write back to the source. Not run by this change.
"""

from __future__ import annotations

import os
import sys

DENIED = {
    "kitplatform",
    "kitplatform_test",
    "postgres",
    "pharmacore",
    "novixa_prod",
}

# Direct identifiers dropped or replaced. Keys and foreign keys stay.
ANONYMIZE = {
    "customers": ["full_name", "phone", "email", "address_line", "date_of_birth", "identity_number"],
    "employees": ["full_name", "phone", "email", "identity_number"],
    "users": ["username", "email", "phone", "password_hash"],
}

TABLES = [
    "products",
    "product_units",
    "product_barcodes",
    "suppliers",
    "purchase_orders",
    "purchase_order_items",
    "goods_receipts",
    "inventory_batches",
    "stock_movements",
    "sales_shifts",
    "sales_orders",
    "sales_order_items",
    "sales_payments",
    "sales_returns",
    "sales_return_items",
    "sales_return_payments",
    "customers",
    "repurchase_suggestions",
]


def database_name(connection: str) -> str:
    for part in connection.split(";"):
        key, _, value = part.partition("=")
        if key.strip().lower() in {"database", "dbname"}:
            return value.strip()
    raise SystemExit("Connection string has no database name.")


def main() -> None:
    source = os.environ.get("NOVIXA_EXPORT_SOURCE", "").strip()
    dest = os.environ.get("NOVIXA_EXPORT_DEST", "").strip()
    if not source or not dest:
        raise SystemExit(
            "BLOCKED: set NOVIXA_EXPORT_SOURCE and NOVIXA_EXPORT_DEST. "
            "This script did not open a connection."
        )

    source_db = database_name(source)
    dest_db = database_name(dest)
    if source_db.lower() in DENIED or dest_db.lower() in DENIED:
        raise SystemExit(f"Refusing database '{source_db}' -> '{dest_db}'. No connection was opened.")
    if source_db.lower() == dest_db.lower():
        raise SystemExit("Source and destination are the same database. No connection was opened.")
    if "prod" in dest_db.lower():
        raise SystemExit("Destination looks like production. No connection was opened.")
    if os.environ.get("NOVIXA_EXPORT_SOURCE_CONFIRMED") != "readonly-xuanhoa":
        raise SystemExit(
            "Set NOVIXA_EXPORT_SOURCE_CONFIRMED=readonly-xuanhoa after a human confirms "
            "the source session is read-only. No connection was opened."
        )

    print("export_not_executed")
    print("tables", ",".join(TABLES))
    print("anonymize", ",".join(ANONYMIZE))
    print("Would copy keys and replace direct identifiers on the destination only.")
    raise SystemExit("Stopped before connect. Wire the copy only after the isolated destination exists.")


if __name__ == "__main__":
    main()
