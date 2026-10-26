using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.MasterData.Commands.SetMasterDataStatus;

/// <summary>Enables or disables a master data lookup entry (UC-ADM-16).</summary>
public sealed record SetMasterDataStatusCommand(long Id, bool IsActive) : ICommand;
