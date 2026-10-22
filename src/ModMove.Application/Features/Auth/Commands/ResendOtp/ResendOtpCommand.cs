using ModMove.Application.Common.Messaging;
using ModMove.Domain.Enums;

namespace ModMove.Application.Features.Auth.Commands.ResendOtp;

/// <summary>Send a new code (GB-25: at most 3 sends per 15 minutes, each valid 5 minutes).</summary>
public sealed record ResendOtpCommand(string Email, OtpPurpose Purpose) : ICommand;
