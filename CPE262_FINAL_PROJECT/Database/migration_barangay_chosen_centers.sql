-- ============================================================
-- FIRETRACK Migration: Barangay_Chosen_Centers
-- Run this once against your MySQL database before deploying
-- the updated application code.
-- ============================================================

CREATE TABLE IF NOT EXISTS Barangay_Chosen_Centers (
    BarangayCenterID INT          NOT NULL AUTO_INCREMENT,
    Barangay         VARCHAR(100) NOT NULL,
    CenterID         INT          NOT NULL,
    PRIMARY KEY (BarangayCenterID),
    UNIQUE KEY uq_brgy_center (Barangay, CenterID),
    CONSTRAINT fk_bcc_center FOREIGN KEY (CenterID)
        REFERENCES Evacuation_Centers (CenterID)
        ON DELETE CASCADE
);

-- Back-fill: any existing own barangay center that already has
-- occupancy > 0 is considered "already chosen" so existing data
-- continues to show in the Centers tab without manual re-selection.
INSERT IGNORE INTO Barangay_Chosen_Centers (Barangay, CenterID)
SELECT Barangay, CenterID
FROM   Evacuation_Centers
WHERE  CenterType = 'Barangay'
  AND  CurrentOccupancy > 0;
