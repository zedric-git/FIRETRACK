namespace CPE262_FINAL_PROJECT.Models
{
    public class Incident
    {
        public int IncidentID { get; set; }
        public string Barangay { get; set; } = string.Empty;
        public string Sitio { get; set; } = string.Empty;
        public double GPSLat { get; set; }
        public double GPSLong { get; set; }
        public int AlarmLevel { get; set; }
        public string DateTime { get; set; } = string.Empty;
        public string CauseOfFire { get; set; } = string.Empty;
        public string PhotoPath { get; set; } = string.Empty;
        public string Status { get; set; } = "Active";
        public string DSDWStatus { get; set; } = "Pending";
        public int RegisteredBy { get; set; }
    }
}
