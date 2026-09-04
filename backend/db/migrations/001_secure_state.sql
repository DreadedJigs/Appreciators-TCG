-- The secure state document is deliberately updated within SELECT … FOR UPDATE
-- transactions. This provides a safe migration path from the mounted-disk
-- alpha store to multi-instance PostgreSQL without silently trusting client
-- state. Run this with the application database owner before setting
-- DATABASE_URL in production.

CREATE TABLE IF NOT EXISTS appreciators_secure_state (
  store_key TEXT PRIMARY KEY,
  document JSONB NOT NULL,
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

INSERT INTO appreciators_secure_state (store_key, document)
VALUES ('primary', '{"schemaVersion":1,"accounts":[],"sessions":[],"cloudSaves":{},"matchQueues":[],"matches":[]}'::jsonb)
ON CONFLICT (store_key) DO NOTHING;
