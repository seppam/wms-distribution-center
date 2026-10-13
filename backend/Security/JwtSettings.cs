using System.Security.Cryptography;

namespace WmsDistributionCenter.Api.Security;

/// <summary>Validated JWT configuration. Construction fails fast when the configuration is unsafe.</summary>
public sealed record JwtSettings(string Key, string Issuer, string Audience, int ExpiryMinutes)
{
    private static readonly string[] PlaceholderMarkers =
        ["change-me", "changeme", "replace-me", "replace", "dev-only", "insecure", "your-secret", "example", "placeholder"];

    public static JwtSettings Load(IConfiguration config, IHostEnvironment env, ILogger? logger = null)
    {
        var key = config["Jwt:Key"];
        var issuer = config["Jwt:Issuer"];
        var audience = config["Jwt:Audience"];
        var expiryRaw = config["Jwt:ExpiryMinutes"] ?? "480";

        if (string.IsNullOrWhiteSpace(key))
        {
            if (!env.IsDevelopment() && !config.GetValue<bool>("Demo:Enabled"))
                throw new InvalidOperationException(
                    "Jwt:Key is not configured. Set the Jwt__Key environment variable to a random value of at least 32 characters.");

            // Development and public demo mode only: an ephemeral key per process, so no signing key ever lives in the repository.
            key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
            logger?.LogWarning("Jwt:Key not set: generated an ephemeral signing key. Tokens become invalid when the API restarts.");
        }
        else if (!env.IsDevelopment())
        {
            if (key.Length < 32)
                throw new InvalidOperationException("Jwt:Key must be at least 32 characters.");
            if (key.Distinct().Count() < 10)
                throw new InvalidOperationException("Jwt:Key has too little entropy; use a long random value.");
            var lowered = key.ToLowerInvariant();
            if (PlaceholderMarkers.Any(lowered.Contains))
                throw new InvalidOperationException("Jwt:Key looks like a placeholder value; set a real random secret.");
        }
        else if (key.Length < 32)
        {
            throw new InvalidOperationException("Jwt:Key must be at least 32 characters.");
        }

        if (string.IsNullOrWhiteSpace(issuer))
            throw new InvalidOperationException("Jwt:Issuer is not configured.");
        if (string.IsNullOrWhiteSpace(audience))
            throw new InvalidOperationException("Jwt:Audience is not configured.");
        if (!int.TryParse(expiryRaw, out var expiry) || expiry is < 1 or > 1440)
            throw new InvalidOperationException("Jwt:ExpiryMinutes must be a number between 1 and 1440.");

        return new JwtSettings(key, issuer, audience, expiry);
    }
}
