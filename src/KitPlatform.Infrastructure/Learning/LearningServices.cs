using KitPlatform.Application.Abstractions;
using KitPlatform.Application.Learning;

namespace KitPlatform.Infrastructure.Learning;

internal sealed class LearningCatalogService : ILearningCatalogService
{
    private readonly LearningRepository _repo;

    public LearningCatalogService(LearningRepository repo) => _repo = repo;

    public Task<IReadOnlyList<LearningProgramListItemDto>> ListProgramsAsync(
        CancellationToken cancellationToken = default) =>
        _repo.ListProgramsAsync(cancellationToken);

    public Task<LearningProgramDetailDto?> GetProgramAsync(
        Guid programId,
        CancellationToken cancellationToken = default) =>
        _repo.GetProgramAsync(programId, cancellationToken);

    public Task<LearningModuleDetailDto?> GetModuleAsync(
        Guid moduleId,
        CancellationToken cancellationToken = default) =>
        _repo.GetModuleAsync(moduleId, cancellationToken);

    public Task<LearningModuleDetailDto> UpsertModuleTenantContentAsync(
        Guid moduleId,
        UpsertLearningModuleTenantContentRequest request,
        CancellationToken cancellationToken = default) =>
        _repo.UpsertModuleTenantContentAsync(moduleId, request, cancellationToken);

    public Task<LearningModuleDetailDto> RevertModuleTenantContentAsync(
        Guid moduleId,
        CancellationToken cancellationToken = default) =>
        _repo.RevertModuleTenantContentAsync(moduleId, cancellationToken);
}

internal sealed class LearningProgressService : ILearningProgressService
{
    private readonly LearningRepository _repo;
    private readonly ITenantContext _tenant;

    public LearningProgressService(LearningRepository repo, ITenantContext tenant)
    {
        _repo = repo;
        _tenant = tenant;
    }

    public async Task<LearningMyLearningDto> GetMyLearningAsync(
        CancellationToken cancellationToken = default)
    {
        var employeeId = await RequireEmployeeAsync(cancellationToken);
        var programs = await _repo.ListProgramsAsync(cancellationToken);
        var primary = programs.FirstOrDefault();
        var credentials = await _repo.ListCredentialsAsync(employeeId, cancellationToken);

        if (primary is null)
            return new LearningMyLearningDto(null, [], credentials);

        var enrollment = await _repo.GetEnrollmentAsync(employeeId, primary.Id, cancellationToken);
        if (enrollment is null)
            return new LearningMyLearningDto(null, [], credentials);

        var modules = await _repo.ListProgressAsync(enrollment.Id, cancellationToken);
        return new LearningMyLearningDto(enrollment, modules, credentials);
    }

    public async Task<LearningEnrollmentDto> EnsureMyEnrollmentAsync(
        Guid programId,
        CancellationToken cancellationToken = default)
    {
        var employeeId = await RequireEmployeeAsync(cancellationToken);
        return await _repo.UpsertEnrollmentAsync(
            employeeId, programId, _tenant.UserId, cancellationToken);
    }

    public async Task<LearningEnrollmentDto> AssignAsync(
        AssignLearningProgramRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.EmployeeId is null || request.EmployeeId == Guid.Empty)
            throw new ArgumentException("Thiếu employeeId.");

