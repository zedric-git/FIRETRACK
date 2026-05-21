using CPE262_FINAL_PROJECT.Database;
using CPE262_FINAL_PROJECT.Models;
using MySqlConnector;
using System;
using System.Collections.Generic;

namespace CPE262_FINAL_PROJECT.Repositories
{
    public class IncidentRepository
    {
        public List<Incident> GetAll()
        {
            var list = new List<Incident>();
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT IncidentID, Barangay, Sitio, GPSLat, GPSLong,
                       AlarmLevel, DateTime, CauseOfFire, PhotoPath,
                       Status, DSDWStatus, RegisteredBy
                FROM Incidents ORDER BY DateTime DESC";
            using var r = cmd.ExecuteReader();
            while (r.Read()) list.Add(Map(r));
            return list;
        }

        public Incident? GetById(int id)
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT IncidentID, Barangay, Sitio, GPSLat, GPSLong,
                       AlarmLevel, DateTime, CauseOfFire, PhotoPath,
                       Status, DSDWStatus, RegisteredBy
                FROM Incidents WHERE IncidentID = @id";
            cmd.Parameters.AddWithValue("@id", id);
            using var r = cmd.ExecuteReader();
            if (r.Read()) return Map(r);
            return null;
        }

        public List<Incident> GetByBarangay(string barangay)
        {
            var list = new List<Incident>();
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT IncidentID, Barangay, Sitio, GPSLat, GPSLong,
                       AlarmLevel, DateTime, CauseOfFire, PhotoPath,
                       Status, DSDWStatus, RegisteredBy
                FROM Incidents WHERE Barangay = @b ORDER BY DateTime DESC";
            cmd.Parameters.AddWithValue("@b", barangay);
            using var r = cmd.ExecuteReader();
            while (r.Read()) list.Add(Map(r));
            return list;
        }

        public int Create(Incident i)
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO Incidents
                    (Barangay, Sitio, GPSLat, GPSLong, AlarmLevel, DateTime,
                     CauseOfFire, PhotoPath, Status, DSDWStatus, RegisteredBy)
                VALUES
                    (@b, @s, @lat, @lng, @al, @dt, @cause, @photo, @status, @dswd, @by);
                SELECT LAST_INSERT_ID();";
            cmd.Parameters.AddWithValue("@b",      i.Barangay);
            cmd.Parameters.AddWithValue("@s",      i.Sitio);
            cmd.Parameters.AddWithValue("@lat",    i.GPSLat);
            cmd.Parameters.AddWithValue("@lng",    i.GPSLong);
            cmd.Parameters.AddWithValue("@al",     i.AlarmLevel);
            cmd.Parameters.AddWithValue("@dt",     i.DateTime);
            cmd.Parameters.AddWithValue("@cause",  i.CauseOfFire);
            cmd.Parameters.AddWithValue("@photo",  i.PhotoPath);
            cmd.Parameters.AddWithValue("@status", i.Status);
            cmd.Parameters.AddWithValue("@dswd",   i.DSDWStatus);
            cmd.Parameters.AddWithValue("@by",     i.RegisteredBy == 0
                ? (object)DBNull.Value : i.RegisteredBy);
            return Convert.ToInt32(cmd.ExecuteScalar()!);
        }

        public void UpdateStatus(int incidentId, string status)
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE Incidents SET Status = @s WHERE IncidentID = @id";
            cmd.Parameters.AddWithValue("@s",  status);
            cmd.Parameters.AddWithValue("@id", incidentId);
            cmd.ExecuteNonQuery();
        }

        public void UpdateDSDWStatus(int incidentId, string dsdwStatus)
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE Incidents SET DSDWStatus = @s WHERE IncidentID = @id";
            cmd.Parameters.AddWithValue("@s",  dsdwStatus);
            cmd.Parameters.AddWithValue("@id", incidentId);
            cmd.ExecuteNonQuery();
        }

        public bool DeletePermanent(int incidentId)
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            using var tx = conn.BeginTransaction();

            try
            {
                int Execute(string sql)
                {
                    using var cmd = conn.CreateCommand();
                    cmd.Transaction = tx;
                    cmd.CommandText = sql;
                    cmd.Parameters.AddWithValue("@id", incidentId);
                    return cmd.ExecuteNonQuery();
                }

                Execute(@"
                    UPDATE Evacuation_Centers ec
                    JOIN (
                        SELECT EvacuationCenterID, SUM(MemberCount) AS Members
                        FROM Families
                        WHERE IncidentID = @id
                          AND EvacuationCenterID IS NOT NULL
                        GROUP BY EvacuationCenterID
                    ) f ON f.EvacuationCenterID = ec.CenterID
                    SET ec.IsFull = IF(GREATEST(0, ec.CurrentOccupancy - f.Members) >= ec.Capacity, 1, 0),
                        ec.CurrentOccupancy = GREATEST(0, ec.CurrentOccupancy - f.Members),
                        ec.LastUpdated = NOW()");

                Execute(@"
                    DELETE rr FROM Relief_Records rr
                    JOIN Families f ON f.FamilyID = rr.FamilyID
                    WHERE f.IncidentID = @id");

                Execute("DELETE FROM DSWD_Messages WHERE IncidentID = @id");
                Execute("DELETE FROM Families WHERE IncidentID = @id");
                int deleted = Execute("DELETE FROM Incidents WHERE IncidentID = @id");

                tx.Commit();
                return deleted > 0;
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        public List<(string Barangay, int Count, double AvgAlarm)> GetSeverityByBarangay()
        {
            var list = new List<(string, int, double)>();
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT Barangay, COUNT(*) AS Cnt, AVG(AlarmLevel) AS AvgAlarm
                FROM Incidents
                GROUP BY Barangay
                ORDER BY Cnt DESC";
            using var r = cmd.ExecuteReader();
            while (r.Read())
                list.Add((r.GetString(0), r.GetInt32(1), r.GetDouble(2)));
            return list;
        }

        public double GetAvgResolutionHoursByBarangay(string barangay)
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT AVG(TIMESTAMPDIFF(MINUTE, i.DateTime, a.Timestamp)) / 60.0
                FROM Incidents i
                JOIN Audit_Logs a
                  ON a.TargetTable = 'Incidents' AND a.TargetID = i.IncidentID
                WHERE i.Status = 'Fire Out'
                  AND a.Reason LIKE '%to Fire Out'
                  AND i.Barangay = @b";
            cmd.Parameters.AddWithValue("@b", barangay);
            var result = cmd.ExecuteScalar();
            if (result == null || result == DBNull.Value) return -1;
            return Convert.ToDouble(result);
        }

        private static Incident Map(MySqlDataReader r) => new()
        {
            IncidentID   = r.GetInt32(0),
            Barangay     = r.GetString(1),
            Sitio        = r.GetString(2),
            GPSLat       = r.GetDouble(3),
            GPSLong      = r.GetDouble(4),
            AlarmLevel   = r.GetInt32(5),
            DateTime     = r.GetString(6),
            CauseOfFire  = r.IsDBNull(7)  ? "" : r.GetString(7),
            PhotoPath    = r.GetString(8),
            Status       = r.GetString(9),
            DSDWStatus   = r.GetString(10),
            RegisteredBy = r.IsDBNull(11) ? 0 : r.GetInt32(11)
        };
    }
}
