using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.MasterData.Commands.UpdateMasterDataItem;

/// <summary>Updates name and/or description of a master data entry, e.g. policy texts (UC-ADM-16, DECISIONS B13).</summary>
public sealed record UpdateMasterDataItemCommand(long Id, string Name, string? Description) : ICommand;
