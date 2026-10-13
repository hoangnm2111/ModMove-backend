using MediatR;

namespace ModMove.Application.Common.Messaging;

/// <summary>
/// Read side of CQRS: a use case that only reads.
/// Handlers use ISqlConnectionFactory (Dapper, no change tracking, no locks)
/// and must never depend on repositories or IUnitOfWork.
/// </summary>
public interface IQuery<out TResponse> : IRequest<TResponse>;

public interface IQueryHandler<in TQuery, TResponse> : IRequestHandler<TQuery, TResponse>
    where TQuery : IQuery<TResponse>;
