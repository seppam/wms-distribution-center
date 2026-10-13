using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace WmsDistributionCenter.Tests;

/// <summary>
/// Verifies the permission matrix documented in the README against the running pipeline:
/// every controller endpoint must be listed here and must answer 401 / 403 / allowed exactly as specified.
/// </summary>
public class AuthorizationMatrixTests : IAsyncLifetime
{
    private ApiFactory _factory = null!;
    private readonly Dictionary<string, HttpClient> _clients = new();

    private static readonly string[] Principals = ["anonymous", "viewer", "operator", "supervisor", "admin"];

    private static readonly Dictionary<string, string[]> Allowed = new()
    {
        ["anon"] = ["anonymous", "viewer", "operator", "supervisor", "admin"],
        ["read"] = ["viewer", "operator", "supervisor", "admin"],
        ["operate"] = ["operator", "supervisor", "admin"],
        ["manage"] = ["supervisor", "admin"],
        ["admin"] = ["admin"],
    };

    /// <summary>"METHOD route" -> policy. Routes are lower-case with constraints stripped.</summary>
    public static readonly Dictionary<string, string> Matrix = BuildMatrix();

    private static Dictionary<string, string> BuildMatrix()
    {
        var m = new Dictionary<string, string>
        {
            ["POST api/auth/login"] = "anon",
            ["POST api/auth/register"] = "anon",
            ["GET api/auth/me"] = "read",
            ["GET api/demo-info"] = "anon", ["POST api/demo/reset"] = "admin",

            ["GET api/users"] = "manage", ["GET api/users/{id}"] = "manage",
            ["POST api/users"] = "admin", ["PUT api/users/{id}"] = "admin", ["DELETE api/users/{id}"] = "admin",
            ["GET api/roles"] = "admin", ["GET api/roles/{id}"] = "admin",
            ["POST api/roles"] = "admin", ["PUT api/roles/{id}"] = "admin", ["DELETE api/roles/{id}"] = "admin",

            ["GET api/products/search"] = "read",
            ["GET api/categories/tree"] = "read",

            ["GET api/inboundreceipts"] = "read", ["GET api/inboundreceipts/{id}"] = "read",
            ["POST api/inboundreceipts"] = "manage", ["PUT api/inboundreceipts/{id}"] = "manage",
            ["DELETE api/inboundreceipts/{id}"] = "manage", ["POST api/inboundreceipts/{id}/items"] = "manage",
            ["POST api/inboundreceipts/{id}/receive"] = "operate", ["POST api/inboundreceipts/{id}/complete"] = "operate",

            ["GET api/outboundorders"] = "read", ["GET api/outboundorders/{id}"] = "read", ["GET api/outboundorders/{id}/items"] = "read",
            ["POST api/outboundorders"] = "manage", ["PUT api/outboundorders/{id}"] = "manage",
            ["DELETE api/outboundorders/{id}"] = "manage", ["POST api/outboundorders/{id}/items"] = "manage",
            ["POST api/outboundorders/{id}/start-picking"] = "manage", ["POST api/outboundorders/{id}/assign-picker"] = "manage",
            ["POST api/outboundorders/{id}/pack"] = "operate", ["POST api/outboundorders/{id}/ship"] = "operate",
            ["POST api/outboundorders/{id}/cancel"] = "manage",
            ["GET api/pick-tasks"] = "read", ["POST api/pick-tasks/{id}/confirm"] = "operate",
            ["GET api/shipments"] = "read", ["POST api/shipments/{id}/deliver"] = "operate",
            ["GET api/transfers"] = "read", ["GET api/transfers/{id}"] = "read",
            ["POST api/transfers"] = "operate", ["POST api/transfers/{id}/complete"] = "operate", ["POST api/transfers/{id}/cancel"] = "operate",
            ["GET api/returns"] = "read", ["GET api/returns/{id}"] = "read",
            ["POST api/returns"] = "operate", ["POST api/returns/{id}/process"] = "manage",

            ["GET api/inventory"] = "read", ["GET api/inventory/{id}"] = "read", ["GET api/inventory/adjustments"] = "read",
            ["POST api/inventory/adjustments"] = "operate", ["POST api/inventory/adjustments/{id}/approve"] = "manage",
            ["POST api/inventory/adjustments/{id}/reject"] = "manage", ["GET api/inventory/transactions"] = "read",

            ["GET api/reports/dashboard"] = "read", ["GET api/reports/inventory-summary"] = "read",
            ["GET api/reports/inbound-summary"] = "read", ["GET api/reports/outbound-summary"] = "read",
        };
        // Master data: read for everyone, write for Admin/Supervisor, delete for Admin
        foreach (var r in new[] { "products", "categories", "suppliers", "carriers", "warehouses", "zones", "locations", "uoms" })
        {
            m[$"GET api/{r}"] = "read"; m[$"GET api/{r}/{{id}}"] = "read";
            m[$"POST api/{r}"] = "manage"; m[$"PUT api/{r}/{{id}}"] = "manage"; m[$"DELETE api/{r}/{{id}}"] = "admin";
        }
        return m;
    }

    public async Task InitializeAsync()
    {
        _factory = await ApiFactory.CreateSeededAsync();
        foreach (var p in Principals)
            _clients[p] = await _factory.ClientAsAsync(p == "anonymous" ? null : p);
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    private static string Normalize(string pattern) =>
        Regex.Replace(pattern, @"\{(\w+)(:[^}]+)?\}", "{$1}").ToLowerInvariant();

    private IEnumerable<string> DiscoverEndpoints()
    {
        var sources = _factory.Services.GetServices<EndpointDataSource>();
        foreach (var e in sources.SelectMany(s => s.Endpoints).OfType<RouteEndpoint>())
        {
            if (e.Metadata.GetMetadata<ControllerActionDescriptor>() is null) continue;
            var methods = e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [];
            foreach (var method in methods)
                yield return $"{method} {Normalize(e.RoutePattern.RawText!)}";
        }
    }

    [Fact]
    public void EveryEndpointIsListedInTheMatrix_AndViceVersa()
    {
        var discovered = DiscoverEndpoints().ToHashSet();
        Assert.Empty(discovered.Except(Matrix.Keys));
        Assert.Empty(Matrix.Keys.Except(discovered));
    }

    [Fact]
    public async Task Matrix_IsEnforced_ForEveryRole()
    {
        var failures = new List<string>();
        foreach (var (key, policy) in Matrix)
        {
            var parts = key.Split(' ', 2);
            var path = "/" + Regex.Replace(parts[1], @"\{\w+\}", _ => Guid.NewGuid().ToString());
            foreach (var principal in Principals)
            {
                using var req = new HttpRequestMessage(new HttpMethod(parts[0]), path);
                if (parts[0] is "POST" or "PUT") req.Content = JsonContent.Create(new { });
                var res = await _clients[principal].SendAsync(req);
                var allowed = Allowed[policy].Contains(principal);
                var ok = allowed
                    ? res.StatusCode is not (HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden) && (int)res.StatusCode < 500
                    : res.StatusCode == (principal == "anonymous" ? HttpStatusCode.Unauthorized : HttpStatusCode.Forbidden);
                if (!ok) failures.Add($"{principal,-10} {key} [{policy}] -> {(int)res.StatusCode}");
            }
        }
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }
}
