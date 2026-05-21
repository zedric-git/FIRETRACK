using CPE262_FINAL_PROJECT.Database;
using CPE262_FINAL_PROJECT.Models;
using MySqlConnector;
using System;
using System.Collections.Generic;

namespace CPE262_FINAL_PROJECT.Repositories
{
    public class ReliefRecordRepository
    {
        public List<ReliefRecord> GetAll()
        {
            var list = new List<ReliefRecord>();
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT RecordID, FamilyID, AgencyName, ItemType, Quantity,
                       DateDistributed, DistributedBy
                FROM Relief_Records
                ORDER BY DateDistributed DESC";
            using var r = cmd.ExecuteReader();
            while (r.Read()) list.Add(Map(r));
            return list;
        }

        public List<ReliefRecord> GetByFamily(int familyId)
        {
            var list = new List<ReliefRecord>();
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT RecordID, FamilyID, AgencyName, ItemType, Quantity,
                       DateDistributed, DistributedBy
                FROM Relief_Records WHERE FamilyID = @fid
                ORDER BY DateDistributed DESC";
            cmd.Parameters.AddWithValue("@fid", familyId);
            using var r = cmd.ExecuteReader();
            while (r.Read()) list.Add(Map(r));
            return list;
        }

        public int CountFamiliesServed()
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(DISTINCT FamilyID) FROM Relief_Records";
            return Convert.ToInt32(cmd.ExecuteScalar()!);
        }

        public int CountTotalDistributions()
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM Relief_Records";
            return Convert.ToInt32(cmd.ExecuteScalar()!);
        }

        public int CountUnservedFamilies()
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT COUNT(*) FROM Families
                WHERE FamilyID NOT IN (SELECT DISTINCT FamilyID FROM Relief_Records)";
            return Convert.ToInt32(cmd.ExecuteScalar()!);
        }

        public int CountPossibleDuplicates()
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT COUNT(*) FROM (
                    SELECT LOWER(TRIM(HeadName)) AS n
                    FROM Families
                    GROUP BY n
                    HAVING COUNT(DISTINCT IncidentID) > 1
                ) AS dups";
            return Convert.ToInt32(cmd.ExecuteScalar()!);
        }

        public int Create(ReliefRecord rec)
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            using var tx = conn.BeginTransaction();

            try
            {
                var cmd = conn.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = @"
                    INSERT INTO Relief_Records
                        (FamilyID, AgencyName, ItemType, Quantity, DateDistributed, DistributedBy)
                    VALUES (@fid, @agency, @item, @qty, @date, @by);
                    SELECT LAST_INSERT_ID();";
                cmd.Parameters.AddWithValue("@fid",    rec.FamilyID);
                cmd.Parameters.AddWithValue("@agency", rec.AgencyName);
                cmd.Parameters.AddWithValue("@item",   rec.ItemType);
                cmd.Parameters.AddWithValue("@qty",    rec.Quantity);
                cmd.Parameters.AddWithValue("@date",   rec.DateDistributed);
                cmd.Parameters.AddWithValue("@by",     rec.DistributedBy == 0
                    ? (object)DBNull.Value : rec.DistributedBy);
                int newId = Convert.ToInt32(cmd.ExecuteScalar()!);

                var status = conn.CreateCommand();
                status.Transaction = tx;
                status.CommandText = "UPDATE Families SET ReliefStatus = 'Received' WHERE FamilyID = @fid";
                status.Parameters.AddWithValue("@fid", rec.FamilyID);
                status.ExecuteNonQuery();

                tx.Commit();
                return newId;
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        public List<(int RecordID, string HeadName, string Agency, string ItemType, int Qty, string Date)>
            GetLedger(int limit = 100)
        {
            var list = new List<(int, string, string, string, int, string)>();
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT rr.RecordID, f.HeadName, rr.AgencyName, rr.ItemType,
                       rr.Quantity, rr.DateDistributed
                FROM Relief_Records rr
                JOIN Families f ON rr.FamilyID = f.FamilyID
                ORDER BY rr.DateDistributed DESC
                LIMIT @lim";
            cmd.Parameters.AddWithValue("@lim", limit);
            using var r = cmd.ExecuteReader();
            while (r.Read())
                list.Add((r.GetInt32(0), r.GetString(1), r.GetString(2),
                          r.GetString(3), r.GetInt32(4), r.GetString(5)));
            return list;
        }

        public List<(int FamilyID, string HeadName, int Members, string Barangay, int IncidentID)>
            GetUnservedFamilies()
        {
            var list = new List<(int, string, int, string, int)>();
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT f.FamilyID, f.HeadName, f.MemberCount, i.Barangay, f.IncidentID
                FROM Families f
                JOIN Incidents i ON f.IncidentID = i.IncidentID
                WHERE f.FamilyID NOT IN (SELECT DISTINCT FamilyID FROM Relief_Records)
                ORDER BY i.Barangay, f.HeadName";
            using var r = cmd.ExecuteReader();
            while (r.Read())
                list.Add((r.GetInt32(0), r.GetString(1), r.GetInt32(2),
                          r.GetString(3), r.GetInt32(4)));
            return list;
        }

        public List<(string HeadName, int IncidentCount, int TotalMembers)>
            GetDuplicateFamilies()
        {
            var list = new List<(string, int, int)>();
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT MIN(f.HeadName)               AS HeadName,
                       COUNT(DISTINCT f.IncidentID)  AS IncidentCount,
                       SUM(f.MemberCount)            AS TotalMembers
                FROM Families f
                GROUP BY LOWER(TRIM(f.HeadName))
                HAVING COUNT(DISTINCT f.IncidentID) > 1
                ORDER BY IncidentCount DESC, MIN(f.HeadName)";
            using var r = cmd.ExecuteReader();
            while (r.Read())
                list.Add((r.GetString(0), r.GetInt32(1), r.GetInt32(2)));
            return list;
        }

        private static ReliefRecord Map(MySqlDataReader r) => new()
        {
            RecordID        = r.GetInt32(0),
            FamilyID        = r.GetInt32(1),
            AgencyName      = r.GetString(2),
            ItemType        = r.GetString(3),
            Quantity        = r.GetInt32(4),
            DateDistributed = r.GetString(5),
            DistributedBy   = r.IsDBNull(6) ? 0 : r.GetInt32(6)
        };
    }
}
