using MySqlConnector;
using System;

namespace CPE262_FINAL_PROJECT.Database
{
    public static class DatabaseHelper
    {
        private const string _serverConnStr =
            "Server=localhost;Port=3306;User=root;Password=Firetrack@2026;CharSet=utf8mb4;";

        private const string _connStr =
            "Server=localhost;Port=3306;Database=firetrack;User=root;Password=Firetrack@2026;CharSet=utf8mb4;";

        public static MySqlConnection GetConnection()
            => new MySqlConnection(_connStr);

        public static void InitializeDatabase()
        {
            using (var serverConn = new MySqlConnection(_serverConnStr))
            {
                serverConn.Open();
                Execute(serverConn,
                    "CREATE DATABASE IF NOT EXISTS `firetrack` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;");
            }

            using var conn = GetConnection();
            conn.Open();

            Execute(conn, @"
                CREATE TABLE IF NOT EXISTS Barangays (
                    BarangayID INT          PRIMARY KEY AUTO_INCREMENT,
                    Name       VARCHAR(120) UNIQUE NOT NULL
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;");

            Execute(conn, @"
                CREATE TABLE IF NOT EXISTS Users (
                    UserID           INT          PRIMARY KEY AUTO_INCREMENT,
                    FullName         VARCHAR(150) NOT NULL,
                    Role             VARCHAR(50)  NOT NULL,
                    Email            VARCHAR(150) UNIQUE NOT NULL,
                    PasswordHash     VARCHAR(64)  NOT NULL,
                    IsActive         TINYINT(1)   DEFAULT 1,
                    FailedAttempts   INT          DEFAULT 0,
                    LockedUntil      DATETIME     NULL,
                    AssignedBarangay VARCHAR(120) NULL,
                    CreatedAt        DATETIME     DEFAULT CURRENT_TIMESTAMP
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;");

            Execute(conn, @"
                CREATE TABLE IF NOT EXISTS Evacuation_Centers (
                    CenterID         INT          PRIMARY KEY AUTO_INCREMENT,
                    Name             VARCHAR(150) NOT NULL,
                    Barangay         VARCHAR(120) NOT NULL,
                    Capacity         INT          NOT NULL,
                    CurrentOccupancy INT          DEFAULT 0,
                    LastUpdated      DATETIME     DEFAULT CURRENT_TIMESTAMP
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;");

            Execute(conn, @"
                CREATE TABLE IF NOT EXISTS Incidents (
                    IncidentID   INT          PRIMARY KEY AUTO_INCREMENT,
                    Barangay     VARCHAR(120) NOT NULL,
                    Sitio        VARCHAR(200) NOT NULL,
                    GPSLat       DOUBLE       NOT NULL,
                    GPSLong      DOUBLE       NOT NULL,
                    AlarmLevel   INT          NOT NULL,
                    DateTime     VARCHAR(30)  NOT NULL,
                    CauseOfFire  VARCHAR(100) NULL,
                    PhotoPath    VARCHAR(500) NOT NULL,
                    Status       VARCHAR(30)  DEFAULT 'Active',
                    DSDWStatus   VARCHAR(30)  DEFAULT 'Pending',
                    RegisteredBy INT          NULL,
                    FOREIGN KEY (RegisteredBy) REFERENCES Users(UserID)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;");

            Execute(conn, @"
                CREATE TABLE IF NOT EXISTS Families (
                    FamilyID           INT          PRIMARY KEY AUTO_INCREMENT,
                    IncidentID         INT          NOT NULL,
                    HeadName           VARCHAR(150) NOT NULL,
                    MemberCount        INT          NOT NULL,
                    EvacuationCenterID INT          NULL,
                    ReliefStatus       VARCHAR(30)  DEFAULT 'Pending',
                    IsRepeatDisplaced  TINYINT(1)   DEFAULT 0,
                    FOREIGN KEY (IncidentID)         REFERENCES Incidents(IncidentID),
                    FOREIGN KEY (EvacuationCenterID) REFERENCES Evacuation_Centers(CenterID)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;");

            Execute(conn, @"
                CREATE TABLE IF NOT EXISTS Relief_Records (
                    RecordID        INT          PRIMARY KEY AUTO_INCREMENT,
                    FamilyID        INT          NOT NULL,
                    AgencyName      VARCHAR(150) NOT NULL,
                    ItemType        VARCHAR(100) NOT NULL,
                    Quantity        INT          NOT NULL,
                    DateDistributed VARCHAR(30)  NOT NULL,
                    DistributedBy   INT          NULL,
                    FOREIGN KEY (FamilyID)      REFERENCES Families(FamilyID),
                    FOREIGN KEY (DistributedBy) REFERENCES Users(UserID)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;");

            Execute(conn, @"
                CREATE TABLE IF NOT EXISTS Audit_Logs (
                    LogID       INT          PRIMARY KEY AUTO_INCREMENT,
                    UserID      INT          NOT NULL,
                    Action      VARCHAR(50)  NOT NULL,
                    TargetTable VARCHAR(50)  NULL,
                    TargetID    INT          NULL,
                    Reason      VARCHAR(500) NULL,
                    Timestamp   DATETIME     DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY (UserID) REFERENCES Users(UserID)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;");

            MigrateEvacSchema(conn);

            SeedDefaultAdmin(conn);
            SeedBarangays(conn);
        }

        private static void Execute(MySqlConnection conn, string sql)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
        }

        private static bool ColumnExists(MySqlConnection conn, string tableName, string columnName)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"SELECT COUNT(*) FROM information_schema.COLUMNS
                WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME=@table
                AND COLUMN_NAME=@column";
            cmd.Parameters.AddWithValue("@table", tableName);
            cmd.Parameters.AddWithValue("@column", columnName);
            return Convert.ToInt64(cmd.ExecuteScalar()!) > 0;
        }

        private static void AddColumnIfMissing(
            MySqlConnection conn, string tableName, string columnName, string definition)
        {
            if (!ColumnExists(conn, tableName, columnName))
            {
                Execute(conn, $"ALTER TABLE `{tableName}` ADD COLUMN `{columnName}` {definition}");
            }
        }

        private static void SeedDefaultAdmin(MySqlConnection conn)
        {
            var check = conn.CreateCommand();
            check.CommandText = "SELECT COUNT(*) FROM Users WHERE Role='Admin'";
            if (Convert.ToInt64(check.ExecuteScalar()!) > 0) return;

            var ins = conn.CreateCommand();
            ins.CommandText = @"
                INSERT INTO Users (FullName, Role, Email, PasswordHash)
                VALUES ('System Admin','Admin','admin@firetrack.com',
                        '8c6976e5b5410415bde908bd4dee15dfb167a9c873fc4bb8a81f6f2ab448a918')";
            ins.ExecuteNonQuery();
        }

        private static void SeedBarangays(MySqlConnection conn)
        {
            var check = conn.CreateCommand();
            check.CommandText = "SELECT COUNT(*) FROM Barangays";
            if (Convert.ToInt64(check.ExecuteScalar()!) > 0) return;

            var ins = conn.CreateCommand();
            ins.CommandText = @"
                INSERT IGNORE INTO Barangays (Name) VALUES
                ('Apas'),('Banilad'),('Budlaan'),('Busay'),
                ('Capitol Site'),('Carreta'),('Central'),('Cogon Ramos'),
                ('Day-as'),('Ermita'),('Forbes'),('Guba'),
                ('Hippodromo'),('Kamputhaw'),('Kasambagan'),('Lahug'),
                ('Lorega San Miguel'),('Luz'),('Mabini'),('Mabolo'),
                ('Nasipit'),('Pahina Central'),('Pahina San Nicolas'),
                ('Pit-os'),('Pulangbato'),('Sambag I'),('Sambag II'),
                ('San Antonio'),('San Jose'),('San Roque'),
                ('Santa Cruz'),('Santo Nino'),('Sirao'),
                ('T. Padilla'),('Talamban'),('Tejero'),
                ('Tinago'),('Zapatera'),
                ('Bacayan'),('Basak Pardo'),('Basak San Nicolas'),
                ('Binaliw'),('Bonbon'),('Bulacao'),
                ('Buot-Taup Pardo'),('Calamba'),
                ('Carbon'),('Cogon Pardo'),('Duljo-Fatima'),
                ('Guadalupe'),('Inayawan'),('Kalubihan'),
                ('Kinasang-an Pardo'),('Labangon'),('Mambaling'),
                ('Pamutan'),('Pardo'),('Pasil'),
                ('Poblacion Pardo'),('Pung-ol-Sibugay'),
                ('Quiot Pardo'),('San Martin'),('Sapangdako'),
                ('Suba'),('Sudlon I'),('Sudlon II'),
                ('Tabok'),('Taptap'),('Tisa'),('Toong'),
                ('Buhisan'),('Bulacao Pardo'),('Cogon Cruz'),('Lusaran'),
                ('Cambinocot'),('Pari-an'),('San Nicolas Proper'),
                ('Sawang Calero'),('Sto. Nino'),
                ('Paril'),('Sinsin'),
                ('Babag'),('Kalunasan'),('Pakna-an');";
            ins.ExecuteNonQuery();
        }

        private static void MigrateEvacSchema(MySqlConnection conn)
        {
            foreach (var col in new[] {
                ("GPSLat",     "DOUBLE NULL"),
                ("GPSLong",    "DOUBLE NULL"),
                ("CenterType", "VARCHAR(20) NOT NULL DEFAULT 'Barangay'"),
                ("IsFull",     "TINYINT(1) DEFAULT 0")
            })
            {
                AddColumnIfMissing(conn, "Evacuation_Centers", col.Item1, col.Item2);
            }

            AddColumnIfMissing(conn, "Users", "PhoneNumber", "VARCHAR(20) NULL AFTER AssignedBarangay");

            Execute(conn, @"
                CREATE TABLE IF NOT EXISTS Cross_Barangay_Requests (
                    RequestID         INT          PRIMARY KEY AUTO_INCREMENT,
                    RequesterUserID   INT          NOT NULL,
                    RequesterBarangay VARCHAR(120) NOT NULL,
                    TargetCenterID    INT          NOT NULL,
                    Status            VARCHAR(20)  NOT NULL DEFAULT 'Pending',
                    Reason            VARCHAR(500) NULL,
                    RequestedAt       DATETIME     DEFAULT CURRENT_TIMESTAMP,
                    ResolvedAt        DATETIME     NULL,
                    FOREIGN KEY (RequesterUserID) REFERENCES Users(UserID),
                    FOREIGN KEY (TargetCenterID)  REFERENCES Evacuation_Centers(CenterID)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;");

            Execute(conn, @"
                CREATE TABLE IF NOT EXISTS Barangay_Chosen_Centers (
                    BarangayCenterID INT          NOT NULL AUTO_INCREMENT,
                    Barangay         VARCHAR(100) NOT NULL,
                    CenterID         INT          NOT NULL,
                    PRIMARY KEY (BarangayCenterID),
                    UNIQUE KEY uq_brgy_center (Barangay, CenterID),
                    CONSTRAINT fk_bcc_center FOREIGN KEY (CenterID)
                        REFERENCES Evacuation_Centers (CenterID)
                        ON DELETE CASCADE
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;");

            Execute(conn, @"
                CREATE TABLE IF NOT EXISTS Citizen_Reports (
                    ReportID    INT          PRIMARY KEY AUTO_INCREMENT,
                    ReporterID  INT          NOT NULL,
                    FullName    VARCHAR(150) NOT NULL,
                    Phone       VARCHAR(20)  NOT NULL,
                    Address     VARCHAR(300) NOT NULL,
                    Barangay    VARCHAR(120) NOT NULL,
                    Status      VARCHAR(30)  DEFAULT 'Pending',
                    IsVerified  TINYINT(1)   DEFAULT 0,
                    HiddenFromBfp TINYINT(1) DEFAULT 0,
                    HiddenFromBarangay TINYINT(1) DEFAULT 0,
                    SubmittedAt DATETIME     DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY (ReporterID) REFERENCES Users(UserID)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;");

            foreach (var col in new[] {
                ("HiddenFromBfp",      "TINYINT(1) DEFAULT 0"),
                ("HiddenFromBarangay", "TINYINT(1) DEFAULT 0")
            })
            {
                AddColumnIfMissing(conn, "Citizen_Reports", col.Item1, col.Item2);
            }

            Execute(conn, @"
                CREATE TABLE IF NOT EXISTS DSWD_Messages (
                    MessageID       INT          PRIMARY KEY AUTO_INCREMENT,
                    SenderID        INT          NOT NULL,
                    IncidentID      INT          NOT NULL,
                    Message         TEXT         NOT NULL,
                    Status          VARCHAR(20)  NOT NULL DEFAULT 'Pending',
                    RejectionReason VARCHAR(500) NULL,
                    HiddenFromDswd  TINYINT(1)   DEFAULT 0,
                    HiddenFromCitizen TINYINT(1) DEFAULT 0,
                    SentAt          DATETIME     DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY (SenderID)   REFERENCES Users(UserID),
                    FOREIGN KEY (IncidentID) REFERENCES Incidents(IncidentID)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;");

            foreach (var (col, def) in new[]
            {
                ("Status",          "VARCHAR(20) NOT NULL DEFAULT 'Pending'"),
                ("RejectionReason", "VARCHAR(500) NULL"),
                ("HiddenFromDswd",  "TINYINT(1) DEFAULT 0"),
                ("HiddenFromCitizen", "TINYINT(1) DEFAULT 0")
            })
            {
                AddColumnIfMissing(conn, "DSWD_Messages", col, def);
            }
        }
    }
}
