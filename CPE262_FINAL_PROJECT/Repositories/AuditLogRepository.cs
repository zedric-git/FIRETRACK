using CPE262_FINAL_PROJECT.Database;
using CPE262_FINAL_PROJECT.Models;
using MySqlConnector;
using System;
using System.Collections.Generic;

namespace CPE262_FINAL_PROJECT.Repositories
{
    public class AuditLogRepository
    {
        public AuditLog Log(int userId, string action, string targetTable = "", int? targetId = null, string reason = "")
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO Audit_Logs (UserID, Action, TargetTable, TargetID, Reason)
                VALUES (@uid, @action, @table, @tid, @reason)";
            cmd.Parameters.AddWithValue("@uid",    userId);
            cmd.Parameters.AddWithValue("@action", action);
            cmd.Parameters.AddWithValue("@table",  targetTable);
            cmd.Parameters.AddWithValue("@tid",    (object?)targetId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@reason", reason);
            cmd.ExecuteNonQuery();

            var logId = (int)cmd.LastInsertedId;
            var read = conn.CreateCommand();
            read.CommandText = @"
                SELECT LogID, UserID, Action, TargetTable, TargetID, Reason,
                       DATE_FORMAT(Timestamp, '%Y-%m-%d %H:%i:%s') AS Timestamp
                FROM Audit_Logs WHERE LogID = @id";
            read.Parameters.AddWithValue("@id", logId);

            using var r = read.ExecuteReader();
            if (r.Read())
            {
                return new AuditLog
                {
                    LogID       = r.GetInt32(0),
                    UserID      = r.GetInt32(1),
                    Action      = r.GetString(2),
                    TargetTable = r.IsDBNull(3) ? "" : r.GetString(3),
                    TargetID    = r.IsDBNull(4) ? null : r.GetInt32(4),
                    Reason      = r.IsDBNull(5) ? "" : r.GetString(5),
                    Timestamp   = r.IsDBNull(6) ? "" : r.GetString(6)
                };
            }

            return new AuditLog
            {
                LogID       = logId,
                UserID      = userId,
                Action      = action,
                TargetTable = targetTable,
                TargetID    = targetId,
                Reason      = reason,
                Timestamp   = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };
        }

        public List<AuditLog> GetAll()
        {
            var list = new List<AuditLog>();
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT LogID, UserID, Action, TargetTable, TargetID, Reason,
                       DATE_FORMAT(Timestamp, '%Y-%m-%d %H:%i:%s') AS Timestamp
                FROM Audit_Logs ORDER BY Timestamp DESC";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(new AuditLog
                {
                    LogID       = r.GetInt32(0),
                    UserID      = r.GetInt32(1),
                    Action      = r.GetString(2),
                    TargetTable = r.IsDBNull(3) ? "" : r.GetString(3),
                    TargetID    = r.IsDBNull(4) ? null  : r.GetInt32(4),
                    Reason      = r.IsDBNull(5) ? "" : r.GetString(5),
                    Timestamp   = r.IsDBNull(6) ? "" : r.GetString(6)
                });
            }
            return list;
        }
    }
}
