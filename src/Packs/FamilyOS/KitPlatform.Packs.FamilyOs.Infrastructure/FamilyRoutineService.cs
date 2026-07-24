using KitPlatform.Packs.FamilyOs;
namespace KitPlatform.Packs.FamilyOs.Infrastructure;
internal sealed class FamilyRoutineService : IFamilyRoutineService
{
    private readonly FamilyRoutineRepository _repo;
    public FamilyRoutineService(FamilyRoutineRepository repo) => _repo = repo;
    public async Task<IReadOnlyList<RoutineDto>> ListRoutinesAsync(
        Guid familyId,
        CancellationToken cancellationToken = default)
    {
        await EnsureFamilyAsync(familyId, cancellationToken);
        var routines = await _repo.ListRoutinesAsync(familyId, cancellationToken);
        var result = new List<RoutineDto>(routines.Count);
        foreach (var routine in routines)
        {
            var templates = await _repo.ListTemplatesAsync(routine.Id, cancellationToken);
            result.Add(MapRoutine(routine, templates));
        }
        return result;
    }
    public async Task<RoutineDto?> GetRoutineAsync(
        Guid familyId,
        Guid routineId,
        CancellationToken cancellationToken = default)
    {
        await EnsureFamilyAsync(familyId, cancellationToken);
        var routine = await _repo.GetRoutineAsync(familyId, routineId, cancellationToken);
        if (routine is null) return null;
        var templates = await _repo.ListTemplatesAsync(routine.Id, cancellationToken);
        return MapRoutine(routine, templates);
    }
    public async Task<RoutineDto> CreateRoutineAsync(
        Guid familyId,
        CreateRoutineRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsureFamilyAsync(familyId, cancellationToken);
        var code = (request.Code ?? "").Trim().ToLowerInvariant();
        var name = (request.DisplayName ?? "").Trim();
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("code và displayName là bắt buộc.");
        var kind = string.IsNullOrWhiteSpace(request.Kind)
            ? FamilyRoutineKinds.Custom
            : request.Kind.Trim().ToLowerInvariant();
        if (!FamilyRoutineKinds.All.Contains(kind))
            throw new InvalidOperationException(
                "kind phải là school_day | weekend | holiday | exam | travel | custom.");
        var weekdays = NormalizeWeekdays(request.Weekdays);
        var routineId = await _repo.InsertRoutineAsync(
            familyId, code, name, kind, weekdays, request.SortOrder ?? 0, cancellationToken);
        if (request.Templates is { Count: > 0 })
        {
            var order = 0;
            foreach (var template in request.Templates)
            {
                await InsertValidatedTemplateAsync(
                    familyId,
                    routineId,
                    template.Title,
                    template.Description,
                    template.MemberId,
                    template.WindowStart,
                    template.WindowEnd,
                    template.SortOrder ?? order++,
                    template.Priority,
                    template.ExpectedDurationMinutes,
                    template.ContextAnchor,
                    template.DependsOnTemplateIds,
                    excludeTemplateId: null,
                    template.AllowEarlyComplete,
                    template.EarlyLeadMinutes,
                    template.OnTimeGraceMinutes,
                    template.StarReward,
                    cancellationToken);
            }
        }
        return (await GetRoutineAsync(familyId, routineId, cancellationToken))!;
    }
    public async Task<RoutineDto> UpdateRoutineAsync(
        Guid familyId,
        Guid routineId,
        UpdateRoutineRequest request,
        CancellationToken cancellationToken = default)
    {
        var existing = await _repo.GetRoutineAsync(familyId, routineId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy routine.");
        var name = string.IsNullOrWhiteSpace(request.DisplayName)
            ? existing.DisplayName
            : request.DisplayName.Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("displayName là bắt buộc.");
        var kind = string.IsNullOrWhiteSpace(request.Kind)
            ? existing.Kind
            : request.Kind.Trim().ToLowerInvariant();
        if (!FamilyRoutineKinds.All.Contains(kind))
            throw new InvalidOperationException(
                "kind phải là school_day | weekend | holiday | exam | travel | custom.");
        var weekdays = request.Weekdays is null
            ? existing.Weekdays ?? []
            : NormalizeWeekdays(request.Weekdays);
        var updated = await _repo.UpdateRoutineAsync(
            familyId,
            routineId,
            name,
            kind,
            weekdays,
            request.IsActive ?? existing.IsActive,
            request.SortOrder ?? existing.SortOrder,
            cancellationToken)
            ?? throw new InvalidOperationException("Không cập nhật được routine.");
        var templates = await _repo.ListTemplatesAsync(updated.Id, cancellationToken);
        return MapRoutine(updated, templates);
    }
    public async Task<CommitmentTemplateDto> AddTemplateAsync(
        Guid familyId,
        Guid routineId,
        AddCommitmentTemplateRequest request,
        CancellationToken cancellationToken = default)
    {
        var routine = await _repo.GetRoutineAsync(familyId, routineId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy routine.");
        var row = await InsertValidatedTemplateAsync(
            familyId,
            routine.Id,
            request.Title,
            request.Description,
            request.MemberId,
            request.WindowStart,
            request.WindowEnd,
            request.SortOrder ?? 0,
            request.Priority,
            request.ExpectedDurationMinutes,
            request.ContextAnchor,
            request.DependsOnTemplateIds,
            excludeTemplateId: null,
            request.AllowEarlyComplete,
            request.EarlyLeadMinutes,
            request.OnTimeGraceMinutes,
            request.StarReward,
            cancellationToken);
        return MapTemplate(row);
    }
    public async Task<CommitmentTemplateDto> UpdateTemplateAsync(
        Guid familyId,
        Guid routineId,
        Guid templateId,
        UpdateCommitmentTemplateRequest request,
        CancellationToken cancellationToken = default)
    {
        var routine = await _repo.GetRoutineAsync(familyId, routineId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy routine.");
        var existing = await _repo.GetTemplateAsync(routine.Id, templateId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy commitment template.");
        var title = (request.Title ?? "").Trim();
        if (string.IsNullOrWhiteSpace(title))
            throw new InvalidOperationException("title commitment là bắt buộc.");
        if (request.MemberId is Guid mid &&
            !await _repo.MemberBelongsToFamilyAsync(familyId, mid, cancellationToken))
            throw new InvalidOperationException("memberId không thuộc gia đình này.");
        var siblings = await _repo.ListTemplatesAsync(routine.Id, cancellationToken);
        var priority = NormalizePriority(request.Priority ?? existing.Priority);
        var duration = NormalizeDuration(
            request.ExpectedDurationMinutes,
            request.WindowStart,
            request.WindowEnd);
        var anchor = NormalizeContextAnchor(request.ContextAnchor);
        var depends = NormalizeDepends(
            request.DependsOnTemplateIds ?? existing.DependsOnTemplateIds,
            templateId,
            siblings);
        var timing = ResolveTimingFields(
            request.AllowEarlyComplete ?? existing.AllowEarlyComplete,
            request.EarlyLeadMinutes ?? existing.EarlyLeadMinutes,
            request.OnTimeGraceMinutes ?? existing.OnTimeGraceMinutes);
        var updated = await _repo.UpdateTemplateAsync(
            routine.Id,
            templateId,
            title,
            string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            request.MemberId,
            request.WindowStart,
            request.WindowEnd,
            request.SortOrder,
            request.IsActive,
            priority,
            duration,
            anchor,
            depends,
            timing.AllowEarlyComplete,
            timing.EarlyLeadMinutes,
            timing.OnTimeGraceMinutes,
            request.StarReward ?? (existing.StarReward > 0
                ? existing.StarReward
                : FamilyStarCalculator.InferStarReward(title)),
            cancellationToken)
            ?? throw new InvalidOperationException("Không cập nhật được commitment template.");
        return MapTemplate(updated);
    }
    public async Task RemoveTemplateAsync(
        Guid familyId,
        Guid routineId,
        Guid templateId,
        CancellationToken cancellationToken = default)
    {
        var routine = await _repo.GetRoutineAsync(familyId, routineId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy routine.");
        var existing = await _repo.GetTemplateAsync(routine.Id, templateId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy commitment template.");
        if (!await _repo.SoftDeleteTemplateAsync(routine.Id, existing.Id, cancellationToken))
            throw new InvalidOperationException("Không xóa được commitment template.");
    }
    private async Task EnsureFamilyAsync(Guid familyId, CancellationToken cancellationToken)
    {
        if (!await _repo.FamilyExistsAsync(familyId, cancellationToken))
            throw new InvalidOperationException("Không tìm thấy gia đình.");
    }
    private async Task<FamilyRoutineRepository.TemplateRow> InsertValidatedTemplateAsync(
        Guid familyId,
        Guid routineId,
        string? title,
        string? description,
        Guid? memberId,
        TimeOnly? windowStart,
        TimeOnly? windowEnd,
        int sortOrder,
        string? priority,
        int? expectedDurationMinutes,
        string? contextAnchor,
        IReadOnlyList<Guid>? dependsOnTemplateIds,
        Guid? excludeTemplateId,
        bool? allowEarlyComplete,
        int? earlyLeadMinutes,
        int? onTimeGraceMinutes,
        int? starReward,
        CancellationToken cancellationToken)
    {
        var name = (title ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("title commitment là bắt buộc.");
        if (memberId is Guid mid &&
            !await _repo.MemberBelongsToFamilyAsync(familyId, mid, cancellationToken))
            throw new InvalidOperationException("memberId không thuộc gia đình này.");
        var siblings = await _repo.ListTemplatesAsync(routineId, cancellationToken);
        var resolvedPriority = NormalizePriority(priority);
        var duration = NormalizeDuration(expectedDurationMinutes, windowStart, windowEnd);
        var anchor = NormalizeContextAnchor(contextAnchor);
        var depends = NormalizeDepends(dependsOnTemplateIds, excludeTemplateId, siblings);
        var inferredAllowEarly = allowEarlyComplete ?? FamilyCommitmentTiming.InferAllowEarlyComplete(name);
        var timing = ResolveTimingFields(
            inferredAllowEarly,
            earlyLeadMinutes ?? FamilyCommitmentTiming.InferEarlyLeadMinutes(name, inferredAllowEarly),
            onTimeGraceMinutes ?? FamilyCommitmentTiming.InferOnTimeGraceMinutes(name, inferredAllowEarly));
        var resolvedStarReward = starReward ?? FamilyStarCalculator.InferStarReward(name);
        if (resolvedStarReward < 1 || resolvedStarReward > 999)
            throw new InvalidOperationException("starReward phải từ 1–999.");
        return await _repo.InsertTemplateAsync(
            routineId,
            name,
            string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            memberId,
            windowStart,
            windowEnd,
            sortOrder,
            resolvedPriority,
            duration,
            anchor,
            depends,
            timing.AllowEarlyComplete,
            timing.EarlyLeadMinutes,
            timing.OnTimeGraceMinutes,
            resolvedStarReward,
            cancellationToken);
    }
    private static (bool AllowEarlyComplete, int EarlyLeadMinutes, int OnTimeGraceMinutes) ResolveTimingFields(
        bool allowEarlyComplete,
        int earlyLeadMinutes,
        int onTimeGraceMinutes)
    {
        var lead = NormalizeEarlyLeadMinutes(earlyLeadMinutes);
        var grace = NormalizeOnTimeGraceMinutes(onTimeGraceMinutes);
        var allowEarly = allowEarlyComplete || lead > 0;
        if (lead > 0 && !allowEarlyComplete)
            allowEarly = true;
        return (allowEarly, lead, grace);
    }
    private static int NormalizeEarlyLeadMinutes(int value)
    {
        if (value < 0 || value > 12 * 60)
            throw new InvalidOperationException("earlyLeadMinutes phải từ 0–720.");
        return value;
    }
    private static int NormalizeOnTimeGraceMinutes(int value)
    {
        if (value < 0 || value > 120)
            throw new InvalidOperationException("onTimeGraceMinutes phải từ 0–120.");
        return value;
    }
    private static string NormalizePriority(string? priority)
    {
        var value = string.IsNullOrWhiteSpace(priority)
            ? FamilyCommitmentPriorities.Normal
            : priority.Trim().ToLowerInvariant();
        if (!FamilyCommitmentPriorities.All.Contains(value))
            throw new InvalidOperationException("priority phải là critical | normal | optional.");
        return value;
    }
    private static int? NormalizeDuration(
        int? expectedDurationMinutes,
        TimeOnly? windowStart,
        TimeOnly? windowEnd)
    {
        if (expectedDurationMinutes is int d)
        {
            if (d < 1 || d > 24 * 60)
                throw new InvalidOperationException("expectedDurationMinutes phải từ 1–1440.");
            return d;
        }
        if (windowStart is TimeOnly start && windowEnd is TimeOnly end && end > start)
            return Math.Max(1, (int)Math.Round((end - start).TotalMinutes));
        return null;
    }
    private static string? NormalizeContextAnchor(string? contextAnchor)
    {
        if (string.IsNullOrWhiteSpace(contextAnchor)) return null;
        var value = contextAnchor.Trim().ToLowerInvariant();
        if (!FamilyContextAnchors.All.Contains(value))
            throw new InvalidOperationException(
                "contextAnchor không hợp lệ (after_wake, before_breakfast, …).");
        return value;
    }
    private static Guid[] NormalizeDepends(
        IReadOnlyList<Guid>? dependsOnTemplateIds,
        Guid? selfId,
        IReadOnlyList<FamilyRoutineRepository.TemplateRow> siblings)
    {
        var raw = (dependsOnTemplateIds ?? Array.Empty<Guid>())
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToArray();
        if (raw.Length == 0) return [];
        if (selfId is Guid sid && raw.Contains(sid))
            throw new InvalidOperationException("dependsOn không được trỏ vào chính nó.");
        var siblingIds = siblings.Select(t => t.Id).ToHashSet();
        foreach (var id in raw)
        {
            if (!siblingIds.Contains(id))
                throw new InvalidOperationException(
                    "dependsOn chỉ được chọn cam kết trong cùng routine.");
        }
        // Cycle check using proposed edges for selfId (if known) + existing sibling edges
        var edges = siblings.ToDictionary(
            t => t.Id,
            t => (t.DependsOnTemplateIds ?? []).ToList());
        if (selfId is Guid node)
            edges[node] = raw.ToList();
        if (HasCycle(edges))
            throw new InvalidOperationException("dependsOn tạo vòng lặp trong routine.");
        return raw;
    }
    private static bool HasCycle(Dictionary<Guid, List<Guid>> edges)
    {
        var visiting = new HashSet<Guid>();
        var visited = new HashSet<Guid>();
        bool Dfs(Guid node)
        {
            if (visited.Contains(node)) return false;
            if (!visiting.Add(node)) return true;
            if (edges.TryGetValue(node, out var deps))
            {
                foreach (var d in deps)
                {
                    if (Dfs(d)) return true;
                }
            }
            visiting.Remove(node);
            visited.Add(node);
            return false;
        }
        foreach (var id in edges.Keys)
        {
            if (Dfs(id)) return true;
        }
        return false;
    }
    private static int[] NormalizeWeekdays(IReadOnlyList<int>? weekdays)
    {
        if (weekdays is null || weekdays.Count == 0) return [];
        return weekdays
            .Where(d => d is >= 1 and <= 7)
            .Distinct()
            .OrderBy(d => d)
            .ToArray();
    }
    private static RoutineDto MapRoutine(
        FamilyRoutineRepository.RoutineRow routine,
        IReadOnlyList<FamilyRoutineRepository.TemplateRow> templates) =>
        new(
            routine.Id,
            routine.FamilyId,
            routine.Code,
            routine.DisplayName,
            routine.Kind,
            (routine.Weekdays ?? []).ToList(),
            routine.IsActive,
            routine.SortOrder,
            templates.Select(MapTemplate).ToList());
    private static CommitmentTemplateDto MapTemplate(FamilyRoutineRepository.TemplateRow t) =>
        new(
            t.Id,
            t.RoutineId,
            t.MemberId,
            t.Title,
            t.Description,
            t.WindowStart,
            t.WindowEnd,
            t.SortOrder,
            t.IsActive,
            string.IsNullOrWhiteSpace(t.Priority) ? FamilyCommitmentPriorities.Normal : t.Priority,
            t.ExpectedDurationMinutes,
            t.ContextAnchor,
            (t.DependsOnTemplateIds ?? []).ToList(),
            t.AllowEarlyComplete,
            t.EarlyLeadMinutes,
            t.OnTimeGraceMinutes,
            t.StarReward > 0 ? t.StarReward : FamilyStarCalculator.InferStarReward(t.Title));
}
