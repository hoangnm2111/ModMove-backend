using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.MasterData.Queries.GetMasterDataList;

/// <summary>Lists master data items, optionally filtered by category (UC-ADM-14..16).</summary>
public sealed record GetMasterDataListQuery(string? Category = null) : IQuery<IReadOnlyList<MasterDataItemDto>>;
