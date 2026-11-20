using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Pharmacy.Infrastructure.Knowledge;
using KitPlatform.Packs.Pharmacy.Knowledge;
using Npgsql;
using Xunit;

namespace KitPlatform.Tests;

public sealed class PharmacyOwnedKnowledgeSnapshotTests
{
    private const string ConnectionString =
        "Host=127.0.0.1;Port=5436;Database=postgres;Username=m1_owned;Timeout=15;Command Timeout=30";

    [Fact]
    public async Task Three_snapshots_stay_complete_and_the_other_tenant_cannot_read_them()
    {
        RequireLocalCluster();
        var service = new PharmacyOwnedKnowledgeService(new NpgsqlConnectionFactory(ConnectionString));
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var user = Guid.NewGuid();
        try
        {
            var created = await service.CreateAsync(
                new PharmacyOwnedKnowledgeWriteRequest("GUIDANCE", "Ban dau", "Noi dung ban dau", "Nhom A"),
                tenantA,
                user);
            var once = await service.UpdateAsync(
                created.Id,
                new PharmacyOwnedKnowledgeWriteRequest("OPERATING_PROCEDURE", "Sau sua 1", "Noi dung sua 1", "Nhom B"),
                tenantA,
                user);
            var twice = await service.UpdateAsync(
                created.Id,
                new PharmacyOwnedKnowledgeWriteRequest("CLINICAL_KNOWLEDGE", "Sau sua 2", "Noi dung sua 2", null),
                tenantA,
                user);

            Assert.Equal(1, twice.Version);
            Assert.Equal(3, twice.History.Count);
            AssertSnapshot(twice.History[0], "CREATED", "GUIDANCE", "Ban dau", "Noi dung ban dau", "Nhom A", user);
            AssertSnapshot(twice.History[1], "UPDATED", "OPERATING_PROCEDURE", "Sau sua 1", "Noi dung sua 1", "Nhom B", user);
            AssertSnapshot(twice.History[2], "UPDATED", "CLINICAL_KNOWLEDGE", "Sau sua 2", "Noi dung sua 2", null, user);
            Assert.Equal("Noi dung ban dau", once.History[0].Body);
            Assert.Null(await service.GetAsync(tenantB, created.Id));
            Assert.Empty(await service.ListAsync(tenantB, null));

            await using var conn = await OpenAsync();
            var eventId = await new NpgsqlCommand(
                "SELECT id FROM pharmacy_owned_knowledge_event WHERE tenant_id = @Tenant AND knowledge_id = @Id ORDER BY created_at LIMIT 1",
                conn)
            {
                Parameters =
                {
                    new("Tenant", tenantA),
                    new("Id", created.Id),
                },
            }.ExecuteScalarAsync();
            await Assert.ThrowsAsync<PostgresException>(() => RejectAsync(
                conn,
                "UPDATE pharmacy_owned_knowledge_event SET body = 'doi' WHERE id = @Id AND tenant_id = @Tenant",
                eventId,
                tenantA));
            await Assert.ThrowsAsync<PostgresException>(() => RejectAsync(
                conn,
                "DELETE FROM pharmacy_owned_knowledge_event WHERE id = @Id AND tenant_id = @Tenant",
                eventId,
                tenantA));
            var still = await service.GetAsync(tenantA, created.Id);
            Assert.Equal("Noi dung ban dau", still!.History[0].Body);
        }
        finally
        {
            await DeleteTenantAsync(tenantA);
            await DeleteTenantAsync(tenantB);
        }
    }

    [Fact]
    public async Task Backfilled_legacy_event_is_unavailable_and_stays_in_its_tenant()
    {
        RequireLocalCluster();
        var service = new PharmacyOwnedKnowledgeService(new NpgsqlConnectionFactory(ConnectionString));
        var legacyRows = await ReadLegacyAsync();
        Assert.NotEmpty(legacyRows);

        foreach (var row in legacyRows)
        {
            Assert.Null(row.Body);
            Assert.Null(row.Classification);
            Assert.False(string.IsNullOrWhiteSpace(row.VersionBody));
            Assert.Null(await service.GetAsync(Guid.NewGuid(), row.KnowledgeId));

            var detail = await service.GetAsync(row.TenantId, row.KnowledgeId);
            Assert.NotNull(detail);
            var snapshot = Assert.Single(detail!.History, item => item.Id == row.EventId);
            Assert.Equal(PharmacyOwnedKnowledgeRules.LegacyUnavailable, snapshot.SnapshotFidelity);
            Assert.Equal(row.Action, snapshot.Action);
            Assert.Equal(row.Kind, snapshot.Kind);
            Assert.Equal(row.Title, snapshot.Title);
            Assert.Null(snapshot.Body);
            Assert.Null(snapshot.Classification);
            Assert.NotEqual(row.VersionBody, snapshot.Body);
            Assert.Contains(detail.History, item => item.Id == row.EventId);
        }
    }

