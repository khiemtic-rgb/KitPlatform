namespace PharmaCore.Application.Sales;

public sealed record SaleDiscountInput(short? DiscountType, decimal? DiscountValue);

public sealed record PricedSaleLineResult(
    Guid ProductId,
    Guid ProductUnitId,
    decimal Quantity,
    decimal ConversionFactor,
    decimal UnitPrice,
    decimal GrossTotal,
    decimal DiscountAmount,
    short? DiscountType,
    decimal DiscountValue,
    decimal LineTotal);

public sealed record SaleOrderPricingResult(
    decimal SubtotalGross,
    decimal LineDiscountTotal,
    decimal MerchandiseNet,
    decimal OrderDiscountAmount,
    decimal TotalAmount,
    IReadOnlyList<PricedSaleLineResult> Lines);

public sealed record SalesDiscountPolicy(bool CanApply, bool Unlimited, decimal MaxPercent)
{
    public static SalesDiscountPolicy FromPermissions(IReadOnlyList<string> permissions, bool isAdmin)
    {
        if (isAdmin || permissions.Contains(SalesDiscountPermissions.Unlimited))
            return new SalesDiscountPolicy(true, true, 100m);

        if (permissions.Contains(SalesDiscountPermissions.Apply))
            return new SalesDiscountPolicy(true, false, SalesDiscountLimits.StaffMaxPercent);

        return new SalesDiscountPolicy(false, false, 0m);
    }
}

public static class SalesPricing
{
    public static decimal ComputeDiscountAmount(decimal basis, short? discountType, decimal? discountValue)
    {
        if (basis <= 0 || discountType is null || discountValue is null || discountValue <= 0)
            return 0;

        return discountType switch
        {
            SalesDiscountTypes.Percent => Math.Round(
                basis * Math.Min(discountValue.Value, 100m) / 100m,
                0,
                MidpointRounding.AwayFromZero),
            SalesDiscountTypes.Fixed => Math.Min(discountValue.Value, basis),
            _ => 0,
        };
    }

    public static SaleOrderPricingResult PriceOrder(
        IReadOnlyList<(CreateSaleLineRequest Request, decimal UnitPrice, decimal ConversionFactor)> lines,
        SaleDiscountInput? orderDiscount)
    {
        var pricedLines = new List<PricedSaleLineResult>();
        decimal subtotalGross = 0;
        decimal lineDiscountTotal = 0;

        foreach (var (request, unitPrice, conversionFactor) in lines)
        {
            var gross = request.Quantity * unitPrice;
            var discountAmount = ComputeDiscountAmount(gross, request.DiscountType, request.DiscountValue);
            subtotalGross += gross;
            lineDiscountTotal += discountAmount;
            pricedLines.Add(new PricedSaleLineResult(
                request.ProductId,
                request.ProductUnitId,
                request.Quantity,
                conversionFactor,
                unitPrice,
                gross,
                discountAmount,
                request.DiscountType,
                request.DiscountValue ?? 0,
                gross - discountAmount));
        }

        var merchandiseNet = subtotalGross - lineDiscountTotal;
        var orderDiscountAmount = ComputeDiscountAmount(
            merchandiseNet,
            orderDiscount?.DiscountType,
            orderDiscount?.DiscountValue);
        var totalAmount = merchandiseNet - orderDiscountAmount;

        return new SaleOrderPricingResult(
            subtotalGross,
            lineDiscountTotal,
            merchandiseNet,
            orderDiscountAmount,
            totalAmount,
            pricedLines);
    }

    /// <summary>Tiền hoàn một dòng trả — phân bổ CK đơn theo tỷ lệ thành tiền dòng.</summary>
    public static decimal ComputeLineRefundAmount(
        decimal lineTotal,
        decimal soldQuantity,
        decimal returnQuantity,
        decimal merchandiseNet,
        decimal orderDiscountAmount)
    {
        if (soldQuantity <= 0 || returnQuantity <= 0)
            return 0;

        var lineOrderDiscountShare = merchandiseNet > 0
            ? lineTotal / merchandiseNet * orderDiscountAmount
            : 0;
        var refundableLineNet = lineTotal - lineOrderDiscountShare;
        return Math.Round(
            refundableLineNet * returnQuantity / soldQuantity,
            0,
            MidpointRounding.AwayFromZero);
    }

    public static void ValidateDiscounts(SaleOrderPricingResult pricing, SalesDiscountPolicy policy)
    {
        if (pricing.LineDiscountTotal <= 0 && pricing.OrderDiscountAmount <= 0)
            return;

        if (!policy.CanApply)
            throw new InvalidOperationException("Tài khoản không có quyền chiết khấu.");

        foreach (var line in pricing.Lines)
        {
            if (line.DiscountAmount <= 0)
                continue;

            var effectivePercent = line.GrossTotal > 0
                ? line.DiscountAmount / line.GrossTotal * 100m
                : 0m;
            if (!policy.Unlimited && effectivePercent > policy.MaxPercent + 0.01m)
                throw new InvalidOperationException(
                    $"Chiết khấu dòng vượt quá {policy.MaxPercent:0.#}% cho phép.");
        }

        if (pricing.OrderDiscountAmount > 0 && pricing.MerchandiseNet > 0)
        {
            var orderPercent = pricing.OrderDiscountAmount / pricing.MerchandiseNet * 100m;
            if (!policy.Unlimited && orderPercent > policy.MaxPercent + 0.01m)
                throw new InvalidOperationException(
                    $"Chiết khấu đơn vượt quá {policy.MaxPercent:0.#}% cho phép.");
        }
    }
}
