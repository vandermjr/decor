# Product and service separation

## Deployment gate

Do not deploy the product changes against an unmigrated database. Do not migrate
while an older Decor instance is connected. Coordinate deployment with the service
catalog and quote/order changes: commercial items must use nullable ProductID and
nullable ServiceID, with exactly one supplied. This migration does not update their
backend or UI code by itself; the coordinated backend and UI changes are included
in this application version.

The SQL is embedded in Decor.Infrastructure for a trusted maintenance runner and
integration tests. Nothing invokes it from application startup, repository access,
DI registration or authentication. No production database is used by these tests.

## Execute

1. Back up the database and verify restoration before scheduling maintenance.
2. Rehearse on a restored copy with the same MariaDB version, tables and constraints.
   The tested baseline is MariaDB 10.11.6. Required legacy columns include pricing,
   commission, observations, ProductType, stock quantities and DefaultInstallationServiceID.
3. Close every Decor instance and disable other writers. Review service stock,
   purchase, reservation, balance and kit references. These are invalid, not history
   to delete automatically. Resolve them through an approved business correction.
4. Use a dedicated, explicitly authorized maintenance connection. Read
   `Migrations/20261008_separate_services_from_products.sql` in its entirety and send
   it as a single MySqlConnector/Dapper ExecuteAsync command, as demonstrated in
   ServiceSeparationMigrationIntegrationTests.ApplyAsync. Do not split on semicolons:
   the MariaDB anonymous compound statement contains internal semicolons. No
   DELIMITER or Allow User Variables setting is required with this API.
5. Verify the queries below, the default-service FKs and preserved item IDs/prices.
   Only reopen Decor with all coordinated application changes installed.

Do not paste credentials into chat or logs. This document does not supply an
automatic runner or a production connection string.

```sql
SELECT COUNT(*) AS RemainingLegacyServices FROM products WHERE ProductType <> 1;
SELECT COUNT(*) AS InvalidQuoteItems FROM quote_items
WHERE (ProductID IS NULL) = (ServiceID IS NULL);
SELECT COUNT(*) AS InvalidOrderItems FROM order_items
WHERE (ProductID IS NULL) = (ServiceID IS NULL);
SELECT TABLE_NAME, COLUMN_NAME, REFERENCED_TABLE_NAME
FROM information_schema.KEY_COLUMN_USAGE
WHERE TABLE_SCHEMA = DATABASE() AND REFERENCED_TABLE_NAME = 'services';
```

The first three counts must be zero. Expected services FKs are quote_items.ServiceID,
order_items.ServiceID and products.DefaultInstallationServiceID. Compare the
services catalog against the backup, including inactive services and all monetary
fields. Installation/execution history continues referencing the same OrderItemID.

## Safety and compatibility

- Legacy services are copied with the same ID, description, activity, cost, sale
  price, employee commission and observations. Conflicting existing service IDs
  abort instead of overwriting either catalog. Zero stock and no nested default
  service are required on legacy service rows.
- Both commercial tables move service references to ServiceID and clear ProductID.
  The copy, item updates and legacy-row deletion share a transaction. There are no
  service rows retained in products after successful execution.
- All known stock/purchase/reservation/kit references, plus discovered inbound
  product FKs, are checked before transfer, including cascading constraints.
  Goods-only kits retain both product FKs. Invalid service kit components abort;
  the current kit validators do not support service components.
- DDL implicitly commits in MariaDB. The script is not fully transactional: a
  failure may leave an empty services table, new nullable columns or changed FKs.
  Keep writers stopped; inspect the failure, restore the backup or correct and
  rerun under maintenance. Never claim that ROLLBACK reverses schema changes.
- Reexecution does not overwrite separated services or regrant revoked permissions.
  Administrator defaults are granted only when each Services permission is first
  created. Existing permission codes are not treated as permission to grant access.
- ProductType and EmployeeCommissionValue remain in the legacy entity/DTO/SQL
  shape to avoid breaking positional constructors and existing consumers. DTO
  members are Browsable(false); outbound product DTOs use Good and null commission.
  Non-Good DTO inputs and repository saves are rejected, not silently converted.
  The products CHECK constrains ProductType to Good. Type.Service remains available
  for source compatibility; `tipo:servico` returns no goods in product searches.
- ServiceProductExists retains its legacy interface name but queries services.
  Product and service numeric IDs can match; this is not a self-reference.

Run focused tests without a production connection:

```sh
dotnet test tests/Decor.Infrastructure.IntegrationTests/Decor.Infrastructure.IntegrationTests.csproj --filter 'FullyQualifiedName~ServiceSeparationMigrationIntegrationTests|FullyQualifiedName~ProductSeparationContractTests'
```

Testcontainers creates and disposes a dedicated MariaDB database. Database grants
must permit the required DDL and DML; free-form references outside discoverable
columns/FKs and unsupported schema customizations still require manual audit.