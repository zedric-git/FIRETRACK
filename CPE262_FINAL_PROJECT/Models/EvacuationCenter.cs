namespace CPE262_FINAL_PROJECT.Models
{
    public class EvacuationCenter
    {
        public int    CenterID         { get; set; }
        public string Name             { get; set; } = string.Empty;
        public string Barangay         { get; set; } = string.Empty;
        public double GPSLat           { get; set; }
        public double GPSLong          { get; set; }
        public int    Capacity         { get; set; }
        public int    CurrentOccupancy { get; set; }
        public string CenterType       { get; set; } = "Barangay";
        public bool   IsFull           { get; set; }
        public string LastUpdated      { get; set; } = string.Empty;

        public int  AvailableSlots => Capacity - CurrentOccupancy;
        public bool IsAtCapacity   => CurrentOccupancy >= Capacity;
    }
}
