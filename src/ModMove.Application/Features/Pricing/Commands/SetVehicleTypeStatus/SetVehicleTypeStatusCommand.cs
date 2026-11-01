using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Pricing.Commands.SetVehicleTypeStatus;

/// <summary>Enables or disables a vehicle type (UC-ADM-11).</summary>
public sealed record SetVehicleTypeStatusCommand(long Id, bool IsActive) : ICommand;
