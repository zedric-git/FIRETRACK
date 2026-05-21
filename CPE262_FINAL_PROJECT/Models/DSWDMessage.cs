namespace CPE262_FINAL_PROJECT.Models
{
    public class DSWDMessage
    {
        public int    MessageID  { get; set; }
        public int    SenderID   { get; set; }
        public int    IncidentID { get; set; }
        public string Message    { get; set; } = string.Empty;
        public string Status          { get; set; } = "Pending";
        public string RejectionReason { get; set; } = string.Empty;
        public string SentAt     { get; set; } = string.Empty;

        public string SenderName     { get; set; } = string.Empty;
        public string SenderPhone    { get; set; } = string.Empty;
        public string IncidentBrgy   { get; set; } = string.Empty;
        public string IncidentStatus { get; set; } = string.Empty;

        public int    FamilyID       { get; set; } = 0;
        public string FamilyHeadName { get; set; } = string.Empty;
    }
}
