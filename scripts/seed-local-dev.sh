#!/usr/bin/env bash
# Dev-only convenience data for manual local testing - NOT a production seed script.
#
# This service has no reference/lookup data of its own to seed (kart-identity-service's own
# precedent: no seed script exists there either, for the same reason - every table here is
# populated exclusively by consuming events, per ddd-model.md's audit-actor invariants). This
# script exists only so a developer manually exercising the send pipeline locally (e.g. publishing
# a fake OrderConfirmed via the RabbitMQ management UI) has a `notification_preferences` row to
# test the opt-out check against, without needing kart-user-service running too.
#
# Usage: scripts/seed-local-dev.sh [connection-string]
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."

CONNECTION_STRING="${1:-${NOTIFICATION_DB_CONNECTION_STRING:-Host=localhost;Port=5433;Database=kart_notification;Username=postgres;Password=postgres}}"

# Parse the .NET-style connection string into psql args (only the fields this script needs).
HOST=$(echo "$CONNECTION_STRING" | grep -oP '(?<=Host=)[^;]+')
PORT=$(echo "$CONNECTION_STRING" | grep -oP '(?<=Port=)[^;]+')
DATABASE=$(echo "$CONNECTION_STRING" | grep -oP '(?<=Database=)[^;]+')
USERNAME=$(echo "$CONNECTION_STRING" | grep -oP '(?<=Username=)[^;]+')
PASSWORD=$(echo "$CONNECTION_STRING" | grep -oP '(?<=Password=)[^;]+')

PGPASSWORD="$PASSWORD" psql -h "$HOST" -p "$PORT" -U "$USERNAME" -d "$DATABASE" <<SQL
INSERT INTO notification_preferences (user_id, opt_out_matrix, app_installed)
VALUES
    ('00000000-0000-0000-0000-000000000001', '{}'::jsonb, true),
    ('00000000-0000-0000-0000-000000000002', '{"Email": {"marketing": true}}'::jsonb, false)
ON CONFLICT (user_id) DO NOTHING;
SQL

echo "Seeded 2 sample notification_preferences rows for local testing."
