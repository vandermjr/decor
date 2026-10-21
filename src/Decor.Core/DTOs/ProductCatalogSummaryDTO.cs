namespace Decor.Core.DTOs;

public sealed record ProductCatalogSummaryDTO(
    long TotalProducts,
    long ActiveProducts,
    long OutOfStockProducts,
    long LowStockProducts);