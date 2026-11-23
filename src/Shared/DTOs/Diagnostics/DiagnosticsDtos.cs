namespace PBL3.Shared.DTOs.Diagnostics
{
    public class ReadReplicaDiagnosticResponse
    {
        public bool ReadOnlyConfigured { get; set; }
        public bool ReadOnlyContextInRecovery { get; set; }
        public bool PrimaryContextInRecovery { get; set; }
    }

    public class DataProtectionVerifyRequest
    {
        public string Mode { get; set; } = "roundtrip";
        public string Payload { get; set; } = string.Empty;
    }

    public class DataProtectionVerifyResponse
    {
        public bool SsmPrefixConfigured { get; set; }
        public string Mode { get; set; } = string.Empty;
        public bool? RoundTripMatched { get; set; }
        public int? ProtectedLength { get; set; }
        public string? ProtectedPayload { get; set; }
        public string? UnprotectedPayload { get; set; }
    }
}
