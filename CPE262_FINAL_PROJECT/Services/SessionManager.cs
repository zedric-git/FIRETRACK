namespace CPE262_FINAL_PROJECT.Services
{
    public static class SessionManager
    {
        public static int    UserID           { get; private set; }
        public static string FullName         { get; private set; } = string.Empty;
        public static string Role             { get; private set; } = string.Empty;
        public static string AssignedBarangay { get; private set; } = string.Empty;
        public static string PhoneNumber      { get; private set; } = string.Empty;
        public static bool   IsLoggedIn       => UserID > 0;

        public static void Login(int userId, string fullName, string role,
            string assignedBarangay = "", string phoneNumber = "")
        {
            UserID           = userId;
            FullName         = fullName;
            Role             = role;
            AssignedBarangay = assignedBarangay;
            PhoneNumber      = phoneNumber;
        }

        public static void Logout()
        {
            UserID           = 0;
            FullName         = string.Empty;
            Role             = string.Empty;
            AssignedBarangay = string.Empty;
            PhoneNumber      = string.Empty;
        }
    }
}
