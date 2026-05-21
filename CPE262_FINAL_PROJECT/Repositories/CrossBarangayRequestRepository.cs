using CPE262_FINAL_PROJECT.Database;
using CPE262_FINAL_PROJECT.Models;
using MySqlConnector;
using System;
using System.Collections.Generic;

namespace CPE262_FINAL_PROJECT.Repositories
{
    public class CrossBarangayRequestRepository
    {
        public List<CrossBarangayRequest> GetIncomingPending(string targetBarangay)
        {
            var list = new List<CrossBarangayRequest>();
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT cbr.RequestID, cbr.RequesterUserID, cbr.RequesterBarangay,
                       cbr.TargetCenterID, ec.Name, ec.Barangay,
                       cbr.Status, IFNULL(cbr.Reason,''),
                       DATE_FORMAT(cbr.RequestedAt,'%Y-%m-%d %H:%i:%s'),
                       IFNULL(DATE_FORMAT(cbr.ResolvedAt,'%Y-%m-%d %H:%i:%s'),'')
                FROM Cross_Barangay_Requests cbr
                JOIN Evacuation_Centers ec ON cbr.TargetCenterID = ec.CenterID
                WHERE ec.Barangay = @b AND cbr.Status = 'Pending'
                ORDER BY cbr.RequestedAt DESC";
            cmd.Parameters.AddWithValue("@b", targetBarangay);
            using var r = cmd.ExecuteReader();
            while (r.Read()) list.Add(MapRequest(r));
            return list;
        }

        public List<CrossBarangayRequest> GetOutgoing(string requesterBarangay)
        {
            var list = new List<CrossBarangayRequest>();
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT cbr.RequestID, cbr.RequesterUserID, cbr.RequesterBarangay,
                       cbr.TargetCenterID, ec.Name, ec.Barangay,
                       cbr.Status, IFNULL(cbr.Reason,''),
                       DATE_FORMAT(cbr.RequestedAt,'%Y-%m-%d %H:%i:%s'),
                       IFNULL(DATE_FORMAT(cbr.ResolvedAt,'%Y-%m-%d %H:%i:%s'),'')
                FROM Cross_Barangay_Requests cbr
                JOIN Evacuation_Centers ec ON cbr.TargetCenterID = ec.CenterID
                WHERE cbr.RequesterBarangay = @b
                ORDER BY cbr.RequestedAt DESC";
            cmd.Parameters.AddWithValue("@b", requesterBarangay);
            using var r = cmd.ExecuteReader();
            while (r.Read()) list.Add(MapRequest(r));
            return list;
        }

        public List<int> GetApprovedTargetCenterIds(string requesterBarangay)
        {
            var ids = new List<int>();
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT DISTINCT TargetCenterID FROM Cross_Barangay_Requests
                WHERE RequesterBarangay = @b AND Status = 'Approved'";
            cmd.Parameters.AddWithValue("@b", requesterBarangay);
            using var r = cmd.ExecuteReader();
            while (r.Read()) ids.Add(r.GetInt32(0));
            return ids;
        }

        public int CancelAllOutgoing(string requesterBarangay)
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                UPDATE Cross_Barangay_Requests
                SET Status     = 'Rejected',
                    Reason     = IFNULL(NULLIF(Reason,''), 'Bulk cancelled via Block All Access'),
                    ResolvedAt = NOW()
                WHERE RequesterBarangay = @b
                  AND Status IN ('Pending', 'Approved')";
            cmd.Parameters.AddWithValue("@b", requesterBarangay);
            return cmd.ExecuteNonQuery();
        }

