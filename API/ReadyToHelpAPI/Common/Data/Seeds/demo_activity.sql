-- LOCAL DEVELOPMENT ONLY. Apply AppDbContext migrations and then
-- portuguese_responsible_entities.sql before this script.
-- Fresh database: 3 users, 18 occurrences, 42 reports and 24 feedback entries.
-- Known test credentials: admin@gmail.com / admin (ADMIN),
-- manager@gmail.com / manager (MANAGER), citizen@gmail.com / citizen (CITIZEN).
-- Passwords are BCrypt cost 11 hashes verified with the API's BCrypt.Net library.
-- Reruns preserve existing users and occurrences; no hard-coded database IDs.
-- Timestamps use UTC, matching the schema's timestamp-without-time-zone columns.
BEGIN;
SET LOCAL client_encoding = 'UTF8';
LOCK TABLE users, reports, occurrences, feedback IN SHARE ROW EXCLUSIVE MODE;

INSERT INTO users ("Name", "Email", "Password", "Profile") VALUES
    ('Admin', 'admin@gmail.com', '$2a$11$aRa2kOEhp/tkJb1dXGyvGuUO/KHy03x49gjnfvo6MD9PQKFBSxnTa', 'ADMIN'),
    ('Manager', 'manager@gmail.com', '$2a$11$LdrRa5bSIJA4QhKbMUUV4uXpJ/ccI1oo/BS8PzcBR562w3k2LVakS', 'MANAGER'),
    ('Citizen', 'citizen@gmail.com', '$2a$11$dkPwU06qXfXqFLGOTkEbhuxOPYpodWnqj2FH3.sX7gXUIsy0lFDYe', 'CITIZEN')
ON CONFLICT ("Email") DO NOTHING;

DO $seed$
DECLARE
    scenario record;
    citizen_id integer;
    manager_id integer;
    entity_id integer;
    occurrence_id integer;
    report_id integer;
    first_report_id integer;
    report_number integer;
    reports_total integer;
    seed_title text;
    seed_description text;
    started_at timestamp;
    seed_now timestamp := CURRENT_TIMESTAMP AT TIME ZONE 'UTC';
