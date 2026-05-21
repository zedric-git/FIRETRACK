using CPE262_FINAL_PROJECT.Database;
using CPE262_FINAL_PROJECT.Models;
using MySqlConnector;
using System;
using System.Collections.Generic;

namespace CPE262_FINAL_PROJECT.Repositories
{
    public class CitizenReportRepository
    {
        public int Insert(CitizenReport report)
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO Citizen_Reports
                    (ReporterID, FullName, Phone, Address, Barangay, Status, IsVerified)
                VALUES
                    (@reporterId, @name, @phone, @addr, @brgy, @status, @verified);
                SELECT LAST_INSERT_ID();";
            cmd.Parameters.AddWithValue("@reporterId", report.ReporterID);
            cmd.Parameters.AddWithValue("@name",       report.FullName.Trim());
            cmd.Parameters.AddWithValue("@phone",      report.Phone.Trim());
            cmd.Parameters.AddWithValue("@addr",       report.Address.Trim());
            cmd.Parameters.AddWithValue("@brgy",       report.Barangay.Trim());
            cmd.Parameters.AddWithValue("@status",     string.IsNullOrEmpty(report.Status)
                ? "Pending" : report.Status);
            cmd.Parameters.AddWithValue("@verified",   report.IsVerified ? 1 : 0);
            return Convert.ToInt32(cmd.ExecuteScalar()!);
        }

        public List<CitizenReport> GetAll()
        {
            var list = new List<CitizenReport>();
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT ReportID, ReporterID, FullName, Phone, Address, Barangay,
                       Status, IsVerified,
                       DATE_FORMAT(SubmittedAt, '%Y-%m-%d %H:%i:%s') AS SubmittedAt
                FROM Citizen_Reports
                WHERE HiddenFromBfp = 0
                ORDER BY SubmittedAt DESC";
            using var r = cmd.ExecuteReader();
            while (r.Read()) list.Add(Map(r));
            return list;
        }

        public List<CitizenReport> GetAllForBarangayInbox()
        {
            var list = new List<CitizenReport>();
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT ReportID, ReporterID, FullName, Phone, Address, Barangay,
                       Status, IsVerified,
                       DATE_FORMAT(SubmittedAt, '%Y-%m-%d %H:%i:%s') AS SubmittedAt
                FROM Citizen_Reports
                WHERE HiddenFromBarangay = 0
                ORDER BY SubmittedAt DESC";
            using var r = cmd.ExecuteReader();
            while (r.Read()) list.Add(Map(r));
            return list;
        }

        public List<CitizenReport> GetByBarangay(string barangay)
        {
            var list = new List<CitizenReport>();
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT ReportID, ReporterID, FullName, Phone, Address, Barangay,
                       Status, IsVerified,
                       DATE_FORMAT(SubmittedAt, '%Y-%m-%d %H:%i:%s') AS SubmittedAt
                FROM Citizen_Reports
                WHERE Barangay = @b AND HiddenFromBarangay = 0
                ORDER BY SubmittedAt DESC";
            cmd.Parameters.AddWithValue("@b", barangay);
            using var r = cmd.ExecuteReader();
            while (r.Read()) list.Add(Map(r));
            return list;
        }

        public List<CitizenReport> GetByReporter(int userId)
        {
            var list = new List<CitizenReport>();
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT ReportID, ReporterID, FullName, Phone, Address, Barangay,
                       Status, IsVerified,
                       DATE_FORMAT(SubmittedAt, '%Y-%m-%d %H:%i:%s') AS SubmittedAt
                FROM Citizen_Reports
                WHERE ReporterID = @id
                ORDER BY SubmittedAt DESC";
            cmd.Parameters.AddWithValue("@id", userId);
            using var r = cmd.ExecuteReader();
            while (r.Read()) list.Add(Map(r));
            return list;
        }

        public void SetVerified(int reportId)
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                UPDATE Citizen_Reports
                SET IsVerified = 1, Status = 'Verified'
                WHERE ReportID = @id";
            cmd.Parameters.AddWithValue("@id", reportId);
            cmd.ExecuteNonQuery();
        }

        public void DismissFromBfpInbox(int reportId)
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                UPDATE Citizen_Reports
                SET HiddenFromBfp = 1
                WHERE ReportID = @id AND IsVerified = 1";
            cmd.Parameters.AddWithValue("@id", reportId);
            cmd.ExecuteNonQuery();
        }

        public void DismissFromBarangayInbox(int reportId)
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                UPDATE Citizen_Reports
                SET HiddenFromBarangay = 1
                WHERE ReportID = @id AND IsVerified = 1";
            cmd.Parameters.AddWithValue("@id", reportId);
            cmd.ExecuteNonQuery();
        }

        private static CitizenReport Map(MySqlDataReader r) => new()
        {
            ReportID    = r.GetInt32(0),
            ReporterID  = r.GetInt32(1),
            FullName    = r.GetString(2),
            Phone       = r.GetString(3),
            Address     = r.GetString(4),
            Barangay    = r.GetString(5),
            Status      = r.IsDBNull(6) ? "Pending" : r.GetString(6),
            IsVerified  = Convert.ToInt32(r.GetValue(7)) == 1,
            SubmittedAt = r.IsDBNull(8) ? "" : r.GetString(8)
        };
    }
}