        public int SendRequest(int requesterUserId, string requesterBarangay, int targetCenterId)
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO Cross_Barangay_Requests
                    (RequesterUserID, RequesterBarangay, TargetCenterID, Status)
                VALUES (@uid, @brgy, @cid, 'Pending');
                SELECT LAST_INSERT_ID();";
            cmd.Parameters.AddWithValue("@uid",  requesterUserId);
            cmd.Parameters.AddWithValue("@brgy", requesterBarangay);
            cmd.Parameters.AddWithValue("@cid",  targetCenterId);
            return Convert.ToInt32(cmd.ExecuteScalar()!);
        }

        public void Approve(int requestId)
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();

            var upd = conn.CreateCommand();
            upd.CommandText = @"
                UPDATE Cross_Barangay_Requests
                SET Status='Approved', ResolvedAt=NOW()
                WHERE RequestID=@id";
            upd.Parameters.AddWithValue("@id", requestId);
            upd.ExecuteNonQuery();
        }

        public void Reject(int requestId, string reason)
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                UPDATE Cross_Barangay_Requests
                SET Status='Rejected', Reason=@r, ResolvedAt=NOW()
                WHERE RequestID=@id";
            cmd.Parameters.AddWithValue("@r",  reason);
            cmd.Parameters.AddWithValue("@id", requestId);
            cmd.ExecuteNonQuery();
        }

        public bool HasPendingRequest(string requesterBarangay, int targetCenterId)
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT COUNT(*) FROM Cross_Barangay_Requests
                WHERE RequesterBarangay=@b AND TargetCenterID=@c AND Status='Pending'";
            cmd.Parameters.AddWithValue("@b", requesterBarangay);
            cmd.Parameters.AddWithValue("@c", targetCenterId);
            return Convert.ToInt64(cmd.ExecuteScalar()!) > 0;
        }


        public List<CrossBarangayRequest> GetIncomingActive(string targetBarangay)
        {
            var list = new List<CrossBarangayRequest>();
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT cbr.RequestID, cbr.RequesterUserID, cbr.RequesterBarangay,
                       cbr.TargetCenterID, ec.Name, ec.Barangay,
                       cbr.Status, IFNULL(cbr.Reason,''),
                       DATE_FORMAT(cbr.RequestedAt,'%Y-%m-%d %H:%i:%s'),
                       IFNULL(DATE_FORMAT(cbr.ResolvedAt,'%Y-%m-%d %H:%i:%s'),'')
                FROM Cross_Barangay_Requests cbr
                JOIN Evacuation_Centers ec ON cbr.TargetCenterID = ec.CenterID
                WHERE ec.Barangay = @b AND cbr.Status IN ('Pending','Approved')
                ORDER BY cbr.RequestedAt DESC";
            cmd.Parameters.AddWithValue("@b", targetBarangay);
            using var r = cmd.ExecuteReader();
            while (r.Read()) list.Add(MapRequest(r));
            return list;
        }

        public List<(int CenterId, string RequesterBarangay)> GetApprovedIncomingPairs(string targetBarangay)
        {
            var pairs = new List<(int, string)>();
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT DISTINCT cbr.TargetCenterID, cbr.RequesterBarangay
                FROM Cross_Barangay_Requests cbr
                JOIN Evacuation_Centers ec ON cbr.TargetCenterID = ec.CenterID
                WHERE ec.Barangay = @b AND cbr.Status = 'Approved'";
            cmd.Parameters.AddWithValue("@b", targetBarangay);
            using var r = cmd.ExecuteReader();
            while (r.Read()) pairs.Add((r.GetInt32(0), r.GetString(1)));
            return pairs;
        }

        public int RevokeAllIncoming(string ownerBarangay)
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                UPDATE Cross_Barangay_Requests cbr
                JOIN Evacuation_Centers ec ON cbr.TargetCenterID = ec.CenterID
                SET cbr.Status     = 'Rejected',
                    cbr.Reason     = IFNULL(NULLIF(cbr.Reason,''), 'Access revoked by center owner'),
                    cbr.ResolvedAt = NOW()
                WHERE ec.Barangay = @b
                  AND cbr.Status IN ('Pending','Approved')";
            cmd.Parameters.AddWithValue("@b", ownerBarangay);
            return cmd.ExecuteNonQuery();
        }

        public bool DeleteRequest(int requestId)
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();

            var check = conn.CreateCommand();
            check.CommandText = "SELECT COUNT(*) FROM Cross_Barangay_Requests WHERE RequestID = @id AND Status = 'Rejected'";
            check.Parameters.AddWithValue("@id", requestId);
            if (Convert.ToInt64(check.ExecuteScalar()!) == 0) return false;

            var delReq = conn.CreateCommand();
            delReq.CommandText = "DELETE FROM Cross_Barangay_Requests WHERE RequestID = @id";
            delReq.Parameters.AddWithValue("@id", requestId);
            return delReq.ExecuteNonQuery() > 0;
        }

        private static CrossBarangayRequest MapRequest(MySqlDataReader r) => new()
        {
            RequestID         = r.GetInt32(0),
            RequesterUserID   = r.GetInt32(1),
            RequesterBarangay = r.GetString(2),
            TargetCenterID    = r.GetInt32(3),
            TargetCenterName  = r.GetString(4),
            TargetBarangay    = r.GetString(5),
            Status            = r.GetString(6),
            Reason            = r.GetString(7),
            RequestedAt       = r.GetString(8),
            ResolvedAt        = r.GetString(9)
        };
    }
}
