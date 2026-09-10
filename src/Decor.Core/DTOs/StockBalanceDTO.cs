namespace Decor.Core.DTOs;
public record StockBalanceDTO(int StockBalanceID,
                              int ProductID,
                              int StockLocationID,
                              decimal Quantity,
                              DateTime UpdatedAt);