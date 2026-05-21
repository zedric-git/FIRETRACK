using CPE262_FINAL_PROJECT.Database;
using CPE262_FINAL_PROJECT.Models;
using MySqlConnector;
using System;
using System.Collections.Generic;

namespace CPE262_FINAL_PROJECT.Repositories
{
    public class UserRepository
    {
        private const int MaxFailedAttempts = 3;
        private const int LockoutMinutes    = 15;

        public (User? user, string? error) Authenticate(string email, string plainPassword)
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();

            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT UserID, FullName, Role, Email, PasswordHash,
                       IsActive, FailedAttempts, LockedUntil, AssignedBarangay, PhoneNumber
                FROM Users WHERE LOWER(TRIM(Email)) = LOWER(TRIM(@e))";
            cmd.Parameters.AddWithValue("@e", email);

            using var r = cmd.ExecuteReader();
            if (!r.Read()) return (null, "No account found with that email.");

            var userId         = r.GetInt32(0);
            var fullName       = r.GetString(1);
            var role           = r.GetString(2);
            var storedEmail    = r.GetString(3);
            var storedHash     = r.GetString(4);
            var isActive       = Convert.ToInt32(r.GetValue(5)) == 1;
            var failedAttempts = r.GetInt32(6);
            var lockedUntil    = r.IsDBNull(7) ? (DateTime?)null : r.GetDateTime(7);
            var barangay       = r.IsDBNull(8) ? "" : r.GetString(8);
            var phoneNumber    = r.IsDBNull(9) ? "" : r.GetString(9);
            r.Close();

            if (!isActive)
                return (null, "This account has been deactivated. Contact your administrator.");

            if (lockedUntil.HasValue && DateTime.UtcNow < lockedUntil.Value)
            {
                var remaining = (int)(lockedUntil.Value - DateTime.UtcNow).TotalMinutes + 1;
                return (null, $"Account locked. Try again in {remaining} minute(s).");
            }

            if (lockedUntil.HasValue && DateTime.UtcNow >= lockedUntil.Value)
            {
                ResetFailedAttempts(conn, userId);
                failedAttempts = 0;
            }

            if (HashPassword(plainPassword) != storedHash)
            {
                failedAttempts++;
                if (failedAttempts >= MaxFailedAttempts)
                {
                    LockAccount(conn, userId, failedAttempts, DateTime.UtcNow.AddMinutes(LockoutMinutes));
                    return (null, $"Too many failed attempts. Account locked for {LockoutMinutes} minutes.");
                }
                IncrementFailedAttempts(conn, userId, failedAttempts);
                return (null, $"Incorrect password. {MaxFailedAttempts - failedAttempts} attempt(s) remaining.");
            }

            ResetFailedAttempts(conn, userId);
            return (new User
            {
                UserID           = userId,
                FullName         = fullName,
                Role             = role,
                Email            = storedEmail,
                IsActive         = isActive,
                AssignedBarangay = barangay,
                PhoneNumber      = phoneNumber
            }, null);
        }

        public List<User> GetAll(bool excludeAdmins = true)
        {
            var list = new List<User>();
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = excludeAdmins
                ? @"SELECT UserID, FullName, Role, Email, IsActive, AssignedBarangay,
                           DATE_FORMAT(CreatedAt, '%Y-%m-%d %H:%i:%s') AS CreatedAt, PhoneNumber
                    FROM Users WHERE Role != 'Admin' ORDER BY FullName ASC"
                : @"SELECT UserID, FullName, Role, Email, IsActive, AssignedBarangay,
                           DATE_FORMAT(CreatedAt, '%Y-%m-%d %H:%i:%s') AS CreatedAt, PhoneNumber
                    FROM Users ORDER BY FullName ASC";
            using var r = cmd.ExecuteReader();
            while (r.Read()) list.Add(MapFull(r));
            return list;
        }

