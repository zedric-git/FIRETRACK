namespace CPE262_FINAL_PROJECT.Models
{
    public class Family
    {
        public int FamilyID { get; set; }
        public int IncidentID { get; set; }
        public string HeadName { get; set; } = string.Empty;
        public int MemberCount { get; set; }
        public int? EvacuationCenterID { get; set; }
        public string ReliefStatus { get; set; } = "Pending";
        public bool IsRepeatDisplaced { get; set; } = false;
    }
}
