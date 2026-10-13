using Microsoft.EntityFrameworkCore;
using WmsDistributionCenter.Api.Data;
using WmsDistributionCenter.Api.Mapping;
using WmsDistributionCenter.Api.DTOs.Product;
using WmsDistributionCenter.Api.Models;

namespace WmsDistributionCenter.Api.Services;

public class ProductService : IProductService
{
    private readonly WmsDbContext _db;

    public ProductService(WmsDbContext db)
    {
        _db = db;
    }

    public async Task<IEnumerable<ProductResponseDto>> GetAllAsync()
    {
        var products = await _db.Products
            .Include(p => p.Category)
            .Include(p => p.Uom)
            .ToListAsync();
        return products.Select(x => x.ToDto()).ToList();
    }

    public async Task<ProductResponseDto?> GetByIdAsync(Guid id)
    {
        var product = await _db.Products
            .Include(p => p.Category)
            .Include(p => p.Uom)
            .FirstOrDefaultAsync(p => p.Id == id);
        return product?.ToDto();
    }

    public async Task<ProductResponseDto> CreateAsync(CreateProductDto dto)
    {
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Sku = dto.Sku,
            Name = dto.Name,
            CategoryId = dto.CategoryId,
            UomId = dto.UomId,
            WeightKg = dto.WeightKg,
            VolumeM3 = dto.VolumeM3,
            Barcode = dto.Barcode,
            MinStock = dto.MinStock,
            MaxStock = dto.MaxStock,
            IsActive = true
        };

        _db.Products.Add(product);
        await _db.SaveChangesAsync();

        await _db.Entry(product).Reference(p => p.Category).LoadAsync();
        await _db.Entry(product).Reference(p => p.Uom).LoadAsync();

        return product.ToDto();
    }

    public async Task<ProductResponseDto?> UpdateAsync(Guid id, UpdateProductDto dto)
    {
        var product = await _db.Products.FindAsync(id);
        if (product == null) return null;

        if (dto.Name != null) product.Name = dto.Name;
        if (dto.CategoryId.HasValue) product.CategoryId = dto.CategoryId.Value;
        if (dto.UomId.HasValue) product.UomId = dto.UomId.Value;
        if (dto.WeightKg.HasValue) product.WeightKg = dto.WeightKg.Value;
        if (dto.VolumeM3.HasValue) product.VolumeM3 = dto.VolumeM3.Value;
        if (dto.Barcode != null) product.Barcode = dto.Barcode;
        if (dto.MinStock.HasValue) product.MinStock = dto.MinStock.Value;
        if (dto.MaxStock.HasValue) product.MaxStock = dto.MaxStock;
        if (dto.IsActive.HasValue) product.IsActive = dto.IsActive.Value;

        await _db.SaveChangesAsync();

        await _db.Entry(product).Reference(p => p.Category).LoadAsync();
        await _db.Entry(product).Reference(p => p.Uom).LoadAsync();

        return product.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var product = await _db.Products.FindAsync(id);
        if (product == null) return false;
        _db.Products.Remove(product);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<IEnumerable<ProductResponseDto>> SearchAsync(string? query)
    {
        var q = _db.Products
            .Include(p => p.Category)
            .Include(p => p.Uom)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query))
            q = q.Where(p => p.Name.Contains(query) || p.Sku.Contains(query) || (p.Barcode != null && p.Barcode.Contains(query)));

        return (await q.ToListAsync()).Select(x => x.ToDto()).ToList();
    }
}
