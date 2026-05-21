namespace CPE262_FINAL_PROJECT.Models
{
    public class ReliefRecord
    {
        public int RecordID { get; set; }
        public int FamilyID { get; set; }
        public string AgencyName { get; set; } = string.Empty;
        public string ItemType { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public string DateDistributed { get; set; } = string.Empty;
        public int DistributedBy { get; set; }
    }
}