BEGIN
    -- Never silently promote an existing account with the same email.
    IF EXISTS (
        SELECT 1 FROM (VALUES ('admin@gmail.com', 'ADMIN'),
            ('manager@gmail.com', 'MANAGER'), ('citizen@gmail.com', 'CITIZEN')) expected(email, profile)
        JOIN users u ON u."Email" = expected.email
        WHERE u."Profile" <> expected.profile
    ) THEN
        RAISE EXCEPTION 'A demo email already exists with another profile. No data was changed.';
    END IF;
    SELECT "Id" INTO STRICT citizen_id FROM users WHERE "Email" = 'citizen@gmail.com';
    SELECT "Id" INTO STRICT manager_id FROM users WHERE "Email" = 'manager@gmail.com';

    -- Coordinates lie within the corresponding district. Priority/radius follow
    -- OccurrenceServiceImpl for the report counts used here (1 or 3).
    FOR scenario IN
        SELECT * FROM (VALUES
            (1, 'Aveiro', 40.6405, -8.6455, 'URBAN_FIRE', 'BOMBEIROS', 'HIGH', 3000, 'WAITING', 'Fumo num edifício'),
            (2, 'Beja', 38.0151, -7.8632, 'FLOOD', 'PROTECAO_CIVIL', 'HIGH', 4000, 'ACTIVE', 'Inundação na via pública'),
            (3, 'Braga', 41.5454, -8.4265, 'ROAD_ACCIDENT', 'POLICIA', 'HIGH', 800, 'CLOSED', 'Colisão entre veículos'),
            (4, 'Bragança', 41.806, -6.756, 'PUBLIC_LIGHTING', 'CAMARA_MUNICIPAL', 'LOW', 100, 'WAITING', 'Iluminação pública avariada'),
            (5, 'Castelo Branco', 39.82, -7.49, 'INJURED_ANIMAL', 'SERVICOS_ANIMAL_AMBIENTAL', 'MEDIUM', 450, 'ACTIVE', 'Animal ferido na rua'),
            (6, 'Coimbra', 40.211, -8.429, 'MEDICAL_EMERGENCY', 'INEM', 'HIGH', 2000, 'CLOSED', 'Assistência médica urgente'),
            (7, 'Évora', 38.571, -7.909, 'URBAN_FIRE', 'BOMBEIROS', 'HIGH', 3000, 'ACTIVE', 'Incêndio numa garagem'),
            (8, 'Faro', 37.019, -7.932, 'FLOOD', 'PROTECAO_CIVIL', 'HIGH', 4000, 'CLOSED', 'Acumulação de água na estrada'),
            (9, 'Guarda', 40.537, -7.266, 'TRAFFIC_CONGESTION', 'POLICIA', 'MEDIUM', 300, 'WAITING', 'Trânsito congestionado'),
            (10, 'Leiria', 39.744, -8.807, 'SANITATION', 'CAMARA_MUNICIPAL', 'MEDIUM', 225, 'ACTIVE', 'Contentores a transbordar'),
            (11, 'Lisboa', 38.722, -9.139, 'LOST_ANIMAL', 'SERVICOS_ANIMAL_AMBIENTAL', 'LOW', 250, 'CLOSED', 'Animal perdido encontrado'),
            (12, 'Portalegre', 39.296, -7.431, 'WORK_ACCIDENT', 'INEM', 'HIGH', 600, 'WAITING', 'Acidente num local de trabalho'),
            (13, 'Porto', 41.149, -8.61, 'URBAN_FIRE', 'BOMBEIROS', 'HIGH', 3000, 'CLOSED', 'Incêndio num armazém'),
            (14, 'Santarém', 39.236, -8.686, 'LANDSLIDE', 'PROTECAO_CIVIL', 'HIGH', 1000, 'WAITING', 'Deslizamento de terras'),
            (15, 'Setúbal', 38.524, -8.889, 'PUBLIC_DISTURBANCE', 'POLICIA', 'MEDIUM', 450, 'ACTIVE', 'Perturbação na via pública'),
            (16, 'Viana do Castelo', 41.694, -8.832, 'ROAD_DAMAGE', 'CAMARA_MUNICIPAL', 'LOW', 200, 'CLOSED', 'Pavimento danificado'),
            (17, 'Vila Real', 41.301, -7.744, 'POLLUTION', 'SERVICOS_ANIMAL_AMBIENTAL', 'MEDIUM', 1125, 'WAITING', 'Resíduos junto à estrada'),
            (18, 'Viseu', 40.656, -7.913, 'MEDICAL_EMERGENCY', 'INEM', 'HIGH', 2000, 'ACTIVE', 'Pessoa a necessitar de socorro')
        ) AS s(n, district, latitude, longitude, occurrence_type, entity_type, priority, radius, status, title)
    LOOP
        seed_title := '[DEMO-SEED-' || lpad(scenario.n::text, 2, '0') || '] ' || scenario.title || ' - ' || scenario.district;
        -- Stable titles identify only this dataset; reruns preserve subsequent app edits.
        IF EXISTS (SELECT 1 FROM occurrences WHERE "Title" = seed_title) THEN
            CONTINUE;
        END IF;

        SELECT re."Id" INTO entity_id FROM responsible_entities re
        WHERE re."Type" = scenario.entity_type
          AND re."Name" LIKE '[DEMO] % - ' || scenario.district
          AND ST_Contains(re."GeoArea", ST_SetSRID(ST_MakePoint(scenario.longitude, scenario.latitude), 4326))
        ORDER BY re."Id" LIMIT 1;
        IF entity_id IS NULL THEN
            RAISE EXCEPTION 'Missing district entity: % / %. Run portuguese_responsible_entities.sql first.', scenario.district, scenario.entity_type;
        END IF;

        started_at := seed_now - make_interval(days => scenario.n);
        reports_total := CASE WHEN scenario.status = 'WAITING' THEN 1 ELSE 3 END;
        seed_description := 'Cenário fictício para testes em ' || scenario.district || ': ' || scenario.title || '. Não corresponde a um incidente real.';
        FOR report_number IN 1..reports_total LOOP
            INSERT INTO reports ("Title", "Description", "ReportDateTime", "Type", "UserId", "Latitude", "Longitude")
            VALUES (seed_title || ' / relato ' || report_number, seed_description,
                started_at + make_interval(mins => report_number - 1), scenario.occurrence_type,
                citizen_id, scenario.latitude, scenario.longitude)
            RETURNING "Id" INTO report_id;
            IF report_number = 1 THEN first_report_id := report_id; END IF;
        END LOOP;

        -- The model stores only the first ReportId; additional reports are linked
        -- by matching type/proximity in the service, not by a nonexistent FK.
        -- Open events use the API's DateTime.MinValue sentinel (Npgsql: -infinity).
        INSERT INTO occurrences ("Title", "Description", "Type", "Status", "Priority",
            "ProximityRadius", "CreationDateTime", "EndDateTime", "ReportCount",
            "ReportId", "ResponsibleEntityId", "Latitude", "Longitude")
        VALUES (seed_title, seed_description, scenario.occurrence_type, scenario.status,
            scenario.priority, scenario.radius, started_at,
            CASE WHEN scenario.status = 'CLOSED' THEN started_at + interval '4 hours' ELSE '-infinity'::timestamp END,
            reports_total, first_report_id, entity_id, scenario.latitude, scenario.longitude)
        RETURNING "Id" INTO occurrence_id;

        -- WAITING events cannot receive feedback in the API. Two different users
        -- leave feedback before closure, including some negative confirmations.
        IF scenario.status <> 'WAITING' THEN
            INSERT INTO feedback ("OccurrenceId", "UserId", "FeedbackDateTime", "IsConfirmed") VALUES
                (occurrence_id, citizen_id, started_at + interval '1 hour', true),
                (occurrence_id, manager_id, started_at + interval '2 hours', scenario.status <> 'CLOSED');
        END IF;
    END LOOP;
END;
$seed$;

-- Dataset totals (not whole-database counts).
SELECT
    (SELECT count(*) FROM users WHERE "Email" IN ('admin@gmail.com', 'manager@gmail.com', 'citizen@gmail.com')) AS demo_users,
    (SELECT count(*) FROM occurrences WHERE "Title" LIKE '[DEMO-SEED-%') AS demo_occurrences,
    (SELECT count(*) FROM reports WHERE "Title" LIKE '[DEMO-SEED-%') AS demo_reports,
    (SELECT count(*) FROM feedback f JOIN occurrences o ON o."Id" = f."OccurrenceId" WHERE o."Title" LIKE '[DEMO-SEED-%') AS demo_feedback;
COMMIT;
