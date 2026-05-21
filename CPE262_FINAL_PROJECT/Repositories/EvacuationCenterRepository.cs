using CPE262_FINAL_PROJECT.Database;
using CPE262_FINAL_PROJECT.Models;
using MySqlConnector;
using System;
using System.Collections.Generic;

namespace CPE262_FINAL_PROJECT.Repositories
{
    public class EvacuationCenterRepository
    {

        public List<EvacuationCenter> GetAll()
        {
            var list = new List<EvacuationCenter>();
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = SelectSql("1=1", "ORDER BY Name");
            using var r = cmd.ExecuteReader();
            while (r.Read()) list.Add(Map(r));
            return list;
        }

        public List<EvacuationCenter> GetByBarangay(string barangay)
        {
            var list = new List<EvacuationCenter>();
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = SelectSql("Barangay = @b AND CenterType = 'Barangay'", "ORDER BY Name");
            cmd.Parameters.AddWithValue("@b", barangay);
            using var r = cmd.ExecuteReader();
            while (r.Read()) list.Add(Map(r));
            return list;
        }

        public List<EvacuationCenter> GetByBarangayIncludingCity(string barangay)
        {
            var list = new List<EvacuationCenter>();
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                -- (A) Own barangay centers explicitly chosen via junction table
                SELECT ec.CenterID, ec.Name, ec.Barangay,
                       IFNULL(ec.GPSLat,0), IFNULL(ec.GPSLong,0),
                       ec.Capacity, ec.CurrentOccupancy,
                       IFNULL(ec.CenterType,'Barangay'),
                       IFNULL(ec.IsFull,0),
                       DATE_FORMAT(ec.LastUpdated,'%Y-%m-%d %H:%i:%s')
                FROM Evacuation_Centers ec
                JOIN Barangay_Chosen_Centers bcc ON bcc.CenterID = ec.CenterID
                WHERE bcc.Barangay = @b
                  AND ec.CenterType = 'Barangay'
                  AND ec.Barangay = @b
                UNION
                -- (B) City centers explicitly chosen (opted into) by this barangay
                SELECT ec.CenterID, ec.Name, ec.Barangay,
                       IFNULL(ec.GPSLat,0), IFNULL(ec.GPSLong,0),
                       ec.Capacity, ec.CurrentOccupancy,
                       IFNULL(ec.CenterType,'Barangay'),
                       IFNULL(ec.IsFull,0),
                       DATE_FORMAT(ec.LastUpdated,'%Y-%m-%d %H:%i:%s')
                FROM Evacuation_Centers ec
                JOIN Barangay_Chosen_Centers bcc ON bcc.CenterID = ec.CenterID
                WHERE bcc.Barangay = @b
                  AND ec.CenterType = 'City'
                UNION
                -- (C) Cross-barangay centers with an Approved request from this barangay
                --     (approval IS the activation for cross-brgy; no chosen-record needed)
                SELECT ec.CenterID, ec.Name, ec.Barangay,
                       IFNULL(ec.GPSLat,0), IFNULL(ec.GPSLong,0),
                       ec.Capacity, ec.CurrentOccupancy,
                       IFNULL(ec.CenterType,'Barangay'),
                       IFNULL(ec.IsFull,0),
                       DATE_FORMAT(ec.LastUpdated,'%Y-%m-%d %H:%i:%s')
                FROM Evacuation_Centers ec
                JOIN Cross_Barangay_Requests cbr ON cbr.TargetCenterID = ec.CenterID
                WHERE cbr.RequesterBarangay = @b AND cbr.Status = 'Approved'
                UNION
                -- (D) Safety net: any shared center (city or other-brgy) that already has
                --     at least one family from this barangay's incidents assigned.
                SELECT ec.CenterID, ec.Name, ec.Barangay,
                       IFNULL(ec.GPSLat,0), IFNULL(ec.GPSLong,0),
                       ec.Capacity, ec.CurrentOccupancy,
                       IFNULL(ec.CenterType,'Barangay'),
                       IFNULL(ec.IsFull,0),
                       DATE_FORMAT(ec.LastUpdated,'%Y-%m-%d %H:%i:%s')
                FROM Evacuation_Centers ec
                WHERE (ec.CenterType = 'City'
                       OR (ec.CenterType = 'Barangay' AND ec.Barangay <> @b))
                  AND ec.CenterID IN (
                      SELECT DISTINCT f.EvacuationCenterID
                      FROM Families f
                      JOIN Incidents i ON f.IncidentID = i.IncidentID
                      WHERE i.Barangay = @b
                        AND f.EvacuationCenterID IS NOT NULL
                  )
                ORDER BY 2";
            cmd.Parameters.AddWithValue("@b", barangay);
            using var r = cmd.ExecuteReader();
            while (r.Read()) list.Add(Map(r));
            return list;
        }

