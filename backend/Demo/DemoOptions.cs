namespace WmsDistributionCenter.Api.Demo;

/// <summary>
/// Public demo mode (configuration section "Demo"). OFF unless Demo:Enabled is explicitly true.
/// When on, the API seeds one account per role with the public Demo:Password plus a fictional dataset,
/// reports itself as a demo through GET /api/demo-info and lets an Admin restore the dataset.
/// </summary>
public sealed class DemoOptions
{
    public const string Section = "Demo";

    public bool Enabled { get; set; }

    /// <summary>Shared password of the four demo accounts. Public by design; set in the demo compose file / demo scripts.</summary>
    public string? Password { get; set; }

    /// <summary>How many resets are allowed per window (global, not per user), to keep the reset endpoint from being abused.</summary>
    public int ResetPermitLimit { get; set; } = 3;

    public int ResetWindowSeconds { get; set; } = 300;

    public static DemoOptions Load(IConfiguration config)
    {
        var o = config.GetSection(Section).Get<DemoOptions>() ?? new DemoOptions();
        if (o.Enabled && (string.IsNullOrWhiteSpace(o.Password) || o.Password.Length < 8))
            throw new InvalidOperationException("Demo:Enabled is true but Demo:Password is missing or shorter than 8 characters.");
        if (o.ResetPermitLimit < 1 || o.ResetWindowSeconds < 1)
            throw new InvalidOperationException("Demo:ResetPermitLimit and Demo:ResetWindowSeconds must be positive.");
        return o;
    }
}

/// <summary>Fixed-window limiter for the reset action (one shared budget for the whole instance).</summary>
public sealed class DemoResetThrottle(DemoOptions options)
{
    private readonly object _gate = new();
    private DateTime _windowStart = DateTime.MinValue;
    private int _used;

    /// <summary>Returns true when a reset may run now; otherwise how many seconds to wait.</summary>
    public bool TryAcquire(out int retryAfterSeconds)
    {
        lock (_gate)
        {
            var now = DateTime.UtcNow;
            var window = TimeSpan.FromSeconds(options.ResetWindowSeconds);
            if (now - _windowStart >= window) { _windowStart = now; _used = 0; }
            if (_used >= options.ResetPermitLimit)
            {
                retryAfterSeconds = Math.Max(1, (int)Math.Ceiling((_windowStart + window - now).TotalSeconds));
                return false;
            }
            _used++;
            retryAfterSeconds = 0;
            return true;
        }
    }
}
