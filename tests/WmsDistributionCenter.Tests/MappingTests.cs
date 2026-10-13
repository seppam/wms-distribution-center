using System.Text.Json;
using WmsDistributionCenter.Api.Enums;
using WmsDistributionCenter.Api.Mapping;
using WmsDistributionCenter.Api.Models;

namespace WmsDistributionCenter.Tests;

public class MappingTests
{
    [Fact]
    public void Role_MapsPermissionsAsRawJson()
    {
        var role = new Role { Id = Guid.NewGuid(), Name = "Custom", Permissions = JsonDocument.Parse("{\"inventory\":[\"read\"]}") };
        Assert.Equal("{\"inventory\":[\"read\"]}", role.ToDto().Permissions);
    }

    [Fact]
    public void Entities_WithoutLoadedNavigations_MapWithoutThrowing()
    {
        var product = new Product { Id = Guid.NewGuid(), Sku = "SKU-1", Name = "P" };
        var dto = product.ToDto();
        Assert.Equal("SKU-1", dto.Sku);
        Assert.Null(dto.CategoryName);
        Assert.Equal(string.Empty, dto.UomName);
    }

    [Fact]
    public void OrderItem_MapsQuantitiesAndProductInfo()
    {
        var product = new Product { Id = Guid.NewGuid(), Sku = "SKU-9", Name = "Widget" };
        var item = new OrderItem { Id = Guid.NewGuid(), OrderId = Guid.NewGuid(), ProductId = product.Id, Product = product, OrderedQty = 10, PickedQty = 8, ShippedQty = 0, UnitPrice = 2.5m, Status = OrderItemStatus.Partial };
        var dto = item.ToDto();
        Assert.Equal(("Widget", "SKU-9", 10m, 8m), (dto.ProductName, dto.ProductSku, dto.OrderedQty, dto.PickedQty));
        Assert.Equal(OrderItemStatus.Partial, dto.Status);
    }

    [Fact]
    public void Inventory_MapsLocationAndZone()
    {
        var zone = new Zone { Name = "Ambient" };
        var inv = new Inventory
        {
            Id = Guid.NewGuid(), QtyOnHand = 12, QtyReserved = 2,
            Product = new Product { Sku = "S", Name = "N" },
            Location = new Location { LocationCode = "A-01-01", Zone = zone }
        };
        var dto = inv.ToDto();
        Assert.Equal(("A-01-01", "Ambient", 12m, 2m), (dto.LocationCode, dto.ZoneName, dto.QtyOnHand, dto.QtyReserved));
    }
}
