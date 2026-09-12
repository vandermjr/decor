using System.ComponentModel.DataAnnotations;
using Decor.Core.Entities;

namespace Decor.Core.DTOs;

public record StockReservationDTO(
    [property: Display(Name = "ID da Reserva")] int ReservationID,
    [property: Display(Name = "ID do Item do Pedido")] int OrderItemID,
    [property: Display(Name = "ID do Produto")] int ProductID,
    [property: Display(Name = "ID do Depósito")] int StockLocationID,
    [property: Display(Name = "Quantidade")] decimal Quantity,
    [property: Display(Name = "Status")] StockReservationStatus Status,
    [property: Display(Name = "Criado pelo Funcionário")] int CreatedByEmployeeID,
    [property: Display(Name = "Data de Criação")] DateTime CreatedAt,
    [property: Display(Name = "Data de Liberação")] DateTime? ReleasedAt
);
