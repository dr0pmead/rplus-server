using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using MediatR;
using Microsoft.Extensions.Logging;
using RPlus.SDK.Wallet.Commands;
using RPlus.SDK.Wallet.Queries;
using RPlus.SDK.Wallet.Results;
using WalletProto = RPlusGrpc.Wallet;

using RPlus.Wallet.Domain.Repositories;
using RPlus.Wallet.Domain.Services;
using RPlus.Wallet.Domain.Entities;
using WalletEntity = RPlus.Wallet.Domain.Entities.Wallet;

namespace RPlus.Wallet.Api.Grpc;

public class WalletGrpcService : WalletProto.WalletService.WalletServiceBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<WalletGrpcService> _logger;
    private readonly IWalletRepository _walletRepository;
    private readonly IEncryptionService _encryptionService;

    public WalletGrpcService(
        IMediator mediator, 
        ILogger<WalletGrpcService> logger,
        IWalletRepository walletRepository,
        IEncryptionService encryptionService)
    {
        _mediator = mediator;
        _logger = logger;
        _walletRepository = walletRepository;
        _encryptionService = encryptionService;
    }

    public override async Task<WalletProto.CreateWalletResponse> CreateWallet(WalletProto.CreateWalletRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.UserId, out var userId) || userId == Guid.Empty)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid UserId"));
        }

        var wallet = await _walletRepository.GetByUserIdAsync(userId, context.CancellationToken);
        if (wallet == null)
        {
             wallet = new WalletEntity(userId, _encryptionService.Encrypt(0L), _encryptionService.GetCurrentKeyId());
             await _walletRepository.AddAsync(wallet, context.CancellationToken);
             await _walletRepository.SaveChangesAsync(context.CancellationToken);
        }

        var balance = _encryptionService.DecryptLong(wallet.BalanceEncrypted, wallet.BalanceKeyId);

        return new WalletProto.CreateWalletResponse
        {
            Id = wallet.Id.ToString(),
            UserId = request.UserId,
            Balance = balance
        };
    }

    public override async Task<WalletProto.AccruePointsResponse> AccruePoints(WalletProto.AccruePointsRequest request, ServerCallContext context)
    {
        var result = await _mediator.Send(new AccruePointsCommand(
            request.UserId,
            request.Amount,
            request.OperationId,
            request.Source,
            request.Description,
            JsonSerializer.Serialize(request.Metadata),
            new DateTimeOffset(request.Timestamp.ToDateTime()).ToUnixTimeMilliseconds(),
            request.RequestId,
            request.Signature,
            string.IsNullOrEmpty(request.SourceType) ? null : request.SourceType,
            string.IsNullOrEmpty(request.SourceCategory) ? null : request.SourceCategory));

        return new WalletProto.AccruePointsResponse
        {
            OperationId = request.OperationId,
            BalanceAfter = result.NewBalance,
            Status = result.Status,
            ErrorCode = result.ErrorCode ?? string.Empty
        };
    }

    public override async Task<WalletProto.GetBalanceResponse> GetBalance(WalletProto.GetBalanceRequest request, ServerCallContext context)
    {
        var balance = await _mediator.Send(new GetBalanceQuery(request.UserId));
        return new WalletProto.GetBalanceResponse { Balance = balance };
    }

    public override async Task<WalletProto.GetHistoryResponse> GetHistory(WalletProto.GetHistoryRequest request, ServerCallContext context)
    {
        var result = await _mediator.Send(new GetHistoryQuery(request.UserId, request.Limit <= 0 ? 20 : request.Limit, request.Cursor, request.Source));
        var response = new WalletProto.GetHistoryResponse { NextCursor = result.NextCursor ?? string.Empty };

        foreach (var item in result.Items)
        {
            var dto = new WalletProto.WalletTransactionDto
            {
                OperationId = item.OperationId,
                Amount = item.Amount,
                BalanceBefore = item.BalanceBefore,
                BalanceAfter = item.BalanceAfter,
                Source = item.Source,
                Status = item.Status,
                CreatedAt = Timestamp.FromDateTime(DateTimeOffset.FromUnixTimeMilliseconds(item.CreatedAt).UtcDateTime),
                ProcessedAt = Timestamp.FromDateTime(DateTimeOffset.FromUnixTimeMilliseconds(item.ProcessedAt).UtcDateTime),
                Description = item.Description
            };
            
            if (!string.IsNullOrEmpty(item.Metadata))
            {
                 try 
                 {
                     var metadata = JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, string>>(item.Metadata);
                     if (metadata != null)
                     {
                         foreach(var kv in metadata)
                         {
                             dto.Metadata.Add(kv.Key, kv.Value);
                         }
                     }
                 }
                 catch { /* Ignore invalid JSON metadata */ }
            }
            
            response.Items.Add(dto);
        }

        return response;
    }

    public override async Task<WalletProto.ReverseTransactionResponse> ReverseTransaction(WalletProto.ReverseTransactionRequest request, ServerCallContext context)
    {
        var result = await _mediator.Send(new ReverseTransactionCommand(
            request.UserId,
            request.OriginalOperationId,
            request.Reason,
            request.RequestId));

        return new WalletProto.ReverseTransactionResponse
        {
            Success = result.Success,
            BalanceAfter = result.BalanceAfter,
            ErrorCode = result.ErrorCode ?? string.Empty
        };
    }

    public override async Task<WalletProto.ReservePointsResponse> ReservePoints(WalletProto.ReservePointsRequest request, ServerCallContext context)
    {
        var result = await _mediator.Send(new ReservePointsCommand(
            request.UserId,
            request.Amount,
            request.OperationId,
            request.Source,
            request.Description,
            JsonSerializer.Serialize(request.Metadata),
            new DateTimeOffset(request.Timestamp.ToDateTime()).ToUnixTimeMilliseconds(),
            request.RequestId,
            request.Signature));

        return new WalletProto.ReservePointsResponse
        {
            OperationId = request.OperationId,
            BalanceAfter = result.AvailableBalance,
            Status = result.Status,
            ErrorCode = result.ErrorCode ?? string.Empty
        };
    }

    public override async Task<WalletProto.CommitReserveResponse> CommitReserve(WalletProto.CommitReserveRequest request, ServerCallContext context)
    {
        var result = await _mediator.Send(new CommitReserveCommand(request.UserId, request.OperationId));
        return new WalletProto.CommitReserveResponse
        {
            Success = result.Success,
            BalanceAfter = result.BalanceAfter,
            ErrorCode = result.ErrorCode ?? string.Empty
        };
    }

    public override async Task<WalletProto.CancelReserveResponse> CancelReserve(WalletProto.CancelReserveRequest request, ServerCallContext context)
    {
        var result = await _mediator.Send(new CancelReserveCommand(request.UserId, request.OperationId));
        return new WalletProto.CancelReserveResponse
        {
            Success = result.Success,
            BalanceAfter = result.BalanceAfter,
            ErrorCode = result.ErrorCode ?? string.Empty
        };
    }

    public override async Task<WalletProto.CheckResponse> Check(WalletProto.CheckRequest request, ServerCallContext context)
    {
        var result = await _mediator.Send(new CheckStatusQuery(request.UserId, request.OperationId));
        return new WalletProto.CheckResponse
        {
            Status = result.Status,
            Balance = result.Balance,
            ReservedBalance = result.ReservedBalance,
            LastError = result.LastError ?? string.Empty
        };
    }

    public override async Task<WalletProto.GetMonthlyPointsResponse> GetMonthlyPoints(WalletProto.GetMonthlyPointsRequest request, ServerCallContext context)
    {
        var sourceTypes = request.SourceTypes.Count > 0 
            ? request.SourceTypes.ToArray() 
            : null;
        
        var result = await _mediator.Send(new RPlus.SDK.Wallet.Queries.GetMonthlyPointsQuery(
            request.UserId,
            request.Year,
            request.Month,
            sourceTypes));

        return new WalletProto.GetMonthlyPointsResponse
        {
            TotalPoints = result.TotalPoints,
            TransactionCount = result.TransactionCount,
            Success = result.Success,
            Error = result.Error ?? string.Empty
        };
    }
}

