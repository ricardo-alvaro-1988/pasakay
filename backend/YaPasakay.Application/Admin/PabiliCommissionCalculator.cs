using YaPasakay.Domain.Entities;
using YaPasakay.Domain.Enums;

namespace YaPasakay.Application.Admin;

public record PabiliCommissionBreakdown(
    decimal FareSystemPercent,
    decimal FareSystemAmount,
    decimal FareOperatorPercent,
    decimal FareOperatorAmount,
    decimal FareRiderPercent,
    decimal FareRiderAmount,
    decimal MarkupSystemPercent,
    decimal MarkupSystemAmount,
    decimal MarkupOperatorPercent,
    decimal MarkupOperatorAmount,
    decimal MarkupRiderPercent,
    decimal MarkupRiderAmount)
{
    public decimal RemitAmount => CommissionCut.Round(
        FareSystemAmount + FareOperatorAmount + MarkupSystemAmount + MarkupOperatorAmount);

    public decimal RemitPercent => FareCommissionSplit.Round(
        FareSystemPercent + FareOperatorPercent + MarkupSystemPercent + MarkupOperatorPercent);
}

public static class PabiliCommissionCalculator
{
    public static PabiliCommissionBreakdown? ForOrder(PabiliOrder order, Operator op, PabiliMatrix? matrix)
    {
        if (order.Status == PabiliOrderStatus.Cancelled)
        {
            return null;
        }

        if (order.FareSystemAmount is not null
            && order.FareOperatorAmount is not null
            && order.FareRiderAmount is not null
            && order.MarkupSystemAmount is not null
            && order.MarkupOperatorAmount is not null
            && order.MarkupRiderAmount is not null)
        {
            return new PabiliCommissionBreakdown(
                order.FareSystemPercent ?? 0,
                order.FareSystemAmount.Value,
                order.FareOperatorPercent ?? 0,
                order.FareOperatorAmount.Value,
                order.FareRiderPercent ?? 0,
                order.FareRiderAmount.Value,
                order.MarkupSystemPercent ?? 0,
                order.MarkupSystemAmount.Value,
                order.MarkupOperatorPercent ?? 0,
                order.MarkupOperatorAmount.Value,
                order.MarkupRiderPercent ?? 0,
                order.MarkupRiderAmount.Value);
        }

        if (matrix is null)
        {
            return null;
        }

        var delivery = order.DeliveryFee;
        var markupBase = Math.Max(0, order.GoodsSubtotal - order.GoodsBaseSubtotal);
        var fareSystem = op.PabiliFareSystemCommissionPercent;
        var markupSystem = op.PabiliMarkupSystemCommissionPercent;

        return new PabiliCommissionBreakdown(
            fareSystem,
            CommissionCut.Round(delivery * fareSystem / 100m),
            matrix.FareOperatorCommissionPercent,
            CommissionCut.Round(delivery * matrix.FareOperatorCommissionPercent / 100m),
            matrix.FareRiderCommissionPercent,
            CommissionCut.Round(delivery * matrix.FareRiderCommissionPercent / 100m),
            markupSystem,
            CommissionCut.Round(markupBase * markupSystem / 100m),
            matrix.MarkupOperatorCommissionPercent,
            CommissionCut.Round(markupBase * matrix.MarkupOperatorCommissionPercent / 100m),
            matrix.MarkupRiderCommissionPercent,
            CommissionCut.Round(markupBase * matrix.MarkupRiderCommissionPercent / 100m));
    }

    public static void Snapshot(PabiliOrder order, PabiliCommissionBreakdown breakdown)
    {
        order.FareSystemPercent = breakdown.FareSystemPercent;
        order.FareSystemAmount = breakdown.FareSystemAmount;
        order.FareOperatorPercent = breakdown.FareOperatorPercent;
        order.FareOperatorAmount = breakdown.FareOperatorAmount;
        order.FareRiderPercent = breakdown.FareRiderPercent;
        order.FareRiderAmount = breakdown.FareRiderAmount;
        order.MarkupSystemPercent = breakdown.MarkupSystemPercent;
        order.MarkupSystemAmount = breakdown.MarkupSystemAmount;
        order.MarkupOperatorPercent = breakdown.MarkupOperatorPercent;
        order.MarkupOperatorAmount = breakdown.MarkupOperatorAmount;
        order.MarkupRiderPercent = breakdown.MarkupRiderPercent;
        order.MarkupRiderAmount = breakdown.MarkupRiderAmount;
    }
}
