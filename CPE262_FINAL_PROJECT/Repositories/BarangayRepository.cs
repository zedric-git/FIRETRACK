using CPE262_FINAL_PROJECT.Database;
using System.Collections.Generic;

namespace CPE262_FINAL_PROJECT.Repositories
{
    public class BarangayRepository
    {
        public List<string> GetAllNames()
        {
            var list = new List<string>();
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Name FROM Barangays ORDER BY Name ASC";
            using var r = cmd.ExecuteReader();
            while (r.Read())
                list.Add(r.GetString(0));
            return list;
        }
    }
}
