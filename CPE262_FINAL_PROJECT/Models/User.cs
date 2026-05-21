namespace CPE262_FINAL_PROJECT.Models
{
    public class User
    {
        public int    UserID           { get; set; }
        public string FullName         { get; set; } = string.Empty;
        public string Role             { get; set; } = string.Empty;
        public string Email            { get; set; } = string.Empty;
        public string PasswordHash     { get; set; } = string.Empty;
        public bool   IsActive         { get; set; } = true;
        public string AssignedBarangay { get; set; } = string.Empty;
        public string PhoneNumber      { get; set; } = string.Empty;
        public string CreatedAt        { get; set; } = string.Empty;
    }
}
