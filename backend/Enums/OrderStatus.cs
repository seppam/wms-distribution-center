namespace WmsDistributionCenter.Api.Enums;

public enum OrderStatus
{
    Draft = 1,
    Picked = 2,
    Packed = 3,
    Shipped = 4,
    Completed = 5,
    Cancelled = 6,
    /// <summary>Stock allocated, pick tasks open.</summary>
    Picking = 7
}
