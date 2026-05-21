using CPE262_FINAL_PROJECT.Database;
using CPE262_FINAL_PROJECT.Models;
using MySqlConnector;
using System;
using System.Collections.Generic;

namespace CPE262_FINAL_PROJECT.Repositories
{
    public class DSWDMessageRepository
    {
        public int Insert(DSWDMessage msg)
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO DSWD_Messages (SenderID, IncidentID, Message)
                VALUES (@sender, @incident, @msg);
                SELECT LAST_INSERT_ID();";
            cmd.Parameters.AddWithValue("@sender",   msg.SenderID);
            cmd.Parameters.AddWithValue("@incident", msg.IncidentID);
            cmd.Parameters.AddWithValue("@msg",      msg.Message.Trim());
            return Convert.ToInt32(cmd.ExecuteScalar()!);
        }

        public int CountPending()
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM DSWD_Messages WHERE Status='Pending' AND HiddenFromDswd = 0";
            return Convert.ToInt32(cmd.ExecuteScalar()!);
        }

        public void Approve(int messageId)
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE DSWD_Messages SET Status='Approved' WHERE MessageID=@id";
            cmd.Parameters.AddWithValue("@id", messageId);
            cmd.ExecuteNonQuery();
        }

        public void Reject(int messageId, string reason)
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"UPDATE DSWD_Messages
                                SET Status='Rejected', RejectionReason=@reason
                                WHERE MessageID=@id";
            cmd.Parameters.AddWithValue("@id",     messageId);
            cmd.Parameters.AddWithValue("@reason", reason.Trim());
            cmd.ExecuteNonQuery();
        }

        public void HideFromDswdInbox(int messageId)
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE DSWD_Messages SET HiddenFromDswd = 1 WHERE MessageID=@id";
            cmd.Parameters.AddWithValue("@id", messageId);
            cmd.ExecuteNonQuery();
        }

        public void HideFromCitizenHistory(int messageId, int senderId)
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                UPDATE DSWD_Messages
                SET HiddenFromCitizen = 1
                WHERE MessageID = @id AND SenderID = @sender";
            cmd.Parameters.AddWithValue("@id", messageId);
            cmd.Parameters.AddWithValue("@sender", senderId);
            cmd.ExecuteNonQuery();
        }

        public List<DSWDMessage> GetAllForInbox()
        {
            var list = new List<DSWDMessage>();
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT m.MessageID, m.SenderID, m.IncidentID, m.Message,
                       DATE_FORMAT(m.SentAt, '%Y-%m-%d %H:%i:%s') AS SentAt,
                       u.FullName, IFNULL(u.PhoneNumber, '') AS PhoneNumber,
                       i.Barangay, i.Status,
                       IFNULL(m.Status,'Pending')          AS MsgStatus,
                       IFNULL(m.RejectionReason,'')        AS RejectionReason,
                       IFNULL(f.FamilyID, 0)              AS FamilyID,
                       IFNULL(f.HeadName, '')             AS HeadName
                FROM DSWD_Messages m
                INNER JOIN Users     u ON u.UserID     = m.SenderID
                INNER JOIN Incidents i ON i.IncidentID = m.IncidentID
                LEFT JOIN  Families  f ON f.IncidentID = m.IncidentID
                                      AND f.HeadName  = u.FullName
                WHERE m.HiddenFromDswd = 0
                ORDER BY m.SentAt DESC";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(new DSWDMessage
                {
                    MessageID       = r.GetInt32(0),
                    SenderID        = r.GetInt32(1),
                    IncidentID      = r.GetInt32(2),
                    Message         = r.GetString(3),
                    SentAt          = r.IsDBNull(4) ? "" : r.GetString(4),
                    SenderName      = r.GetString(5),
                    SenderPhone     = r.GetString(6),
                    IncidentBrgy    = r.GetString(7),
                    IncidentStatus  = r.GetString(8),
                    Status          = r.IsDBNull(9)  ? "Pending" : r.GetString(9),
                    RejectionReason = r.IsDBNull(10) ? ""        : r.GetString(10),
                    FamilyID        = r.IsDBNull(11) ? 0         : r.GetInt32(11),
                    FamilyHeadName  = r.IsDBNull(12) ? ""        : r.GetString(12)
                });
            }
            return list;
        }

        public List<DSWDMessage> GetBySender(int userId)
        {
            var list = new List<DSWDMessage>();
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT m.MessageID, m.SenderID, m.IncidentID, m.Message,
                       DATE_FORMAT(m.SentAt, '%Y-%m-%d %H:%i:%s') AS SentAt,
                       u.FullName, IFNULL(u.PhoneNumber,'') AS PhoneNumber,
                       i.Barangay, i.Status,
                       IFNULL(m.Status,'Pending')          AS MsgStatus,
                       IFNULL(m.RejectionReason,'')        AS RejectionReason
                FROM DSWD_Messages m
                INNER JOIN Users     u ON u.UserID     = m.SenderID
                INNER JOIN Incidents i ON i.IncidentID = m.IncidentID
                WHERE m.SenderID = @uid
                  AND m.HiddenFromCitizen = 0
                ORDER BY m.SentAt DESC";
            cmd.Parameters.AddWithValue("@uid", userId);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(new DSWDMessage
                {
                    MessageID       = r.GetInt32(0),
                    SenderID        = r.GetInt32(1),
                    IncidentID      = r.GetInt32(2),
                    Message         = r.GetString(3),
                    SentAt          = r.IsDBNull(4) ? "" : r.GetString(4),
                    SenderName      = r.GetString(5),
                    SenderPhone     = r.GetString(6),
                    IncidentBrgy    = r.GetString(7),
                    IncidentStatus  = r.GetString(8),
                    Status          = r.IsDBNull(9)  ? "Pending" : r.GetString(9),
                    RejectionReason = r.IsDBNull(10) ? ""        : r.GetString(10)
                });
            }
            return list;
        }
    }
}
