namespace RyanDPI.Models
{
    public enum ComponentState
    {
        NotInstalled,
        Stopped,
        Running
    }

    public sealed class RyanDpiStatus
    {
        public ComponentState EngineState { get; init; }
        public ComponentState RouterState { get; init; }
        public ComponentState DnsProxyState { get; init; }
        public bool NdisDriverInstalled { get; init; }
        public bool HasLegacyServices { get; init; }
        public List<string> DetectedLegacyServices { get; init; } = new();

        public bool IsInstalled =>
            EngineState != ComponentState.NotInstalled || RouterState != ComponentState.NotInstalled;

        public bool IsFullyActive =>
            EngineState == ComponentState.Running && RouterState == ComponentState.Running;

        public bool IsPartiallyRunning =>
            !IsFullyActive && (EngineState == ComponentState.Running || RouterState == ComponentState.Running);
    }

    public sealed class InstallProgressUpdate
    {
        public int StepIndex { get; init; }
        public int TotalSteps { get; init; }
        public string Title { get; init; } = string.Empty;
        public string Detail { get; init; } = string.Empty;
        public double Percent => TotalSteps > 0 ? Math.Clamp((double)StepIndex / TotalSteps * 100.0, 0, 100) : 0;
    }

    public sealed class SelfTestCheckResult
    {
        public string Name { get; init; } = string.Empty;
        public bool Passed { get; init; }
        public long LatencyMs { get; init; }
        public string Summary { get; init; } = string.Empty;
    }

    public sealed class OperationResult
    {
        public bool Success { get; init; }
        public bool RequiresReboot { get; init; }
        public string Message { get; init; } = string.Empty;
        public string? RootAclPolicyFailureReason { get; init; }
    }
}
