using System.ComponentModel.DataAnnotations;
using WmsDistributionCenter.Api.Enums;

namespace WmsDistributionCenter.Api.DTOs.Transfer;

public record CreateTransferDto(
    [Required] Guid ProductId,
    [Required] Guid FromLocationId,
    [Required] Guid ToLocationId,
    [Required] decimal Qty,
    string? Reason
);

public record TransferResponseDto(
    Guid Id,
    string TransferNo,
    Guid ProductId,
    string ProductName,
    string ProductSku,
    Guid FromLocationId,
    string FromLocationCode,
    Guid ToLocationId,
    string ToLocationCode,
    decimal Qty,
    string? Reason,
    Guid RequestedBy,
    string RequesterName,
    TransferStatus Status,
    DateTime? TransferredAt
);
