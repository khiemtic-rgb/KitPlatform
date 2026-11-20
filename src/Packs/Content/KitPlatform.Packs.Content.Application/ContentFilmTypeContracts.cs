namespace KitPlatform.Packs.Content;

public sealed record FilmTypeDto(
    Guid Id,
    Guid BrandId,
    string BrandCode,
    string Code,
    string Name,
    string WorkflowKey,
    string Status,
    string Purpose = "",
    string SeriesLabel = "",
    string Aspect = "",
    bool IsDefault = false,
    string[]? Steps = null,
    int FilmCount = 0);

public sealed record CreateFilmTypeRequest(
    Guid BrandId,
    string Code,
    string Name,
    string WorkflowKey,
    string? Purpose = null,
    string? SeriesLabel = null,
    string? Aspect = null,
    bool IsDefault = false,
    string[]? Steps = null);

public sealed record UpdateFilmTypeRequest(
    string Name,
    string WorkflowKey,
    string Status,
    string? Purpose,
    string? SeriesLabel,
    string? Aspect,
    bool IsDefault,
    string[]? Steps);

public sealed record BindProjectFilmTypeRequest(Guid FilmTypeId);

public sealed record UpdateFilmTypeWorkflowRequest(string WorkflowKey);

public sealed record SetContentFormatRequest(string Format);

public sealed record LinkProductionContentRequest(Guid? TopicId);

public interface IContentFilmTypeService
{
    Task<IReadOnlyList<FilmTypeDto>> ListAsync(Guid brandId, CancellationToken cancellationToken = default);

    Task<FilmTypeDto> CreateAsync(CreateFilmTypeRequest request, CancellationToken cancellationToken = default);

    Task<FilmTypeDto> UpdateAsync(Guid filmTypeId, UpdateFilmTypeRequest request, CancellationToken cancellationToken = default);

    Task BindProjectAsync(Guid projectId, Guid filmTypeId, CancellationToken cancellationToken = default);

    Task ChangeWorkflowAsync(Guid filmTypeId, string workflowKey, CancellationToken cancellationToken = default);

    Task SetTopicFormatAsync(Guid topicId, string format, CancellationToken cancellationToken = default);

    Task LinkProductionAsync(Guid productionId, Guid? topicId, CancellationToken cancellationToken = default);

    Task SelectProductionForPublishAsync(Guid productionId, CancellationToken cancellationToken = default);
}
