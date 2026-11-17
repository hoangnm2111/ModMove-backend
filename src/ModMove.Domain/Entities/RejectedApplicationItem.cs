namespace ModMove.Domain.Entities;

/// <summary>Structured rejection entry for a single item of a driver onboarding application.</summary>
public sealed record RejectedApplicationItem(string Key, string Reason);