        public User? GetById(int userId)
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT UserID, FullName, Role, Email, IsActive, AssignedBarangay,
                       DATE_FORMAT(CreatedAt, '%Y-%m-%d %H:%i:%s') AS CreatedAt, PhoneNumber
                FROM Users WHERE UserID = @id";
            cmd.Parameters.AddWithValue("@id", userId);
            using var r = cmd.ExecuteReader();
            if (r.Read()) return MapFull(r);
            return null;
        }

        public (bool success, string? error) Create(User user, string plainPassword)
        {
            try
            {
                using var conn = DatabaseHelper.GetConnection();
                conn.Open();
                var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    INSERT INTO Users (FullName, Role, Email, PasswordHash, AssignedBarangay, PhoneNumber)
                    VALUES (@name, @role, @email, @hash, @barangay, @phone)";
                cmd.Parameters.AddWithValue("@name",     user.FullName.Trim());
                cmd.Parameters.AddWithValue("@role",     user.Role);
                cmd.Parameters.AddWithValue("@email",    user.Email.Trim().ToLower());
                cmd.Parameters.AddWithValue("@hash",     HashPassword(plainPassword));
                cmd.Parameters.AddWithValue("@barangay", string.IsNullOrEmpty(user.AssignedBarangay)
                    ? (object)DBNull.Value : user.AssignedBarangay);
                cmd.Parameters.AddWithValue("@phone",    string.IsNullOrEmpty(user.PhoneNumber)
                    ? (object)DBNull.Value : user.PhoneNumber.Trim());
                cmd.ExecuteNonQuery();
                return (true, null);
            }
            catch (MySqlException ex) when (ex.Number == 1062)
            {
                return (false, "An account with that email already exists.");
            }
        }

        public (bool ok, string? error) UpdateCredentials(int userId, string fullName,
            string email, string role, string? barangay)
        {
            try
            {
                using var conn = DatabaseHelper.GetConnection();
                conn.Open();

                var check = conn.CreateCommand();
                check.CommandText = @"
                    SELECT COUNT(*) FROM Users
                    WHERE LOWER(TRIM(Email)) = LOWER(TRIM(@e)) AND UserID != @id";
                check.Parameters.AddWithValue("@e",  email.Trim());
                check.Parameters.AddWithValue("@id", userId);
                if (Convert.ToInt64(check.ExecuteScalar()!) > 0)
                    return (false, "That email is already used by another account.");

                var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    UPDATE Users
                    SET FullName = @name, Email = @email,
                        Role = @role, AssignedBarangay = @barangay
                    WHERE UserID = @id";
                cmd.Parameters.AddWithValue("@name",     fullName.Trim());
                cmd.Parameters.AddWithValue("@email",    email.Trim().ToLower());
                cmd.Parameters.AddWithValue("@role",     role);
                cmd.Parameters.AddWithValue("@barangay", string.IsNullOrEmpty(barangay)
                    ? (object)DBNull.Value : barangay);
                cmd.Parameters.AddWithValue("@id",       userId);
                cmd.ExecuteNonQuery();
                return (true, null);
            }
            catch (MySqlException ex) when (ex.Number == 1062)
            {
                return (false, "That email is already used by another account.");
            }
        }

        public void UpdateRole(int userId, string newRole, string? barangay = null)
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                UPDATE Users SET Role = @role, AssignedBarangay = @b WHERE UserID = @id";
            cmd.Parameters.AddWithValue("@role", newRole);
            cmd.Parameters.AddWithValue("@b",    (object?)barangay ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@id",   userId);
            cmd.ExecuteNonQuery();
        }

        public void SetActive(int userId, bool isActive)
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE Users SET IsActive = @a WHERE UserID = @id";
            cmd.Parameters.AddWithValue("@a",  isActive ? 1 : 0);
            cmd.Parameters.AddWithValue("@id", userId);
            cmd.ExecuteNonQuery();
        }

        public (bool ok, string? error) Delete(int userId)
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();

            var roleCmd = conn.CreateCommand();
            roleCmd.CommandText = "SELECT Role FROM Users WHERE UserID = @id";
            roleCmd.Parameters.AddWithValue("@id", userId);
            var role = roleCmd.ExecuteScalar() as string;
            if (role == "Admin") return (false, "Admin accounts cannot be deleted.");

            using var tx = conn.BeginTransaction();
            try
            {
                ExecuteScoped(conn, tx, userId,
                    "DELETE FROM Cross_Barangay_Requests WHERE RequesterUserID = @id");

                ExecuteScoped(conn, tx, userId,
                    "DELETE FROM Audit_Logs       WHERE UserID     = @id");
                ExecuteScoped(conn, tx, userId,
                    "DELETE FROM Citizen_Reports  WHERE ReporterID = @id");
                ExecuteScoped(conn, tx, userId,
                    "DELETE FROM DSWD_Messages    WHERE SenderID   = @id");

                ExecuteScoped(conn, tx, userId,
                    "UPDATE Incidents      SET RegisteredBy  = NULL WHERE RegisteredBy  = @id");
                ExecuteScoped(conn, tx, userId,
                    "UPDATE Relief_Records SET DistributedBy = NULL WHERE DistributedBy = @id");

                var cmd = conn.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = "DELETE FROM Users WHERE UserID = @id AND Role != 'Admin'";
                cmd.Parameters.AddWithValue("@id", userId);
                cmd.ExecuteNonQuery();

                tx.Commit();
                return (true, null);
            }
            catch (Exception ex)
            {
                try { tx.Rollback(); } catch {  }
                return (false, $"Delete failed: {ex.Message}");
            }
        }

        private static void ExecuteScoped(MySqlConnection conn, MySqlTransaction tx,
            int userId, string sql)
        {
            var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = sql;
            cmd.Parameters.AddWithValue("@id", userId);
            cmd.ExecuteNonQuery();
        }

        public void ResetPassword(int userId, string newPlainPassword)
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                UPDATE Users SET PasswordHash = @hash, FailedAttempts = 0, LockedUntil = NULL
                WHERE UserID = @id";
            cmd.Parameters.AddWithValue("@hash", HashPassword(newPlainPassword));
            cmd.Parameters.AddWithValue("@id",   userId);
            cmd.ExecuteNonQuery();
        }

        public bool EmailExists(string email)
        {
            using var conn = DatabaseHelper.GetConnection();
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM Users WHERE LOWER(TRIM(Email))=LOWER(TRIM(@e))";
            cmd.Parameters.AddWithValue("@e", email);
            return Convert.ToInt64(cmd.ExecuteScalar()!) > 0;
        }

        private static User MapFull(MySqlDataReader r) => new()
        {
            UserID           = r.GetInt32(0),
            FullName         = r.GetString(1),
            Role             = r.GetString(2),
            Email            = r.GetString(3),
            IsActive         = Convert.ToInt32(r.GetValue(4)) == 1,
            AssignedBarangay = r.IsDBNull(5) ? "" : r.GetString(5),
            CreatedAt        = r.IsDBNull(6) ? "" : r.GetString(6),
            PhoneNumber      = r.IsDBNull(7) ? "" : r.GetString(7)
        };

        private static void IncrementFailedAttempts(MySqlConnection conn, int userId, int n)
        {
            var c = conn.CreateCommand();
            c.CommandText = "UPDATE Users SET FailedAttempts=@c WHERE UserID=@id";
            c.Parameters.AddWithValue("@c",  n);
            c.Parameters.AddWithValue("@id", userId);
            c.ExecuteNonQuery();
        }

        private static void LockAccount(MySqlConnection conn, int userId, int n, DateTime until)
        {
            var c = conn.CreateCommand();
            c.CommandText = "UPDATE Users SET FailedAttempts=@c, LockedUntil=@lu WHERE UserID=@id";
            c.Parameters.AddWithValue("@c",  n);
            c.Parameters.AddWithValue("@lu", until);
            c.Parameters.AddWithValue("@id", userId);
            c.ExecuteNonQuery();
        }

        private static void ResetFailedAttempts(MySqlConnection conn, int userId)
        {
            var c = conn.CreateCommand();
            c.CommandText = "UPDATE Users SET FailedAttempts=0, LockedUntil=NULL WHERE UserID=@id";
            c.Parameters.AddWithValue("@id", userId);
            c.ExecuteNonQuery();
        }

        public static string HashPassword(string plain)
        {
            var bytes = System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(plain));
            var sb = new System.Text.StringBuilder();
            foreach (var b in bytes) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }
}
