using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.MasterData.Commands.CreateMasterDataItem;

/// <summary>Creates a new master data lookup entry (UC-ADM-15).</summary>
public sealed record CreateMasterDataItemCommand(
    string Category,
    string Code,
    string Name,
    string? Description = null) : ICommand<long>;
