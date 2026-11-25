using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Auth.Queries.CheckEmailAvailable;

/// <summary>Sign-up form: tell the user at once that an email already has an account (same check as register, GB-01).</summary>
public sealed record CheckEmailAvailableQuery(string Email) : IQuery<CheckEmailAvailableResponse>;

public sealed record CheckEmailAvailableResponse(bool Available);
