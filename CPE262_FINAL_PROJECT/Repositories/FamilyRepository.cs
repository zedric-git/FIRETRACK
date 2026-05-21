using CPE262_FINAL_PROJECT.Database;
using CPE262_FINAL_PROJECT.Models;
using MySqlConnector;
using System;
using System.Collections.Generic;

namespace CPE262_FINAL_PROJECT.Repositories
{
    public class FamilyRepository
    {
        public List<Family> GetByIncident(int incidentId)
        {
            var list = new List<Family>();
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT FamilyID, IncidentID, HeadName, MemberCount,
                       EvacuationCenterID, ReliefStatus, IsRepeatDisplaced
                FROM Families WHERE IncidentID = @id";
            cmd.Parameters.AddWithValue("@id", incidentId);
            using var r = cmd.ExecuteReader();
            while (r.Read()) list.Add(Map(r));
            return list;
        }

        public Family? GetByHeadNameAndBarangay(string headName, string barangay)
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT f.FamilyID, f.IncidentID, f.HeadName, f.MemberCount,
                       f.EvacuationCenterID, f.ReliefStatus, f.IsRepeatDisplaced
                FROM Families f
                JOIN Incidents i ON f.IncidentID = i.IncidentID
                WHERE LOWER(TRIM(f.HeadName)) = LOWER(TRIM(@name))
                  AND i.Barangay = @barangay
                LIMIT 1";
            cmd.Parameters.AddWithValue("@name",     headName);
            cmd.Parameters.AddWithValue("@barangay", barangay);
            using var r = cmd.ExecuteReader();
            if (r.Read()) return Map(r);
            return null;
        }