        return await _repo.UpsertEnrollmentAsync(
            request.EmployeeId.Value,
            request.ProgramId,
            _tenant.UserId,
            cancellationToken);
    }

    public Task<IReadOnlyList<LearningEnrollmentDto>> ListEnrollmentsAsync(
        CancellationToken cancellationToken = default) =>
        _repo.ListEnrollmentsAsync(cancellationToken);

    public Task<IReadOnlyList<LearningCompetencyRosterItemDto>> ListCompetencyRosterAsync(
        CancellationToken cancellationToken = default) =>
        _repo.ListCompetencyRosterAsync(cancellationToken);

    public async Task<LearningGateCheckDto> CheckGateAsync(
        string permissionCode,
        CancellationToken cancellationToken = default)
    {
        var employeeId = await RequireEmployeeAsync(cancellationToken);
        return await _repo.CheckGateAsync(employeeId, permissionCode, cancellationToken);
    }

    public async Task CreateGateOverrideAsync(
        CreateLearningGateOverrideRequest request,
        CancellationToken cancellationToken = default)
    {
        await _repo.CreateGateOverrideAsync(
            request.EmployeeId,
            request.PermissionCode,
            request.Reason,
            request.ExpiresAt,
            cancellationToken);
    }

    public Task<LearningEvaluationDto> UpsertEvaluationAsync(
        UpsertLearningEvaluationRequest request,
        CancellationToken cancellationToken = default) =>
        _repo.UpsertEvaluationAsync(request, cancellationToken);

    public Task<IReadOnlyList<LearningEvaluationDto>> ListEvaluationsAsync(
        int? year,
        int? month,
        CancellationToken cancellationToken = default) =>
        _repo.ListEvaluationsAsync(year, month, cancellationToken);

    public async Task<IReadOnlyList<LearningEvaluationDto>> ListMyEvaluationsAsync(
        CancellationToken cancellationToken = default)
    {
        var employeeId = await RequireEmployeeAsync(cancellationToken);
        return await _repo.ListEvaluationsForEmployeeAsync(employeeId, cancellationToken);
    }

    public async Task<LearningEvaluationDto> SubmitMyEvaluationFeedbackAsync(
        Guid evaluationId,
        SubmitLearningEvaluationFeedbackRequest request,
        CancellationToken cancellationToken = default)
    {
        var employeeId = await RequireEmployeeAsync(cancellationToken);
        return await _repo.SubmitEmployeeEvaluationFeedbackAsync(
            employeeId, evaluationId, request, cancellationToken);
    }

    public Task<LearningRecognitionDto> CreateRecognitionAsync(
        CreateLearningRecognitionRequest request,
        CancellationToken cancellationToken = default) =>
        _repo.CreateRecognitionAsync(request, cancellationToken);

    public async Task<IReadOnlyList<LearningRecognitionDto>> ListRecognitionsAsync(
        int take = 30,
        bool mineOnly = false,
        CancellationToken cancellationToken = default)
    {
        Guid? employeeId = null;
        if (mineOnly)
            employeeId = await RequireEmployeeAsync(cancellationToken);
        return await _repo.ListRecognitionsAsync(take, employeeId, cancellationToken);
    }

    public Task<IReadOnlyList<LearningCustomerFeedbackDto>> ListRecentCustomerFeedbackAsync(
        int hours = 48,
        int take = 20,
        CancellationToken cancellationToken = default) =>
        _repo.ListRecentCustomerFeedbackAsync(hours, take, cancellationToken);

    public async Task<IReadOnlyList<LearningBadgeDto>> ListMyBadgesAsync(
        CancellationToken cancellationToken = default)
    {
        var employeeId = await RequireEmployeeAsync(cancellationToken);
        return await _repo.ListBadgesForEmployeeAsync(employeeId, cancellationToken);
    }

    public Task<IReadOnlyList<LearningCareerLevelDto>> ListCareerLevelsAsync(
        CancellationToken cancellationToken = default) =>
        _repo.ListCareerLevelsAsync(cancellationToken);

    public Task<IReadOnlyList<LearningCareerRosterItemDto>> ListCareerRosterAsync(
        CancellationToken cancellationToken = default) =>
        _repo.ListCareerRosterAsync(cancellationToken);

    public Task<LearningCareerPromotionDto> PromoteCareerAsync(
        PromoteLearningCareerRequest request,
        CancellationToken cancellationToken = default) =>
        _repo.PromoteCareerAsync(request, cancellationToken);

    public Task<IReadOnlyList<LearningCareerPromotionDto>> ListCareerPromotionsAsync(
        int take = 30,
        CancellationToken cancellationToken = default) =>
        _repo.ListCareerPromotionsAsync(take, cancellationToken);

    public Task<LearningPeopleDashboardDto> GetPeopleDashboardAsync(
        CancellationToken cancellationToken = default) =>
        _repo.GetPeopleDashboardAsync(cancellationToken);

    public Task<LearningEmployeeEvidenceDto?> GetEmployeeEvidenceAsync(
        Guid employeeId,
        CancellationToken cancellationToken = default) =>
        _repo.GetEmployeeEvidenceAsync(employeeId, cancellationToken);

    public async Task AcknowledgeModuleAsync(
        Guid moduleId,
        string? selfieUrl = null,
        CancellationToken cancellationToken = default)
    {
        var employeeId = await RequireEmployeeAsync(cancellationToken);
        await _repo.AcknowledgeModuleAsync(employeeId, moduleId, selfieUrl, cancellationToken);
    }

    public async Task StartModuleAsync(
        Guid moduleId,
        CancellationToken cancellationToken = default)
    {
        var employeeId = await RequireEmployeeAsync(cancellationToken);
        await _repo.StartModuleAsync(employeeId, moduleId, cancellationToken);
    }

    public async Task<SubmitLearningQuizResultDto> SubmitQuizAsync(
        Guid moduleId,
        SubmitLearningQuizRequest request,
        CancellationToken cancellationToken = default)
    {
        var employeeId = await RequireEmployeeAsync(cancellationToken);
        return await _repo.SubmitQuizAsync(
            employeeId,
            moduleId,
            request.Answers ?? [],
            request.Practice,
            cancellationToken);
    }

    public async Task<LearningMonthlyDrillStatusDto> GetMonthlyDrillStatusAsync(
        CancellationToken cancellationToken = default)
    {
        var employeeId = await RequireEmployeeAsync(cancellationToken);
        return await _repo.GetMonthlyDrillStatusAsync(employeeId, cancellationToken);
    }

    public async Task<LearningMonthlyDrillStartDto> StartMonthlyDrillAsync(
        CancellationToken cancellationToken = default)
    {
        var employeeId = await RequireEmployeeAsync(cancellationToken);
        return await _repo.StartMonthlyDrillAsync(employeeId, cancellationToken);
    }

    public async Task<LearningMonthlyDrillResultDto> SubmitMonthlyDrillAsync(
        SubmitLearningMonthlyDrillRequest request,
        CancellationToken cancellationToken = default)
    {
        var employeeId = await RequireEmployeeAsync(cancellationToken);
        return await _repo.SubmitMonthlyDrillAsync(employeeId, request, cancellationToken);
    }

    public async Task<LearningMyHabitsDto> GetMyHabitsAsync(
        CancellationToken cancellationToken = default)
    {
        var employeeId = await RequireEmployeeAsync(cancellationToken);
        return await _repo.GetMyHabitsAsync(employeeId, cancellationToken);
    }

    public Task<IReadOnlyList<LearningObservationPendingDto>> ListPendingObservationsAsync(
        CancellationToken cancellationToken = default) =>
        _repo.ListPendingObservationsAsync(cancellationToken);

    public Task<LearningObservationDto> SubmitObservationAsync(
        SubmitLearningObservationRequest request,
        CancellationToken cancellationToken = default) =>
        _repo.SubmitObservationAsync(_tenant.UserId, request, cancellationToken);

    public async Task<LearningObservationDto?> GetMyModuleObservationAsync(
        Guid moduleId,
        CancellationToken cancellationToken = default)
    {
        var employeeId = await RequireEmployeeAsync(cancellationToken);
        return await _repo.GetModuleObservationForEmployeeAsync(employeeId, moduleId, cancellationToken);
    }

    public Task<CreateLearningMailThreadsResultDto> CreateMailThreadAsync(
        CreateLearningMailThreadRequest request,
        CancellationToken cancellationToken = default) =>
        _repo.CreateMailThreadAsync(request, cancellationToken);

    public Task<IReadOnlyList<LearningMailThreadListItemDto>> ListMailThreadsAsync(
        CancellationToken cancellationToken = default) =>
        _repo.ListMailThreadsAsync(cancellationToken);

    public Task<LearningMailThreadDetailDto?> GetMailThreadAsync(
        Guid threadId,
        CancellationToken cancellationToken = default) =>
        _repo.GetMailThreadAsync(threadId, cancellationToken);

    public Task<LearningMailMessageDto> ReplyMailThreadAsync(
        Guid threadId,
        ReplyLearningMailRequest request,
        CancellationToken cancellationToken = default) =>
        _repo.ReplyMailThreadAsync(threadId, request, cancellationToken);

    public Task MarkMailThreadReadAsync(
        Guid threadId,
        CancellationToken cancellationToken = default) =>
        _repo.MarkMailThreadReadAsync(threadId, cancellationToken);

    public Task<LearningMailUnreadCountDto> GetMailUnreadCountAsync(
        CancellationToken cancellationToken = default) =>
        _repo.GetMailUnreadCountAsync(cancellationToken);

    private async Task<Guid> RequireEmployeeAsync(CancellationToken cancellationToken)
    {
        var employeeId = await _repo.GetEmployeeIdForCurrentUserAsync(cancellationToken);
        if (employeeId is null)
            throw new InvalidOperationException(
                "Tài khoản chưa gắn nhân viên — không thể ghi nhận tiến độ học.");
        return employeeId.Value;
    }
}
