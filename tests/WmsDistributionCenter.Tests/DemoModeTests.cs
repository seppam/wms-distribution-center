using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WmsDistributionCenter.Api.Data;
using WmsDistributionCenter.Api.Enums;

namespace WmsDistributionCenter.Tests;

/// <summary>Public demo mode: off by default, inert outside demo mode, seeded states, one account per role, safe reset.</summary>
public class DemoModeTests
{
    private static async Task<JsonElement> InfoAsync(ApiFactory f)
    {
        var res = await f.CreateClient().GetAsync("/api/demo-info");
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<JsonElement>();
    }

    // ---------------------------------------------------------------- off by default

    [Theory]
    [InlineData("Testing")]
    [InlineData("Production")]
    [InlineData("Development")]
    public async Task DemoInfo_IsDisabled_UnlessExplicitlyEnabled(string environment)
    {
        using var f = new ApiFactory(environment: environment);
        var info = await InfoAsync(f);
        Assert.False(info.GetProperty("enabled").GetBoolean());
        Assert.False(info.TryGetProperty("accounts", out _));
    }

    [Fact]
    public async Task DemoMode_Off_CreatesNoDemoAccountsOrData()
    {
        using var f = new ApiFactory(environment: "Production");
        _ = f.CreateClient();
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WmsDbContext>();
        Assert.Empty(await db.Users.ToListAsync());
        Assert.Empty(await db.Warehouses.ToListAsync());
    }

    [Fact]
    public async Task Reset_IsNotFound_WhenDemoModeIsOff_AndChangesNothing()
    {
        using var f = await ApiFactory.CreateSeededAsync();
        var admin = await f.ClientAsAsync("admin");
        var before = await CountAsync(f, d => d.Products.CountAsync());
        var res = await admin.PostAsync("/api/demo/reset", null);
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        Assert.Equal(before, await CountAsync(f, d => d.Products.CountAsync()));
    }

    [Fact]
    public void DemoMode_RefusesToStart_WithoutAPassword()
    {
        using var f = new ApiFactory(demo: true, demoPassword: null);
        Assert.Throws<InvalidOperationException>(() => f.Services);
    }

    // ---------------------------------------------------------------- on

    [Fact]
    public async Task DemoInfo_ReportsRoles_ButNeverAPassword()
    {
        using var f = ApiFactory.CreateDemo();
        var res = await f.CreateClient().GetAsync("/api/demo-info");
        var raw = await res.Content.ReadAsStringAsync();
        var info = JsonDocument.Parse(raw).RootElement;
        Assert.True(info.GetProperty("enabled").GetBoolean());
        var roles = info.GetProperty("accounts").EnumerateArray().Select(a => a.GetProperty("role").GetString()!).ToArray();
        Assert.Equal(["Admin", "Supervisor", "Operator", "Viewer"], roles);
        Assert.DoesNotContain(ApiFactory.DemoPassword, raw);
        Assert.DoesNotContain("assword", raw);
    }

    [Fact]
    public async Task EveryRole_CanSignIn_WithItsOwnPermissions()
    {
        using var f = ApiFactory.CreateDemo();
        foreach (var (user, role) in new[] { ("admin", "Admin"), ("supervisor", "Supervisor"), ("operator", "Operator"), ("viewer", "Viewer") })
        {
            var login = await f.CreateClient().PostAsJsonAsync("/api/auth/login", new { username = user, password = ApiFactory.DemoPassword });
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
            var me = await (await f.ClientAsAsync(user)).GetFromJsonAsync<JsonElement>("/api/auth/me");
            Assert.Equal(role, me.GetProperty("role").GetString()!);
        }
        var wrong = await f.CreateClient().PostAsJsonAsync("/api/auth/login", new { username = "admin", password = "not-the-password" });
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);