        public Family? GetByHeadNameAndCenter(string headName, int centerId)
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT FamilyID, IncidentID, HeadName, MemberCount,
                       EvacuationCenterID, ReliefStatus, IsRepeatDisplaced
                FROM Families
                WHERE BINARY TRIM(HeadName) = TRIM(@name)
                  AND EvacuationCenterID = @cid
                LIMIT 1";
            cmd.Parameters.AddWithValue("@name", headName.Trim());
            cmd.Parameters.AddWithValue("@cid",  centerId);
            using var r = cmd.ExecuteReader();
            if (r.Read()) return Map(r);
            return null;
        }

        public bool IsRepeatDisplaced(string headName, int currentIncidentId)
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT COUNT(*) FROM Families
                WHERE LOWER(TRIM(HeadName)) = LOWER(TRIM(@name))
                  AND IncidentID != @iid";
            cmd.Parameters.AddWithValue("@name", headName);
            cmd.Parameters.AddWithValue("@iid",  currentIncidentId);
            return Convert.ToInt64(cmd.ExecuteScalar()!) > 0;
        }

        public int Create(Family f)
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            using var tx = conn.BeginTransaction();

            try
            {
                var cmd = conn.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = @"
                    INSERT INTO Families
                        (IncidentID, HeadName, MemberCount, EvacuationCenterID, ReliefStatus, IsRepeatDisplaced)
                    VALUES (@iid, @name, @count, @center, @relief, @repeat);
                    SELECT LAST_INSERT_ID();";
                cmd.Parameters.AddWithValue("@iid",    f.IncidentID);
                cmd.Parameters.AddWithValue("@name",   f.HeadName.Trim());
                cmd.Parameters.AddWithValue("@count",  f.MemberCount);
                cmd.Parameters.AddWithValue("@center", (object?)f.EvacuationCenterID ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@relief", f.ReliefStatus);
                cmd.Parameters.AddWithValue("@repeat", f.IsRepeatDisplaced ? 1 : 0);
                int newId = Convert.ToInt32(cmd.ExecuteScalar()!);

                if (f.EvacuationCenterID.HasValue)
                {
                    var occ = conn.CreateCommand();
                    occ.Transaction = tx;
                    occ.CommandText = @"
                        UPDATE Evacuation_Centers
                        SET IsFull = IF(CurrentOccupancy + @m >= Capacity, 1, 0),
                            CurrentOccupancy = CurrentOccupancy + @m,
                            LastUpdated = NOW()
                        WHERE CenterID = @cid
                          AND CurrentOccupancy + @m <= Capacity";
                    occ.Parameters.AddWithValue("@m",   f.MemberCount);
                    occ.Parameters.AddWithValue("@cid", f.EvacuationCenterID.Value);
                    if (occ.ExecuteNonQuery() == 0)
                        throw new InvalidOperationException("Selected center does not have enough available slots.");
                }

                tx.Commit();
                return newId;
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        public void UnassignCenter(int familyId, int membersToSubtract, int centerId)
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            using var tx = conn.BeginTransaction();

            try
            {
                var cmd1 = conn.CreateCommand();
                cmd1.Transaction = tx;
                cmd1.CommandText = "UPDATE Families SET EvacuationCenterID = NULL WHERE FamilyID = @fid";
                cmd1.Parameters.AddWithValue("@fid", familyId);
                cmd1.ExecuteNonQuery();

                var cmd2 = conn.CreateCommand();
                cmd2.Transaction = tx;
                cmd2.CommandText = @"
                    UPDATE Evacuation_Centers
                    SET IsFull = IF(GREATEST(0, CurrentOccupancy - @m) >= Capacity, 1, 0),
                        CurrentOccupancy = GREATEST(0, CurrentOccupancy - @m),
                        LastUpdated = NOW()
                    WHERE CenterID = @cid";
                cmd2.Parameters.AddWithValue("@m",   membersToSubtract);
                cmd2.Parameters.AddWithValue("@cid", centerId);
                cmd2.ExecuteNonQuery();

                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        public (int ReliefDeleted, bool FamilyDeleted) DeleteFamily(int familyId, int? centerId, int memberCount)
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            using var tx = conn.BeginTransaction();

            try
            {
                var cmd1 = conn.CreateCommand();
                cmd1.Transaction = tx;
                cmd1.CommandText = "DELETE FROM Relief_Records WHERE FamilyID = @fid";
                cmd1.Parameters.AddWithValue("@fid", familyId);
                int reliefDeleted = cmd1.ExecuteNonQuery();

                if (centerId.HasValue)
                {
                    var cmd2 = conn.CreateCommand();
                    cmd2.Transaction = tx;
                    cmd2.CommandText = @"
                        UPDATE Evacuation_Centers
                        SET IsFull = IF(GREATEST(0, CurrentOccupancy - @m) >= Capacity, 1, 0),
                            CurrentOccupancy = GREATEST(0, CurrentOccupancy - @m),
                            LastUpdated = NOW()
                        WHERE CenterID = @cid";
                    cmd2.Parameters.AddWithValue("@m",   memberCount);
                    cmd2.Parameters.AddWithValue("@cid", centerId.Value);
                    cmd2.ExecuteNonQuery();
                }

                var cmd3 = conn.CreateCommand();
                cmd3.Transaction = tx;
                cmd3.CommandText = "DELETE FROM Families WHERE FamilyID = @fid";
                cmd3.Parameters.AddWithValue("@fid", familyId);
                bool deleted = cmd3.ExecuteNonQuery() > 0;

                tx.Commit();
                return (reliefDeleted, deleted);
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        public void AssignCenter(int familyId, int newCenterId, int memberCount, int? oldCenterId)
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            using var tx = conn.BeginTransaction();

            try
            {
                if (oldCenterId.HasValue)
                {
                    var cmd1 = conn.CreateCommand();
                    cmd1.Transaction = tx;
                    cmd1.CommandText = @"UPDATE Evacuation_Centers
                        SET IsFull = IF(GREATEST(0, CurrentOccupancy - @m) >= Capacity, 1, 0),
                            CurrentOccupancy = GREATEST(0, CurrentOccupancy - @m),
                            LastUpdated = NOW()
                        WHERE CenterID = @cid";
                    cmd1.Parameters.AddWithValue("@m",   memberCount);
                    cmd1.Parameters.AddWithValue("@cid", oldCenterId.Value);
                    cmd1.ExecuteNonQuery();
                }

                var cmd2 = conn.CreateCommand();
                cmd2.Transaction = tx;
                cmd2.CommandText = "UPDATE Families SET EvacuationCenterID = @ncid WHERE FamilyID = @fid";
                cmd2.Parameters.AddWithValue("@ncid", newCenterId);
                cmd2.Parameters.AddWithValue("@fid",  familyId);
                cmd2.ExecuteNonQuery();

                var cmd3 = conn.CreateCommand();
                cmd3.Transaction = tx;
                cmd3.CommandText = @"UPDATE Evacuation_Centers
                    SET IsFull = IF(CurrentOccupancy + @m >= Capacity, 1, 0),
                        CurrentOccupancy = CurrentOccupancy + @m,
                        LastUpdated = NOW()
                    WHERE CenterID = @ncid
                      AND CurrentOccupancy + @m <= Capacity";
                cmd3.Parameters.AddWithValue("@m",    memberCount);
                cmd3.Parameters.AddWithValue("@ncid", newCenterId);
                if (cmd3.ExecuteNonQuery() == 0)
                    throw new InvalidOperationException("Selected center does not have enough available slots.");

                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        public List<Family> GetByCenter(int centerId)
        {
            var list = new List<Family>();
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT FamilyID, IncidentID, HeadName, MemberCount,
                       EvacuationCenterID, ReliefStatus, IsRepeatDisplaced
                FROM Families WHERE EvacuationCenterID = @cid
                ORDER BY HeadName";
            cmd.Parameters.AddWithValue("@cid", centerId);
            using var r = cmd.ExecuteReader();
            while (r.Read()) list.Add(Map(r));
            return list;
        }

        public List<FamilyWithBarangay> GetByCenterWithBarangay(int centerId)
        {
            var list = new List<FamilyWithBarangay>();
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT f.FamilyID, f.IncidentID, f.HeadName, f.MemberCount,
                       f.EvacuationCenterID, f.ReliefStatus, f.IsRepeatDisplaced,
                       i.Barangay
                FROM Families f
                JOIN Incidents i ON f.IncidentID = i.IncidentID
                WHERE f.EvacuationCenterID = @cid
                ORDER BY i.Barangay, f.HeadName";
            cmd.Parameters.AddWithValue("@cid", centerId);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(new FamilyWithBarangay
                {
                    FamilyID           = r.GetInt32(0),
                    IncidentID         = r.GetInt32(1),
                    HeadName           = r.GetString(2),
                    MemberCount        = r.GetInt32(3),
                    EvacuationCenterID = r.IsDBNull(4) ? null : r.GetInt32(4),
                    ReliefStatus       = r.GetString(5),
                    IsRepeatDisplaced  = Convert.ToInt32(r.GetValue(6)) == 1,
                    Barangay           = r.GetString(7)
                });
            }
            return list;
        }

        public int UnassignAllFromCenterByBarangay(int centerId, string barangay)
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            using var tx = conn.BeginTransaction();
            try
            {
                var sumCmd = conn.CreateCommand();
                sumCmd.Transaction = tx;
                sumCmd.CommandText = @"
                    SELECT IFNULL(SUM(f.MemberCount), 0)
                    FROM Families f
                    JOIN Incidents i ON f.IncidentID = i.IncidentID
                    WHERE f.EvacuationCenterID = @cid AND i.Barangay = @brgy";
                sumCmd.Parameters.AddWithValue("@cid", centerId);
                sumCmd.Parameters.AddWithValue("@brgy", barangay);
                int totalMembers = Convert.ToInt32(sumCmd.ExecuteScalar() ?? 0);
                if (totalMembers == 0) { tx.Commit(); return 0; }

                var nullCmd = conn.CreateCommand();
                nullCmd.Transaction = tx;
                nullCmd.CommandText = @"
                    UPDATE Families f
                    JOIN Incidents i ON f.IncidentID = i.IncidentID
                    SET f.EvacuationCenterID = NULL
                    WHERE f.EvacuationCenterID = @cid AND i.Barangay = @brgy";
                nullCmd.Parameters.AddWithValue("@cid", centerId);
                nullCmd.Parameters.AddWithValue("@brgy", barangay);
                nullCmd.ExecuteNonQuery();

                var decCmd = conn.CreateCommand();
                decCmd.Transaction = tx;
                decCmd.CommandText = @"
                    UPDATE Evacuation_Centers
                    SET IsFull = IF(GREATEST(0, CurrentOccupancy - @m) >= Capacity, 1, 0),
                        CurrentOccupancy = GREATEST(0, CurrentOccupancy - @m),
                        LastUpdated = NOW()
                    WHERE CenterID = @cid";
                decCmd.Parameters.AddWithValue("@m", totalMembers);
                decCmd.Parameters.AddWithValue("@cid", centerId);
                decCmd.ExecuteNonQuery();

                tx.Commit();
                return totalMembers;
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        public void UpdateReliefStatus(int familyId, string status)
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE Families SET ReliefStatus = @s WHERE FamilyID = @id";
            cmd.Parameters.AddWithValue("@s",  status);
            cmd.Parameters.AddWithValue("@id", familyId);
            cmd.ExecuteNonQuery();
        }

        public List<(string Barangay, int Families, int Members)> GetFamilyCountByBarangay()
        {
            var list = new List<(string, int, int)>();
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT i.Barangay, COUNT(f.FamilyID), COALESCE(SUM(f.MemberCount), 0)
                FROM Families f
                JOIN Incidents i ON f.IncidentID = i.IncidentID
                GROUP BY i.Barangay
                ORDER BY COUNT(f.FamilyID) DESC";
            using var r = cmd.ExecuteReader();
            while (r.Read())
                list.Add((r.GetString(0), r.GetInt32(1), Convert.ToInt32(r.GetValue(2))));
            return list;
        }

        public (int Families, int Members) GetTotalDisplacedByBarangay(string barangay)
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT COUNT(f.FamilyID), COALESCE(SUM(f.MemberCount), 0)
                FROM Families f
                JOIN Incidents i ON f.IncidentID = i.IncidentID
                WHERE i.Barangay = @b";
            cmd.Parameters.AddWithValue("@b", barangay);
            using var r = cmd.ExecuteReader();
            if (r.Read()) return (r.GetInt32(0), Convert.ToInt32(r.GetValue(1)));
            return (0, 0);
        }

        private static Family Map(MySqlDataReader r) => new()
        {
            FamilyID           = r.GetInt32(0),
            IncidentID         = r.GetInt32(1),
            HeadName           = r.GetString(2),
            MemberCount        = r.GetInt32(3),
            EvacuationCenterID = r.IsDBNull(4) ? null : r.GetInt32(4),
            ReliefStatus       = r.GetString(5),
            IsRepeatDisplaced  = Convert.ToInt32(r.GetValue(6)) == 1
        };
    }
}