        public List<EvacuationCenter> GetCityLevel()
        {
            var list = new List<EvacuationCenter>();
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = SelectSql("CenterType = 'City'", "ORDER BY Name");
            using var r = cmd.ExecuteReader();
            while (r.Read()) list.Add(Map(r));
            return list;
        }

        public List<EvacuationCenter> GetChosenCityByBarangay(string barangay)
        {
            var list = new List<EvacuationCenter>();
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT ec.CenterID, ec.Name, ec.Barangay,
                       IFNULL(ec.GPSLat,0), IFNULL(ec.GPSLong,0),
                       ec.Capacity, ec.CurrentOccupancy,
                       IFNULL(ec.CenterType,'Barangay'),
                       IFNULL(ec.IsFull,0),
                       DATE_FORMAT(ec.LastUpdated,'%Y-%m-%d %H:%i:%s')
                FROM Evacuation_Centers ec
                JOIN Barangay_Chosen_Centers bcc ON bcc.CenterID = ec.CenterID
                WHERE bcc.Barangay = @b AND ec.CenterType = 'City'
                ORDER BY ec.Name";
            cmd.Parameters.AddWithValue("@b", barangay);
            using var r = cmd.ExecuteReader();
            while (r.Read()) list.Add(Map(r));
            return list;
        }

        public List<EvacuationCenter> GetApprovedCrossBarangayCenters(string requesterBarangay)
        {
            var list = new List<EvacuationCenter>();
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT ec.CenterID, ec.Name, ec.Barangay,
                       IFNULL(ec.GPSLat,0), IFNULL(ec.GPSLong,0),
                       ec.Capacity, ec.CurrentOccupancy,
                       IFNULL(ec.CenterType,'Barangay'),
                       IFNULL(ec.IsFull,0),
                       DATE_FORMAT(ec.LastUpdated,'%Y-%m-%d %H:%i:%s')
                FROM Evacuation_Centers ec
                JOIN Cross_Barangay_Requests cbr ON cbr.TargetCenterID = ec.CenterID
                WHERE cbr.RequesterBarangay = @brgy
                  AND cbr.Status = 'Approved'
                ORDER BY ec.Name";
            cmd.Parameters.AddWithValue("@brgy", requesterBarangay);
            using var r = cmd.ExecuteReader();
            while (r.Read()) list.Add(Map(r));
            return list;
        }

        public List<EvacuationCenter> GetNeighboringAvailable(string excludeBarangay)
        {
            var list = new List<EvacuationCenter>();
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT CenterID, Name, Barangay,
                       IFNULL(GPSLat,0), IFNULL(GPSLong,0),
                       Capacity, CurrentOccupancy,
                       IFNULL(CenterType,'Barangay'),
                       IFNULL(IsFull,0),
                       DATE_FORMAT(LastUpdated,'%Y-%m-%d %H:%i:%s')
                FROM Evacuation_Centers
                WHERE Barangay <> @b
                  AND CenterType = 'Barangay'
                  AND CurrentOccupancy < Capacity
                  AND CenterID NOT IN (
                      SELECT TargetCenterID FROM Cross_Barangay_Requests
                      WHERE RequesterBarangay = @b
                        AND Status IN ('Approved', 'Pending')
                  )
                ORDER BY Barangay, Name";
            cmd.Parameters.AddWithValue("@b", excludeBarangay);
            using var r = cmd.ExecuteReader();
            while (r.Read()) list.Add(Map(r));
            return list;
        }

        public EvacuationCenter? GetById(int id)
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = SelectSql("CenterID = @id", "");
            cmd.Parameters.AddWithValue("@id", id);
            using var r = cmd.ExecuteReader();
            if (r.Read()) return Map(r);
            return null;
        }


