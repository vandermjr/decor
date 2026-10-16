namespace Decor.Core.DTOs;

public sealed record ProductCatalogSummaryDTO(
    int TotalProducts,
    int ActiveProducts,
    int OutOfStockProducts,
    int LowStockProducts);