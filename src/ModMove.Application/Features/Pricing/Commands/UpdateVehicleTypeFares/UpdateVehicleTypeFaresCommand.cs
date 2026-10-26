using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Pricing.Commands.UpdateVehicleTypeFares;

/// <summary>UC-ADM-11: base fare, price per km and minimum fare of one vehicle type (DECISIONS B1).</summary>
public sealed record UpdateVehicleTypeFaresCommand(long Id, decimal BaseFare, decimal PricePerKm, decimal MinimumFare) : ICommand;