        var viewer = await f.ClientAsAsync("viewer");
        var op = await f.ClientAsAsync("operator");
        var sup = await f.ClientAsAsync("supervisor");
        var admin = await f.ClientAsAsync("admin");
        Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync("/api/outboundorders")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsJsonAsync("/api/outboundorders", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await op.PostAsJsonAsync("/api/products", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await sup.GetAsync("/api/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await sup.GetAsync("/api/roles")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/roles")).StatusCode);
    }

    [Fact]
    public async Task SeededDataset_CoversEveryWorkflowState()
    {
        using var f = ApiFactory.CreateDemo();
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WmsDbContext>();

        var orders = await db.OutboundOrders.Select(o => o.Status).Distinct().ToListAsync();
        foreach (var s in new[] { OrderStatus.Draft, OrderStatus.Picking, OrderStatus.Picked, OrderStatus.Packed, OrderStatus.Shipped, OrderStatus.Completed, OrderStatus.Cancelled })
            Assert.Contains(s, orders);
        var tasks = await db.PickTasks.Select(t => t.Status).Distinct().ToListAsync();
        Assert.True(tasks.Count >= 2, "expected open and confirmed pick tasks");

        var receipts = await db.InboundReceipts.Select(r => r.Status).Distinct().ToListAsync();
        foreach (var s in new[] { InboundStatus.Draft, InboundStatus.InProgress, InboundStatus.Partial, InboundStatus.Received })
            Assert.Contains(s, receipts);

        var shipments = await db.Shipments.Select(s => s.Status).Distinct().ToListAsync();
        Assert.Contains(ShipmentStatus.Dispatched, shipments);
        Assert.Contains(ShipmentStatus.Delivered, shipments);

        var adj = await db.Adjustments.Select(a => a.Status).Distinct().ToListAsync();
        Assert.Contains(AdjStatus.Draft, adj);
        Assert.Contains(AdjStatus.Approved, adj);
        Assert.Contains(AdjStatus.Cancelled, adj);
        var transfers = await db.Transfers.Select(t => t.Status).Distinct().ToListAsync();
        foreach (var s in new[] { TransferStatus.Draft, TransferStatus.Completed, TransferStatus.Cancelled })
            Assert.Contains(s, transfers);
        var returns = await db.ReturnOrders.Select(r => r.Status).Distinct().ToListAsync();
        Assert.Contains(ReturnStatus.Received, returns);
        Assert.Contains(ReturnStatus.Completed, returns);
        Assert.Contains(Disposition.Restock, await db.ReturnOrders.Where(r => r.Disposition != null).Select(r => r.Disposition!.Value).ToListAsync());

        Assert.True(await db.Products.CountAsync() >= 20);
        Assert.True(await db.Uoms.CountAsync() >= 3);
        Assert.True(await db.Suppliers.CountAsync() >= 5);
        Assert.True(await db.Carriers.CountAsync() >= 3);
        Assert.True(await db.Warehouses.CountAsync() >= 2);
        Assert.True(await db.Zones.CountAsync() >= 5);
        Assert.True(await db.Locations.CountAsync() >= 30);
        Assert.True(await db.InventoryTransactions.CountAsync() > 30);

        // stock bookkeeping stays consistent
        Assert.DoesNotContain(await db.Inventories.ToListAsync(), i => i.QtyOnHand < 0 || i.QtyReserved < 0 || i.QtyReserved > i.QtyOnHand);
        var reserved = await db.Inventories.SumAsync(i => i.QtyReserved);
        var openPicks = await db.PickTasks.Where(t => t.Status != PickTaskStatus.Picked).SumAsync(t => t.QtyToPick - t.QtyPicked);
        Assert.True(reserved >= openPicks);

        var dash = await (await f.ClientAsAsync("viewer")).GetAsync("/api/reports/dashboard");
        Assert.Equal(HttpStatusCode.OK, dash.StatusCode);
    }

    // ---------------------------------------------------------------- reset

    [Fact]
    public async Task Reset_IsAdminOnly()
    {
        using var f = ApiFactory.CreateDemo();
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.CreateClient().PostAsync("/api/demo/reset", null)).StatusCode);
        foreach (var user in new[] { "viewer", "operator", "supervisor" })
            Assert.Equal(HttpStatusCode.Forbidden, (await (await f.ClientAsAsync(user)).PostAsync("/api/demo/reset", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await (await f.ClientAsAsync("admin")).PostAsync("/api/demo/reset", null)).StatusCode);
    }

    [Fact]
    public async Task Reset_RestoresTheDataset_AndTheAccounts_AndKeepsSessionsValid()
    {
        using var f = ApiFactory.CreateDemo();
        var admin = await f.ClientAsAsync("admin");
        var orders = await CountAsync(f, d => d.OutboundOrders.CountAsync());
        var products = await CountAsync(f, d => d.Products.CountAsync());
        var viewerId = (await ReadAsync(f, d => d.Users.Where(u => u.Username == "viewer").Select(u => u.Id).FirstAsync()));

        // a visitor experiments: wipes orders, adds junk, breaks the viewer account
        using (var scope = f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WmsDbContext>();
            db.PickTasks.RemoveRange(db.PickTasks);
            db.OrderItems.RemoveRange(db.OrderItems);
            db.OutboundOrders.RemoveRange(db.OutboundOrders);
            db.Suppliers.Add(new() { Id = Guid.NewGuid(), Code = "JUNK", Name = "Visitor junk" });
            var viewer = await db.Users.FirstAsync(u => u.Username == "viewer");
            viewer.PasswordHash = BCrypt.Net.BCrypt.HashPassword("changed-by-visitor", 4);
            viewer.IsActive = false;
            db.Users.Add(new() { Id = Guid.NewGuid(), Username = "visitor", Email = "v@example.com", FullName = "V", PasswordHash = "x", RoleId = viewer.RoleId, IsActive = true });
            await db.SaveChangesAsync();
        }
        Assert.Equal(0, await CountAsync(f, d => d.OutboundOrders.CountAsync()));

        var res = await admin.PostAsync("/api/demo/reset", null);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        Assert.Equal(orders, await CountAsync(f, d => d.OutboundOrders.CountAsync()));
        Assert.Equal(products, await CountAsync(f, d => d.Products.CountAsync()));
        Assert.Equal(0, await CountAsync(f, d => d.Suppliers.CountAsync(s => s.Code == "JUNK")));
        Assert.Equal(0, await CountAsync(f, d => d.Users.CountAsync(u => u.Username == "visitor")));
        Assert.Equal(4, await CountAsync(f, d => d.Users.CountAsync()));
        Assert.Equal(viewerId, await ReadAsync(f, d => d.Users.Where(u => u.Username == "viewer").Select(u => u.Id).FirstAsync()));

        // the admin's existing token still works, and the repaired viewer account can sign in again
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await f.CreateClient().PostAsJsonAsync("/api/auth/login", new { username = "viewer", password = ApiFactory.DemoPassword })).StatusCode);
    }

    [Fact]
    public async Task Reset_IsRateLimited()
    {
        using var f = ApiFactory.CreateDemo(resetPermits: 2);
        var admin = await f.ClientAsAsync("admin");
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync("/api/demo/reset", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync("/api/demo/reset", null)).StatusCode);
        var third = await admin.PostAsync("/api/demo/reset", null);
        Assert.Equal(HttpStatusCode.TooManyRequests, third.StatusCode);
        Assert.True(third.Headers.Contains("Retry-After"));
    }

    private static async Task<int> CountAsync(ApiFactory f, Func<WmsDbContext, Task<int>> q)
    {
        using var scope = f.Services.CreateScope();
        return await q(scope.ServiceProvider.GetRequiredService<WmsDbContext>());
    }

    private static async Task<T> ReadAsync<T>(ApiFactory f, Func<WmsDbContext, Task<T>> q)
    {
        using var scope = f.Services.CreateScope();
        return await q(scope.ServiceProvider.GetRequiredService<WmsDbContext>());
    }
}
