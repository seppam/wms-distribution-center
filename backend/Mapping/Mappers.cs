using WmsDistributionCenter.Api.DTOs.Carrier;
using WmsDistributionCenter.Api.DTOs.Category;
using WmsDistributionCenter.Api.DTOs.InboundReceipt;
using WmsDistributionCenter.Api.DTOs.Inventory;
using WmsDistributionCenter.Api.DTOs.Location;
using WmsDistributionCenter.Api.DTOs.OutboundOrder;
using WmsDistributionCenter.Api.DTOs.Product;
using WmsDistributionCenter.Api.DTOs.Role;
using WmsDistributionCenter.Api.DTOs.Supplier;
using WmsDistributionCenter.Api.DTOs.Transfer;
using WmsDistributionCenter.Api.DTOs.Uom;
using WmsDistributionCenter.Api.DTOs.User;
using WmsDistributionCenter.Api.DTOs.Warehouse;
using WmsDistributionCenter.Api.DTOs.Zone;
using WmsDistributionCenter.Api.Models;

namespace WmsDistributionCenter.Api.Mapping;

/// <summary>
/// Explicit entity-to-DTO mapping. Navigation properties must be loaded (Include) by the caller;
/// missing navigations map to null/empty instead of throwing.
/// </summary>
public static class Mappers
{
    public static RoleResponseDto ToDto(this Role r) =>
        new(r.Id, r.Name, r.Description, r.Permissions?.RootElement.GetRawText(), r.IsSystem, r.CreatedAt);

    public static UserResponseDto ToDto(this User u) =>
        new(u.Id, u.Username, u.Email, u.FullName, u.RoleId, u.Role?.Name ?? string.Empty,
            u.WarehouseId, u.Warehouse?.Name, u.Phone, u.IsActive, u.LastLogin, u.CreatedAt);

    public static WarehouseResponseDto ToDto(this Warehouse w) =>
        new(w.Id, w.Code, w.Name, w.Address, w.City, w.Capacity, w.IsActive, w.CreatedAt);

    public static ZoneResponseDto ToDto(this Zone z) =>
        new(z.Id, z.WarehouseId, z.Warehouse?.Name ?? string.Empty, z.Name, z.ZoneType, z.Description, z.IsActive);

    public static LocationResponseDto ToDto(this Location l) =>
        new(l.Id, l.ZoneId, l.Zone?.Name ?? string.Empty, l.Zone?.Warehouse?.Name ?? string.Empty,
            l.LocationCode, l.Aisle, l.Rack, l.Level, l.Bin, l.LocationType, l.MaxCapacity, l.Status);

    public static CategoryResponseDto ToDto(this Category c) =>
        new(c.Id, c.Name, c.ParentId, c.Parent?.Name, c.Level, c.Description);

    public static UomResponseDto ToDto(this Uom u) =>
        new(u.Id, u.Code, u.Name, u.BaseUomId, u.BaseUom?.Name, u.ConversionFactor, u.IsBase);

    public static ProductResponseDto ToDto(this Product p) =>
        new(p.Id, p.Sku, p.Name, p.CategoryId, p.Category?.Name, p.UomId, p.Uom?.Name ?? string.Empty,
            p.WeightKg, p.VolumeM3, p.Barcode, p.MinStock, p.MaxStock, p.IsActive);

    public static SupplierResponseDto ToDto(this Supplier s) =>
        new(s.Id, s.Code, s.Name, s.ContactPerson, s.Email, s.Phone, s.Address, s.IsActive);

    public static CarrierResponseDto ToDto(this Carrier c) =>
        new(c.Id, c.Name, c.ContactPerson, c.Phone, c.Email, c.VehicleType, c.IsActive);

    public static InventoryResponseDto ToDto(this Inventory i) =>
        new(i.Id, i.ProductId, i.Product?.Sku ?? string.Empty, i.Product?.Name ?? string.Empty,
            i.LocationId, i.Location?.LocationCode ?? string.Empty, i.Location?.Zone?.Name ?? string.Empty,
            i.QtyOnHand, i.QtyReserved, i.LotNumber, i.ExpiryDate, i.UnitCost, i.Status, i.LastCountedDate);

    public static InboundReceiptResponseDto ToDto(this InboundReceipt r) =>
        new(r.Id, r.ReceiptNo, r.SupplierId, r.Supplier?.Name ?? string.Empty, r.WarehouseId, r.Warehouse?.Name ?? string.Empty,
            r.PoNumber, r.AsnNumber, r.Status, r.ExpectedDate, r.ReceivedDate, r.ReceivedBy, r.Receiver?.FullName,
            r.DockDoor, r.Notes, r.CreatedAt);

    public static ReceiptItemResponseDto ToDto(this ReceiptItem i) =>
        new(i.Id, i.ReceiptId, i.ProductId, i.Product?.Name ?? string.Empty, i.Product?.Sku ?? string.Empty,
            i.ExpectedQty, i.ReceivedQty, i.DamagedQty, i.LotNumber, i.ExpiryDate, i.UnitCost, i.Status,
            i.PutawayLocationId, i.PutawayLocation?.LocationCode);

    public static OutboundOrderResponseDto ToDto(this OutboundOrder o) =>
        new(o.Id, o.OrderNumber, o.WarehouseId, o.Warehouse?.Name ?? string.Empty, o.OrderType, o.Status, o.Priority,
            o.Destination, o.RequestedDate, o.DueDate, o.CreatedBy, o.Creator?.FullName ?? string.Empty, o.Notes, o.CreatedAt);

    public static OrderItemResponseDto ToDto(this OrderItem i) =>
        new(i.Id, i.OrderId, i.ProductId, i.Product?.Name ?? string.Empty, i.Product?.Sku ?? string.Empty,
            i.OrderedQty, i.PickedQty, i.ShippedQty, i.UnitPrice, i.Status);

    public static ShipmentResponseDto ToDto(this Shipment s) =>
        new(s.Id, s.OrderId, s.Order?.OrderNumber ?? string.Empty, s.CarrierId, s.Carrier?.Name ?? string.Empty,
            s.TrackingNo, s.BolNumber, s.VehiclePlate, s.DriverName, s.Status, s.ShippedAt, s.DeliveredAt);

    public static TransferResponseDto ToDto(this Transfer t) =>
        new(t.Id, t.TransferNo, t.ProductId, t.Product?.Name ?? string.Empty, t.Product?.Sku ?? string.Empty,
            t.FromLocationId, t.FromLocation?.LocationCode ?? string.Empty, t.ToLocationId, t.ToLocation?.LocationCode ?? string.Empty,
            t.Qty, t.Reason, t.RequestedBy, t.Requester?.FullName ?? string.Empty, t.Status, t.TransferredAt);

    public static AdjustmentResponseDto ToDto(this Adjustment a) =>
        new(a.Id, a.AdjNumber, a.ProductId, a.Product?.Name ?? string.Empty, a.LocationId, a.Location?.LocationCode ?? string.Empty,
            a.AdjType, a.QtyBefore, a.AdjQty, a.QtyAfter, a.Reason, a.CreatedBy, a.Creator?.FullName ?? string.Empty,
            a.ApprovedBy, a.Approver?.FullName, a.Status, a.CreatedAt);
}
