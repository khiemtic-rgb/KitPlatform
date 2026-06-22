namespace PharmaCore.Application.Abstractions;

public interface ICurrentUserAccessor
{
    IReadOnlyList<string> Permissions { get; }

    bool IsInRole(string role);
}
