-- ============================================================
-- FIRETRACK cleanup: unused/ghost ERD objects
-- Date: 2026-05-02
--
-- Removes objects that are no longer used by runtime code:
--   Tables: DSWD_Households, Notifications, Cross_Barangay_Assignments
--   Columns: Barangays.District, Barangay_Chosen_Centers.ChosenAt
--
-- Precaution: live table data is copied into backup tables first.
-- Re-run safe for the DROP steps; backup INSERTs ignore duplicates.
-- ============================================================

USE firetrack;

DROP PROCEDURE IF EXISTS firetrack_backup_table_if_exists;
DROP PROCEDURE IF EXISTS firetrack_drop_table_if_exists;
DROP PROCEDURE IF EXISTS firetrack_drop_column_if_exists;

DELIMITER $$

CREATE PROCEDURE firetrack_backup_table_if_exists(
    IN source_table VARCHAR(128),
    IN backup_table VARCHAR(128)
)
BEGIN
    IF EXISTS (
        SELECT 1
        FROM information_schema.TABLES
        WHERE TABLE_SCHEMA = DATABASE()
          AND TABLE_NAME = source_table
    ) THEN
        SET @create_sql = CONCAT(
            'CREATE TABLE IF NOT EXISTS `', backup_table, '` LIKE `', source_table, '`'
        );
        PREPARE create_stmt FROM @create_sql;
        EXECUTE create_stmt;
        DEALLOCATE PREPARE create_stmt;

        SET @copy_sql = CONCAT(
            'INSERT IGNORE INTO `', backup_table, '` SELECT * FROM `', source_table, '`'
        );
        PREPARE copy_stmt FROM @copy_sql;
        EXECUTE copy_stmt;
        DEALLOCATE PREPARE copy_stmt;
    END IF;
END$$

CREATE PROCEDURE firetrack_drop_table_if_exists(IN table_name VARCHAR(128))
BEGIN
    IF EXISTS (
        SELECT 1
        FROM information_schema.TABLES
        WHERE TABLE_SCHEMA = DATABASE()
          AND TABLE_NAME = table_name
    ) THEN
        SET @drop_sql = CONCAT('DROP TABLE `', table_name, '`');
        PREPARE drop_stmt FROM @drop_sql;
        EXECUTE drop_stmt;
        DEALLOCATE PREPARE drop_stmt;
    END IF;
END$$

CREATE PROCEDURE firetrack_drop_column_if_exists(
    IN table_name VARCHAR(128),
    IN column_name VARCHAR(128)
)
BEGIN
    IF EXISTS (
        SELECT 1
        FROM information_schema.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE()
          AND TABLE_NAME = table_name
          AND COLUMN_NAME = column_name
    ) THEN
        SET @drop_col_sql = CONCAT(
            'ALTER TABLE `', table_name, '` DROP COLUMN `', column_name, '`'
        );
        PREPARE drop_col_stmt FROM @drop_col_sql;
        EXECUTE drop_col_stmt;
        DEALLOCATE PREPARE drop_col_stmt;
    END IF;
END$$

DELIMITER ;

CALL firetrack_backup_table_if_exists('DSWD_Households', '_backup_DSWD_Households_20260502');
CALL firetrack_backup_table_if_exists('Notifications', '_backup_Notifications_20260502');
CALL firetrack_backup_table_if_exists('Cross_Barangay_Assignments', '_backup_Cross_Barangay_Assignments_20260502');

CALL firetrack_drop_table_if_exists('Cross_Barangay_Assignments');
CALL firetrack_drop_table_if_exists('Notifications');
CALL firetrack_drop_table_if_exists('DSWD_Households');

CALL firetrack_drop_column_if_exists('Barangays', 'District');
CALL firetrack_drop_column_if_exists('Barangay_Chosen_Centers', 'ChosenAt');

DROP PROCEDURE IF EXISTS firetrack_backup_table_if_exists;
DROP PROCEDURE IF EXISTS firetrack_drop_table_if_exists;
DROP PROCEDURE IF EXISTS firetrack_drop_column_if_exists;
