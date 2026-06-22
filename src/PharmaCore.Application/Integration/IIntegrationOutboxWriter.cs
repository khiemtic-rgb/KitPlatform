using System.Data;

namespace PharmaCore.Application.Integration;

public interface IIntegrationOutboxWriter
{
    Task WriteAsync(
        IDbConnection conn,
        IDbTransaction tx,
        string eventType,
        string aggregateType,
        Guid aggregateId,
        object data,
        Guid? actorUserId,
        CancellationToken cancellationToken = default);
}
