using System.Globalization;
using ETCS.Shared.Infrastructure.Pos;

namespace ETCS.Shared.Application.Pos;

public sealed class PosNfcPurchaseService : IPosNfcPurchaseService
{
    private readonly IPosNfcMemberRepository _nfcMemberRepository;
    private readonly IPosLegacyTransactionRepository _legacyRepository;

    public PosNfcPurchaseService(
        IPosNfcMemberRepository nfcMemberRepository,
        IPosLegacyTransactionRepository legacyRepository)
    {
        _nfcMemberRepository = nfcMemberRepository;
        _legacyRepository = legacyRepository;
    }

    public async Task<PosNfcPurchaseResponse> PurchaseAsync(
        PosNfcPurchaseRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return Fail("Request body is required.");
        }

        var lines = request.Lines?
            .Where(line => !string.IsNullOrWhiteSpace(line.SkuCode) && line.Amount > 0)
            .ToList() ?? [];

        if (lines.Count == 0)
        {
            return Fail("At least one purchase line is required.");
        }

        if (string.IsNullOrWhiteSpace(request.TransactionId))
        {
            return Fail("TransactionId is required.");
        }

        var member = await ResolveMemberAsync(
            request.CardSn,
            request.UidHex,
            request.UidHexReversed,
            request.UidDecimal,
            request.UidDecimalReversed,
            cancellationToken);
        if (member is not { IsSuccess: true } resolved || resolved.Member is null)
        {
            return member?.Response ?? Fail("Card not found.", "CARD_NOT_FOUND");
        }

        var payable = lines.Sum(line => line.Amount);
        var row = resolved.Member;
        if (row.BalPrepaid < payable)
        {
            return Fail(
                "Insufficient balance. Current balance: AED " + row.BalPrepaid.ToString("0.00", CultureInfo.InvariantCulture) + ".",
                "INSUFFICIENT_BALANCE",
                row);
        }

        var now = DateTime.Now;
        var legacy = await _legacyRepository.GetSpendLimitInfoAsync(
            row.CustomerId,
            now.Date,
            PosSpendWeekHelper.GetWeekStartDate(now),
            cancellationToken);

        if (legacy is not null)
        {
            if (legacy.WeeklySpendLimit > 0 && legacy.WeeklyNetSpent + payable > legacy.WeeklySpendLimit)
            {
                return Fail("Weekly spending limit exceeded!", "WEEKLY_LIMIT", row);
            }

            if (legacy.DailySpendLimit > 0 && legacy.DailyNetSpent + payable > legacy.DailySpendLimit)
            {
                return Fail("Daily spending limit exceeded!", "DAILY_LIMIT", row);
            }
        }

        return await _nfcMemberRepository.PurchaseAsync(
            row,
            payable,
            request.TransactionId.Trim(),
            request.IpAddress?.Trim() ?? string.Empty,
            now,
            lines,
            cancellationToken);
    }

    public async Task<PosNfcPurchaseResponse> UndoAsync(
        PosNfcUndoRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return Fail("Request body is required.");
        }

        if (request.Amount <= 0)
        {
            return Fail("Amount is required.");
        }

        var member = await ResolveMemberAsync(
            request.CardSn,
            request.UidHex,
            request.UidHexReversed,
            request.UidDecimal,
            request.UidDecimalReversed,
            cancellationToken);
        if (member is not { IsSuccess: true } resolved || resolved.Member is null)
        {
            return member?.Response ?? Fail("Card not found.", "CARD_NOT_FOUND");
        }

        return await _nfcMemberRepository.UndoAsync(
            resolved.Member,
            request.Amount,
            request.TransactionId?.Trim() ?? string.Empty,
            cancellationToken);
    }

    private async Task<MemberLookup> ResolveMemberAsync(
        string? cardSn,
        string? uidHex,
        string? uidHexReversed,
        string? uidDecimal,
        string? uidDecimalReversed,
        CancellationToken cancellationToken)
    {
        var candidates = PosNfcCardSnNormalizer.BuildCandidates(
            cardSn,
            uidHex,
            uidHexReversed,
            uidDecimal,
            uidDecimalReversed);
        if (candidates.Count == 0)
        {
            return MemberLookup.Failed(Fail("Card serial is required.", "CARD_REQUIRED"));
        }

        var row = await _nfcMemberRepository.FindByCardSnAsync(candidates, cancellationToken);
        if (row is null || string.IsNullOrWhiteSpace(row.CustomerId))
        {
            return MemberLookup.Failed(Fail("Card not found.", "CARD_NOT_FOUND"));
        }

        if (!row.IsActive)
        {
            return MemberLookup.Failed(Fail("Card is not active.", "CARD_INACTIVE", row));
        }

        return MemberLookup.Ok(row);
    }

    private static PosNfcPurchaseResponse Fail(string message, string? code = null, PosNfcMemberRow? member = null) =>
        new()
        {
            IsSuccess = false,
            Message = message,
            Code = code,
            CustomerId = member?.CustomerId ?? string.Empty,
            CardSn = member?.CardSn ?? string.Empty,
            Balance = member?.BalPrepaid ?? 0m
        };

    private sealed class MemberLookup
    {
        public bool IsSuccess { get; private init; }
        public PosNfcMemberRow? Member { get; private init; }
        public PosNfcPurchaseResponse? Response { get; private init; }

        public static MemberLookup Ok(PosNfcMemberRow member) =>
            new() { IsSuccess = true, Member = member };

        public static MemberLookup Failed(PosNfcPurchaseResponse response) =>
            new() { IsSuccess = false, Response = response };
    }
}
