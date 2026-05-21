-- ============================================================
-- FIRETRACK — Seed Evacuation Centers (one per barangay)
-- Run in MySQL Workbench or via mysql CLI
-- ============================================================
USE firetrack;

-- Step 1: Insert centers (80 barangays, GPS = polygon centroid)
INSERT INTO Evacuation_Centers
  (Name, Barangay, GPSLat, GPSLong, Capacity, CurrentOccupancy, CenterType, IsFull, LastUpdated)
VALUES
  ('Adlaon Elementary School', 'Adlaon', 10.446809, 123.874613, 300, 0, 'Barangay', 0, NOW()),
  ('Agsungot Barangay Hall Evacuation Area', 'Agsungot', 10.440094, 123.905371, 180, 0, 'Barangay', 0, NOW()),
  ('Apas Covered Court', 'Apas', 10.340049, 123.903914, 180, 0, 'Barangay', 0, NOW()),
  ('Babag Elementary School', 'Babag', 10.372943, 123.855222, 300, 0, 'Barangay', 0, NOW()),
  ('Bacayan Covered Court', 'Bacayan', 10.381342, 123.921432, 180, 0, 'Barangay', 0, NOW()),
  ('Banilad Elementary School', 'Banilad', 10.352021, 123.904011, 300, 0, 'Barangay', 0, NOW()),
  ('Basak Pardo Covered Court', 'Basak Pardo', 10.282173, 123.868734, 180, 0, 'Barangay', 0, NOW()),
  ('Basak San Nicolas Community Gym', 'Basak San Nicolas', 10.287067, 123.869285, 300, 0, 'Barangay', 0, NOW()),
  ('Binaliw Barangay Hall', 'Binaliw', 10.420165, 123.916036, 180, 0, 'Barangay', 0, NOW()),
  ('Bonbon Barangay Hall Evacuation Area', 'Bonbon', 10.366141, 123.820096, 300, 0, 'Barangay', 0, NOW()),
  ('Budla-an Elementary School', 'Budla-an', 10.373309, 123.897831, 180, 0, 'Barangay', 0, NOW()),
  ('Buhisan Covered Court', 'Buhisan', 10.310994, 123.855129, 300, 0, 'Barangay', 0, NOW()),
  ('Bulacao Elementary School', 'Bulacao', 10.281166, 123.843518, 300, 0, 'Barangay', 0, NOW()),
  ('Buot-Taup Pardo Multi-Purpose Hall', 'Buot-Taup Pardo', 10.335771, 123.806827, 120, 0, 'Barangay', 0, NOW()),
  ('Busay Barangay Hall Evacuation Area', 'Busay', 10.352221, 123.893945, 300, 0, 'Barangay', 0, NOW()),
  ('Calamba Covered Court', 'Calamba', 10.303301, 123.887103, 120, 0, 'Barangay', 0, NOW()),
  ('Cambinocot Barangay Hall', 'Cambinocot', 10.461043, 123.899853, 180, 0, 'Barangay', 0, NOW()),
  ('Camputhaw Multi-Purpose Gym', 'Camputhaw', 10.316723, 123.900039, 300, 0, 'Barangay', 0, NOW()),
  ('Capitol Site Community Hall', 'Capitol Site', 10.31663, 123.89048, 180, 0, 'Barangay', 0, NOW()),
  ('Carreta Multi-Purpose Gym', 'Carreta', 10.308672, 123.913288, 300, 0, 'Barangay', 0, NOW()),
  ('Central Elementary School', 'Central', 10.294934, 123.900981, 120, 0, 'Barangay', 0, NOW()),
  ('Cogon Pardo Covered Court', 'Cogon Pardo', 10.272745, 123.865605, 180, 0, 'Barangay', 0, NOW()),
  ('Cogon Ramos Barangay Hall', 'Cogon Ramos', 10.307303, 123.898468, 180, 0, 'Barangay', 0, NOW()),
  ('Day-as Multi-Purpose Hall', 'Day-as', 10.301914, 123.902554, 120, 0, 'Barangay', 0, NOW()),
  ('Duljo-Fatima Elementary School', 'Duljo', 10.292425, 123.884412, 180, 0, 'Barangay', 0, NOW()),
  ('Ermita Covered Court', 'Ermita', 10.291445, 123.898474, 120, 0, 'Barangay', 0, NOW()),
  ('Guadalupe National High School', 'Guadalupe', 10.318083, 123.878927, 300, 0, 'Barangay', 0, NOW()),
  ('Guba Elementary School', 'Guba', 10.425961, 123.893733, 300, 0, 'Barangay', 0, NOW()),
  ('Hippodromo Community Covered Court', 'Hippodromo', 10.314207, 123.907401, 180, 0, 'Barangay', 0, NOW()),
  ('Inayawan Elementary School', 'Inayawan', 10.26673, 123.86313, 300, 0, 'Barangay', 0, NOW()),
  ('Kalubihan Barangay Hall', 'Kalubihan', 10.297035, 123.898264, 180, 0, 'Barangay', 0, NOW()),
  ('Kalunasan Covered Court', 'Kalunasan', 10.331422, 123.884204, 180, 0, 'Barangay', 0, NOW()),
  ('Kamagayan Community Hall', 'Kamagayan', 10.299611, 123.900009, 120, 0, 'Barangay', 0, NOW()),
  ('Kasambagan Elementary School', 'Kasambagan', 10.332023, 123.915706, 300, 0, 'Barangay', 0, NOW()),
  ('Kinasang-an Pardo Barangay Hall', 'Kinasang-an Pardo', 10.285566, 123.857511, 300, 0, 'Barangay', 0, NOW()),
  ('Labangon National High School', 'Labangon', 10.302439, 123.879554, 300, 0, 'Barangay', 0, NOW()),
  ('Lahug Elementary School', 'Lahug', 10.330913, 123.896047, 300, 0, 'Barangay', 0, NOW()),
  ('Lorega-San Miguel Covered Court', 'Lorega', 10.307419, 123.904886, 120, 0, 'Barangay', 0, NOW()),
  ('Lusaran Barangay Hall', 'Lusaran', 10.479759, 123.887196, 180, 0, 'Barangay', 0, NOW()),
  ('Luz Elementary School', 'Luz', 10.318622, 123.906724, 180, 0, 'Barangay', 0, NOW()),
  ('Mabini Elementary School', 'Mabini', 10.455443, 123.916011, 180, 0, 'Barangay', 0, NOW()),
  ('Mabolo National High School', 'Mabolo', 10.315772, 123.917201, 300, 0, 'Barangay', 0, NOW()),
  ('Malubog Barangay Hall', 'Malubog', 10.390609, 123.868654, 300, 0, 'Barangay', 0, NOW()),
  ('Mambaling Elementary School', 'Mambaling', 10.288882, 123.880835, 300, 0, 'Barangay', 0, NOW()),
  ('Pahina Central Covered Court', 'Pahina Central', 10.295743, 123.894071, 120, 0, 'Barangay', 0, NOW()),
  ('Pahina San Nicolas Community Hall', 'Pahina San Nicolas', 10.29409, 123.893362, 120, 0, 'Barangay', 0, NOW()),
  ('Pamutan Barangay Hall', 'Pamutan', 10.346828, 123.836636, 300, 0, 'Barangay', 0, NOW()),
  ('Pardo Elementary School', 'Pardo', 10.286465, 123.849569, 300, 0, 'Barangay', 0, NOW()),
  ('Pari-an Multi-Purpose Hall', 'Pari-an', 10.298906, 123.90238, 120, 0, 'Barangay', 0, NOW()),
  ('Paril Barangay Hall Evacuation Area', 'Paril', 10.473284, 123.917607, 180, 0, 'Barangay', 0, NOW()),
  ('Pasil Covered Court', 'Pasil', 10.290955, 123.89499, 120, 0, 'Barangay', 0, NOW()),
  ('Pit-os Elementary School', 'Pit-os', 10.401072, 123.917994, 180, 0, 'Barangay', 0, NOW()),
  ('Pulangbato Covered Court', 'Pulangbato', 10.398191, 123.909991, 180, 0, 'Barangay', 0, NOW()),
  ('Pung-ol-Sibugay Barangay Hall', 'Pung-ol-Sibugay', 10.400832, 123.847993, 300, 0, 'Barangay', 0, NOW()),
  ('Punta Princesa Covered Court', 'Punta Princesa', 10.296129, 123.870776, 180, 0, 'Barangay', 0, NOW()),
  ('Quiot Pardo Barangay Hall', 'Quiot Pardo', 10.289398, 123.858411, 180, 0, 'Barangay', 0, NOW()),
  ('Sambag I Elementary School', 'Sambag I', 10.301157, 123.891866, 180, 0, 'Barangay', 0, NOW()),
  ('Sambag II Elementary School', 'Sambag II', 10.304875, 123.890557, 180, 0, 'Barangay', 0, NOW()),
  ('San Antonio Covered Court', 'San Antonio', 10.301887, 123.898821, 120, 0, 'Barangay', 0, NOW()),
  ('San Jose Elementary School', 'San Jose', 10.378822, 123.912702, 180, 0, 'Barangay', 0, NOW()),
  ('San Nicolas Central Community Gym', 'San Nicolas Central', 10.295365, 123.88878, 180, 0, 'Barangay', 0, NOW()),
  ('San Roque Covered Court', 'San Roque', 10.294775, 123.905037, 180, 0, 'Barangay', 0, NOW()),
  ('Santa Cruz Barangay Hall', 'Santa Cruz', 10.305682, 123.896227, 120, 0, 'Barangay', 0, NOW()),
  ('Sapangdaku Elementary School', 'Sapangdaku', 10.339748, 123.867818, 300, 0, 'Barangay', 0, NOW()),
  ('Sawang Calero Covered Court', 'Sawang Calero', 10.290575, 123.888568, 120, 0, 'Barangay', 0, NOW()),
  ('Sinsin Barangay Hall Evacuation Area', 'Sinsin', 10.343597, 123.786932, 300, 0, 'Barangay', 0, NOW()),
  ('Sirao Barangay Hall', 'Sirao', 10.409275, 123.879002, 300, 0, 'Barangay', 0, NOW()),
  ('Suba Pob. Covered Court', 'Suba Pob.', 10.290826, 123.893112, 120, 0, 'Barangay', 0, NOW()),
  ('Sudlon I Barangay Hall', 'Sudlon I', 10.364569, 123.785447, 120, 0, 'Barangay', 0, NOW()),
  ('Sudlon II Elementary School', 'Sudlon II', 10.385456, 123.798106, 300, 0, 'Barangay', 0, NOW()),
  ('T. Padilla Multi-Purpose Hall', 'T. Padilla', 10.303091, 123.905046, 120, 0, 'Barangay', 0, NOW()),
  ('Tabunan Barangay Hall Evacuation Area', 'Tabunan', 10.429272, 123.817661, 300, 0, 'Barangay', 0, NOW()),
  ('Tagbao Elementary School', 'Tagbao', 10.452637, 123.845376, 300, 0, 'Barangay', 0, NOW()),
  ('Talamban National High School', 'Talamban', 10.368266, 123.916548, 300, 0, 'Barangay', 0, NOW()),
  ('Taptap Barangay Hall', 'Taptap', 10.420503, 123.85452, 300, 0, 'Barangay', 0, NOW()),
  ('Tejero Covered Court', 'Tejero', 10.30259, 123.909762, 120, 0, 'Barangay', 0, NOW()),
  ('Tinago Multi-Purpose Hall', 'Tinago', 10.298658, 123.907787, 120, 0, 'Barangay', 0, NOW()),
  ('Tisa Elementary School', 'Tisa', 10.304801, 123.868439, 300, 0, 'Barangay', 0, NOW()),
  ('To-ong Pardo Barangay Hall', 'To-ong Pardo', 10.316685, 123.829197, 120, 0, 'Barangay', 0, NOW()),
  ('Zapatera Covered Court', 'Zapatera', 10.30708, 123.901882, 120, 0, 'Barangay', 0, NOW());

-- Step 2: Mark all centers as chosen so they appear ACTIVE on each barangay's dashboard
-- (sets isChosen=true → red solid pin instead of 40%-opacity ghost pin)
INSERT IGNORE INTO Barangay_Chosen_Centers (Barangay, CenterID)
SELECT ec.Barangay, ec.CenterID
FROM Evacuation_Centers ec
WHERE ec.CenterType = 'Barangay';

-- ─────────────────────────────
-- Verify
SELECT COUNT(*) AS total_centers FROM Evacuation_Centers WHERE CenterType='Barangay';
SELECT COUNT(*) AS chosen_links  FROM Barangay_Chosen_Centers;
