---
name: dapper
description: >
  Dapper micro-ORM for high-performance data access in .NET. Covers Query/TryQuery,
  multi-mapping, batch operations, stored procedures, transaction handling, typed results,
  and when to use Dapper vs EF Core.
  Load this skill when working with raw SQL, high-throughput queries, bulk operations,
  stored procedures, or when the user mentions "Dapper", "micro-ORM", "raw SQL",
  "QueryAsync", "Map", "storedProcedure", "Transaction", or "performance query".
---

# Dapper

## Core Principles

1. **Dapper is for performance-critical paths** — Use when EF Core's N+1, over-fetching, or query plan issues become a bottleneck. Not a replacement for EF Core; a complement.
2. **SQL is explicit** — No magic, no implicit queries. Every SQL statement is visible and reviewable. Write it, test it, own it.
3. **Multi-mapping over joins in application** — Let the DB do joins; let Dapper do mapping. This is where Dapper shines over raw ADO.NET.
4. **Dapper is a library, not a framework** — No conventions, no base classes, no configuration. It just adds extension methods to `IDbConnection`.

## Patterns

### Basic Query

```csharp
// Single entity
var order = await connection.QueryFirstOrDefaultAsync<Order>(
    "SELECT Id, CustomerId, Total, Status FROM Orders WHERE Id = @Id",
    new { Id = orderId });

// Projection to anonymous/DTO
var orders = await connection.QueryAsync<OrderDto>(
    @"SELECT o.Id, o.Total, c.Name AS CustomerName
      FROM Orders o
      JOIN Customers c ON o.CustomerId = c.Id
      WHERE o.Status = @Status",
    new { Status = OrderStatus.Pending });
```

### Multi-Mapping (the killer feature)

```csharp
// One query, multiple entities — splits on key positions
var orderWithItems = await connection.QueryAsync<Order, OrderItem, Order>(
    @"SELECT o.Id, o.Total, i.Id AS ItemId, i.ProductName, i.Quantity, i.Price
      FROM Orders o
      JOIN OrderItems i ON i.OrderId = o.Id
      WHERE o.Id = @Id",
    (order, item) =>
    {
        order.Items ??= new List<OrderItem>();
        order.Items.Add(item);
        return order;
    },
    new { Id = orderId },
    splitOn: "ItemId");
```

### Stored Procedures

```csharp
// Execute with parameters
var affected = await connection.ExecuteAsync(
    "sp_UpdateOrderStatus",
    new { OrderId = orderId, NewStatus = (int)OrderStatus.Shipped },
    commandType: CommandType.StoredProcedure);

// Query from stored procedure
var reports = await connection.QueryAsync<SalesReport>(
    "sp_GetSalesByRegion",
    new { Year = 2026, Region = "APAC" },
    commandType: CommandType.StoredProcedure);
```

### Batch Inserts

```csharp
// Simple batch — one row per parameter object
await connection.ExecuteAsync(
    @"INSERT INTO Orders (CustomerId, Total, Status, CreatedAt)
      VALUES (@CustomerId, @Total, @Status, @CreatedAt)",
    orders.Select(o => new
    {
        o.CustomerId,
        o.Total,
        o.Status = (int)o.Status,
        o.CreatedAt = clock.GetUtcNow()
    }));

// With column list order control (for large batches)
await connection.ExecuteAsync(
    @"INSERT INTO Orders (CustomerId, Total, Status, CreatedAt)
      VALUES (@CustomerId, @Total, @Status, @CreatedAt)",
    orders, // Dapper handles IEnumerable<T> automatically
    transaction: transaction);
```

### Transactions

```csharp
using var transaction = connection.BeginTransaction();
try
{
    await connection.ExecuteAsync(
        "INSERT INTO Orders (CustomerId, Total) VALUES (@CustomerId, @Total)",
        new { CustomerId = customerId, Total = 99.99m },
        transaction: transaction);

    await connection.ExecuteAsync(
        "UPDATE Inventory SET Quantity = Quantity - @Qty WHERE ProductId = @ProductId",
        new { Qty = 1, ProductId = productId },
        transaction: transaction);

    transaction.Commit();
}
catch
{
    transaction.Rollback();
    throw;
}
```

### TryQuery for Optional Results

```csharp
// Returns false instead of null — avoids exceptions for missing rows
bool found = await connection.TryQueryAsync<Customer>(
    "SELECT * FROM Customers WHERE Email = @Email",
    new { Email = email },
    out var customer);

if (!found) return NotFound();
```

## Anti-patterns

### Don't Use Dapper for Everything

```csharp
// BAD — using Dapper for complex relationships EF Core handles well
var order = await conn.QueryFirstOrDefaultAsync<Order>(
    "SELECT * FROM Orders o JOIN Customers c ON o.CustomerId = c.Id " +
    "JOIN OrderItems oi ON oi.OrderId = o.Id JOIN Products p ON p.Id = oi.ProductId");
// What table does the result map to? Unclear.

// GOOD — use EF Core for complex graph queries, Dapper for scalar/bulk ops
```

### Don't Concatenate SQL

```csharp
// BAD — SQL injection risk
$"SELECT * FROM Orders WHERE Status = '{status}'";

// GOOD — always use parameters
await connection.QueryAsync<Order>(
    "SELECT * FROM Orders WHERE Status = @Status",
    new { Status = status });
```

### Don't Forget `CommandTimeout`

```csharp
// BAD — long-running queries timeout at default (30s) and fail silently
await connection.QueryAsync<Order>(longRunningSql);

// GOOD — set explicit timeout for known long operations
await connection.QueryAsync<Order>(
    longRunningSql,
    commandTimeout: 120);
```

### Don't Map to `object` Without Type Info

```csharp
// BAD — Dapper infers `Dictionary<string, object>` for anonymous selects
var rows = await connection.Query("SELECT * FROM Orders");

// GOOD — always provide a target type
var orders = await connection.Query<Order>("SELECT * FROM Orders");
```

## Decision Guide

| Scenario | Recommendation |
|----------|---------------|
| Simple scalar query (`SELECT COUNT`) | `connection.QueryFirstOrDefault<int>()` |
| Single entity by ID | `connection.QueryFirstOrDefaultAsync<T>()` |
| Projection to DTO (not full entity) | `connection.QueryAsync<Dto>()` |
| Parent-child single query | Multi-mapping with `splitOn` |
| Bulk insert (100+ rows) | `connection.ExecuteAsync()` with `IEnumerable<T>` |
| Stored procedure call | `CommandType.StoredProcedure` |
| Complex graph with navigation | EF Core (not Dapper) |
| Ad-hoc reporting with dynamic columns | `Query<T>()` with explicit DTO |
| Micro-optimization on hot path | Dapper (it's the fastest ORM) |