    [Fact]
    public async Task Same_timestamp_events_keep_one_order()
    {
        RequireLocalCluster();
        var service = new PharmacyOwnedKnowledgeService(new NpgsqlConnectionFactory(ConnectionString));
        var tenant = Guid.NewGuid();
        var knowledgeId = Guid.NewGuid();
        var user = Guid.NewGuid();
        var at = new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);
        var firstId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var middleId = Guid.Parse("00000000-0000-0000-0000-0000000000aa");
        var lastId = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
        try
        {
            await using (var conn = await OpenAsync())
            {
                await InsertDraftAsync(conn, tenant, knowledgeId, user);
                await InsertExactAsync(conn, lastId, tenant, knowledgeId, user, at, "UPDATED", "Sau", "Noi dung sau");
                await InsertExactAsync(conn, middleId, tenant, knowledgeId, user, at, "UPDATED", "Giua", "Noi dung giua");
                await InsertExactAsync(conn, firstId, tenant, knowledgeId, user, at, "CREATED", "Truoc", "Noi dung truoc");
            }

            var firstRead = await service.GetAsync(tenant, knowledgeId);
            var secondRead = await service.GetAsync(tenant, knowledgeId);
            Assert.NotNull(firstRead);
            Assert.Equal(
                firstRead!.History.Select(item => item.Id),
                secondRead!.History.Select(item => item.Id));
            Assert.Equal(new[] { firstId, middleId, lastId }, firstRead.History.Select(item => item.Id).ToArray());
            Assert.Equal(new[] { "CREATED", "UPDATED", "UPDATED" }, firstRead.History.Select(item => item.Action).ToArray());
            Assert.All(firstRead.History, item => Assert.Equal(PharmacyOwnedKnowledgeRules.ExactSnapshot, item.SnapshotFidelity));
            Assert.Equal("Noi dung truoc", firstRead.History[0].Body);
            Assert.Equal("Noi dung sau", firstRead.History[2].Body);

            await using var check = await OpenAsync();
            var storedOrder = new List<Guid>();
            await using (var command = new NpgsqlCommand(
                """
                SELECT id
                FROM pharmacy_owned_knowledge_event
                WHERE tenant_id = @Tenant
                  AND knowledge_id = @Knowledge
                ORDER BY created_at, id
                """,
                check))
            {
                command.Parameters.AddWithValue("Tenant", tenant);
                command.Parameters.AddWithValue("Knowledge", knowledgeId);
                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    storedOrder.Add(reader.GetGuid(0));
            }

            Assert.Equal(storedOrder, firstRead.History.Select(item => item.Id));
            await Assert.ThrowsAsync<PostgresException>(() => RejectAsync(
                check,
                "UPDATE pharmacy_owned_knowledge_event SET body = 'doi' WHERE id = @Id AND tenant_id = @Tenant",
                lastId,
                tenant));
            var afterReject = await service.GetAsync(tenant, knowledgeId);
            Assert.Equal("Noi dung sau", afterReject!.History.Single(item => item.Id == lastId).Body);
            Assert.Null(await service.GetAsync(Guid.NewGuid(), knowledgeId));
        }
        finally
        {
            await DeleteTenantAsync(tenant);
        }
    }

    [Fact]
    public async Task Governance_freezes_approval_and_keeps_each_activation()
    {
        RequireLocalCluster();
        var service = new PharmacyOwnedKnowledgeService(new NpgsqlConnectionFactory(ConnectionString));
        var tenant = Guid.NewGuid();
        var other = Guid.NewGuid();
        var admin = Guid.NewGuid();
        var staff = Guid.NewGuid();
        try
        {
            var created = await service.CreateAsync(
                new PharmacyOwnedKnowledgeWriteRequest("GUIDANCE", "Huong dan", GuidanceBody(), "Noi bo"),
                tenant,
                admin);
            var versionId = created.Versions[0].VersionId;
            await Assert.ThrowsAnyAsync<InvalidOperationException>(() => service.SubmitAsync(
                created.Id, versionId, created.UpdatedAt.AddMinutes(-5), tenant, admin, true));
            var submitted = await service.SubmitAsync(created.Id, versionId, created.UpdatedAt, tenant, admin, true);
            Assert.Equal("IN_REVIEW", submitted.Versions[0].LifecycleStatus);
            await Assert.ThrowsAnyAsync<InvalidOperationException>(() => service.StartNextDraftAsync(
                created.Id, versionId, tenant, admin, true));
            Assert.Contains(submitted.History, item => item.Action == "SUBMITTED" && item.SnapshotFidelity == "EXACT" && item.Body == GuidanceBody());
            await Assert.ThrowsAnyAsync<InvalidOperationException>(() => service.UpdateAsync(
                created.Id,
                new PharmacyOwnedKnowledgeWriteRequest("GUIDANCE", "Doi", GuidanceBody(), null),
                tenant,
                admin));
            var approved = await service.ApproveAsync(created.Id, versionId, submitted.UpdatedAt, tenant, admin, true);
            Assert.Equal("APPROVED", approved.Versions[0].LifecycleStatus);
            Assert.Empty(approved.Activations);
            var active = await service.ActivateAsync(created.Id, versionId, tenant, admin, true);
            Assert.Equal("ACTIVE", Assert.Single(active.Activations).Status);
            var second = await service.StartNextDraftAsync(created.Id, versionId, tenant, admin, true);
            Assert.Equal(2, second.Versions.Count);
            Assert.Equal("APPROVED", second.Versions[0].LifecycleStatus);
            Assert.Equal("DRAFT", second.Versions[1].LifecycleStatus);
            Assert.Equal("ACTIVE", Assert.Single(second.Activations).Status);

            var clinical = await service.CreateAsync(
                new PharmacyOwnedKnowledgeWriteRequest("CLINICAL_KNOWLEDGE", "Noi bo", ClinicalBody(), null),
                tenant,
                admin);
            var clinicalVersion = clinical.Versions[0].VersionId;
            var clinicalReview = await service.SubmitAsync(clinical.Id, clinicalVersion, clinical.UpdatedAt, tenant, admin, true);
            var clinicalApproved = await service.ApproveAsync(clinical.Id, clinicalVersion, clinicalReview.UpdatedAt, tenant, admin, true);
            await Assert.ThrowsAnyAsync<InvalidOperationException>(() => service.ActivateAsync(
                clinical.Id, clinicalVersion, tenant, admin, true));
            Assert.Empty(clinicalApproved.Activations);
            await Assert.ThrowsAnyAsync<InvalidOperationException>(() => service.SubmitAsync(
                created.Id, second.Versions[1].VersionId, second.UpdatedAt, tenant, staff, false));
            await Assert.ThrowsAnyAsync<InvalidOperationException>(() => service.GrantAsync(
                tenant, admin, true, staff, "clinical", "CLINICAL_KNOWLEDGE"));
            await service.GrantAsync(tenant, admin, true, staff, "draft", "CLINICAL_KNOWLEDGE");
            var staffClinical = await service.CreateAsync(
                new PharmacyOwnedKnowledgeWriteRequest("CLINICAL_KNOWLEDGE", "Soan lam sang", ClinicalBody(), null),
                tenant,
                staff,
                default,
                false);
            await Assert.ThrowsAnyAsync<InvalidOperationException>(() => service.SubmitAsync(
                staffClinical.Id,
                staffClinical.Versions[0].VersionId,
                staffClinical.UpdatedAt,
                tenant,
                staff,
                false));
            await service.GrantAsync(tenant, admin, true, staff, "read", "GUIDANCE");
            var visible = await service.ListInUseAsync(tenant, staff, false, null, null, null);
            Assert.Contains(visible, item => item.Id == created.Id && item.Kind == "GUIDANCE");
            Assert.DoesNotContain(visible, item => item.Kind == "CLINICAL_KNOWLEDGE");
            Assert.Empty(await service.ListInUseAsync(other, staff, false, null, null, null));
            Assert.Null(await service.GetAsync(other, created.Id));
        }
        finally
        {
            await DeleteTenantAsync(tenant);
            await DeleteTenantAsync(other);
        }
    }

    [Fact]
    public async Task Concurrent_activation_records_one_event()
    {
        RequireLocalCluster();
        var service = new PharmacyOwnedKnowledgeService(new NpgsqlConnectionFactory(ConnectionString));
        var tenant = Guid.NewGuid();
        var admin = Guid.NewGuid();
        try
        {
            var created = await service.CreateAsync(
                new PharmacyOwnedKnowledgeWriteRequest("GUIDANCE", "Ap dung", GuidanceBody(), null),
                tenant,
                admin);
            var versionId = created.Versions[0].VersionId;
            var submitted = await service.SubmitAsync(created.Id, versionId, created.UpdatedAt, tenant, admin, true);
            await service.ApproveAsync(created.Id, versionId, submitted.UpdatedAt, tenant, admin, true);
            var first = Task.Run(() => service.ActivateAsync(created.Id, versionId, tenant, admin, true));
            var second = Task.Run(() => service.ActivateAsync(created.Id, versionId, tenant, admin, true));
            var done = await Task.WhenAll(first.ContinueWith(task => task), second.ContinueWith(task => task));
            Assert.Equal(1, done.Count(task => task.Status == TaskStatus.RanToCompletion));
            Assert.Equal(1, done.Count(task => task.IsFaulted));
            await using var conn = await new NpgsqlConnectionFactory(ConnectionString).CreateOpenConnectionAsync();
            await using var command = new NpgsqlCommand(
                """
                SELECT COUNT(*)
                FROM pharmacy_owned_knowledge_activation_event
                WHERE tenant_id = @Tenant AND knowledge_id = @Knowledge
                """,
                conn);
            command.Parameters.AddWithValue("Tenant", tenant);
            command.Parameters.AddWithValue("Knowledge", created.Id);
            var events = (long)(await command.ExecuteScalarAsync() ?? 0L);
            Assert.Equal(1L, events);
        }
        finally
        {
            await DeleteTenantAsync(tenant);
        }
    }

    private static string GuidanceBody() =>
        """
        ## Mục đích
        Giải thích cho nhân viên

        ## Đối tượng áp dụng
        Nhân viên quầy

        ## Nội dung chính
        Nói rõ cách bảo quản

        ## Giới hạn
        Không dùng để chọn thuốc
        """;

    private static string ClinicalBody() =>
        """
        ## Mục đích
        Ghi chú nội bộ

        ## Nội dung chính
        Không đưa ra quầy

        ## Cảnh báo và giới hạn
        Không dùng để tư vấn hoặc bán
        """;

    [Fact]
    public async Task Failed_snapshot_rolls_back_the_draft_update()
    {
        RequireLocalCluster();
        var service = new PharmacyOwnedKnowledgeService(new NpgsqlConnectionFactory(ConnectionString));
        var tenant = Guid.NewGuid();
        var user = Guid.NewGuid();
        try
        {
            var created = await service.CreateAsync(
                new PharmacyOwnedKnowledgeWriteRequest("GUIDANCE", "Giu nguyen", "Noi dung goc", "Ngan"),
                tenant,
                user);
            await using (var conn = await OpenAsync())
            {
                await new NpgsqlCommand(
                    "ALTER TABLE pharmacy_owned_knowledge_event ALTER COLUMN classification TYPE VARCHAR(10)",
                    conn).ExecuteNonQueryAsync();
            }

            await Assert.ThrowsAnyAsync<PostgresException>(() => service.UpdateAsync(
                created.Id,
                new PharmacyOwnedKnowledgeWriteRequest("PRODUCT_KNOWLEDGE", "Da doi", "Noi dung moi", "Phan loai qua dai cho cot"),
                tenant,
                user));

            var current = await service.GetAsync(tenant, created.Id);
            Assert.NotNull(current);
            Assert.Equal("Giu nguyen", current.Title);
            Assert.Equal("Noi dung goc", current.Body);
            Assert.Equal("GUIDANCE", current.Kind);
            Assert.Single(current.History);
            Assert.Equal("Noi dung goc", current.History[0].Body);
        }
        finally
        {
            await using var conn = await OpenAsync();
            await new NpgsqlCommand(
                "ALTER TABLE pharmacy_owned_knowledge_event ALTER COLUMN classification TYPE VARCHAR(200)",
                conn).ExecuteNonQueryAsync();
            await DeleteTenantAsync(tenant);
        }
    }

    private static void AssertSnapshot(
        PharmacyOwnedKnowledgeEventDto snapshot,
        string action,
        string kind,
        string title,
        string body,
        string? classification,
        Guid actor)
    {
        Assert.Equal(action, snapshot.Action);
        Assert.Equal(1, snapshot.Version);
        Assert.Equal(kind, snapshot.Kind);
        Assert.Equal(title, snapshot.Title);
        Assert.Equal(body, snapshot.Body);
        Assert.Equal(classification, snapshot.Classification);
        Assert.Equal(PharmacyOwnedKnowledgeRules.ExactSnapshot, snapshot.SnapshotFidelity);
        Assert.NotEqual(Guid.Empty, snapshot.Id);
        Assert.Equal(actor.ToString(), snapshot.ActorUserId);
        Assert.NotEqual(default, snapshot.CreatedAt);
    }

    private static void RequireLocalCluster()
    {
        var builder = new NpgsqlConnectionStringBuilder(ConnectionString);
        if (builder.Host != "127.0.0.1" || builder.Port != 5436 || builder.Database != "postgres")
            throw new InvalidOperationException("Snapshot tests refuse every database except the local disposable cluster.");
    }

    private static async Task<NpgsqlConnection> OpenAsync()
    {
        var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();
        return conn;
    }

    private static async Task RejectAsync(NpgsqlConnection conn, string sql, object? id, Guid tenant)
    {
        await using var command = new NpgsqlCommand(sql, conn);
        command.Parameters.AddWithValue("Id", id ?? Guid.Empty);
        command.Parameters.AddWithValue("Tenant", tenant);
        await command.ExecuteNonQueryAsync();
    }

    private sealed record LegacyRow(
        Guid EventId,
        Guid TenantId,
        Guid KnowledgeId,
        string Action,
        string Kind,
        string Title,
        string? Body,
        string? Classification,
        string VersionBody);

    private static async Task<List<LegacyRow>> ReadLegacyAsync()
    {
        var rows = new List<LegacyRow>();
        await using var conn = await OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            SELECT e.id,
                   e.tenant_id,
                   e.knowledge_id,
                   e.action,
                   e.kind,
                   e.title,
                   e.body,
                   e.classification,
                   v.body
            FROM pharmacy_owned_knowledge_event e
            JOIN pharmacy_owned_knowledge_version v
              ON v.knowledge_id = e.knowledge_id
             AND v.tenant_id = e.tenant_id
             AND v.version_no = e.version_no
            WHERE e.snapshot_fidelity = 'LEGACY_UNAVAILABLE'
            """,
            conn);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows.Add(new LegacyRow(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetGuid(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.GetString(8)));
        }

        return rows;
    }

    private static async Task InsertDraftAsync(NpgsqlConnection conn, Guid tenant, Guid knowledgeId, Guid user)
    {
        await using (var head = new NpgsqlCommand(
            """
            INSERT INTO pharmacy_owned_knowledge (id, tenant_id, created_by)
            VALUES (@Id, @Tenant, @Actor)
            """,
            conn))
        {
            head.Parameters.AddWithValue("Id", knowledgeId);
            head.Parameters.AddWithValue("Tenant", tenant);
            head.Parameters.AddWithValue("Actor", user.ToString());
            await head.ExecuteNonQueryAsync();
        }

        await using var version = new NpgsqlCommand(
            """
            INSERT INTO pharmacy_owned_knowledge_version (
                knowledge_id, tenant_id, version_no, kind, title, body, classification, lifecycle_status)
            VALUES (@Id, @Tenant, 1, 'GUIDANCE', 'Ban hien tai', 'Noi dung hien tai', 'Nhom', 'DRAFT')
            """,
            conn);
        version.Parameters.AddWithValue("Id", knowledgeId);
        version.Parameters.AddWithValue("Tenant", tenant);
        await version.ExecuteNonQueryAsync();
    }

    private static async Task InsertExactAsync(
        NpgsqlConnection conn,
        Guid eventId,
        Guid tenant,
        Guid knowledgeId,
        Guid user,
        DateTimeOffset createdAt,
        string action,
        string title,
        string body)
    {
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO pharmacy_owned_knowledge_event (
                id, tenant_id, knowledge_id, version_no, action, kind, title, body, classification,
                snapshot_fidelity, actor_user_id, created_at)
            VALUES (
                @Id, @Tenant, @Knowledge, 1, @Action, 'GUIDANCE', @Title, @Body, 'Nhom',
                'EXACT', @Actor, @CreatedAt)
            """,
            conn);
        command.Parameters.AddWithValue("Id", eventId);
        command.Parameters.AddWithValue("Tenant", tenant);
        command.Parameters.AddWithValue("Knowledge", knowledgeId);
        command.Parameters.AddWithValue("Action", action);
        command.Parameters.AddWithValue("Title", title);
        command.Parameters.AddWithValue("Body", body);
        command.Parameters.AddWithValue("Actor", user.ToString());
        command.Parameters.AddWithValue("CreatedAt", createdAt);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task DeleteTenantAsync(Guid tenant)
    {
        await using var conn = await OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();
        foreach (var drop in new[]
        {
            "DROP TRIGGER IF EXISTS trg_pharmacy_owned_knowledge_version_governance ON pharmacy_owned_knowledge_version",
            "DROP TRIGGER IF EXISTS trg_pharmacy_owned_knowledge_event_immutable ON pharmacy_owned_knowledge_event",
            "DROP TRIGGER IF EXISTS trg_pharmacy_owned_knowledge_activation_event_immutable ON pharmacy_owned_knowledge_activation_event",
            "DROP TRIGGER IF EXISTS trg_pharmacy_owned_knowledge_activation_guard ON pharmacy_owned_knowledge_activation",
        })
        {
            await new NpgsqlCommand(drop, conn, tx).ExecuteNonQueryAsync();
        }
        foreach (var sql in new[]
        {
            "DELETE FROM pharmacy_owned_knowledge_activation_event WHERE tenant_id = @Tenant",
            "DELETE FROM pharmacy_owned_knowledge_activation WHERE tenant_id = @Tenant",
            "DELETE FROM pharmacy_owned_knowledge_grant WHERE tenant_id = @Tenant",
            "DELETE FROM pharmacy_owned_knowledge_event WHERE tenant_id = @Tenant",
            "DELETE FROM pharmacy_owned_knowledge_version WHERE tenant_id = @Tenant",
            "DELETE FROM pharmacy_owned_knowledge WHERE tenant_id = @Tenant",
        })
        {
            await using var command = new NpgsqlCommand(sql, conn, tx);
            command.Parameters.AddWithValue("Tenant", tenant);
            await command.ExecuteNonQueryAsync();
        }

        foreach (var create in new[]
        {
            """
            CREATE TRIGGER trg_pharmacy_owned_knowledge_version_governance
                BEFORE INSERT OR UPDATE OR DELETE ON pharmacy_owned_knowledge_version
                FOR EACH ROW
                EXECUTE FUNCTION pharmacy_owned_knowledge_version_governance()
            """,
            """
            CREATE TRIGGER trg_pharmacy_owned_knowledge_event_immutable
                BEFORE UPDATE OR DELETE ON pharmacy_owned_knowledge_event
                FOR EACH ROW
                EXECUTE FUNCTION pharmacy_owned_knowledge_event_immutable()
            """,
            """
            CREATE TRIGGER trg_pharmacy_owned_knowledge_activation_guard
                BEFORE UPDATE OR DELETE ON pharmacy_owned_knowledge_activation
                FOR EACH ROW
                EXECUTE FUNCTION pharmacy_owned_knowledge_activation_guard()
            """,
            """
            CREATE TRIGGER trg_pharmacy_owned_knowledge_activation_event_immutable
                BEFORE UPDATE OR DELETE ON pharmacy_owned_knowledge_activation_event
                FOR EACH ROW
                EXECUTE FUNCTION pharmacy_owned_knowledge_activation_event_immutable()
            """,
        })
        {
            await new NpgsqlCommand(create, conn, tx).ExecuteNonQueryAsync();
        }
        await tx.CommitAsync();
    }
}
