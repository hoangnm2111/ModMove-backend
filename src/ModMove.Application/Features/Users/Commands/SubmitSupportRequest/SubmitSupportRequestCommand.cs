using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Users.Commands.SubmitSupportRequest;

/// <summary>UC-GST-05 / DECISIONS B14: a guest's question; staff call or message back outside the system.</summary>
public sealed record SubmitSupportRequestCommand(string FullName, string Contact, string Content) : ICommand;