        public void MarkAsChosen(int centerId, string barangay)
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"INSERT IGNORE INTO Barangay_Chosen_Centers (Barangay, CenterID)
                                VALUES (@b, @c)";
            cmd.Parameters.AddWithValue("@b", barangay);
            cmd.Parameters.AddWithValue("@c", centerId);
            cmd.ExecuteNonQuery();
        }

        public void UnmarkChosen(int centerId, string barangay)
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"DELETE FROM Barangay_Chosen_Centers
                                WHERE CenterID = @c AND Barangay = @b";
            cmd.Parameters.AddWithValue("@b", barangay);
            cmd.Parameters.AddWithValue("@c", centerId);
            cmd.ExecuteNonQuery();
        }

        public HashSet<int> GetChosenCenterIds(string barangay)
        {
            var set = new HashSet<int>();
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"SELECT CenterID FROM Barangay_Chosen_Centers WHERE Barangay = @b";
            cmd.Parameters.AddWithValue("@b", barangay);
            using var r = cmd.ExecuteReader();
            while (r.Read()) set.Add(r.GetInt32(0));
            return set;
        }


        public int Create(EvacuationCenter ec)
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO Evacuation_Centers
                    (Name, Barangay, GPSLat, GPSLong, Capacity, CurrentOccupancy, CenterType, IsFull)
                VALUES (@name, @b, @lat, @lng, @cap, @occ, @type, 0);
                SELECT LAST_INSERT_ID();";
            cmd.Parameters.AddWithValue("@name", ec.Name);
            cmd.Parameters.AddWithValue("@b",    ec.Barangay);
            cmd.Parameters.AddWithValue("@lat",  ec.GPSLat);
            cmd.Parameters.AddWithValue("@lng",  ec.GPSLong);
            cmd.Parameters.AddWithValue("@cap",  ec.Capacity);
            cmd.Parameters.AddWithValue("@occ",  ec.CurrentOccupancy);
            cmd.Parameters.AddWithValue("@type", ec.CenterType);
            return Convert.ToInt32(cmd.ExecuteScalar()!);
        }

        public List<(EvacuationCenter Center, int ApprovedBarangays, List<string> BarangayNames)>
            GetCityLevelWithUsage()
        {
            var centers = GetCityLevel();
            var result  = new List<(EvacuationCenter, int, List<string>)>();

            using var conn = DatabaseHelper.GetConnection();
            conn.Open();

            foreach (var c in centers)
            {
                var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    SELECT DISTINCT cbr.RequesterBarangay
                    FROM Cross_Barangay_Requests cbr
                    WHERE cbr.TargetCenterID = @id AND cbr.Status = 'Approved'
                    ORDER BY cbr.RequesterBarangay";
                cmd.Parameters.AddWithValue("@id", c.CenterID);
                var names = new List<string>();
                using var r = cmd.ExecuteReader();
                while (r.Read()) names.Add(r.GetString(0));
                result.Add((c, names.Count, names));
            }
            return result;
        }

        public void UpdateCityCenter(int centerId, string name, int capacity)
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                UPDATE Evacuation_Centers
                SET Name=@name, Capacity=@cap, LastUpdated=NOW()
                WHERE CenterID=@id AND CenterType='City'";
            cmd.Parameters.AddWithValue("@name", name);
            cmd.Parameters.AddWithValue("@cap",  capacity);
            cmd.Parameters.AddWithValue("@id",   centerId);
            cmd.ExecuteNonQuery();
        }

        public (bool Success, string Error) DeleteBarangayCenter(int centerId)
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            var chkF = conn.CreateCommand();
            chkF.CommandText = "SELECT COUNT(*) FROM Families WHERE EvacuationCenterID = @id";
            chkF.Parameters.AddWithValue("@id", centerId);
            long familyCount = Convert.ToInt64(chkF.ExecuteScalar()!);
            if (familyCount > 0)
                return (false, $"Cannot delete — {familyCount} family(ies) still assigned. Unassign or delete them first.");
            var cmdReq = conn.CreateCommand();
            cmdReq.CommandText = "SELECT COUNT(*) FROM Cross_Barangay_Requests WHERE TargetCenterID = @id";
            cmdReq.Parameters.AddWithValue("@id", centerId);
            long reqCount = Convert.ToInt64(cmdReq.ExecuteScalar()!);
            if (reqCount > 0)
            {
                var cmdDelReq = conn.CreateCommand();
                cmdDelReq.CommandText = "DELETE FROM Cross_Barangay_Requests WHERE TargetCenterID = @id";
                cmdDelReq.Parameters.AddWithValue("@id", centerId);
                cmdDelReq.ExecuteNonQuery();
            }
            var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM Evacuation_Centers WHERE CenterID=@id AND CenterType='Barangay'";
            cmd.Parameters.AddWithValue("@id", centerId);
            cmd.ExecuteNonQuery();
            string warning = reqCount > 0 ? $" ({reqCount} cross-barangay request(s) were also cancelled.)" : "";
            return (true, warning);
        }

        public (bool Success, string Error) DeleteCityCenter(int centerId)
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            var chkF = conn.CreateCommand();
            chkF.CommandText = "SELECT COUNT(*) FROM Families WHERE EvacuationCenterID = @id";
            chkF.Parameters.AddWithValue("@id", centerId);
            long familyCount = Convert.ToInt64(chkF.ExecuteScalar()!);
            if (familyCount > 0)
                return (false,
                    $"Cannot remove — {familyCount} family(ies) are still assigned to this center. " +
                    $"Each barangay using this center must RELEASE it from their own dashboard first.");
            var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM Evacuation_Centers WHERE CenterID=@id AND CenterType='City'";
            cmd.Parameters.AddWithValue("@id", centerId);
            cmd.ExecuteNonQuery();
            return (true, "");
        }

        public List<(string Barangay, int Families, int Persons)>
            GetCenterUsageReport(int centerId)
        {
            var list = new List<(string, int, int)>();
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT i.Barangay,
                       COUNT(DISTINCT f.FamilyID) AS Families,
                       SUM(f.MemberCount)         AS Persons
                FROM Families f
                JOIN Incidents i ON f.IncidentID = i.IncidentID
                WHERE f.EvacuationCenterID = @cid
                GROUP BY i.Barangay
                ORDER BY Families DESC";
            cmd.Parameters.AddWithValue("@cid", centerId);
            using var r = cmd.ExecuteReader();
            while (r.Read())
                list.Add((r.GetString(0), r.GetInt32(1),
                          r.IsDBNull(2) ? 0 : Convert.ToInt32(r.GetValue(2))));
            return list;
        }

        public void UpdateOccupancy(int centerId, int newOccupancy, int capacity)
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                UPDATE Evacuation_Centers
                SET CurrentOccupancy = @occ,
                    IsFull = IF(@occ >= @cap, 1, 0),
                    LastUpdated = NOW()
                WHERE CenterID = @id";
            cmd.Parameters.AddWithValue("@occ", newOccupancy);
            cmd.Parameters.AddWithValue("@cap", capacity);
            cmd.Parameters.AddWithValue("@id",  centerId);
            cmd.ExecuteNonQuery();
        }


        private static string SelectSql(string where, string order) => $@"
            SELECT CenterID, Name, Barangay,
                   IFNULL(GPSLat,0), IFNULL(GPSLong,0),
                   Capacity, CurrentOccupancy,
                   IFNULL(CenterType,'Barangay'),
                   IFNULL(IsFull,0),
                   DATE_FORMAT(LastUpdated,'%Y-%m-%d %H:%i:%s')
            FROM Evacuation_Centers
            WHERE {where}
            {order}";

        private static EvacuationCenter Map(MySqlDataReader r) => new()
        {
            CenterID         = r.GetInt32(0),
            Name             = r.GetString(1),
            Barangay         = r.GetString(2),
            GPSLat           = r.GetDouble(3),
            GPSLong          = r.GetDouble(4),
            Capacity         = r.GetInt32(5),
            CurrentOccupancy = r.GetInt32(6),
            CenterType       = r.GetString(7),
            IsFull           = Convert.ToInt32(r.GetValue(8)) == 1,
            LastUpdated      = r.IsDBNull(9) ? "" : r.GetString(9)
        };
    }
}
