using AutoMapper;
using WmsDistributionCenter.Api.Models;

namespace WmsDistributionCenter.Api.Configuration;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        // Roles
        CreateMap<Role, DTOs.Role.RoleResponseDto>();

        // Users
        CreateMap<User, DTOs.User.UserResponseDto>()
            .ForMember(d => d.RoleName, o => o.MapFrom(s => s.Role != null ? s.Role.Name : null))
            .ForMember(d => d.WarehouseName, o => o.MapFrom(s => s.Warehouse != null ? s.Warehouse.Name : null));

        // Warehouses
        CreateMap<Warehouse, DTOs.Warehouse.WarehouseResponseDto>();

        // Zones
        CreateMap<Zone, DTOs.Zone.ZoneResponseDto>()
            .ForMember(d => d.WarehouseName, o => o.MapFrom(s => s.Warehouse != null ? s.Warehouse.Name : null));

        // Locations
        CreateMap<Location, DTOs.Location.LocationResponseDto>()
            .ForCtorParam("ZoneName", o => o.MapFrom(s => s.Zone != null ? s.Zone.Name : null))
            .ForCtorParam("WarehouseName", o => o.MapFrom(s => s.Zone != null && s.Zone.Warehouse != null ? s.Zone.Warehouse.Name : null));

        // Categories
        CreateMap<Category, DTOs.Category.CategoryResponseDto>()
            .ForMember(d => d.ParentName, o => o.MapFrom(s => s.Parent != null ? s.Parent.Name : null));

        CreateMap<Category, DTOs.Category.CategoryTreeDto>()
            .ForMember(d => d.Children, o => o.MapFrom(s => s.Children));

        // Uoms
        CreateMap<Uom, DTOs.Uom.UomResponseDto>()
            .ForMember(d => d.BaseUomName, o => o.MapFrom(s => s.BaseUom != null ? s.BaseUom.Name : null));

        // Products
        CreateMap<Product, DTOs.Product.ProductResponseDto>()
            .ForMember(d => d.CategoryName, o => o.MapFrom(s => s.Category != null ? s.Category.Name : null))
            .ForMember(d => d.UomName, o => o.MapFrom(s => s.Uom != null ? s.Uom.Name : null));

        // Suppliers
        CreateMap<Supplier, DTOs.Supplier.SupplierResponseDto>();

        // Carriers
        CreateMap<Carrier, DTOs.Carrier.CarrierResponseDto>();

        // Inventory
        CreateMap<Inventory, DTOs.Inventory.InventoryResponseDto>()
            .ForCtorParam("ProductSku", o => o.MapFrom(s => s.Product != null ? s.Product.Sku : null))
            .ForCtorParam("ProductName", o => o.MapFrom(s => s.Product != null ? s.Product.Name : null))
            .ForCtorParam("LocationCode", o => o.MapFrom(s => s.Location != null ? s.Location.LocationCode : null))
            .ForCtorParam("ZoneName", o => o.MapFrom(s => s.Location != null && s.Location.Zone != null ? s.Location.Zone.Name : null));

        // InboundReceipt
        CreateMap<InboundReceipt, DTOs.InboundReceipt.InboundReceiptResponseDto>()
            .ForCtorParam("SupplierName", o => o.MapFrom(s => s.Supplier != null ? s.Supplier.Name : null))
            .ForCtorParam("WarehouseName", o => o.MapFrom(s => s.Warehouse != null ? s.Warehouse.Name : null))
            .ForCtorParam("ReceiverName", o => o.MapFrom(s => s.Receiver != null ? s.Receiver.FullName : null));

        // ReceiptItem
        CreateMap<ReceiptItem, DTOs.InboundReceipt.ReceiptItemResponseDto>()
            .ForCtorParam("ProductName", o => o.MapFrom(s => s.Product != null ? s.Product.Name : null))
            .ForCtorParam("ProductSku", o => o.MapFrom(s => s.Product != null ? s.Product.Sku : null))
            .ForCtorParam("PutawayLocationCode", o => o.MapFrom(s => s.PutawayLocation != null ? s.PutawayLocation.LocationCode : null));

        // OutboundOrder
        CreateMap<OutboundOrder, DTOs.OutboundOrder.OutboundOrderResponseDto>()
            .ForCtorParam("WarehouseName", o => o.MapFrom(s => s.Warehouse != null ? s.Warehouse.Name : null))
            .ForCtorParam("CreatorName", o => o.MapFrom(s => s.Creator != null ? s.Creator.FullName : null));

        // OrderItem
        CreateMap<OrderItem, DTOs.OutboundOrder.OrderItemResponseDto>()
            .ForCtorParam("ProductName", o => o.MapFrom(s => s.Product != null ? s.Product.Name : null))
            .ForCtorParam("ProductSku", o => o.MapFrom(s => s.Product != null ? s.Product.Sku : null));

        // Shipment
        CreateMap<Shipment, DTOs.OutboundOrder.ShipmentResponseDto>()
            .ForCtorParam("OrderNumber", o => o.MapFrom(s => s.Order != null ? s.Order.OrderNumber : null))
            .ForCtorParam("CarrierName", o => o.MapFrom(s => s.Carrier != null ? s.Carrier.Name : null));

        // Transfer
        CreateMap<Transfer, DTOs.Transfer.TransferResponseDto>()
            .ForCtorParam("ProductName", o => o.MapFrom(s => s.Product != null ? s.Product.Name : null))
            .ForCtorParam("ProductSku", o => o.MapFrom(s => s.Product != null ? s.Product.Sku : null))
            .ForCtorParam("FromLocationCode", o => o.MapFrom(s => s.FromLocation != null ? s.FromLocation.LocationCode : null))
            .ForCtorParam("ToLocationCode", o => o.MapFrom(s => s.ToLocation != null ? s.ToLocation.LocationCode : null))
            .ForCtorParam("RequesterName", o => o.MapFrom(s => s.Requester != null ? s.Requester.FullName : null));

        // Adjustment
        CreateMap<Adjustment, DTOs.Inventory.AdjustmentResponseDto>()
            .ForCtorParam("ProductName", o => o.MapFrom(s => s.Product != null ? s.Product.Name : null))
            .ForCtorParam("LocationCode", o => o.MapFrom(s => s.Location != null ? s.Location.LocationCode : null))
            .ForCtorParam("CreatorName", o => o.MapFrom(s => s.Creator != null ? s.Creator.FullName : null))
            .ForCtorParam("ApproverName", o => o.MapFrom(s => s.Approver != null ? s.Approver.FullName : null));
    }
}
