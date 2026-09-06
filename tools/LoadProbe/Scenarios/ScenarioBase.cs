using PBL3.Tools.LoadProbe.Infra;

namespace PBL3.Tools.LoadProbe.Scenarios;

public abstract class ScenarioBase : IProbeScenario
{
    public abstract string Id { get; }
    public abstract string Title { get; }
    public abstract string Invariant { get; }
    public abstract int ExpectedRequests { get; }

    /// <inheritdoc />
    /// <remarks>Chỉ S11 (đo chính rate limiter) ghi đè cờ này.</remarks>
    public virtual bool RateLimitIsUnderTest => false;

    protected ProbeFixture Fixture { get; private set; } = null!;

    public virtual Task SetupAsync(ProbeEnvironment env, ProbeFixture fixture)
    {
        Fixture = fixture;
        return SetupCoreAsync(env);
    }

    protected abstract Task SetupCoreAsync(ProbeEnvironment env);

    public abstract Task<FireReport> FireAsync(ProbeEnvironment env);

    public abstract Task<ProbeOutcome> AssertAsync(ProbeEnvironment env, FireReport fire);
}
