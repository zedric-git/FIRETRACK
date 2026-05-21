namespace CPE262_FINAL_PROJECT.Models
{
    public class CitizenReport
    {
        public int    ReportID    { get; set; }
        public int    ReporterID  { get; set; }
        public string FullName    { get; set; } = string.Empty;
        public string Phone       { get; set; } = string.Empty;
        public string Address     { get; set; } = string.Empty;
        public string Barangay    { get; set; } = string.Empty;
        public string Status      { get; set; } = "Pending";
        public bool   IsVerified  { get; set; }
        public string SubmittedAt { get; set; } = string.Empty;
    }
}
