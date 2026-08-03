using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kart.Notification.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // notification_attempts is hand-written raw SQL, not the EF-generated CreateTable a
            // plain fluent configuration would produce: EF Core has no native `PARTITION BY HASH`
            // support, and this table's HASH-on-event_id partitioning (not the more obvious
            // RANGE-on-created_at) is load-bearing for correctness, not just an operational
            // optimization - see database-design.md's "Partitioning Rationale" section. Every
            // column/constraint/index name below still matches
            // Configurations/NotificationAttemptConfiguration.cs exactly, so EF's own model
            // (LINQ reads) works against the physical schema this SQL actually creates.
            migrationBuilder.Sql(
                """
                CREATE TABLE notification_attempts (
                    event_id              UUID NOT NULL,
                    channel               TEXT NOT NULL
                                             CHECK (channel IN ('Email', 'SMS', 'Push')),
                    user_id               UUID NOT NULL,
                    triggering_event_type TEXT NOT NULL,
                    criticality_tier      TEXT NOT NULL
                                             CHECK (criticality_tier IN ('Tier1', 'Tier2', 'Tier3')),
                    category              TEXT NOT NULL,
                    status                TEXT NOT NULL DEFAULT 'Pending'
                                             CHECK (status IN ('Pending', 'Sent', 'Failed', 'Suppressed')),
                    attempt_count         INTEGER NOT NULL DEFAULT 0,
                    suppressed_reason     TEXT NULL,
                    created_at            TIMESTAMPTZ NOT NULL DEFAULT now(),
                    last_attempt_at       TIMESTAMPTZ NULL,
                    created_by            TEXT NOT NULL DEFAULT 'system:notification-send-pipeline',
                    updated_by            TEXT NOT NULL DEFAULT 'system:notification-send-pipeline',

                    PRIMARY KEY (event_id, channel),

                    CONSTRAINT chk_notification_attempt_suppressed_reason_shape CHECK (
                        (status = 'Suppressed' AND suppressed_reason IS NOT NULL)
                        OR (status <> 'Suppressed' AND suppressed_reason IS NULL)
                    ),

                    CONSTRAINT chk_notification_attempt_count_within_tier CHECK (
                        (criticality_tier = 'Tier1' AND attempt_count <= 5)
                        OR (criticality_tier = 'Tier2' AND attempt_count <= 3)
                        OR (criticality_tier = 'Tier3' AND attempt_count <= 2)
                    )
                )
                PARTITION BY HASH (event_id);
                """);

            for (var partition = 0; partition < 16; partition++)
            {
                migrationBuilder.Sql(
                    $"""
                     CREATE TABLE notification_attempts_p{partition} PARTITION OF notification_attempts
                         FOR VALUES WITH (MODULUS 16, REMAINDER {partition});
                     """);
            }

            migrationBuilder.Sql(
                """
                CREATE OR REPLACE FUNCTION enforce_notification_attempt_status_transition() RETURNS trigger AS $$
                BEGIN
                    IF OLD.status IN ('Sent', 'Failed', 'Suppressed') AND NEW.status <> OLD.status THEN
                        RAISE EXCEPTION 'illegal DeliveryOutcome transition: % is terminal, cannot move to %', OLD.status, NEW.status;
                    END IF;
                    NEW.last_attempt_at := now();
                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER trg_notification_attempts_status_guard
                    BEFORE UPDATE OF status ON notification_attempts
                    FOR EACH ROW EXECUTE FUNCTION enforce_notification_attempt_status_transition();
                """);

            migrationBuilder.Sql(
                """
                CREATE INDEX idx_notification_attempts_user_audit ON notification_attempts (user_id, created_at);
                CREATE INDEX idx_notification_attempts_failed ON notification_attempts (created_at)
                    WHERE status = 'Failed';
                """);

            // BRD §24.1.4 - defense-in-depth even though every session today is `system`-kind
            // (no end-user-facing request path exists for this consumer-only service). See
            // database-design.md's "Row-Level Security Policy" section for the full rationale.
            migrationBuilder.Sql(
                """
                ALTER TABLE notification_attempts ENABLE ROW LEVEL SECURITY;
                CREATE POLICY notification_attempts_owner_or_system ON notification_attempts
                    USING (
                        user_id = current_setting('app.current_principal', true)::uuid
                        OR current_setting('app.current_principal_kind', true) IN ('service', 'system')
                    );
                """);

            migrationBuilder.CreateTable(
                name: "notification_audit_log",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_name = table.Column<string>(type: "text", nullable: false),
                    actor_id = table.Column<string>(type: "text", nullable: false),
                    actor_type = table.Column<string>(type: "text", nullable: false),
                    action = table.Column<string>(type: "text", nullable: false),
                    entity_type = table.Column<string>(type: "text", nullable: false),
                    entity_id = table.Column<string>(type: "text", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    metadata = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_audit_log", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "notification_preferences",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    opt_out_matrix = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    app_installed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    last_updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<string>(type: "text", nullable: false, defaultValue: "system:notification-preference-sync-consumer"),
                    updated_by = table.Column<string>(type: "text", nullable: false, defaultValue: "system:notification-preference-sync-consumer")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_preferences", x => x.user_id);
                });

            // Same BRD §24.1.4 defense-in-depth rationale as notification_attempts above.
            migrationBuilder.Sql(
                """
                ALTER TABLE notification_preferences ENABLE ROW LEVEL SECURITY;
                CREATE POLICY notification_preferences_owner_or_system ON notification_preferences
                    USING (
                        user_id = current_setting('app.current_principal', true)::uuid
                        OR current_setting('app.current_principal_kind', true) IN ('service', 'system')
                    );
                """);

            migrationBuilder.CreateTable(
                name: "order_user_index",
                columns: table => new
                {
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<string>(type: "text", nullable: false, defaultValue: "system:notification-order-user-index-consumer"),
                    updated_by = table.Column<string>(type: "text", nullable: false, defaultValue: "system:notification-order-user-index-consumer")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_user_index", x => x.order_id);
                });

            migrationBuilder.CreateTable(
                name: "tracking_order_index",
                columns: table => new
                {
                    tracking_id = table.Column<string>(type: "text", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<string>(type: "text", nullable: false, defaultValue: "system:notification-tracking-order-index-consumer"),
                    updated_by = table.Column<string>(type: "text", nullable: false, defaultValue: "system:notification-tracking-order-index-consumer")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tracking_order_index", x => x.tracking_id);
                });

            migrationBuilder.CreateIndex(
                name: "idx_notification_audit_log_entity",
                table: "notification_audit_log",
                columns: new[] { "entity_type", "entity_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // DROP TABLE on a partitioned parent drops all 16 partitions along with it (Postgres
            // behavior, no CASCADE needed) - the trigger function is a separate object and must be
            // dropped explicitly.
            migrationBuilder.DropTable(
                name: "notification_attempts");

            migrationBuilder.Sql("DROP FUNCTION IF EXISTS enforce_notification_attempt_status_transition();");

            migrationBuilder.DropTable(
                name: "notification_audit_log");

            migrationBuilder.DropTable(
                name: "notification_preferences");

            migrationBuilder.DropTable(
                name: "order_user_index");

            migrationBuilder.DropTable(
                name: "tracking_order_index");
        }
    }
}
