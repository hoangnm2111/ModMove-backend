using MediatR;

namespace ModMove.Application.Common.Messaging;

/// <summary>
/// Write side of CQRS: a use case that changes state.
/// Handlers may use repositories + IUnitOfWork (tracked, transactional).
/// </summary>
public interface ICommand : IRequest;

public interface ICommand<out TResponse> : IRequest<TResponse>;

public interface ICommandHandler<in TCommand> : IRequestHandler<TCommand>
    where TCommand : ICommand;

public interface ICommandHandler<in TCommand, TResponse> : IRequestHandler<TCommand, TResponse>
    where TCommand : ICommand<TResponse>;
