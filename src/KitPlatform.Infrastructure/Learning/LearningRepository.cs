using System.Text.Json;
using System.Text.RegularExpressions;
using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Application.Learning;
using KitPlatform.Infrastructure.Data;

namespace KitPlatform.Infrastructure.Learning;

internal sealed class LearningRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;

    public LearningRepository(IDbConnectionFactory db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    private Guid TenantId => _tenant.TenantId;
    private Guid UserId => _tenant.UserId;

    public async Task<Guid?> GetEmployeeIdForCurrentUserAsync(CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteScalarAsync<Guid?>(
            """
            SELECT employee_id FROM users
            WHERE id = @UserId AND tenant_id = @TenantId AND deleted_at IS NULL
            """,
            new { UserId, TenantId });
    }

    public async Task<IReadOnlyList<LearningProgramListItemDto>> ListProgramsAsync(
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<(
            Guid Id, string Code, string PackCode, string Title, string? Summary,
            string Locale, int Version, int ModuleCount, int SortOrder)>(
            """
            SELECT p.id AS Id, p.code AS Code, p.pack_code AS PackCode, p.title AS Title,
                   p.summary AS Summary, p.locale AS Locale, p.version AS Version,
                   (SELECT COUNT(*)::int FROM pack_learning.module m WHERE m.program_id = p.id) AS ModuleCount,
                   p.sort_order AS SortOrder
            FROM pack_learning.program p
            WHERE p.status = 'published'
              AND (p.tenant_id IS NULL OR p.tenant_id = @TenantId)
            ORDER BY p.sort_order, p.title
            """,
            new { TenantId });

        return rows.Select(r => new LearningProgramListItemDto(
            r.Id, r.Code, r.PackCode, r.Title, r.Summary, r.Locale, r.Version, r.ModuleCount, r.SortOrder))
            .ToList();
    }

    public async Task<LearningProgramDetailDto?> GetProgramAsync(
        Guid programId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var program = await conn.QuerySingleOrDefaultAsync<ProgramHeaderRow>(
            """
            SELECT id AS Id, code AS Code, pack_code AS PackCode, title AS Title,
                   summary AS Summary, locale AS Locale, version AS Version
            FROM pack_learning.program
            WHERE id = @ProgramId
              AND status = 'published'
              AND (tenant_id IS NULL OR tenant_id = @TenantId)
            """,
            new { ProgramId = programId, TenantId });

        if (program is null)
            return null;

        var modules = (await conn.QueryAsync<ModuleRow>(
            """
            SELECT m.id AS Id, m.code AS Code, m.title AS Title, m.summary AS Summary,
                   m.duration_minutes AS DurationMinutes, m.level_code AS LevelCode,
                   m.competency_codes AS CompetencyCodes, m.sort_order AS SortOrder,
                   m.pass_score_pct AS PassScorePct, m.require_ack AS RequireAck,
                   (SELECT COUNT(*)::int FROM pack_learning.quiz_question q WHERE q.module_id = m.id) AS QuestionCount
            FROM pack_learning.module m
            WHERE m.program_id = @ProgramId
            ORDER BY m.sort_order, m.title
            """,
            new { ProgramId = programId })).ToList();

        return new LearningProgramDetailDto(
            program.Id,
            program.Code,
            program.PackCode,
            program.Title,
            program.Summary,
            program.Locale,
            program.Version,
            modules.Select(MapModuleList).ToList());
    }

    public async Task<LearningModuleDetailDto?> GetModuleAsync(
        Guid moduleId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var mod = await conn.QuerySingleOrDefaultAsync<(
            Guid Id, Guid ProgramId, string Code, string Title, string? Summary, string BodyMarkdown,
            int DurationMinutes, string LevelCode, string[]? CompetencyCodes, int PassScorePct,
            bool RequireAck, bool IsTenantCustomized, bool RequireObservation)>(
            """
            SELECT m.id, m.program_id, m.code,
                   COALESCE(o.title, m.title),
                   COALESCE(o.summary, m.summary),
                   COALESCE(o.body_markdown, m.body_markdown),
                   m.duration_minutes, m.level_code, m.competency_codes, m.pass_score_pct,
                   m.require_ack,
                   (o.module_id IS NOT NULL) AS is_tenant_customized,
                   COALESCE(m.require_observation, FALSE) AS require_observation
            FROM pack_learning.module m
            INNER JOIN pack_learning.program p ON p.id = m.program_id
            LEFT JOIN pack_learning.module_tenant_override o
                ON o.module_id = m.id AND o.tenant_id = @TenantId
            WHERE m.id = @ModuleId
              AND p.status = 'published'
              AND (p.tenant_id IS NULL OR p.tenant_id = @TenantId)
            """,
            new { ModuleId = moduleId, TenantId });

        if (mod.Id == Guid.Empty)
            return null;

        var questions = (await conn.QueryAsync<(Guid Id, int SortOrder, string Prompt, string OptionsJson)>(
            """
            SELECT id, sort_order, prompt, options_json::text
            FROM pack_learning.quiz_question
            WHERE module_id = @ModuleId
            ORDER BY sort_order
            """,
            new { ModuleId = moduleId })).ToList();

        return new LearningModuleDetailDto(
            mod.Id,
            mod.ProgramId,
            mod.Code,
            mod.Title,
            mod.Summary,
            mod.BodyMarkdown,
            mod.DurationMinutes,
            mod.LevelCode,
            mod.CompetencyCodes ?? [],
            mod.PassScorePct,
            mod.RequireAck,
            questions.Select(q => new LearningQuizQuestionPublicDto(
                q.Id,
                q.SortOrder,
                q.Prompt,
                ParseOptions(q.OptionsJson))).ToList(),
            mod.IsTenantCustomized,
            mod.RequireObservation);
    }

    public async Task<LearningModuleDetailDto> UpsertModuleTenantContentAsync(
        Guid moduleId,
        UpsertLearningModuleTenantContentRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.BodyMarkdown))
            throw new ArgumentException("Nội dung bài (markdown) không được trống.");

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var exists = await conn.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*)::int
            FROM pack_learning.module m
            INNER JOIN pack_learning.program p ON p.id = m.program_id
            WHERE m.id = @ModuleId AND p.status = 'published'
              AND (p.tenant_id IS NULL OR p.tenant_id = @TenantId)
            """,
            new { ModuleId = moduleId, TenantId });
        if (exists == 0)
            throw new InvalidOperationException("Không tìm thấy bài học.");

        await conn.ExecuteAsync(
            """
            INSERT INTO pack_learning.module_tenant_override (
                tenant_id, module_id, title, summary, body_markdown, updated_by_user_id, updated_at
            )
            VALUES (
                @TenantId, @ModuleId, @Title, @Summary, @Body, @UserId, NOW()
            )
            ON CONFLICT (tenant_id, module_id) DO UPDATE SET
                title = EXCLUDED.title,
                summary = EXCLUDED.summary,
                body_markdown = EXCLUDED.body_markdown,
                updated_by_user_id = EXCLUDED.updated_by_user_id,
                updated_at = NOW()
            """,
            new
            {
                TenantId,
                ModuleId = moduleId,
                Title = string.IsNullOrWhiteSpace(request.Title) ? null : request.Title.Trim(),
                Summary = string.IsNullOrWhiteSpace(request.Summary) ? null : request.Summary.Trim(),
                Body = request.BodyMarkdown.Trim(),
                UserId = UserId == Guid.Empty ? (Guid?)null : UserId,
            });

        return (await GetModuleAsync(moduleId, cancellationToken))!;
    }

    public async Task<LearningModuleDetailDto> RevertModuleTenantContentAsync(
        Guid moduleId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(
            """
            DELETE FROM pack_learning.module_tenant_override
            WHERE tenant_id = @TenantId AND module_id = @ModuleId
            """,
            new { TenantId, ModuleId = moduleId });

        var mod = await GetModuleAsync(moduleId, cancellationToken);
        if (mod is null)
            throw new InvalidOperationException("Không tìm thấy bài học.");
        return mod;
    }

    public async Task<LearningEnrollmentDto?> GetEnrollmentAsync(
        Guid employeeId,
        Guid programId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await QueryEnrollmentAsync(conn, employeeId, programId);
    }

    public async Task<LearningEnrollmentDto> UpsertEnrollmentAsync(
        Guid employeeId,
        Guid programId,
        Guid? assignedByUserId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);

        var programOk = await conn.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*)::int FROM pack_learning.program
            WHERE id = @ProgramId AND status = 'published'
              AND (tenant_id IS NULL OR tenant_id = @TenantId)
            """,
            new { ProgramId = programId, TenantId });
        if (programOk == 0)
            throw new InvalidOperationException("Không tìm thấy lộ trình học.");

        var employeeOk = await conn.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*)::int FROM employees
            WHERE id = @EmployeeId AND tenant_id = @TenantId AND deleted_at IS NULL
            """,
            new { EmployeeId = employeeId, TenantId });
        if (employeeOk == 0)
            throw new InvalidOperationException("Không tìm thấy nhân viên.");

        await conn.ExecuteAsync(
            """
            INSERT INTO pack_learning.enrollment (
                tenant_id, employee_id, program_id, status, assigned_by_user_id
            )
            VALUES (@TenantId, @EmployeeId, @ProgramId, 'assigned', @AssignedBy)
            ON CONFLICT (tenant_id, employee_id, program_id) DO UPDATE SET
                status = CASE
                    WHEN pack_learning.enrollment.status = 'cancelled' THEN 'assigned'
                    ELSE pack_learning.enrollment.status
                END,
                assigned_by_user_id = COALESCE(EXCLUDED.assigned_by_user_id, pack_learning.enrollment.assigned_by_user_id)
            """,
            new
            {
                TenantId,
                EmployeeId = employeeId,
                ProgramId = programId,
                AssignedBy = assignedByUserId,
            });

        var enrollment = await QueryEnrollmentAsync(conn, employeeId, programId)
            ?? throw new InvalidOperationException("Không tạo được enrollment.");

        // Ensure progress rows for all modules
        await conn.ExecuteAsync(
            """
            INSERT INTO pack_learning.module_progress (enrollment_id, module_id, status)
            SELECT @EnrollmentId, m.id, 'not_started'
            FROM pack_learning.module m
            WHERE m.program_id = @ProgramId
            ON CONFLICT (enrollment_id, module_id) DO NOTHING
            """,
            new { EnrollmentId = enrollment.Id, ProgramId = programId });

        return (await QueryEnrollmentAsync(conn, employeeId, programId))!;
    }

    public async Task<IReadOnlyList<LearningEnrollmentDto>> ListEnrollmentsAsync(
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<EnrollmentRow>(
            """
            SELECT e.id AS Id, e.program_id AS ProgramId, p.title AS ProgramTitle, p.code AS ProgramCode,
                   e.employee_id AS EmployeeId, emp.full_name AS EmployeeName, e.status AS Status,
                   e.assigned_at AS AssignedAt, e.started_at AS StartedAt, e.completed_at AS CompletedAt,
                   (SELECT COUNT(*)::int FROM pack_learning.module m WHERE m.program_id = e.program_id) AS ModulesTotal,
                   (SELECT COUNT(*)::int FROM pack_learning.module_progress mp
                    WHERE mp.enrollment_id = e.id AND mp.status = 'passed') AS ModulesPassed
            FROM pack_learning.enrollment e
            INNER JOIN pack_learning.program p ON p.id = e.program_id
            INNER JOIN employees emp ON emp.id = e.employee_id
            WHERE e.tenant_id = @TenantId AND e.status <> 'cancelled'
            ORDER BY e.assigned_at DESC
            """,
            new { TenantId });

        return rows.Select(MapEnrollment).ToList();
    }

    public async Task<IReadOnlyList<LearningModuleProgressDto>> ListProgressAsync(
        Guid enrollmentId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<(
            Guid ModuleId, string ModuleCode, string Title, string LevelCode, int SortOrder,
            string Status, int? ScorePct, int QuizAttempts, DateTime? AcknowledgedAt, bool RequireAck,
            bool RequireObservation, DateTime? ObservedAt, string? ObserverName, string? AcknowledgeSelfieUrl)>(
            """
            SELECT m.id, m.code, m.title, m.level_code, m.sort_order,
                   COALESCE(mp.status, 'not_started'),
                   mp.score_pct, COALESCE(mp.quiz_attempts, 0),
                   mp.acknowledged_at, m.require_ack,
                   COALESCE(m.require_observation, FALSE),
                   obs.observed_at,
                   ou.username,
                   mp.acknowledge_selfie_url
            FROM pack_learning.module m
            INNER JOIN pack_learning.enrollment e ON e.program_id = m.program_id
            LEFT JOIN pack_learning.module_progress mp
                ON mp.enrollment_id = e.id AND mp.module_id = m.id
            LEFT JOIN pack_learning.module_observation obs
                ON obs.enrollment_id = e.id AND obs.module_id = m.id AND obs.tenant_id = @TenantId
            LEFT JOIN public.users ou ON ou.id = obs.observed_by_user_id
            WHERE e.id = @EnrollmentId AND e.tenant_id = @TenantId
            ORDER BY m.sort_order
            """,
            new { EnrollmentId = enrollmentId, TenantId });

        return rows.Select(r => new LearningModuleProgressDto(
            r.ModuleId, r.ModuleCode, r.Title, r.LevelCode, r.SortOrder,
            r.Status, r.ScorePct, r.QuizAttempts, r.AcknowledgedAt, r.RequireAck,
            r.RequireObservation, r.ObservedAt, r.ObserverName, r.AcknowledgeSelfieUrl)).ToList();
    }

    public async Task StartModuleAsync(
        Guid employeeId,
        Guid moduleId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var enrollmentId = await EnsureEnrollmentForModuleAsync(conn, employeeId, moduleId);

        await conn.ExecuteAsync(
            """
            UPDATE pack_learning.module_progress SET
                status = CASE WHEN status = 'passed' THEN status ELSE 'in_progress' END,
                started_at = COALESCE(started_at, NOW()),
                updated_at = NOW()
            WHERE enrollment_id = @EnrollmentId AND module_id = @ModuleId
            """,
            new { EnrollmentId = enrollmentId, ModuleId = moduleId });

        await conn.ExecuteAsync(
            """
            UPDATE pack_learning.enrollment SET
                status = CASE WHEN status = 'completed' THEN status ELSE 'in_progress' END,
                started_at = COALESCE(started_at, NOW())
            WHERE id = @EnrollmentId
            """,
            new { EnrollmentId = enrollmentId });
    }

    public async Task AcknowledgeModuleAsync(
        Guid employeeId,
        Guid moduleId,
        string? selfieUrl,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var enrollmentId = await EnsureEnrollmentForModuleAsync(conn, employeeId, moduleId);

        var levelCode = await conn.ExecuteScalarAsync<string>(
            "SELECT level_code FROM pack_learning.module WHERE id = @ModuleId",
            new { ModuleId = moduleId });

        string? selfie = null;
        if (!string.IsNullOrWhiteSpace(selfieUrl)
            && string.Equals(levelCode, "L0", StringComparison.OrdinalIgnoreCase))
        {
            selfie = selfieUrl.Trim();
            if (selfie.Length > 500)
                throw new InvalidOperationException("URL ảnh selfie quá dài.");
        }

        var n = await conn.ExecuteAsync(
            """
            UPDATE pack_learning.module_progress SET
                acknowledged_at = NOW(),
                acknowledged_by_user_id = @UserId,
                acknowledge_selfie_url = COALESCE(@SelfieUrl, acknowledge_selfie_url),
                status = CASE WHEN status = 'not_started' THEN 'in_progress' ELSE status END,
                started_at = COALESCE(started_at, NOW()),
                updated_at = NOW()
            WHERE enrollment_id = @EnrollmentId AND module_id = @ModuleId
            """,
            new { EnrollmentId = enrollmentId, ModuleId = moduleId, UserId, SelfieUrl = selfie });

        if (n == 0)
            throw new InvalidOperationException("Không cập nhật được xác nhận.");
    }

    public async Task<SubmitLearningQuizResultDto> SubmitQuizAsync(
        Guid employeeId,
        Guid moduleId,
        IReadOnlyList<int> answers,
        bool practice,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        var enrollmentId = await EnsureEnrollmentForModuleAsync(conn, employeeId, moduleId, tx);

        var passScore = await conn.ExecuteScalarAsync<int>(
            """
            SELECT pass_score_pct FROM pack_learning.module WHERE id = @ModuleId
            """,
            new { ModuleId = moduleId },
            tx);

        var requireAck = await conn.ExecuteScalarAsync<bool>(
            """
            SELECT require_ack FROM pack_learning.module WHERE id = @ModuleId
            """,
            new { ModuleId = moduleId },
            tx);

        if (requireAck)
        {
            var ack = await conn.ExecuteScalarAsync<DateTime?>(
                """
                SELECT acknowledged_at FROM pack_learning.module_progress
                WHERE enrollment_id = @EnrollmentId AND module_id = @ModuleId
                """,
                new { EnrollmentId = enrollmentId, ModuleId = moduleId },
                tx);
            if (ack is null)
                throw new InvalidOperationException("Cần ký xác nhận đã đọc trước khi làm bài kiểm tra.");
        }

        var keys = (await conn.QueryAsync<(Guid Id, int Correct)>(
            """
            SELECT id, correct_option_index
            FROM pack_learning.quiz_question
            WHERE module_id = @ModuleId
            ORDER BY sort_order
            """,
            new { ModuleId = moduleId },
            tx)).ToList();

        if (keys.Count == 0)
            throw new InvalidOperationException("Bài học chưa có câu hỏi.");

        if (answers.Count != keys.Count)
            throw new ArgumentException($"Cần trả lời đủ {keys.Count} câu.");

        var correct = 0;
        for (var i = 0; i < keys.Count; i++)
        {
            if (answers[i] == keys[i].Correct)
                correct++;
        }

        var scorePct = (int)Math.Round(100.0 * correct / keys.Count);
        var passed = scorePct >= passScore;

        if (practice)
        {
            var currentStatus = await conn.ExecuteScalarAsync<string>(
                """
                SELECT status FROM pack_learning.module_progress
                WHERE enrollment_id = @EnrollmentId AND module_id = @ModuleId
                """,
                new { EnrollmentId = enrollmentId, ModuleId = moduleId },
                tx) ?? "in_progress";
            await tx.CommitAsync(cancellationToken);
            return new SubmitLearningQuizResultDto(
                passed, scorePct, passScore, currentStatus, []);
        }

        var status = passed ? "passed" : "failed";

        await conn.ExecuteAsync(
            """
            UPDATE pack_learning.module_progress SET
                status = @Status,
                score_pct = @ScorePct,
                quiz_attempts = quiz_attempts + 1,
                started_at = COALESCE(started_at, NOW()),
                completed_at = CASE WHEN @Passed THEN NOW() ELSE completed_at END,
                updated_at = NOW()
            WHERE enrollment_id = @EnrollmentId AND module_id = @ModuleId
            """,
            new
            {
                EnrollmentId = enrollmentId,
                ModuleId = moduleId,
                Status = status,
                ScorePct = scorePct,
                Passed = passed,
            },
            tx);

        if (passed)
        {
            var comps = (await conn.QueryAsync<string>(
                """
                SELECT UNNEST(competency_codes)
                FROM pack_learning.module
                WHERE id = @ModuleId
                """,
                new { ModuleId = moduleId },
                tx)).Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().ToList();

            var levelCode = await conn.ExecuteScalarAsync<string>(
                """
                SELECT level_code FROM pack_learning.module WHERE id = @ModuleId
                """,
                new { ModuleId = moduleId },
                tx) ?? "L0";

            foreach (var code in comps)
            {
                await conn.ExecuteAsync(
                    """
                    INSERT INTO pack_learning.credential (
                        tenant_id, employee_id, competency_code, level_code,
                        source_module_id, score_pct, earned_at
                    )
                    VALUES (
                        @TenantId, @EmployeeId, @Code, @LevelCode,
                        @ModuleId, @ScorePct, NOW()
                    )
                    ON CONFLICT (tenant_id, employee_id, competency_code) DO UPDATE SET
                        level_code = EXCLUDED.level_code,
                        source_module_id = EXCLUDED.source_module_id,
                        score_pct = EXCLUDED.score_pct,
                        earned_at = NOW()
                    """,
                    new
                    {
                        TenantId,
                        EmployeeId = employeeId,
                        Code = code,
                        LevelCode = levelCode,
                        ModuleId = moduleId,
                        ScorePct = scorePct,
                    },
                    tx);
            }

            var moduleTitle = await conn.ExecuteScalarAsync<string>(
                """
                SELECT title FROM pack_learning.module WHERE id = @ModuleId
                """,
                new { ModuleId = moduleId },
                tx) ?? "Bài học";

            // L0–L6 = lộ trình / level — không tạo huy hiệu mỗi lần hoàn thành (tránh quá nhiều huy hiệu).
            // Chỉ recognition feed + credential; huy hiệu khi xuất sắc 100% hoặc quản lý trao tay.
            await InsertRecognitionAsync(
                conn,
                tx,
                employeeId,
                "module_complete",
                $"Hoàn thành: {moduleTitle}",
                $"Đạt {scorePct}% — {levelCode}",
                badgeCode: null,
                badgeTitle: null);

            if (scorePct >= 100)
            {
                var perfectTitle = levelCode.ToUpperInvariant() switch
                {
                    "L0" => "L0 Xuất sắc — Onboarding",
                    "L1" => "L1 Xuất sắc — Bán tại quầy",
                    "L2" => "L2 Xuất sắc — Chăm sóc khách",
                    "L3" => "L3 Xuất sắc — An toàn thuốc",
                    "L4" => "L4 Xuất sắc — Vận hành ca",
                    "L5" => "L5 Xuất sắc — Tư vấn",
                    "L6" => "L6 Xuất sắc — Ca trưởng",
                    _ => $"{levelCode} Xuất sắc",
                };
                await InsertRecognitionAsync(
                    conn,
                    tx,
                    employeeId,
                    "badge_award",
                    $"Xuất sắc 100%: {moduleTitle}",
                    levelCode,
                    badgeCode: $"perfect_{levelCode.ToLowerInvariant()}",
                    badgeTitle: perfectTitle);
            }

            var remaining = await conn.ExecuteScalarAsync<int>(
                """
                SELECT COUNT(*)::int
                FROM pack_learning.module m
                LEFT JOIN pack_learning.module_progress mp
                    ON mp.module_id = m.id AND mp.enrollment_id = @EnrollmentId
                WHERE m.program_id = (SELECT program_id FROM pack_learning.enrollment WHERE id = @EnrollmentId)
                  AND COALESCE(mp.status, 'not_started') <> 'passed'
                """,
                new { EnrollmentId = enrollmentId },
                tx);

            if (remaining == 0)
            {
                await conn.ExecuteAsync(
                    """
                    UPDATE pack_learning.enrollment SET
                        status = 'completed',
                        completed_at = NOW()
                    WHERE id = @EnrollmentId
                    """,
                    new { EnrollmentId = enrollmentId },
                    tx);

                var programTitle = await conn.ExecuteScalarAsync<string>(
                    """
                    SELECT p.title
                    FROM pack_learning.enrollment e
                    INNER JOIN pack_learning.program p ON p.id = e.program_id
                    WHERE e.id = @EnrollmentId
                    """,
                    new { EnrollmentId = enrollmentId },
                    tx) ?? "Lộ trình";

                await InsertRecognitionAsync(
                    conn,
                    tx,
                    employeeId,
                    "course_complete",
                    $"Hoàn thành lộ trình: {programTitle}",
                    "Đã đạt 100% các bài trong lộ trình onboarding.",
                    "onboarding_complete",
                    "Onboarding hoàn thành");
            }
            else
            {
                await conn.ExecuteAsync(
                    """
                    UPDATE pack_learning.enrollment SET
                        status = 'in_progress',
                        started_at = COALESCE(started_at, NOW())
                    WHERE id = @EnrollmentId
                    """,
                    new { EnrollmentId = enrollmentId },
                    tx);
            }

            await tx.CommitAsync(cancellationToken);
            return new SubmitLearningQuizResultDto(passed, scorePct, passScore, status, comps);
        }

        await tx.CommitAsync(cancellationToken);
        return new SubmitLearningQuizResultDto(passed, scorePct, passScore, status, []);
    }

    public async Task<IReadOnlyList<LearningCredentialDto>> ListCredentialsAsync(
        Guid employeeId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<(string Code, string Level, int? Score, DateTime Earned, Guid? ModuleId)>(
            """
            SELECT competency_code, level_code, score_pct, earned_at, source_module_id
            FROM pack_learning.credential
            WHERE tenant_id = @TenantId AND employee_id = @EmployeeId
            ORDER BY earned_at DESC
            """,
            new { TenantId, EmployeeId = employeeId });

        return rows.Select(r => new LearningCredentialDto(r.Code, r.Level, r.Score, r.Earned, r.ModuleId)).ToList();
    }

    public async Task<IReadOnlyList<LearningCompetencyRosterItemDto>> ListCompetencyRosterAsync(
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<(
            Guid EmployeeId, string EmployeeName, int CredCount, int Passed, int Total,
            string? Status, string[]? Codes)>(
            """
            WITH emp AS (
                SELECT e.id, e.full_name
                FROM employees e
                WHERE e.tenant_id = @TenantId AND e.deleted_at IS NULL
            ),
            enr AS (
                SELECT DISTINCT ON (employee_id)
                    employee_id, status, id AS enrollment_id, program_id
                FROM pack_learning.enrollment
                WHERE tenant_id = @TenantId AND status <> 'cancelled'
                ORDER BY employee_id, assigned_at DESC
            )
            SELECT
                emp.id,
                emp.full_name,
                (SELECT COUNT(*)::int FROM pack_learning.credential c
                 WHERE c.tenant_id = @TenantId AND c.employee_id = emp.id) AS cred_count,
                COALESCE((
                    SELECT COUNT(*)::int FROM pack_learning.module_progress mp
                    WHERE mp.enrollment_id = enr.enrollment_id AND mp.status = 'passed'
                ), 0) AS passed,
                COALESCE((
                    SELECT COUNT(*)::int FROM pack_learning.module m WHERE m.program_id = enr.program_id
                ), 0) AS total,
                enr.status,
                COALESCE((
                    SELECT array_agg(c.competency_code ORDER BY c.competency_code)
                    FROM pack_learning.credential c
                    WHERE c.tenant_id = @TenantId AND c.employee_id = emp.id
                ), '{}'::text[]) AS codes
            FROM emp
            LEFT JOIN enr ON enr.employee_id = emp.id
            ORDER BY emp.full_name
            """,
            new { TenantId });

        return rows.Select(r => new LearningCompetencyRosterItemDto(
            r.EmployeeId,
            r.EmployeeName,
            r.CredCount,
            r.Passed,
            r.Total,
            r.Status,
            r.Codes ?? [])).ToList();
    }

    public async Task<LearningGateCheckDto> CheckGateAsync(
        Guid employeeId,
        string permissionCode,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var permission = permissionCode.Trim();

        var policy = await conn.QuerySingleOrDefaultAsync<(string? Mode, string[]? Required)>(
            """
            SELECT mode, required_competency_codes
            FROM pack_learning.gate_policy
            WHERE is_active AND permission_code = @Permission
            ORDER BY pack_code
            LIMIT 1
            """,
            new { Permission = permission });

        if (string.IsNullOrEmpty(policy.Mode))
        {
            return new LearningGateCheckDto(
                permission, "none", true, false, [], [],
                "Không có chính sách gate cho quyền này.");
        }

        var mode = policy.Mode;
        var required = policy.Required ?? [];

        var owned = (await conn.QueryAsync<string>(
            """
            SELECT competency_code FROM pack_learning.credential
            WHERE tenant_id = @TenantId AND employee_id = @EmployeeId
            """,
            new { TenantId, EmployeeId = employeeId })).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missing = required.Where(c => !owned.Contains(c)).ToList();

        var hasOverride = await conn.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*)::int FROM pack_learning.gate_override
            WHERE tenant_id = @TenantId AND employee_id = @EmployeeId
              AND permission_code = @Permission AND revoked_at IS NULL
              AND (expires_at IS NULL OR expires_at > NOW())
            """,
            new { TenantId, EmployeeId = employeeId, Permission = permission }) > 0;

        var satisfied = missing.Count == 0 || hasOverride;
        var message = satisfied
            ? (hasOverride && missing.Count > 0
                ? "Đang dùng override tạm của quản lý."
                : "Đủ năng lực theo chính sách.")
            : $"Thiếu competency: {string.Join(", ", missing)} (mode={mode}).";

        return new LearningGateCheckDto(
            permission, mode, satisfied, hasOverride, required, missing, message);
    }

    public async Task CreateGateOverrideAsync(
        Guid employeeId,
        string permissionCode,
        string reason,
        DateTime? expiresAt,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 5)
            throw new ArgumentException("Lý do override cần ít nhất 5 ký tự.");

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(
            """
            UPDATE pack_learning.gate_override SET revoked_at = NOW()
            WHERE tenant_id = @TenantId AND employee_id = @EmployeeId
              AND permission_code = @Permission AND revoked_at IS NULL
            """,
            new { TenantId, EmployeeId = employeeId, Permission = permissionCode.Trim() });

        await conn.ExecuteAsync(
            """
            INSERT INTO pack_learning.gate_override (
                tenant_id, employee_id, permission_code, reason, expires_at, created_by_user_id
            )
            VALUES (@TenantId, @EmployeeId, @Permission, @Reason, @ExpiresAt, @UserId)
            """,
            new
            {
                TenantId,
                EmployeeId = employeeId,
                Permission = permissionCode.Trim(),
                Reason = reason.Trim(),
                ExpiresAt = expiresAt,
                UserId,
            });
    }

    public async Task<LearningEvaluationDto> UpsertEvaluationAsync(
        UpsertLearningEvaluationRequest request,
        CancellationToken cancellationToken)
    {
        static int Clamp(int v) => Math.Clamp(v, 0, 100);
        if (request.PeriodMonth is < 1 or > 12)
            throw new ArgumentException("Tháng không hợp lệ.");
        if (request.PeriodYear < 2020 || request.PeriodYear > 2100)
            throw new ArgumentException("Năm không hợp lệ.");

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var empOk = await conn.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*)::int FROM employees
            WHERE id = @EmployeeId AND tenant_id = @TenantId AND deleted_at IS NULL
            """,
            new { request.EmployeeId, TenantId });
        if (empOk == 0)
            throw new InvalidOperationException("Không tìm thấy nhân viên.");

        var id = await conn.ExecuteScalarAsync<Guid>(
            """
            INSERT INTO pack_learning.evaluation (
                tenant_id, employee_id, period_year, period_month,
                score_knowledge, score_attitude, score_care, score_stock, score_discipline,
                comment, reviewed_by_user_id, reviewed_at, updated_at
            )
            VALUES (
                @TenantId, @EmployeeId, @Year, @Month,
                @K, @A, @C, @S, @D,
                @Comment, @UserId, NOW(), NOW()
            )
            ON CONFLICT (tenant_id, employee_id, period_year, period_month) DO UPDATE SET
                score_knowledge = EXCLUDED.score_knowledge,
                score_attitude = EXCLUDED.score_attitude,
                score_care = EXCLUDED.score_care,
                score_stock = EXCLUDED.score_stock,
                score_discipline = EXCLUDED.score_discipline,
                comment = EXCLUDED.comment,
                reviewed_by_user_id = EXCLUDED.reviewed_by_user_id,
                reviewed_at = NOW(),
                updated_at = NOW()
            RETURNING id
            """,
            new
            {
                TenantId,
                request.EmployeeId,
                Year = request.PeriodYear,
                Month = request.PeriodMonth,
                K = Clamp(request.ScoreKnowledge),
                A = Clamp(request.ScoreAttitude),
                C = Clamp(request.ScoreCare),
                S = Clamp(request.ScoreStock),
                D = Clamp(request.ScoreDiscipline),
                Comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim(),
                UserId,
            });

        var row = await conn.QuerySingleAsync<(
            Guid Id, Guid EmployeeId, string EmployeeName, int Year, int Month,
            int K, int A, int C, int S, int D, string? Comment, DateTime ReviewedAt,
            string? EmpFeedback, string? NextGoal, DateTime? RespondedAt, int? Pulse)>(
            """
            SELECT ev.id, ev.employee_id, emp.full_name, ev.period_year, ev.period_month,
                   ev.score_knowledge, ev.score_attitude, ev.score_care, ev.score_stock,
                   ev.score_discipline, ev.comment, ev.reviewed_at,
                   ev.employee_feedback, ev.next_month_goal, ev.employee_responded_at,
                   ev.engagement_pulse
            FROM pack_learning.evaluation ev
            INNER JOIN employees emp ON emp.id = ev.employee_id
            WHERE ev.id = @Id
            """,
            new { Id = id });

        var avg = (int)Math.Round((row.K + row.A + row.C + row.S + row.D) / 5.0);
        return new LearningEvaluationDto(
            row.Id, row.EmployeeId, row.EmployeeName, row.Year, row.Month,
            row.K, row.A, row.C, row.S, row.D, avg, row.Comment, row.ReviewedAt,
            row.EmpFeedback, row.NextGoal, row.RespondedAt, row.Pulse);
    }

    public async Task<IReadOnlyList<LearningEvaluationDto>> ListEvaluationsAsync(
        int? year,
        int? month,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<(
            Guid Id, Guid EmployeeId, string EmployeeName, int Year, int Month,
            int K, int A, int C, int S, int D, string? Comment, DateTime ReviewedAt,
            string? EmpFeedback, string? NextGoal, DateTime? RespondedAt, int? Pulse)>(
            """
            SELECT ev.id, ev.employee_id, emp.full_name, ev.period_year, ev.period_month,
                   ev.score_knowledge, ev.score_attitude, ev.score_care, ev.score_stock,
                   ev.score_discipline, ev.comment, ev.reviewed_at,
                   ev.employee_feedback, ev.next_month_goal, ev.employee_responded_at,
                   ev.engagement_pulse
            FROM pack_learning.evaluation ev
            INNER JOIN employees emp ON emp.id = ev.employee_id
            WHERE ev.tenant_id = @TenantId
              AND (@Year IS NULL OR ev.period_year = @Year)
              AND (@Month IS NULL OR ev.period_month = @Month)
            ORDER BY ev.period_year DESC, ev.period_month DESC, emp.full_name
            """,
            new { TenantId, Year = year, Month = month });

        return rows.Select(r =>
        {
            var avg = (int)Math.Round((r.K + r.A + r.C + r.S + r.D) / 5.0);
            return new LearningEvaluationDto(
                r.Id, r.EmployeeId, r.EmployeeName, r.Year, r.Month,
                r.K, r.A, r.C, r.S, r.D, avg, r.Comment, r.ReviewedAt,
                r.EmpFeedback, r.NextGoal, r.RespondedAt, r.Pulse);
        }).ToList();
    }

    public async Task<IReadOnlyList<LearningEvaluationDto>> ListEvaluationsForEmployeeAsync(
        Guid employeeId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<(
            Guid Id, Guid EmployeeId, string EmployeeName, int Year, int Month,
            int K, int A, int C, int S, int D, string? Comment, DateTime ReviewedAt,
            string? EmpFeedback, string? NextGoal, DateTime? RespondedAt, int? Pulse)>(
            """
            SELECT ev.id, ev.employee_id, emp.full_name, ev.period_year, ev.period_month,
                   ev.score_knowledge, ev.score_attitude, ev.score_care, ev.score_stock,
                   ev.score_discipline, ev.comment, ev.reviewed_at,
                   ev.employee_feedback, ev.next_month_goal, ev.employee_responded_at,
                   ev.engagement_pulse
            FROM pack_learning.evaluation ev
            INNER JOIN employees emp ON emp.id = ev.employee_id
            WHERE ev.tenant_id = @TenantId AND ev.employee_id = @EmployeeId
            ORDER BY ev.period_year DESC, ev.period_month DESC
            LIMIT 12
            """,
            new { TenantId, EmployeeId = employeeId });

        return rows.Select(r =>
        {
            var avg = (int)Math.Round((r.K + r.A + r.C + r.S + r.D) / 5.0);
            return new LearningEvaluationDto(
                r.Id, r.EmployeeId, r.EmployeeName, r.Year, r.Month,
                r.K, r.A, r.C, r.S, r.D, avg, r.Comment, r.ReviewedAt,
                r.EmpFeedback, r.NextGoal, r.RespondedAt, r.Pulse);
        }).ToList();
    }

    public async Task<LearningEvaluationDto> SubmitEmployeeEvaluationFeedbackAsync(
        Guid employeeId,
        Guid evaluationId,
        SubmitLearningEvaluationFeedbackRequest request,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var owner = await conn.ExecuteScalarAsync<Guid?>(
            """
            SELECT employee_id FROM pack_learning.evaluation
            WHERE id = @Id AND tenant_id = @TenantId
            """,
            new { Id = evaluationId, TenantId });
        if (owner is null)
            throw new InvalidOperationException("Không tìm thấy đánh giá.");
        if (owner.Value != employeeId)
            throw new InvalidOperationException("Chỉ nhân viên được đánh giá mới phản hồi được.");

        await conn.ExecuteAsync(
            """
            UPDATE pack_learning.evaluation SET
                employee_feedback = @Feedback,
                next_month_goal = @Goal,
                engagement_pulse = @Pulse,
                employee_responded_at = NOW(),
                updated_at = NOW()
            WHERE id = @Id AND tenant_id = @TenantId
            """,
            new
            {
                Id = evaluationId,
                TenantId,
                Feedback = string.IsNullOrWhiteSpace(request.EmployeeFeedback)
                    ? null
                    : request.EmployeeFeedback.Trim(),
                Goal = string.IsNullOrWhiteSpace(request.NextMonthGoal)
                    ? null
                    : request.NextMonthGoal.Trim(),
                Pulse = request.EngagementPulse is >= 1 and <= 5
                    ? request.EngagementPulse
                    : null,
            });

        var list = await ListEvaluationsForEmployeeAsync(employeeId, cancellationToken);
        return list.First(e => e.Id == evaluationId);
    }

    public async Task<LearningRecognitionDto> CreateRecognitionAsync(
        CreateLearningRecognitionRequest request,
        CancellationToken cancellationToken)
    {
        var kind = (request.Kind ?? "custom").Trim().ToLowerInvariant();
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "course_complete", "module_complete", "birthday", "work_anniversary",
            "customer_praise", "customer_feedback", "custom", "badge_award",
        };
        if (!allowed.Contains(kind))
            throw new ArgumentException("Loại ghi nhận không hợp lệ.");
        if (string.IsNullOrWhiteSpace(request.Title))
            throw new ArgumentException("Thiếu tiêu đề.");

        int? customerRating = null;
        if (request.CustomerRating is int rating)
        {
            if (rating is < 1 or > 5)
                throw new ArgumentException("Sao khách phải từ 1 đến 5.");
            customerRating = rating;
        }

        var title = request.Title.Trim();
        var badgeCode = string.IsNullOrWhiteSpace(request.BadgeCode) ? null : request.BadgeCode.Trim();
        var isCustomerKind = kind is "customer_praise" or "customer_feedback"
            || string.Equals(badgeCode, "customer_praise", StringComparison.OrdinalIgnoreCase);

        if (isCustomerKind && customerRating is int stars)
        {
            // Đồng bộ title với app khách để feed luôn parse được sao.
            if (!title.Contains('★', StringComparison.Ordinal) && !Regex.IsMatch(title, @"đánh giá\s*[1-5]", RegexOptions.IgnoreCase))
                title = $"Khách đánh giá {stars}★";
            if (stars >= 4)
            {
                kind = "customer_praise";
                badgeCode ??= "customer_praise";
            }
            else
            {
                kind = "customer_feedback";
                // Không gắn badge «Được khách hàng khen» khi <4★
                if (string.Equals(badgeCode, "customer_praise", StringComparison.OrdinalIgnoreCase))
                    badgeCode = null;
            }
        }

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        var empOk = await conn.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*)::int FROM employees
            WHERE id = @EmployeeId AND tenant_id = @TenantId AND deleted_at IS NULL
            """,
            new { request.EmployeeId, TenantId },
            tx);
        if (empOk == 0)
            throw new InvalidOperationException("Không tìm thấy nhân viên.");

        var badgeTitle = badgeCode is null
            ? null
            : badgeCode.ToLowerInvariant() switch
            {
                "customer_praise" => "Được khách hàng khen",
                "zero_error_shift" => "Không sai sót",
                "mentor" => "Mentor",
                "complete_l1" => "Hoàn thành L1 — POS",
                "complete_l2" => "Chuyên gia CRM",
                "complete_l3" => "Thành thạo nhập hàng",
                "complete_l4" => "Làm chủ ca",
                "tenure_12m" => "12 tháng gắn bó",
                "close_streak_7" => "7 ngày đóng ca liên tục",
                "onboarding_complete" => "Onboarding hoàn thành",
                _ => title,
            };
        var id = await InsertRecognitionAsync(
            conn,
            tx,
            request.EmployeeId,
            kind,
            title,
            string.IsNullOrWhiteSpace(request.Body) ? null : request.Body.Trim(),
            badgeCode,
            badgeTitle,
            request.IsPublic);

        await tx.CommitAsync(cancellationToken);

        var row = await conn.QuerySingleAsync<(
            Guid Id, Guid EmployeeId, string Name, string Kind, string Title, string? Body, string? Badge, DateTime At)>(
            """
            SELECT r.id, r.employee_id, emp.full_name, r.kind, r.title, r.body, r.badge_code, r.created_at
            FROM pack_learning.recognition r
            INNER JOIN employees emp ON emp.id = r.employee_id
            WHERE r.id = @Id
            """,
            new { Id = id });

        return new LearningRecognitionDto(
            row.Id, row.EmployeeId, row.Name, row.Kind, row.Title, row.Body, row.Badge, row.At, customerRating);
    }

    public async Task<IReadOnlyList<LearningRecognitionDto>> ListRecognitionsAsync(
        int take,
        Guid? employeeId,
        CancellationToken cancellationToken)
    {
        take = Math.Clamp(take, 1, 100);
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<(
            Guid Id, Guid EmployeeId, string Name, string Kind, string Title, string? Body, string? Badge, DateTime At, int? CustomerRating)>(
            """
            SELECT r.id, r.employee_id, emp.full_name, r.kind, r.title, r.body, r.badge_code, r.created_at,
                   fb.rating AS CustomerRating
            FROM pack_learning.recognition r
            INNER JOIN employees emp ON emp.id = r.employee_id
            LEFT JOIN pack_learning.customer_sale_feedback fb
                ON fb.recognition_id = r.id AND fb.tenant_id = r.tenant_id
            WHERE r.tenant_id = @TenantId
              AND r.is_public
              AND (@EmployeeId IS NULL OR r.employee_id = @EmployeeId)
            ORDER BY r.created_at DESC
            LIMIT @Take
            """,
            new { TenantId, Take = take, EmployeeId = employeeId });

        return rows.Select(r => new LearningRecognitionDto(
            r.Id, r.EmployeeId, r.Name, r.Kind, r.Title, r.Body, r.Badge, r.At, r.CustomerRating)).ToList();
    }

    public async Task<IReadOnlyList<LearningCustomerFeedbackDto>> ListRecentCustomerFeedbackAsync(
        int hours,
        int take,
        CancellationToken cancellationToken)
    {
        hours = Math.Clamp(hours, 1, 168);
        take = Math.Clamp(take, 1, 50);
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<(
            Guid Id, Guid? EmployeeId, string? Name, int Rating, string? Comment, DateTime At, Guid? RecognitionId)>(
            """
            SELECT fb.id,
                   fb.employee_id,
                   emp.full_name,
                   fb.rating,
                   fb.comment,
                   fb.created_at,
                   fb.recognition_id
            FROM pack_learning.customer_sale_feedback fb
            LEFT JOIN employees emp
                ON emp.id = fb.employee_id
               AND emp.tenant_id = fb.tenant_id
               AND emp.deleted_at IS NULL
            WHERE fb.tenant_id = @TenantId
              AND fb.created_at >= NOW() - (@Hours * INTERVAL '1 hour')
            ORDER BY fb.created_at DESC
            LIMIT @Take
            """,
            new { TenantId, Hours = hours, Take = take });

        return rows.Select(r => new LearningCustomerFeedbackDto(
            r.Id,
            r.EmployeeId,
            string.IsNullOrWhiteSpace(r.Name) ? "Chưa gắn nhân viên ca" : r.Name!,
            r.Rating,
            r.Comment,
            r.At,
            r.RecognitionId)).ToList();
    }

    public async Task<IReadOnlyList<LearningBadgeDto>> ListBadgesForEmployeeAsync(
        Guid employeeId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<(string Code, string Title, DateTime At)>(
            """
            SELECT badge_code, title, earned_at
            FROM pack_learning.badge
            WHERE tenant_id = @TenantId AND employee_id = @EmployeeId
            ORDER BY earned_at DESC
            """,
            new { TenantId, EmployeeId = employeeId });

        return rows.Select(r => new LearningBadgeDto(r.Code, r.Title, r.At)).ToList();
    }

    public async Task<IReadOnlyList<LearningCareerLevelDto>> ListCareerLevelsAsync(
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<(
            Guid Id, string Code, string Title, string? Summary, int Sort,
            int Months, int Eval, string[] Comps)>(
            """
            SELECT id, code, title, summary, sort_order,
                   min_months_tenure, min_avg_evaluate, required_competency_codes
            FROM pack_learning.career_level
            WHERE tenant_id = @TenantId AND is_active
            ORDER BY sort_order, code
            """,
            new { TenantId });

        return rows.Select(r => new LearningCareerLevelDto(
            r.Id, r.Code, r.Title, r.Summary, r.Sort, r.Months, r.Eval,
            r.Comps ?? [])).ToList();
    }

    public async Task<IReadOnlyList<LearningCareerRosterItemDto>> ListCareerRosterAsync(
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var levels = (await ListCareerLevelsAsync(cancellationToken)).OrderBy(l => l.SortOrder).ToList();
        var employees = (await conn.QueryAsync<(Guid Id, string Name, DateTime? Hire, DateTime Created)>(
            """
            SELECT id, full_name, hire_date, created_at
            FROM employees
            WHERE tenant_id = @TenantId AND deleted_at IS NULL
            ORDER BY full_name
            """,
            new { TenantId })).ToList();

        var careers = (await conn.QueryAsync<(Guid EmployeeId, Guid LevelId)>(
            """
            SELECT employee_id, level_id
            FROM pack_learning.employee_career
            WHERE tenant_id = @TenantId
            """,
            new { TenantId })).ToDictionary(x => x.EmployeeId, x => x.LevelId);

        var evals = (await conn.QueryAsync<(Guid EmployeeId, int Avg)>(
            """
            SELECT DISTINCT ON (employee_id) employee_id,
                ROUND((score_knowledge + score_attitude + score_care + score_stock + score_discipline) / 5.0)::int
            FROM pack_learning.evaluation
            WHERE tenant_id = @TenantId
            ORDER BY employee_id, period_year DESC, period_month DESC
            """,
            new { TenantId })).ToDictionary(x => x.EmployeeId, x => x.Avg);

        var credCounts = (await conn.QueryAsync<(Guid EmployeeId, int Cnt)>(
            """
            SELECT employee_id, COUNT(*)::int
            FROM pack_learning.credential
            WHERE tenant_id = @TenantId
            GROUP BY employee_id
            """,
            new { TenantId })).ToDictionary(x => x.EmployeeId, x => x.Cnt);

        var credSets = (await conn.QueryAsync<(Guid EmployeeId, string Code)>(
            """
            SELECT employee_id, competency_code
            FROM pack_learning.credential
            WHERE tenant_id = @TenantId
            """,
            new { TenantId }))
            .GroupBy(x => x.EmployeeId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Code).ToHashSet(StringComparer.OrdinalIgnoreCase));

        var result = new List<LearningCareerRosterItemDto>();
        var now = DateTime.UtcNow.Date;
        foreach (var emp in employees)
        {
            careers.TryGetValue(emp.Id, out var levelId);
            var current = levelId == Guid.Empty
                ? null
                : levels.FirstOrDefault(l => l.Id == levelId);
            if (current is null && levels.Count > 0)
                current = levels[0];

            var next = current is null
                ? levels.FirstOrDefault()
                : levels.FirstOrDefault(l => l.SortOrder > current.SortOrder);

            var hire = emp.Hire?.Date ?? emp.Created.Date;
            var months = Math.Max(0, ((now.Year - hire.Year) * 12) + now.Month - hire.Month);
            evals.TryGetValue(emp.Id, out var avg);
            int? avgNullable = evals.ContainsKey(emp.Id) ? avg : null;
            credCounts.TryGetValue(emp.Id, out var credCnt);
            credSets.TryGetValue(emp.Id, out var owned);
            owned ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var missing = new List<string>();
            var eligible = false;
            if (next is not null)
            {
                if (months < next.MinMonthsTenure)
                    missing.Add($"Thâm niên ≥ {next.MinMonthsTenure} tháng (hiện {months})");
                if (next.MinAvgEvaluate > 0)
                {
                    if (avgNullable is null)
                        missing.Add($"Cần đánh giá ≥ {next.MinAvgEvaluate}");
                    else if (avgNullable.Value < next.MinAvgEvaluate)
                        missing.Add($"Điểm Evaluate ≥ {next.MinAvgEvaluate} (hiện {avgNullable})");
                }
                foreach (var c in next.RequiredCompetencyCodes)
                {
                    if (!owned.Contains(c))
                        missing.Add($"Thiếu credential: {c}");
                }
                eligible = missing.Count == 0;
            }

            result.Add(new LearningCareerRosterItemDto(
                emp.Id,
                emp.Name,
                current?.Id,
                current?.Code,
                current?.Title,
                next?.Id,
                next?.Code,
                next?.Title,
                eligible,
                missing,
                months,
                avgNullable,
                credCnt));
        }

        return result;
    }

    public async Task<LearningCareerPromotionDto> PromoteCareerAsync(
        PromoteLearningCareerRequest request,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        var empOk = await conn.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*)::int FROM employees
            WHERE id = @EmployeeId AND tenant_id = @TenantId AND deleted_at IS NULL
            """,
            new { request.EmployeeId, TenantId },
            tx);
        if (empOk == 0)
            throw new InvalidOperationException("Không tìm thấy nhân viên.");

        var toLevel = await conn.QuerySingleOrDefaultAsync<(
            Guid Id, string Code, string Title, int Sort, int Months, int Eval, string[] Comps)>(
            """
            SELECT id, code, title, sort_order, min_months_tenure, min_avg_evaluate, required_competency_codes
            FROM pack_learning.career_level
            WHERE id = @Id AND tenant_id = @TenantId AND is_active
            """,
            new { Id = request.ToLevelId, TenantId },
            tx);
        if (toLevel.Id == Guid.Empty)
            throw new InvalidOperationException("Bậc đích không hợp lệ.");

        var fromLevel = await conn.QuerySingleOrDefaultAsync<(Guid? Id, string? Title, int? Sort)>(
            """
            SELECT cl.id, cl.title, cl.sort_order
            FROM pack_learning.employee_career ec
            INNER JOIN pack_learning.career_level cl ON cl.id = ec.level_id
            WHERE ec.tenant_id = @TenantId AND ec.employee_id = @EmployeeId
            """,
            new { TenantId, request.EmployeeId },
            tx);

        if (fromLevel.Sort is int fromSort && toLevel.Sort <= fromSort && !request.Force)
            throw new InvalidOperationException("Bậc đích phải cao hơn bậc hiện tại (hoặc dùng Force).");

        // Recompute eligibility quickly
        var roster = await ListCareerRosterAsync(cancellationToken);
        var item = roster.FirstOrDefault(r => r.EmployeeId == request.EmployeeId);
        var missing = item?.MissingReasons?.ToList() ?? [];
        if (item?.NextLevelId != request.ToLevelId)
        {
            // Allow promote to any higher level with Force; otherwise only next
            if (!request.Force)
                throw new InvalidOperationException("Chỉ duyệt lên bậc kế tiếp, hoặc đánh dấu Force.");
        }
        var eligible = item is { EligibleForNext: true } && item.NextLevelId == request.ToLevelId;
        if (!eligible && !request.Force)
            throw new InvalidOperationException(
                missing.Count > 0
                    ? "Chưa đủ điều kiện: " + string.Join("; ", missing)
                    : "Chưa đủ điều kiện lên bậc.");

        await conn.ExecuteAsync(
            """
            INSERT INTO pack_learning.employee_career (
                tenant_id, employee_id, level_id, assigned_at, assigned_by_user_id, note
            )
            VALUES (@TenantId, @EmployeeId, @LevelId, NOW(), @UserId, @Note)
            ON CONFLICT (tenant_id, employee_id) DO UPDATE SET
                level_id = EXCLUDED.level_id,
                assigned_at = NOW(),
                assigned_by_user_id = EXCLUDED.assigned_by_user_id,
                note = EXCLUDED.note
            """,
            new
            {
                TenantId,
                request.EmployeeId,
                LevelId = request.ToLevelId,
                UserId = UserId == Guid.Empty ? (Guid?)null : UserId,
                Note = request.Comment,
            },
            tx);

        var promoId = await conn.ExecuteScalarAsync<Guid>(
            """
            INSERT INTO pack_learning.career_promotion (
                tenant_id, employee_id, from_level_id, to_level_id,
                status, eligibility_ok, missing_reasons, comment,
                decided_by_user_id, decided_at
            )
            VALUES (
                @TenantId, @EmployeeId, @FromId, @ToId,
                'approved', @Ok, @Missing, @Comment,
                @UserId, NOW()
            )
            RETURNING id
            """,
            new
            {
                TenantId,
                request.EmployeeId,
                FromId = fromLevel.Id,
                ToId = request.ToLevelId,
                Ok = eligible,
                Missing = missing.ToArray(),
                Comment = request.Comment,
                UserId = UserId == Guid.Empty ? (Guid?)null : UserId,
            },
            tx);

        await tx.CommitAsync(cancellationToken);

        var name = await conn.ExecuteScalarAsync<string>(
            "SELECT full_name FROM employees WHERE id = @Id",
            new { Id = request.EmployeeId }) ?? "";

        return new LearningCareerPromotionDto(
            promoId,
            request.EmployeeId,
            name,
            fromLevel.Title,
            toLevel.Title,
            "approved",
            eligible,
            missing,
            request.Comment,
            DateTime.UtcNow);
    }

    public async Task<IReadOnlyList<LearningCareerPromotionDto>> ListCareerPromotionsAsync(
        int take,
        CancellationToken cancellationToken)
    {
        take = Math.Clamp(take, 1, 100);
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<(
            Guid Id, Guid EmployeeId, string Name, string? FromTitle, string ToTitle,
            string Status, bool Ok, string[] Missing, string? Comment, DateTime At)>(
            """
            SELECT p.id, p.employee_id, emp.full_name,
                   fl.title, tl.title,
                   p.status, p.eligibility_ok, p.missing_reasons, p.comment, p.decided_at
            FROM pack_learning.career_promotion p
            INNER JOIN employees emp ON emp.id = p.employee_id
            LEFT JOIN pack_learning.career_level fl ON fl.id = p.from_level_id
            INNER JOIN pack_learning.career_level tl ON tl.id = p.to_level_id
            WHERE p.tenant_id = @TenantId
            ORDER BY p.decided_at DESC
            LIMIT @Take
            """,
            new { TenantId, Take = take });

        return rows.Select(r => new LearningCareerPromotionDto(
            r.Id, r.EmployeeId, r.Name, r.FromTitle, r.ToTitle,
            r.Status, r.Ok, r.Missing ?? [], r.Comment, r.At)).ToList();
    }

    public async Task<LearningPeopleDashboardDto> GetPeopleDashboardAsync(
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var year = now.Year;
        var month = now.Month;

        var employeeCount = await conn.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*)::int FROM employees
            WHERE tenant_id = @TenantId AND deleted_at IS NULL
            """,
            new { TenantId });

        var enrollStats = await conn.QuerySingleAsync<(int Enrolled, int Completed, int Passed, int Total)>(
            """
            SELECT
                COUNT(*)::int AS enrolled,
                COUNT(*) FILTER (WHERE e.status = 'completed')::int AS completed,
                COALESCE(SUM(
                    (SELECT COUNT(*)::int FROM pack_learning.module_progress mp
                     WHERE mp.enrollment_id = e.id AND mp.status = 'passed')
                ), 0)::int AS passed,
                COALESCE(SUM(
                    (SELECT COUNT(*)::int FROM pack_learning.module m WHERE m.program_id = e.program_id)
                ), 0)::int AS total
            FROM pack_learning.enrollment e
            WHERE e.tenant_id = @TenantId AND e.status <> 'cancelled'
            """,
            new { TenantId });

        var trainingPct = enrollStats.Total > 0
            ? (int)Math.Round(100.0 * enrollStats.Passed / enrollStats.Total)
            : 0;

        var credentialCount = await conn.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*)::int FROM pack_learning.credential WHERE tenant_id = @TenantId
            """,
            new { TenantId });

        var avgEval = await conn.ExecuteScalarAsync<decimal?>(
            """
            SELECT AVG((score_knowledge + score_attitude + score_care + score_stock + score_discipline) / 5.0)
            FROM pack_learning.evaluation
            WHERE tenant_id = @TenantId AND period_year = @Year AND period_month = @Month
            """,
            new { TenantId, Year = year, Month = month });

        var evalCount = await conn.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*)::int FROM pack_learning.evaluation
            WHERE tenant_id = @TenantId AND period_year = @Year AND period_month = @Month
            """,
            new { TenantId, Year = year, Month = month });

        var recognition30 = await conn.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*)::int FROM pack_learning.recognition
            WHERE tenant_id = @TenantId AND created_at >= NOW() - INTERVAL '30 days'
            """,
            new { TenantId });

        var badgeCount = await conn.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*)::int FROM pack_learning.badge WHERE tenant_id = @TenantId
            """,
            new { TenantId });

        var levelCounts = (await conn.QueryAsync<(string Code, string Title, int Cnt)>(
            """
            SELECT cl.code, cl.title, COUNT(ec.id)::int
            FROM pack_learning.career_level cl
            LEFT JOIN pack_learning.employee_career ec
                ON ec.level_id = cl.id AND ec.tenant_id = cl.tenant_id
            WHERE cl.tenant_id = @TenantId AND cl.is_active
            GROUP BY cl.code, cl.title, cl.sort_order
            ORDER BY cl.sort_order
            """,
            new { TenantId })).Select(r => new LearningCareerLevelCountDto(r.Code, r.Title, r.Cnt)).ToList();

        var evaluatedEmpIds = (await conn.QueryAsync<Guid>(
            """
            SELECT employee_id FROM pack_learning.evaluation
            WHERE tenant_id = @TenantId AND period_year = @Year AND period_month = @Month
            """,
            new { TenantId, Year = year, Month = month })).ToHashSet();

        var unevaluated = Math.Max(0, employeeCount - evaluatedEmpIds.Count);

        var missingPos = await conn.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*)::int
            FROM employees e
            WHERE e.tenant_id = @TenantId AND e.deleted_at IS NULL
              AND NOT EXISTS (
                SELECT 1 FROM pack_learning.credential c
                WHERE c.tenant_id = e.tenant_id AND c.employee_id = e.id
                  AND c.competency_code = 'pos_basic'
              )
            """,
            new { TenantId });

        var careerRoster = await ListCareerRosterAsync(cancellationToken);
        var eligiblePromo = careerRoster.Count(r => r.EligibleForNext);

        var actions = new List<string>();
        if (missingPos > 0)
            actions.Add($"{missingPos} NV chưa có credential POS (pos_basic) — gán/học bài L1, POS sẽ nhắc soft.");
        if (unevaluated > 0)
            actions.Add($"{unevaluated} NV chưa chấm Evaluate tháng {month}/{year} — mở Đánh giá, xem bằng chứng học rồi lưu điểm.");
        if (eligiblePromo > 0)
            actions.Add($"{eligiblePromo} NV đủ điều kiện lên bậc — vào Bậc nghề để duyệt (lợi ích lộ trình nghề rõ).");
        if (enrollStats.Enrolled == 0 && employeeCount > 0)
            actions.Add("Chưa ai được gán lộ trình học — vào Tiến độ để gán Onboarding L0–L5.");
        if (recognition30 == 0)
            actions.Add("30 ngày chưa có ghi nhận — đăng 1 dòng khách khen để đội thấy được khích lệ.");

        var pendingFeedback = await conn.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*)::int FROM pack_learning.evaluation
            WHERE tenant_id = @TenantId
              AND employee_responded_at IS NULL
              AND (
                (period_year = @Year AND period_month = @Month)
                OR (period_year = @PrevYear AND period_month = @PrevMonth)
              )
            """,
            new
            {
                TenantId,
                Year = year,
                Month = month,
                PrevYear = month == 1 ? year - 1 : year,
                PrevMonth = month == 1 ? 12 : month - 1,
            });

        var today = DateOnly.FromDateTime(now);
        var missingClose = await conn.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*)::int
            FROM branches b
            WHERE b.tenant_id = @TenantId
              AND b.deleted_at IS NULL
              AND b.status = 1
              AND NOT EXISTS (
                SELECT 1 FROM success_shift_checklist_run r
                WHERE r.tenant_id = b.tenant_id
                  AND r.branch_id = b.id
                  AND r.kind = 'close'
                  AND r.status = 'completed'
                  AND r.business_date = @Today::date
              )
            """,
            new { TenantId, Today = today });

        if (pendingFeedback > 0)
            actions.Add($"{pendingFeedback} đánh giá chưa có phản hồi NV — nhắc đội vào Học → Phản hồi chấm tháng.");
        if (missingClose > 0)
            actions.Add($"{missingClose} chi nhánh chưa hoàn thành checklist đóng ca hôm nay — nhắc đóng ca trên Success.");

        if (actions.Count == 0)
            actions.Add("Đội đang ổn theo số liệu hiện có: giữ nhịp học + chấm tháng + ghi nhận.");

        var weekPassed = await conn.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*)::int FROM pack_learning.module_progress mp
            INNER JOIN pack_learning.enrollment e ON e.id = mp.enrollment_id
            WHERE e.tenant_id = @TenantId AND mp.status = 'passed'
              AND COALESCE(mp.completed_at, mp.updated_at) >= NOW() - INTERVAL '7 days'
            """,
            new { TenantId });

        var weekPerfect = await conn.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*)::int FROM pack_learning.module_progress mp
            INNER JOIN pack_learning.enrollment e ON e.id = mp.enrollment_id
            WHERE e.tenant_id = @TenantId AND mp.status = 'passed' AND mp.score_pct >= 100
              AND COALESCE(mp.completed_at, mp.updated_at) >= NOW() - INTERVAL '7 days'
            """,
            new { TenantId });

        var weekRec = await conn.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*)::int FROM pack_learning.recognition
            WHERE tenant_id = @TenantId AND created_at >= NOW() - INTERVAL '7 days'
            """,
            new { TenantId });

        var weekPromo = await conn.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*)::int FROM pack_learning.career_promotion
            WHERE tenant_id = @TenantId AND status = 'approved'
              AND decided_at >= NOW() - INTERVAL '7 days'
            """,
            new { TenantId });

        var celebrations = new List<string>();
        if (weekPassed > 0)
            celebrations.Add($"{weekPassed} lượt hoàn thành bài học trong 7 ngày.");
        if (weekPerfect > 0)
            celebrations.Add($"{weekPerfect} bài đạt điểm tuyệt đối (100%).");
        if (weekRec > 0)
            celebrations.Add($"{weekRec} ghi nhận / huy hiệu mới trong tuần.");
        if (weekPromo > 0)
            celebrations.Add($"{weekPromo} người được duyệt lên bậc.");
        if (celebrations.Count == 0)
            celebrations.Add("Tuần này chưa có cột mốc — hãy gán bài, ghi nhận khách khen hoặc duyệt bậc để tạo động lực.");

        return new LearningPeopleDashboardDto(
            employeeCount,
            enrollStats.Enrolled,
            enrollStats.Completed,
            enrollStats.Passed,
            enrollStats.Total,
            trainingPct,
            credentialCount,
            avgEval is null ? null : (int)Math.Round(avgEval.Value),
            evalCount,
            recognition30,
            badgeCount,
            levelCounts,
            unevaluated,
            missingPos,
            eligiblePromo,
            actions,
            weekPassed,
            weekPerfect,
            weekRec,
            weekPromo,
            celebrations,
            pendingFeedback,
            missingClose);
    }

    public async Task<LearningEmployeeEvidenceDto?> GetEmployeeEvidenceAsync(
        Guid employeeId,
        CancellationToken cancellationToken)
    {
        var roster = await ListCompetencyRosterAsync(cancellationToken);
        var row = roster.FirstOrDefault(r => r.EmployeeId == employeeId);
        if (row is null)
            return null;

        var career = (await ListCareerRosterAsync(cancellationToken))
            .FirstOrDefault(r => r.EmployeeId == employeeId);

        var codes = row.CompetencyCodes ?? [];
        var hasPos = codes.Any(c => string.Equals(c, "pos_basic", StringComparison.OrdinalIgnoreCase));
        var hasTone = codes.Any(c => string.Equals(c, "tone_of_service", StringComparison.OrdinalIgnoreCase));
        var hasGrn = codes.Any(c => string.Equals(c, "grn_receive", StringComparison.OrdinalIgnoreCase));
        var hasShift = codes.Any(c => string.Equals(c, "shift_close", StringComparison.OrdinalIgnoreCase));

        var trainPct = row.ModulesTotal > 0
            ? (int)Math.Round(100.0 * row.ModulesPassed / row.ModulesTotal)
            : 0;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var closeDays = await conn.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(DISTINCT r.business_date)::int
            FROM success_shift_checklist_run r
            INNER JOIN users u ON u.id = COALESCE(r.completed_by_user_id, r.started_by_user_id)
            WHERE r.tenant_id = @TenantId
              AND u.employee_id = @EmployeeId
              AND r.kind = 'close'
              AND r.status = 'completed'
              AND r.business_date >= @MonthStart::date
            """,
            new { TenantId, EmployeeId = employeeId, MonthStart = monthStart });

        var closeDates = (await conn.QueryAsync<DateTime>(
            """
            SELECT DISTINCT r.business_date
            FROM success_shift_checklist_run r
            INNER JOIN users u ON u.id = COALESCE(r.completed_by_user_id, r.started_by_user_id)
            WHERE r.tenant_id = @TenantId
              AND u.employee_id = @EmployeeId
              AND r.kind = 'close'
              AND r.status = 'completed'
            ORDER BY r.business_date DESC
            LIMIT 60
            """,
            new { TenantId, EmployeeId = employeeId }))
            .Select(d => DateOnly.FromDateTime(d))
            .ToList();

        var streak = 0;
        var cursor = today;
        var dateSet = closeDates.ToHashSet();
        // nếu hôm nay chưa đóng, streak tính từ hôm qua
        if (!dateSet.Contains(cursor))
            cursor = cursor.AddDays(-1);
        while (dateSet.Contains(cursor))
        {
            streak++;
            cursor = cursor.AddDays(-1);
        }

        var sales = await conn.QuerySingleOrDefaultAsync<(int Orders, decimal Net)>(
            """
            SELECT COUNT(o.id)::int AS orders,
                   COALESCE(SUM(o.total_amount), 0)::numeric AS net
            FROM sales_orders o
            INNER JOIN sales_shifts s ON s.id = o.sales_shift_id
            INNER JOIN users u ON u.id = s.opened_by
            WHERE o.tenant_id = @TenantId
              AND u.employee_id = @EmployeeId
              AND o.created_at >= @MonthStart
              AND o.status = 2
            """,
            new { TenantId, EmployeeId = employeeId, MonthStart = monthStart });

        var pulse = await conn.ExecuteScalarAsync<int?>(
            """
            SELECT engagement_pulse
            FROM pack_learning.evaluation
            WHERE tenant_id = @TenantId AND employee_id = @EmployeeId
              AND engagement_pulse IS NOT NULL
            ORDER BY period_year DESC, period_month DESC
            LIMIT 1
            """,
            new { TenantId, EmployeeId = employeeId });

        // Gợi ý điểm từ học + chuyên cần checklist
        var sk = hasPos ? Math.Clamp(70 + trainPct / 5, 60, 95) : 55;
        var sa = hasTone ? 82 : 68;
        var sc = hasTone || hasPos ? 80 : 65;
        var ss = hasGrn ? 82 : (trainPct >= 50 ? 72 : 60);
        var expectedDays = Math.Max(1, DateTime.UtcNow.Day);
        var closePct = (int)Math.Round(100.0 * closeDays / expectedDays);
        var sd = hasShift
            ? Math.Clamp(70 + closePct / 5, 60, 95)
            : Math.Clamp(55 + closePct / 4, 50, 85);
        if (streak >= 7) sd = Math.Max(sd, 85);

        var noteParts = new List<string>
        {
            $"Học: {row.ModulesPassed}/{row.ModulesTotal} bài ({trainPct}%).",
            hasPos ? "Đã có pos_basic." : "Thiếu pos_basic — kiến thức/POS nên thấp hơn.",
            $"Đóng ca checklist tháng này: {closeDays} ngày (chuỗi {streak} ngày).",
            sales.Orders > 0 ? $"Bán tháng (ca mở): {sales.Orders} đơn." : "Chưa có đơn gắn ca mở tháng này.",
            career?.CurrentLevelTitle is not null
                ? $"Bậc: {career.CurrentLevelTitle}" + (career.NextLevelTitle is null
                    ? "."
                    : $" → {career.NextLevelTitle} ({(career.EligibleForNext ? "đủ ĐK" : "chưa đủ ĐK")}).")
                : "Chưa có bậc nghề.",
        };

        return new LearningEmployeeEvidenceDto(
            row.EmployeeId,
            row.EmployeeName,
            row.ModulesPassed,
            row.ModulesTotal,
            row.EnrollmentStatus,
            codes,
            hasPos,
            career?.CurrentLevelTitle,
            career?.NextLevelTitle,
            career?.EligibleForNext ?? false,
            career?.MissingReasons ?? [],
            career?.TenureMonths ?? 0,
            sk,
            sa,
            sc,
            ss,
            sd,
            string.Join(" ", noteParts),
            closeDays,
            streak,
            sales.Orders,
            sales.Net,
            pulse);
    }

    private const int MonthlyDrillQuestionCount = 5;
    private const int MonthlyDrillPassScorePct = 60;

    public async Task<LearningMonthlyDrillStatusDto> GetMonthlyDrillStatusAsync(
        Guid employeeId,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);

        var passedModules = await conn.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(DISTINCT mp.module_id)::int
            FROM pack_learning.module_progress mp
            INNER JOIN pack_learning.enrollment e ON e.id = mp.enrollment_id
            INNER JOIN pack_learning.quiz_question q ON q.module_id = mp.module_id
            WHERE e.tenant_id = @TenantId
              AND e.employee_id = @EmployeeId
              AND e.status <> 'cancelled'
              AND mp.status = 'passed'
            """,
            new { TenantId, EmployeeId = employeeId });

        var eligible = passedModules >= 2;
        var row = await conn.QuerySingleOrDefaultAsync<(int Score, DateTime At)?>(
            """
            SELECT score_pct, completed_at
            FROM pack_learning.monthly_drill
            WHERE tenant_id = @TenantId
              AND employee_id = @EmployeeId
              AND period_year = @Y
              AND period_month = @M
            """,
            new { TenantId, EmployeeId = employeeId, Y = now.Year, M = now.Month });

        string? hint;
        if (!eligible)
            hint = "Hoàn thành thêm bài học (có câu hỏi) để mở ôn nhanh tháng.";
        else if (row is null)
            hint = "Ôn 5 câu từ bài đã học — không bắt buộc, không khóa bán hàng.";
        else
            hint = "Đã ôn nhanh tháng này — có thể xem lại điểm bên dưới.";

        return new LearningMonthlyDrillStatusDto(
            now.Year,
            now.Month,
            eligible,
            row is not null,
            row is null ? null : new DateTimeOffset(DateTime.SpecifyKind(row.Value.At, DateTimeKind.Utc)),
            row?.Score,
            passedModules,
            hint);
    }

    public async Task<LearningMonthlyDrillStartDto> StartMonthlyDrillAsync(
        Guid employeeId,
        CancellationToken cancellationToken)
    {
        var status = await GetMonthlyDrillStatusAsync(employeeId, cancellationToken);
        if (!status.Eligible)
            throw new InvalidOperationException("Cần hoàn thành ít nhất 2 bài có câu hỏi trước khi ôn tháng.");
        if (status.Completed)
            throw new InvalidOperationException("Bạn đã hoàn thành ôn nhanh tháng này.");

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var pool = (await conn.QueryAsync<(
            Guid Id, string Prompt, string OptionsJson, string ModuleTitle, string LevelCode)>(
            """
            SELECT q.id, q.prompt, q.options_json::text, m.title, m.level_code
            FROM pack_learning.quiz_question q
            INNER JOIN pack_learning.module m ON m.id = q.module_id
            INNER JOIN pack_learning.module_progress mp ON mp.module_id = m.id
            INNER JOIN pack_learning.enrollment e ON e.id = mp.enrollment_id
            WHERE e.tenant_id = @TenantId
              AND e.employee_id = @EmployeeId
              AND e.status <> 'cancelled'
              AND mp.status = 'passed'
            ORDER BY random()
            LIMIT @Take
            """,
            new { TenantId, EmployeeId = employeeId, Take = MonthlyDrillQuestionCount * 3 })).ToList();

        if (pool.Count < 3)
            throw new InvalidOperationException("Chưa đủ câu hỏi trong ngân hàng đã học.");

        var rng = Random.Shared;
        var picked = pool.OrderBy(_ => rng.Next()).Take(MonthlyDrillQuestionCount).ToList();
        var questions = new List<LearningMonthlyDrillQuestionDto>();
        foreach (var row in picked)
        {
            var options = ParseOptions(row.OptionsJson).ToList();
            if (options.Count == 0) continue;
            // Options giữ thứ tự gốc — client xáo khi hiển thị rồi map lại index khi nộp.
            questions.Add(new LearningMonthlyDrillQuestionDto(
                row.Id,
                row.Prompt,
                options,
                row.ModuleTitle,
                row.LevelCode));
        }

        if (questions.Count < 3)
            throw new InvalidOperationException("Chưa đủ câu hỏi hợp lệ để ôn tháng.");

        return new LearningMonthlyDrillStartDto(status.PeriodYear, status.PeriodMonth, questions);
    }

    public async Task<LearningMonthlyDrillResultDto> SubmitMonthlyDrillAsync(
        Guid employeeId,
        SubmitLearningMonthlyDrillRequest request,
        CancellationToken cancellationToken)
    {
        var answers = request.Answers ?? [];
        if (answers.Count == 0)
            throw new ArgumentException("Thiếu câu trả lời.");

        var now = DateTime.UtcNow;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        var existing = await conn.QuerySingleOrDefaultAsync<(int Score, int QCount)?>(
            """
            SELECT score_pct, question_count
            FROM pack_learning.monthly_drill
            WHERE tenant_id = @TenantId
              AND employee_id = @EmployeeId
              AND period_year = @Y
              AND period_month = @M
            """,
            new { TenantId, EmployeeId = employeeId, Y = now.Year, M = now.Month },
            tx);

        if (existing is not null)
        {
            await tx.CommitAsync(cancellationToken);
            return new LearningMonthlyDrillResultDto(
                existing.Value.Score >= MonthlyDrillPassScorePct,
                existing.Value.Score,
                MonthlyDrillPassScorePct,
                0,
                existing.Value.QCount,
                AlreadyCompleted: true);
        }

        var ids = answers.Select(a => a.QuestionId).Distinct().ToList();
        var keys = (await conn.QueryAsync<(Guid Id, int Correct)>(
            """
            SELECT q.id, q.correct_option_index
            FROM pack_learning.quiz_question q
            INNER JOIN pack_learning.module m ON m.id = q.module_id
            INNER JOIN pack_learning.module_progress mp ON mp.module_id = m.id
            INNER JOIN pack_learning.enrollment e ON e.id = mp.enrollment_id
            WHERE e.tenant_id = @TenantId
              AND e.employee_id = @EmployeeId
              AND e.status <> 'cancelled'
              AND mp.status = 'passed'
              AND q.id = ANY(@Ids)
            """,
            new { TenantId, EmployeeId = employeeId, Ids = ids.ToArray() },
            tx)).ToDictionary(x => x.Id);

        if (keys.Count != ids.Count)
            throw new InvalidOperationException("Có câu hỏi không thuộc bài đã học.");

        var correctCount = 0;
        foreach (var ans in answers)
        {
            if (!keys.TryGetValue(ans.QuestionId, out var key))
                continue;
            // Client maps shuffled display indices back to original option indices.
            if (ans.SelectedIndex == key.Correct)
                correctCount++;
        }

        var scorePct = (int)Math.Round(100.0 * correctCount / answers.Count);
        var passed = scorePct >= MonthlyDrillPassScorePct;

        await conn.ExecuteAsync(
            """
            INSERT INTO pack_learning.monthly_drill (
                tenant_id, employee_id, period_year, period_month,
                score_pct, question_count, completed_at
            )
            VALUES (
                @TenantId, @EmployeeId, @Y, @M,
                @ScorePct, @QCount, NOW()
            )
            """,
            new
            {
                TenantId,
                EmployeeId = employeeId,
                Y = now.Year,
                M = now.Month,
                ScorePct = scorePct,
                QCount = answers.Count,
            },
            tx);

        await tx.CommitAsync(cancellationToken);
        return new LearningMonthlyDrillResultDto(
            passed, scorePct, MonthlyDrillPassScorePct, correctCount, answers.Count, false);
    }

    public async Task<LearningMyHabitsDto> GetMyHabitsAsync(
        Guid employeeId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var closeDays = await conn.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(DISTINCT r.business_date)::int
            FROM success_shift_checklist_run r
            INNER JOIN users u ON u.id = COALESCE(r.completed_by_user_id, r.started_by_user_id)
            WHERE r.tenant_id = @TenantId
              AND u.employee_id = @EmployeeId
              AND r.kind = 'close'
              AND r.status = 'completed'
              AND r.business_date >= @MonthStart::date
            """,
            new { TenantId, EmployeeId = employeeId, MonthStart = monthStart });

        var openToday = await conn.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*)::int
            FROM success_shift_checklist_run r
            INNER JOIN users u ON u.id = COALESCE(r.completed_by_user_id, r.started_by_user_id)
            WHERE r.tenant_id = @TenantId
              AND u.employee_id = @EmployeeId
              AND r.kind = 'open'
              AND r.status = 'completed'
              AND r.business_date = @Today::date
            """,
            new { TenantId, EmployeeId = employeeId, Today = today });

        var closeToday = await conn.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*)::int
            FROM success_shift_checklist_run r
            INNER JOIN users u ON u.id = COALESCE(r.completed_by_user_id, r.started_by_user_id)
            WHERE r.tenant_id = @TenantId
              AND u.employee_id = @EmployeeId
              AND r.kind = 'close'
              AND r.status = 'completed'
              AND r.business_date = @Today::date
            """,
            new { TenantId, EmployeeId = employeeId, Today = today });

        var closeDates = (await conn.QueryAsync<DateTime>(
            """
            SELECT DISTINCT r.business_date
            FROM success_shift_checklist_run r
            INNER JOIN users u ON u.id = COALESCE(r.completed_by_user_id, r.started_by_user_id)
            WHERE r.tenant_id = @TenantId
              AND u.employee_id = @EmployeeId
              AND r.kind = 'close'
              AND r.status = 'completed'
            ORDER BY r.business_date DESC
            LIMIT 60
            """,
            new { TenantId, EmployeeId = employeeId }))
            .Select(d => DateOnly.FromDateTime(d))
            .ToHashSet();

        var streak = 0;
        var cursor = today;
        if (!closeDates.Contains(cursor))
            cursor = cursor.AddDays(-1);
        while (closeDates.Contains(cursor))
        {
            streak++;
            cursor = cursor.AddDays(-1);
        }

        var hire = await conn.QuerySingleOrDefaultAsync<(DateTime? Hire, DateTime Created)>(
            """
            SELECT hire_date, created_at FROM employees
            WHERE id = @EmployeeId AND tenant_id = @TenantId
            """,
            new { EmployeeId = employeeId, TenantId });
        var hireDate = (hire.Hire ?? hire.Created).Date;
        var now = DateTime.UtcNow.Date;
        var tenureMonths = Math.Max(0, ((now.Year - hireDate.Year) * 12) + now.Month - hireDate.Month);

        var badgeCodes = (await conn.QueryAsync<string>(
            """
            SELECT badge_code FROM pack_learning.badge
            WHERE tenant_id = @TenantId AND employee_id = @EmployeeId
              AND badge_code IN ('tenure_12m', 'close_streak_7')
            """,
            new { TenantId, EmployeeId = employeeId }))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var hasTenure12 = badgeCodes.Contains("tenure_12m");
        var hasStreak7 = badgeCodes.Contains("close_streak_7");

        if (tenureMonths >= 12 && !hasTenure12)
        {
            await using var tx = await conn.BeginTransactionAsync(cancellationToken);
            await InsertRecognitionAsync(
                conn, tx, employeeId, "badge_award",
                "12 tháng gắn bó",
                "Cột mốc thâm niên — cảm ơn bạn đồng hành cùng nhà thuốc.",
                "tenure_12m", "12 tháng gắn bó");
            await tx.CommitAsync(cancellationToken);
            hasTenure12 = true;
        }

        if (streak >= 7 && !hasStreak7)
        {
            await using var tx = await conn.BeginTransactionAsync(cancellationToken);
            await InsertRecognitionAsync(
                conn, tx, employeeId, "badge_award",
                "7 ngày đóng ca liên tục",
                "Giữ nhịp checklist đóng ca — kỷ luật ca rõ ràng.",
                "close_streak_7", "7 ngày đóng ca liên tục");
            await tx.CommitAsync(cancellationToken);
            hasStreak7 = true;
        }

        var tips = new List<string>();
        if (closeToday == 0)
            tips.Add("Hôm nay chưa đóng ca checklist — nhớ tick đóng ca trên Success trước khi về.");
        else
            tips.Add("Đã đóng ca hôm nay — giữ chuỗi ngày đủ ca.");
        if (streak > 0 && streak < 7)
            tips.Add($"Chuỗi đóng ca hiện tại: {streak} ngày — tới 7 ngày sẽ nhận huy hiệu.");
        if (streak >= 7)
            tips.Add($"Chuỗi đóng ca {streak} ngày — xuất sắc, duy trì nhịp này.");
        if (tenureMonths >= 10 && tenureMonths < 12)
            tips.Add($"Còn khoảng {12 - tenureMonths} tháng nữa tới huy hiệu 12 tháng gắn bó.");
        if (tips.Count == 0)
            tips.Add("Giữ nhịp mở–đóng ca + học bài — đó là thói quen vận hành bền.");

        return new LearningMyHabitsDto(
            closeDays,
            streak,
            closeToday > 0,
            openToday > 0,
            tenureMonths,
            hasTenure12,
            hasStreak7,
            tips);
    }

    private async Task<Guid> InsertRecognitionAsync(
        System.Data.Common.DbConnection conn,
        System.Data.Common.DbTransaction tx,
        Guid employeeId,
        string kind,
        string title,
        string? body,
        string? badgeCode,
        string? badgeTitle,
        bool isPublic = true)
    {
        var id = await conn.ExecuteScalarAsync<Guid>(
            """
            INSERT INTO pack_learning.recognition (
                tenant_id, employee_id, kind, title, body, badge_code,
                created_by_user_id, is_public
            )
            VALUES (
                @TenantId, @EmployeeId, @Kind, @Title, @Body, @BadgeCode,
                @UserId, @IsPublic
            )
            RETURNING id
            """,
            new
            {
                TenantId,
                EmployeeId = employeeId,
                Kind = kind,
                Title = title,
                Body = body,
                BadgeCode = badgeCode,
                UserId = UserId == Guid.Empty ? (Guid?)null : UserId,
                IsPublic = isPublic,
            },
            tx);

        if (!string.IsNullOrWhiteSpace(badgeCode) && !string.IsNullOrWhiteSpace(badgeTitle))
        {
            await conn.ExecuteAsync(
                """
                INSERT INTO pack_learning.badge (
                    tenant_id, employee_id, badge_code, title, source_recognition_id, earned_at
                )
                VALUES (@TenantId, @EmployeeId, @BadgeCode, @BadgeTitle, @RecognitionId, NOW())
                ON CONFLICT (tenant_id, employee_id, badge_code) DO UPDATE SET
                    title = EXCLUDED.title,
                    source_recognition_id = EXCLUDED.source_recognition_id,
                    earned_at = NOW()
                """,
                new
                {
                    TenantId,
                    EmployeeId = employeeId,
                    BadgeCode = badgeCode,
                    BadgeTitle = badgeTitle,
                    RecognitionId = id,
                },
                tx);
        }

        return id;
    }

    private async Task<Guid> EnsureEnrollmentForModuleAsync(
        System.Data.Common.DbConnection conn,
        Guid employeeId,
        Guid moduleId,
        System.Data.Common.DbTransaction? tx = null)
    {
        var programId = await conn.ExecuteScalarAsync<Guid?>(
            """
            SELECT m.program_id
            FROM pack_learning.module m
            INNER JOIN pack_learning.program p ON p.id = m.program_id
            WHERE m.id = @ModuleId
              AND p.status = 'published'
              AND (p.tenant_id IS NULL OR p.tenant_id = @TenantId)
            """,
            new { ModuleId = moduleId, TenantId },
            tx);

        if (programId is null)
            throw new InvalidOperationException("Không tìm thấy bài học.");

        await UpsertEnrollmentCoreAsync(conn, employeeId, programId.Value, UserId, tx);

        var enrollmentId = await conn.ExecuteScalarAsync<Guid>(
            """
            SELECT id FROM pack_learning.enrollment
            WHERE tenant_id = @TenantId AND employee_id = @EmployeeId AND program_id = @ProgramId
            """,
            new { TenantId, EmployeeId = employeeId, ProgramId = programId.Value },
            tx);

        await conn.ExecuteAsync(
            """
            INSERT INTO pack_learning.module_progress (enrollment_id, module_id, status)
            SELECT @EnrollmentId, m.id, 'not_started'
            FROM pack_learning.module m
            WHERE m.program_id = @ProgramId
            ON CONFLICT (enrollment_id, module_id) DO NOTHING
            """,
            new { EnrollmentId = enrollmentId, ProgramId = programId.Value },
            tx);

        return enrollmentId;
    }

    private async Task UpsertEnrollmentCoreAsync(
        System.Data.Common.DbConnection conn,
        Guid employeeId,
        Guid programId,
        Guid? assignedBy,
        System.Data.Common.DbTransaction? tx)
    {
        await conn.ExecuteAsync(
            """
            INSERT INTO pack_learning.enrollment (
                tenant_id, employee_id, program_id, status, assigned_by_user_id
            )
            VALUES (@TenantId, @EmployeeId, @ProgramId, 'assigned', @AssignedBy)
            ON CONFLICT (tenant_id, employee_id, program_id) DO UPDATE SET
                status = CASE
                    WHEN pack_learning.enrollment.status = 'cancelled' THEN 'assigned'
                    ELSE pack_learning.enrollment.status
                END
            """,
            new
            {
                TenantId,
                EmployeeId = employeeId,
                ProgramId = programId,
                AssignedBy = assignedBy,
            },
            tx);
    }

    private async Task<LearningEnrollmentDto?> QueryEnrollmentAsync(
        System.Data.Common.DbConnection conn,
        Guid employeeId,
        Guid programId)
    {
        var row = await conn.QuerySingleOrDefaultAsync<EnrollmentRow>(
            """
            SELECT e.id AS Id, e.program_id AS ProgramId, p.title AS ProgramTitle, p.code AS ProgramCode,
                   e.employee_id AS EmployeeId, emp.full_name AS EmployeeName, e.status AS Status,
                   e.assigned_at AS AssignedAt, e.started_at AS StartedAt, e.completed_at AS CompletedAt,
                   (SELECT COUNT(*)::int FROM pack_learning.module m WHERE m.program_id = e.program_id) AS ModulesTotal,
                   (SELECT COUNT(*)::int FROM pack_learning.module_progress mp
                    WHERE mp.enrollment_id = e.id AND mp.status = 'passed') AS ModulesPassed
            FROM pack_learning.enrollment e
            INNER JOIN pack_learning.program p ON p.id = e.program_id
            INNER JOIN employees emp ON emp.id = e.employee_id
            WHERE e.tenant_id = @TenantId AND e.employee_id = @EmployeeId AND e.program_id = @ProgramId
            """,
            new { TenantId, EmployeeId = employeeId, ProgramId = programId });

        return row is null ? null : MapEnrollment(row);
    }

    public async Task<IReadOnlyList<LearningObservationPendingDto>> ListPendingObservationsAsync(
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<(
            Guid EmployeeId, string EmployeeName, Guid ModuleId, string ModuleTitle, string ModuleCode,
            string LevelCode, int? ScorePct, DateTime? CompletedAt)>(
            """
            SELECT e.employee_id, emp.full_name, m.id, m.title, m.code, m.level_code,
                   mp.score_pct, mp.completed_at
            FROM pack_learning.module_progress mp
            INNER JOIN pack_learning.enrollment e ON e.id = mp.enrollment_id
            INNER JOIN pack_learning.module m ON m.id = mp.module_id
            INNER JOIN public.employees emp ON emp.id = e.employee_id
            LEFT JOIN pack_learning.module_observation o
                ON o.enrollment_id = e.id AND o.module_id = m.id AND o.tenant_id = @TenantId
            WHERE e.tenant_id = @TenantId
              AND e.status <> 'cancelled'
              AND mp.status = 'passed'
              AND COALESCE(m.require_observation, FALSE) = TRUE
              AND o.id IS NULL
            ORDER BY mp.completed_at NULLS LAST, emp.full_name
            """,
            new { TenantId });

        return rows.Select(r => new LearningObservationPendingDto(
            r.EmployeeId, r.EmployeeName, r.ModuleId, r.ModuleTitle, r.ModuleCode,
            r.LevelCode, r.ScorePct, r.CompletedAt)).ToList();
    }

    public async Task<LearningObservationDto> SubmitObservationAsync(
        Guid observerUserId,
        SubmitLearningObservationRequest request,
        CancellationToken cancellationToken)
    {
        if (request.EmployeeId == Guid.Empty || request.ModuleId == Guid.Empty)
            throw new ArgumentException("Thiếu nhân viên hoặc bài học.");

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);

        var mod = await conn.QuerySingleOrDefaultAsync<(bool RequireObs, string LevelCode)>(
            """
            SELECT COALESCE(require_observation, FALSE), COALESCE(level_code, '')
            FROM pack_learning.module WHERE id = @ModuleId
            """,
            new { request.ModuleId });
        if (!mod.RequireObs)
            throw new InvalidOperationException("Bài này không cần quan sát tại quầy.");

        var criteriaError = LearningObservationRules.ValidateOrError(mod.LevelCode, request.Criteria);
        if (criteriaError is not null)
            throw new ArgumentException(criteriaError);

        var enrollment = await conn.QuerySingleOrDefaultAsync<(Guid EnrollmentId, string Status)>(
            """
            SELECT e.id, mp.status
            FROM pack_learning.enrollment e
            INNER JOIN pack_learning.module_progress mp
                ON mp.enrollment_id = e.id AND mp.module_id = @ModuleId
            WHERE e.tenant_id = @TenantId AND e.employee_id = @EmployeeId AND e.status <> 'cancelled'
            ORDER BY e.assigned_at DESC
            LIMIT 1
            """,
            new { TenantId, request.EmployeeId, request.ModuleId });

        if (enrollment.EnrollmentId == Guid.Empty)
            throw new InvalidOperationException("Nhân viên chưa học bài này.");
        if (!string.Equals(enrollment.Status, "passed", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Nhân viên cần đạt quiz trước khi quan sát tại quầy.");

        var criteriaJson = JsonSerializer.Serialize(request.Criteria);

        await conn.ExecuteAsync(
            """
            INSERT INTO pack_learning.module_observation (
                tenant_id, enrollment_id, module_id, employee_id,
                observed_by_user_id, criteria_json, note, observed_at
            )
            VALUES (
                @TenantId, @EnrollmentId, @ModuleId, @EmployeeId,
                @ObserverUserId, @CriteriaJson::jsonb, @Note, NOW()
            )
            ON CONFLICT (tenant_id, enrollment_id, module_id) DO UPDATE SET
                observed_by_user_id = EXCLUDED.observed_by_user_id,
                criteria_json = EXCLUDED.criteria_json,
                note = EXCLUDED.note,
                observed_at = NOW()
            """,
            new
            {
                TenantId,
                enrollment.EnrollmentId,
                request.ModuleId,
                request.EmployeeId,
                ObserverUserId = observerUserId,
                CriteriaJson = criteriaJson,
                Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            });

        var row = await GetObservationRowAsync(conn, request.EmployeeId, request.ModuleId)
            ?? throw new InvalidOperationException("Không lưu được quan sát.");
        return row;
    }

    public async Task<LearningObservationDto?> GetModuleObservationForEmployeeAsync(
        Guid employeeId,
        Guid moduleId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await GetObservationRowAsync(conn, employeeId, moduleId);
    }

    public async Task<CreateLearningMailThreadsResultDto> CreateMailThreadAsync(
        CreateLearningMailThreadRequest request,
        CancellationToken cancellationToken)
    {
        if (UserId == Guid.Empty)
            throw new InvalidOperationException("Thiếu người dùng hiện tại.");
        if (string.IsNullOrWhiteSpace(request.Subject))
            throw new ArgumentException("Thiếu tiêu đề thư.");
        if (string.IsNullOrWhiteSpace(request.Body))
            throw new ArgumentException("Thiếu nội dung thư.");

        var recipients = (request.RecipientEmployeeIds ?? Array.Empty<Guid>())
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();
        if (recipients.Count == 0)
            throw new ArgumentException("Chọn ít nhất một nhân viên nhận thư.");

        var subject = request.Subject.Trim();
        if (subject.Length > 200)
            subject = subject[..200];
        var body = request.Body.Trim();

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        var validCount = await conn.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*)::int FROM public.employees
            WHERE tenant_id = @TenantId
              AND deleted_at IS NULL
              AND id = ANY(@Ids)
            """,
            new { TenantId, Ids = recipients.ToArray() },
            tx);
        if (validCount != recipients.Count)
            throw new InvalidOperationException("Một hoặc nhiều nhân viên nhận thư không hợp lệ.");

        Guid? recognitionId = request.RelatedRecognitionId;
        Guid? feedbackId = request.RelatedFeedbackId;
        Guid? evaluationId = request.RelatedEvaluationId;
        Guid? recognitionEmployeeId = null;
        Guid? feedbackEmployeeId = null;
        Guid? evaluationEmployeeId = null;

        if (recognitionId is Guid rid)
        {
            recognitionEmployeeId = await conn.ExecuteScalarAsync<Guid?>(
                """
                SELECT employee_id FROM pack_learning.recognition
                WHERE id = @Id AND tenant_id = @TenantId
                """,
                new { Id = rid, TenantId },
                tx);
            if (recognitionEmployeeId is null)
                throw new ArgumentException("Ghi nhận gắn kèm không thuộc cửa hàng này.");
            if (recipients.Count == 1 && recognitionEmployeeId.Value != recipients[0])
                throw new ArgumentException("Ghi nhận gắn kèm phải thuộc nhân viên nhận thư.");
        }

        if (feedbackId is Guid fid)
        {
            var fbExists = await conn.ExecuteScalarAsync<int>(
                """
                SELECT COUNT(*)::int FROM pack_learning.customer_sale_feedback
                WHERE id = @Id AND tenant_id = @TenantId
                """,
                new { Id = fid, TenantId },
                tx);
            if (fbExists == 0)
                throw new ArgumentException("Phản hồi khách gắn kèm không thuộc cửa hàng này.");
            feedbackEmployeeId = await conn.ExecuteScalarAsync<Guid?>(
                """
                SELECT employee_id FROM pack_learning.customer_sale_feedback
                WHERE id = @Id AND tenant_id = @TenantId
                """,
                new { Id = fid, TenantId },
                tx);
            if (recipients.Count == 1
                && feedbackEmployeeId is Guid fbEmp
                && fbEmp != recipients[0])
                throw new ArgumentException("Phản hồi khách gắn kèm phải thuộc nhân viên nhận thư.");
        }

        if (evaluationId is Guid eid)
        {
            evaluationEmployeeId = await conn.ExecuteScalarAsync<Guid?>(
                """
                SELECT employee_id FROM pack_learning.evaluation
                WHERE id = @Id AND tenant_id = @TenantId
                """,
                new { Id = eid, TenantId },
                tx);
            if (evaluationEmployeeId is null)
                throw new ArgumentException("Đánh giá gắn kèm không thuộc cửa hàng này.");
            if (recipients.Count == 1 && evaluationEmployeeId.Value != recipients[0])
                throw new ArgumentException("Đánh giá gắn kèm phải thuộc nhân viên nhận thư.");
        }

        var threadIds = new List<Guid>();
        foreach (var recipientEmployeeId in recipients)
        {
            // Related event only on the matching employee's private thread.
            var threadRecognitionId =
                recognitionId is not null
                && recognitionEmployeeId == recipientEmployeeId
                    ? recognitionId
                    : null;
            var threadFeedbackId =
                feedbackId is not null
                && (feedbackEmployeeId is null || feedbackEmployeeId == recipientEmployeeId)
                    ? feedbackId
                    : null;
            var threadEvaluationId =
                evaluationId is not null
                && evaluationEmployeeId == recipientEmployeeId
                    ? evaluationId
                    : null;

            // Multi-recipient: only attach related when it matches this person.
            if (recipients.Count > 1)
            {
                if (threadRecognitionId is null && recognitionId is not null)
                    threadRecognitionId = null;
                if (feedbackId is not null
                    && feedbackEmployeeId is Guid fbOnly
                    && fbOnly != recipientEmployeeId)
                    threadFeedbackId = null;
                if (threadEvaluationId is null && evaluationId is not null)
                    threadEvaluationId = null;
            }

            var threadId = await conn.ExecuteScalarAsync<Guid>(
                """
                INSERT INTO pack_learning.mail_thread (
                    tenant_id, subject, recipient_employee_id, created_by_user_id,
                    related_recognition_id, related_feedback_id, related_evaluation_id
                )
                VALUES (
                    @TenantId, @Subject, @RecipientEmployeeId, @UserId,
                    @RelatedRecognitionId, @RelatedFeedbackId, @RelatedEvaluationId
                )
                RETURNING id
                """,
                new
                {
                    TenantId,
                    Subject = subject,
                    RecipientEmployeeId = recipientEmployeeId,
                    UserId,
                    RelatedRecognitionId = threadRecognitionId,
                    RelatedFeedbackId = threadFeedbackId,
                    RelatedEvaluationId = threadEvaluationId,
                },
                tx);

            await conn.ExecuteAsync(
                """
                INSERT INTO pack_learning.mail_message (tenant_id, thread_id, sender_user_id, body)
                VALUES (@TenantId, @ThreadId, @UserId, @Body)
                """,
                new { TenantId, ThreadId = threadId, UserId, Body = body },
                tx);

            await conn.ExecuteAsync(
                """
                INSERT INTO pack_learning.mail_read (tenant_id, thread_id, user_id, last_read_at)
                VALUES (@TenantId, @ThreadId, @UserId, NOW())
                ON CONFLICT (thread_id, user_id) DO UPDATE SET last_read_at = EXCLUDED.last_read_at
                """,
                new { TenantId, ThreadId = threadId, UserId },
                tx);

            threadIds.Add(threadId);
        }

        await tx.CommitAsync(cancellationToken);

        var threads = new List<LearningMailThreadDetailDto>();
        foreach (var id in threadIds)
        {
            var detail = await GetMailThreadAsync(id, cancellationToken);
            if (detail is not null)
                threads.Add(detail);
        }

        if (threads.Count == 0)
            throw new InvalidOperationException("Không tạo được thư.");

        return new CreateLearningMailThreadsResultDto(threads.Count, threads);
    }

    public async Task<IReadOnlyList<LearningMailThreadListItemDto>> ListMailThreadsAsync(
        CancellationToken cancellationToken)
    {
        var myEmployeeId = await GetEmployeeIdForCurrentUserAsync(cancellationToken);
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<(
            Guid Id, string Subject, Guid RecipientEmployeeId, string RecipientEmployeeName,
            Guid CreatedByUserId, string CreatedByName, DateTime CreatedAt, DateTime UpdatedAt,
            int UnreadCount, string? LastMessagePreview, string? RelatedEventLabel,
            Guid? RelatedRecognitionId, Guid? RelatedFeedbackId, Guid? RelatedEvaluationId)>(
            """
            SELECT t.id,
                   t.subject,
                   t.recipient_employee_id,
                   recip.full_name,
                   t.created_by_user_id,
                   COALESCE(creator_emp.full_name, creator.username, 'Người dùng'),
                   t.created_at,
                   t.updated_at,
                   (
                       SELECT COUNT(*)::int
                       FROM pack_learning.mail_message m
                       WHERE m.thread_id = t.id
                         AND m.sender_user_id <> @UserId
                         AND m.created_at > COALESCE(rd.last_read_at, TIMESTAMPTZ '-infinity')
                   ) AS unread_count,
                   (
                       SELECT LEFT(m.body, 120)
                       FROM pack_learning.mail_message m
                       WHERE m.thread_id = t.id
                       ORDER BY m.created_at DESC
                       LIMIT 1
                   ) AS last_preview,
                   CASE
                       WHEN t.related_recognition_id IS NOT NULL THEN rec.title
                       WHEN t.related_feedback_id IS NOT NULL
                           THEN 'Khách đánh giá ' || fb.rating::text || '★'
                       WHEN t.related_evaluation_id IS NOT NULL AND ev.id IS NOT NULL
                           THEN 'Đánh giá tháng ' || ev.period_month::text || '/' || ev.period_year::text
                       WHEN t.related_evaluation_id IS NOT NULL THEN 'Đánh giá tháng'
                       ELSE NULL
                   END AS related_label,
                   t.related_recognition_id,
                   t.related_feedback_id,
                   t.related_evaluation_id
            FROM pack_learning.mail_thread t
            INNER JOIN public.employees recip
                ON recip.id = t.recipient_employee_id AND recip.tenant_id = t.tenant_id
            INNER JOIN public.users creator
                ON creator.id = t.created_by_user_id AND creator.tenant_id = t.tenant_id
            LEFT JOIN public.employees creator_emp
                ON creator_emp.id = creator.employee_id AND creator_emp.tenant_id = t.tenant_id
            LEFT JOIN pack_learning.mail_read rd
                ON rd.thread_id = t.id AND rd.user_id = @UserId
            LEFT JOIN pack_learning.recognition rec
                ON rec.id = t.related_recognition_id AND rec.tenant_id = t.tenant_id
            LEFT JOIN pack_learning.customer_sale_feedback fb
                ON fb.id = t.related_feedback_id AND fb.tenant_id = t.tenant_id
            LEFT JOIN pack_learning.evaluation ev
                ON ev.id = t.related_evaluation_id AND ev.tenant_id = t.tenant_id
            WHERE t.tenant_id = @TenantId
              AND (
                    t.created_by_user_id = @UserId
                    OR (@MyEmployeeId IS NOT NULL AND t.recipient_employee_id = @MyEmployeeId)
                  )
            ORDER BY t.updated_at DESC
            """,
            new { TenantId, UserId, MyEmployeeId = myEmployeeId });

        return rows.Select(r => new LearningMailThreadListItemDto(
            r.Id,
            r.Subject,
            r.RecipientEmployeeId,
            r.RecipientEmployeeName,
            r.CreatedByUserId,
            r.CreatedByName,
            r.CreatedAt,
            r.UpdatedAt,
            r.UnreadCount,
            r.LastMessagePreview,
            r.RelatedEventLabel,
            r.RelatedRecognitionId,
            r.RelatedFeedbackId,
            r.RelatedEvaluationId)).ToList();
    }

    public async Task<LearningMailThreadDetailDto?> GetMailThreadAsync(
        Guid threadId,
        CancellationToken cancellationToken)
    {
        var myEmployeeId = await GetEmployeeIdForCurrentUserAsync(cancellationToken);
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);

        var header = await conn.QuerySingleOrDefaultAsync<(
            Guid Id, string Subject, Guid RecipientEmployeeId, string RecipientEmployeeName,
            Guid CreatedByUserId, string CreatedByName, DateTime CreatedAt, DateTime UpdatedAt,
            string? RelatedEventLabel, Guid? RelatedRecognitionId, Guid? RelatedFeedbackId,
            Guid? RelatedEvaluationId)>(
            """
            SELECT t.id,
                   t.subject,
                   t.recipient_employee_id,
                   recip.full_name,
                   t.created_by_user_id,
                   COALESCE(creator_emp.full_name, creator.username, 'Người dùng'),
                   t.created_at,
                   t.updated_at,
                   CASE
                       WHEN t.related_recognition_id IS NOT NULL THEN rec.title
                       WHEN t.related_feedback_id IS NOT NULL
                           THEN 'Khách đánh giá ' || fb.rating::text || '★'
                       WHEN t.related_evaluation_id IS NOT NULL AND ev.id IS NOT NULL
                           THEN 'Đánh giá tháng ' || ev.period_month::text || '/' || ev.period_year::text
                       WHEN t.related_evaluation_id IS NOT NULL THEN 'Đánh giá tháng'
                       ELSE NULL
                   END,
                   t.related_recognition_id,
                   t.related_feedback_id,
                   t.related_evaluation_id
            FROM pack_learning.mail_thread t
            INNER JOIN public.employees recip
                ON recip.id = t.recipient_employee_id AND recip.tenant_id = t.tenant_id
            INNER JOIN public.users creator
                ON creator.id = t.created_by_user_id AND creator.tenant_id = t.tenant_id
            LEFT JOIN public.employees creator_emp
                ON creator_emp.id = creator.employee_id AND creator_emp.tenant_id = t.tenant_id
            LEFT JOIN pack_learning.recognition rec
                ON rec.id = t.related_recognition_id AND rec.tenant_id = t.tenant_id
            LEFT JOIN pack_learning.customer_sale_feedback fb
                ON fb.id = t.related_feedback_id AND fb.tenant_id = t.tenant_id
            LEFT JOIN pack_learning.evaluation ev
                ON ev.id = t.related_evaluation_id AND ev.tenant_id = t.tenant_id
            WHERE t.id = @ThreadId
              AND t.tenant_id = @TenantId
              AND (
                    t.created_by_user_id = @UserId
                    OR (@MyEmployeeId IS NOT NULL AND t.recipient_employee_id = @MyEmployeeId)
                  )
            """,
            new { ThreadId = threadId, TenantId, UserId, MyEmployeeId = myEmployeeId });

        if (header.Id == Guid.Empty)
            return null;

        var h = header;
        var messages = await conn.QueryAsync<(
            Guid Id, Guid SenderUserId, string SenderName, string Body, DateTime CreatedAt)>(
            """
            SELECT m.id,
                   m.sender_user_id,
                   COALESCE(emp.full_name, u.username, 'Người dùng'),
                   m.body,
                   m.created_at
            FROM pack_learning.mail_message m
            INNER JOIN public.users u ON u.id = m.sender_user_id AND u.tenant_id = m.tenant_id
            LEFT JOIN public.employees emp
                ON emp.id = u.employee_id AND emp.tenant_id = m.tenant_id AND emp.deleted_at IS NULL
            WHERE m.thread_id = @ThreadId AND m.tenant_id = @TenantId
            ORDER BY m.created_at ASC
            """,
            new { ThreadId = threadId, TenantId });

        return new LearningMailThreadDetailDto(
            h.Id,
            h.Subject,
            h.RecipientEmployeeId,
            h.RecipientEmployeeName,
            h.CreatedByUserId,
            h.CreatedByName,
            h.CreatedAt,
            h.UpdatedAt,
            h.RelatedEventLabel,
            h.RelatedRecognitionId,
            h.RelatedFeedbackId,
            h.RelatedEvaluationId,
            messages.Select(m => new LearningMailMessageDto(
                m.Id,
                m.SenderUserId,
                m.SenderName,
                m.Body,
                m.CreatedAt,
                m.SenderUserId == UserId)).ToList());
    }

    public async Task<LearningMailMessageDto> ReplyMailThreadAsync(
        Guid threadId,
        ReplyLearningMailRequest request,
        CancellationToken cancellationToken)
    {
        if (UserId == Guid.Empty)
            throw new InvalidOperationException("Thiếu người dùng hiện tại.");
        if (string.IsNullOrWhiteSpace(request.Body))
            throw new ArgumentException("Thiếu nội dung trả lời.");

        var body = request.Body.Trim();
        var myEmployeeId = await GetEmployeeIdForCurrentUserAsync(cancellationToken);
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        var access = await conn.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*)::int FROM pack_learning.mail_thread t
            WHERE t.id = @ThreadId
              AND t.tenant_id = @TenantId
              AND (
                    t.created_by_user_id = @UserId
                    OR (@MyEmployeeId IS NOT NULL AND t.recipient_employee_id = @MyEmployeeId)
                  )
            """,
            new { ThreadId = threadId, TenantId, UserId, MyEmployeeId = myEmployeeId },
            tx);
        if (access == 0)
            throw new InvalidOperationException("Không có quyền trả lời thư này.");

        var msgId = await conn.ExecuteScalarAsync<Guid>(
            """
            INSERT INTO pack_learning.mail_message (tenant_id, thread_id, sender_user_id, body)
            VALUES (@TenantId, @ThreadId, @UserId, @Body)
            RETURNING id
            """,
            new { TenantId, ThreadId = threadId, UserId, Body = body },
            tx);

        await conn.ExecuteAsync(
            """
            UPDATE pack_learning.mail_thread
            SET updated_at = NOW()
            WHERE id = @ThreadId AND tenant_id = @TenantId
            """,
            new { ThreadId = threadId, TenantId },
            tx);

        await conn.ExecuteAsync(
            """
            INSERT INTO pack_learning.mail_read (tenant_id, thread_id, user_id, last_read_at)
            VALUES (@TenantId, @ThreadId, @UserId, NOW())
            ON CONFLICT (thread_id, user_id) DO UPDATE SET last_read_at = EXCLUDED.last_read_at
            """,
            new { TenantId, ThreadId = threadId, UserId },
            tx);

        await tx.CommitAsync(cancellationToken);

        var senderName = await conn.ExecuteScalarAsync<string>(
            """
            SELECT COALESCE(emp.full_name, u.username, 'Người dùng')
            FROM public.users u
            LEFT JOIN public.employees emp
                ON emp.id = u.employee_id AND emp.tenant_id = u.tenant_id AND emp.deleted_at IS NULL
            WHERE u.id = @UserId AND u.tenant_id = @TenantId
            """,
            new { UserId, TenantId }) ?? "Người dùng";

        var createdAt = await conn.ExecuteScalarAsync<DateTime>(
            """
            SELECT created_at FROM pack_learning.mail_message WHERE id = @Id
            """,
            new { Id = msgId });

        return new LearningMailMessageDto(msgId, UserId, senderName, body, createdAt, true);
    }

    public async Task MarkMailThreadReadAsync(
        Guid threadId,
        CancellationToken cancellationToken)
    {
        if (UserId == Guid.Empty)
            throw new InvalidOperationException("Thiếu người dùng hiện tại.");

        var myEmployeeId = await GetEmployeeIdForCurrentUserAsync(cancellationToken);
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);

        var access = await conn.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*)::int FROM pack_learning.mail_thread t
            WHERE t.id = @ThreadId
              AND t.tenant_id = @TenantId
              AND (
                    t.created_by_user_id = @UserId
                    OR (@MyEmployeeId IS NOT NULL AND t.recipient_employee_id = @MyEmployeeId)
                  )
            """,
            new { ThreadId = threadId, TenantId, UserId, MyEmployeeId = myEmployeeId });
        if (access == 0)
            throw new InvalidOperationException("Không có quyền đọc thư này.");

        await conn.ExecuteAsync(
            """
            INSERT INTO pack_learning.mail_read (tenant_id, thread_id, user_id, last_read_at)
            VALUES (@TenantId, @ThreadId, @UserId, NOW())
            ON CONFLICT (thread_id, user_id) DO UPDATE SET last_read_at = EXCLUDED.last_read_at
            """,
            new { TenantId, ThreadId = threadId, UserId });
    }

    public async Task<LearningMailUnreadCountDto> GetMailUnreadCountAsync(
        CancellationToken cancellationToken)
    {
        var myEmployeeId = await GetEmployeeIdForCurrentUserAsync(cancellationToken);
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var count = await conn.ExecuteScalarAsync<int>(
            """
            SELECT COALESCE(SUM(unread.cnt), 0)::int
            FROM pack_learning.mail_thread t
            LEFT JOIN pack_learning.mail_read rd
                ON rd.thread_id = t.id AND rd.user_id = @UserId
            CROSS JOIN LATERAL (
                SELECT COUNT(*)::int AS cnt
                FROM pack_learning.mail_message m
                WHERE m.thread_id = t.id
                  AND m.sender_user_id <> @UserId
                  AND m.created_at > COALESCE(rd.last_read_at, TIMESTAMPTZ '-infinity')
            ) unread
            WHERE t.tenant_id = @TenantId
              AND (
                    t.created_by_user_id = @UserId
                    OR (@MyEmployeeId IS NOT NULL AND t.recipient_employee_id = @MyEmployeeId)
                  )
            """,
            new { TenantId, UserId, MyEmployeeId = myEmployeeId });
        return new LearningMailUnreadCountDto(count);
    }

    private async Task<LearningObservationDto?> GetObservationRowAsync(
        System.Data.Common.DbConnection conn,
        Guid employeeId,
        Guid moduleId)
    {
        var row = await conn.QuerySingleOrDefaultAsync<(
            Guid Id, Guid EmployeeId, string EmployeeName, Guid ModuleId, string ModuleTitle,
            string CriteriaJson, string? Note, DateTime ObservedAt, string ObserverName)>(
            """
            SELECT o.id, o.employee_id, emp.full_name, o.module_id, m.title,
                   o.criteria_json::text, o.note, o.observed_at,
                   COALESCE(u.username, 'Quản lý')
            FROM pack_learning.module_observation o
            INNER JOIN public.employees emp ON emp.id = o.employee_id
            INNER JOIN pack_learning.module m ON m.id = o.module_id
            LEFT JOIN public.users u ON u.id = o.observed_by_user_id
            WHERE o.tenant_id = @TenantId
              AND o.employee_id = @EmployeeId
              AND o.module_id = @ModuleId
            """,
            new { TenantId, EmployeeId = employeeId, ModuleId = moduleId });

        if (row.Id == Guid.Empty)
            return null;

        IReadOnlyDictionary<string, bool> criteria;
        try
        {
            criteria = JsonSerializer.Deserialize<Dictionary<string, bool>>(row.CriteriaJson)
                       ?? new Dictionary<string, bool>();
        }
        catch
        {
            criteria = new Dictionary<string, bool>();
        }

        return new LearningObservationDto(
            row.Id,
            row.EmployeeId,
            row.EmployeeName,
            row.ModuleId,
            row.ModuleTitle,
            criteria,
            row.Note,
            row.ObservedAt,
            row.ObserverName);
    }

    private static LearningEnrollmentDto MapEnrollment(EnrollmentRow r)
    {
        var status = r.Status;
        // Tránh «Đã hoàn thành» khi còn bài (vd. thêm L6 sau khi đã completed).
        if (string.Equals(status, "completed", StringComparison.OrdinalIgnoreCase)
            && r.ModulesTotal > 0
            && r.ModulesPassed < r.ModulesTotal)
        {
            status = "in_progress";
        }

        var completedAt = string.Equals(status, "completed", StringComparison.OrdinalIgnoreCase)
            ? r.CompletedAt
            : null;

        return new(
            r.Id, r.ProgramId, r.ProgramTitle, r.ProgramCode, r.EmployeeId, r.EmployeeName,
            status, r.AssignedAt, r.StartedAt, completedAt, r.ModulesTotal, r.ModulesPassed);
    }

    private static LearningModuleListItemDto MapModuleList(ModuleRow m) =>
        new(m.Id, m.Code, m.Title, m.Summary, m.DurationMinutes, m.LevelCode,
            m.CompetencyCodes ?? [], m.SortOrder, m.PassScorePct, m.RequireAck, m.QuestionCount);

    private static IReadOnlyList<string> ParseOptions(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private sealed class ProgramHeaderRow
    {
        public Guid Id { get; init; }
        public string Code { get; init; } = "";
        public string PackCode { get; init; } = "";
        public string Title { get; init; } = "";
        public string? Summary { get; init; }
        public string Locale { get; init; } = "";
        public int Version { get; init; }
    }

    private sealed class ModuleRow
    {
        public Guid Id { get; init; }
        public string Code { get; init; } = "";
        public string Title { get; init; } = "";
        public string? Summary { get; init; }
        public int DurationMinutes { get; init; }
        public string LevelCode { get; init; } = "";
        public string[]? CompetencyCodes { get; init; }
        public int SortOrder { get; init; }
        public int PassScorePct { get; init; }
        public bool RequireAck { get; init; }
        public int QuestionCount { get; init; }
    }

    private sealed class EnrollmentRow
    {
        public Guid Id { get; init; }
        public Guid ProgramId { get; init; }
        public string ProgramTitle { get; init; } = "";
        public string ProgramCode { get; init; } = "";
        public Guid EmployeeId { get; init; }
        public string EmployeeName { get; init; } = "";
        public string Status { get; init; } = "";
        public DateTime AssignedAt { get; init; }
        public DateTime? StartedAt { get; init; }
        public DateTime? CompletedAt { get; init; }
        public int ModulesTotal { get; init; }
        public int ModulesPassed { get; init; }
    }

    private sealed class ProgressRow
    {
        public Guid ModuleId { get; init; }
        public string ModuleCode { get; init; } = "";
        public string Title { get; init; } = "";
        public string LevelCode { get; init; } = "";
        public int SortOrder { get; init; }
        public string Status { get; init; } = "";
        public int? ScorePct { get; init; }
        public int QuizAttempts { get; init; }
        public DateTime? AcknowledgedAt { get; init; }
        public bool RequireAck { get; init; }
    }
}
