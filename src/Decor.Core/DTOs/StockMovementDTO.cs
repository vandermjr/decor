using Decor.Core.Entities;

namespace Decor.Core.DTOs;
public record StockMovementDTO(int StockMovementID,
                               int ProductID,
                               int StockLocationID,
                               decimal Quantity,
                               StockMovementType MovementType,
                               Guid? TransferID,
                               StockAdjustmentReason? Reason,
                               string? Justification,
                               int? AuthorizedByEmployeeID,
                               int PerformedByEmployeeID,
                               StockMovementReviewStatus? ReviewStatus,
                               int? ReviewedByEmployeeID,
                               DateTime? ReviewedAt,
                               DateTime MovementDate,
                               string? Notes,
                               int? OrderItemID = null);