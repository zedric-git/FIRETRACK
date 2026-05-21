namespace CPE262_FINAL_PROJECT.Models
{
    public class AuditLog
    {
        public int LogID { get; set; }
        public int UserID { get; set; }
        public string Action { get; set; } = string.Empty;
        public string TargetTable { get; set; } = string.Empty;
        public int? TargetID { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string Timestamp { get; set; } = string.Empty;
    }
}
