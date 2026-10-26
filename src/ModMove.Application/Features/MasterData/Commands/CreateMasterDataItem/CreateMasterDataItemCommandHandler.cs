using Dapper;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Domain.Entities;

namespace ModMove.Application.Features.MasterData.Commands.CreateMasterDataItem;

internal sealed class CreateMasterDataItemCommandHandler(
    IRepository<MasterDataItem> repository,
    ISqlConnectionFactory connectionFactory,
    IUnitOfWork unitOfWork)
    : ICommandHandler<CreateMasterDataItemCommand, long>
{
    private const string CheckDuplicateSql = """
        SELECT COUNT(1)
        FROM master_data_items
        WHERE category = @Category AND code = @Code
        """;

    public async Task<long> Handle(CreateMasterDataItemCommand request, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        var count = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(CheckDuplicateSql, new { request.Category, request.Code }, cancellationToken: cancellationToken));

        if (count > 0)
            throw new ConflictException($"Master data item with code '{request.Code}' in category '{request.Category}' already exists.");

        var item = MasterDataItem.Create(request.Category, request.Code, request.Name, request.Description);
        repository.Add(item);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return item.Id;
    }
}
