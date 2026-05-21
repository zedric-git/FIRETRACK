namespace CPE262_FINAL_PROJECT.Models
{
    public class CrossBarangayRequest
    {
        public int    RequestID         { get; set; }
        public int    RequesterUserID   { get; set; }
        public string RequesterBarangay { get; set; } = string.Empty;
        public int    TargetCenterID    { get; set; }
        public string TargetCenterName  { get; set; } = string.Empty;
        public string TargetBarangay    { get; set; } = string.Empty;
        public string Status            { get; set; } = "Pending";
        public string Reason            { get; set; } = string.Empty;
        public string RequestedAt       { get; set; } = string.Empty;
        public string ResolvedAt        { get; set; } = string.Empty;
    }
}
