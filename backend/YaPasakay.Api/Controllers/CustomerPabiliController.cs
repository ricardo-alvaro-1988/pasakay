using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YaPasakay.Api.Services;
using YaPasakay.Application.Admin;
using YaPasakay.Domain.Entities;
using YaPasakay.Domain.Enums;
using YaPasakay.Infrastructure.Persistence;

namespace YaPasakay.Api.Controllers;

[ApiController]
[Authorize(Roles = "Customer")]
[Route("api/customer/pabili")]
public class CustomerPabiliController(
    AppDbContext db,
    PabiliPricingService pricing,
    PabiliOrderBroadcastService broadcast,
    LiveNotify live) : ControllerBase
{
    [HttpGet("merchants")]
    public async Task<ActionResult<IReadOnlyList<CustomerPabiliMerchantCard>>> Merchants(
        [FromQuery] double lat,
        [FromQuery] double lng,
        [FromQuery] Guid? barangayId,
        [FromQuery] string? q,
        CancellationToken cancellationToken)
    {
        var (customer, status, message) = await CustomerContext.RequireAsync(db, User, cancellationToken);
        if (customer is null)
        {
            return StatusCode(status, new { message });
        }

        var op = await ResolveOperatorAsync(barangayId, lat, lng, cancellationToken);
        if (op is null)
        {
            return Ok(Array.Empty<CustomerPabiliMerchantCard>());
        }

        var query = db.Merchants
            .AsNoTracking()
            .Include(x => x.OperatingHours)
            .Where(x => x.OperatorId == op.Id && x.IsActive);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(x => x.BusinessName.Contains(term) || x.PinnedAddress.Contains(term));
        }

        var rows = await query
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.BusinessName)
            .Take(80)
            .ToListAsync(cancellationToken);

        return Ok(rows.Select(MapMerchantCard).ToList());
    }

    [HttpGet("products/popular")]
    public async Task<ActionResult<IReadOnlyList<CustomerPabiliPopularProduct>>> PopularProducts(
        [FromQuery] double lat,
        [FromQuery] double lng,
        [FromQuery] Guid? barangayId,
        [FromQuery] string? q,
        CancellationToken cancellationToken)
    {
        var (customer, status, message) = await CustomerContext.RequireAsync(db, User, cancellationToken);
        if (customer is null)
        {
            return StatusCode(status, new { message });
        }

        var op = await ResolveOperatorAsync(barangayId, lat, lng, cancellationToken);
        if (op is null)
        {
            return Ok(Array.Empty<CustomerPabiliPopularProduct>());
        }

        var merchants = await db.Merchants
            .AsNoTracking()
            .Include(x => x.OperatingHours)
            .Include(x => x.Products)
            .Where(x => x.OperatorId == op.Id && x.IsActive)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.BusinessName)
            .Take(40)
            .ToListAsync(cancellationToken);

        IEnumerable<(Merchant Merchant, MerchantProduct Product)> pairs = merchants
            .SelectMany(m => m.Products
                .Where(p => PabiliPricingService.IsProductAvailableNow(p))
                .OrderBy(p => p.SortOrder)
                .ThenBy(p => p.Name)
                .Select(p => (m, p)));

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            pairs = pairs.Where(x =>
                x.Product.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
                || x.Product.Description.Contains(term, StringComparison.OrdinalIgnoreCase)
                || x.Merchant.BusinessName.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        var items = pairs
            .Take(24)
            .Select(x => new CustomerPabiliPopularProduct(
                x.Product.Id,
                x.Merchant.Id,
                x.Merchant.BusinessName,
                x.Product.Name,
                x.Product.Description,
                x.Product.SellingPrice,
                UploadUrls.FromPath(x.Product.ImagePath),
                PabiliPricingService.IsMerchantOpen(x.Merchant)))
            .ToList();

        return Ok(items);
    }

    [HttpGet("merchants/{id:guid}")]
    public async Task<ActionResult<CustomerPabiliStoreResponse>> Store(Guid id, CancellationToken cancellationToken)
    {
        var (customer, status, message) = await CustomerContext.RequireAsync(db, User, cancellationToken);
        if (customer is null)
        {
            return StatusCode(status, new { message });
        }

        var merchant = await db.Merchants
            .AsNoTracking()
            .Include(x => x.OperatingHours)
            .Include(x => x.Categories)
            .Include(x => x.Products)
                .ThenInclude(x => x.Category)
            .Include(x => x.Products)
                .ThenInclude(x => x.AddonGroups)
                    .ThenInclude(x => x.Options)
            .Include(x => x.Products)
                .ThenInclude(x => x.AdoptedAddons)
                    .ThenInclude(x => x.AddonGroup)
                        .ThenInclude(g => g.Options)
            .FirstOrDefaultAsync(x => x.Id == id && x.IsActive, cancellationToken);
        if (merchant is null)
        {
            return NotFound(new { message = "Store not found." });
        }

        var categories = merchant.Categories
            .Where(x => x.IsActive)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .Select(x => new CustomerPabiliCategoryTab(x.Id, x.Name))
            .ToList();

        var products = merchant.Products
            .Where(x => PabiliPricingService.IsProductAvailableNow(x))
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .Select(MapProduct)
            .ToList();

        return Ok(new CustomerPabiliStoreResponse(
            merchant.Id,
            merchant.BusinessName,
            merchant.PinnedAddress,
            UploadUrls.FromPath(merchant.LogoPath),
            UploadUrls.FromPath(merchant.BackgroundPath),
            PabiliPricingService.IsMerchantOpen(merchant),
            merchant.Latitude,
            merchant.Longitude,
            categories,
            products));
    }

    [HttpPost("quote")]
    public async Task<ActionResult<CustomerPabiliQuoteResponse>> Quote(
        [FromBody] CustomerPabiliQuoteRequest request,
        CancellationToken cancellationToken)
    {
        var (customer, status, message) = await CustomerContext.RequireAsync(db, User, cancellationToken);
        if (customer is null)
        {
            return StatusCode(status, new { message });
        }

        var built = await BuildCartAsync(request.MerchantId, request.Items, cancellationToken);
        if (built.Error is not null)
        {
            return BadRequest(new { message = built.Error });
        }

        var delivery = await pricing.QuoteDeliveryAsync(
            built.Merchant!.OperatorId,
            built.Merchant.Latitude,
            built.Merchant.Longitude,
            request.DropoffLat,
            request.DropoffLng,
            cancellationToken);
        if (delivery.Error is not null)
        {
            return BadRequest(new { message = delivery.Error });
        }

        var total = PabiliPricingService.CustomerTotal(built.GoodsSelling, delivery.DeliveryFee, 0);
        return Ok(new CustomerPabiliQuoteResponse(
            built.Merchant.Id,
            built.Merchant.BusinessName,
            built.GoodsSelling,
            delivery.DeliveryFee,
            delivery.SurchargeTotal,
            delivery.DistanceKm,
            0,
            string.Empty,
            total,
            built.Lines));
    }

    [HttpPost("orders")]
    public async Task<ActionResult<CustomerPabiliOrderDetail>> Place(
        [FromBody] CustomerPabiliPlaceRequest request,
        CancellationToken cancellationToken)
    {
        var (customer, status, message) = await CustomerContext.RequireAsync(db, User, cancellationToken);
        if (customer is null)
        {
            return StatusCode(status, new { message });
        }

        if (string.IsNullOrWhiteSpace(request.DropoffAddress))
        {
            return BadRequest(new { message = "Drop-off address is required." });
        }

        var built = await BuildCartAsync(request.MerchantId, request.Items, cancellationToken);
        if (built.Error is not null || built.Merchant is null)
        {
            return BadRequest(new { message = built.Error ?? "Invalid cart." });
        }

        if (!PabiliPricingService.IsMerchantOpen(built.Merchant))
        {
            return BadRequest(new { message = "This store is closed right now." });
        }

        var delivery = await pricing.QuoteDeliveryAsync(
            built.Merchant.OperatorId,
            built.Merchant.Latitude,
            built.Merchant.Longitude,
            request.DropoffLat,
            request.DropoffLng,
            cancellationToken);
        if (delivery.Error is not null)
        {
            return BadRequest(new { message = delivery.Error });
        }

        await db.Entry(customer).Reference(x => x.AppUser).LoadAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var order = new PabiliOrder
        {
            OperatorId = built.Merchant.OperatorId,
            MerchantId = built.Merchant.Id,
            CustomerId = customer.Id,
            Reference = $"PB{now:yyyyMMdd}-{Random.Shared.Next(10, 99):00}{now:ssff}",
            Status = PabiliOrderStatus.Pending,
            CustomerName = customer.DisplayName,
            CustomerPhone = customer.AppUser.PhoneNumber,
            MerchantName = built.Merchant.BusinessName,
            PickupAddress = built.Merchant.PinnedAddress,
            PickupLat = built.Merchant.Latitude,
            PickupLng = built.Merchant.Longitude,
            DropoffAddress = request.DropoffAddress.Trim(),
            DropoffLat = request.DropoffLat,
            DropoffLng = request.DropoffLng,
            DropoffBarangayId = request.DropoffBarangayId,
            DistanceKm = delivery.DistanceKm,
            GoodsSubtotal = built.GoodsSelling,
            GoodsBaseSubtotal = built.GoodsBase,
            DeliveryFee = delivery.DeliveryFee,
            SurchargeTotal = delivery.SurchargeTotal,
            AdjustmentAmount = 0,
            AdjustmentLabel = string.Empty,
            CustomerTotal = PabiliPricingService.CustomerTotal(built.GoodsSelling, delivery.DeliveryFee, 0),
            PaymentMethod = request.PaymentMethod,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim()
        };

        var sort = 0;
        foreach (var line in built.BuiltItems)
        {
            var item = new PabiliOrderItem
            {
                ProductId = line.ProductId,
                Name = line.Name,
                Quantity = line.Quantity,
                UnitBasePrice = line.UnitBase,
                UnitSellingPrice = line.UnitSelling,
                LineBaseTotal = line.LineBase,
                LineSellingTotal = line.LineSelling,
                SortOrder = sort++
            };
            foreach (var addon in line.Addons)
            {
                item.Addons.Add(new PabiliOrderItemAddon
                {
                    AddonOptionId = addon.OptionId,
                    Name = addon.Name,
                    Quantity = addon.Quantity,
                    UnitBasePrice = addon.UnitBase,
                    UnitSellingPrice = addon.UnitSelling,
                    LineBaseTotal = addon.LineBase,
                    LineSellingTotal = addon.LineSelling
                });
            }

            order.Items.Add(item);
        }

        db.PabiliOrders.Add(order);
        await db.SaveChangesAsync(cancellationToken);
        await broadcast.BroadcastAsync(order.Id, cancellationToken);
        await live.CustomerChangedAsync(customer.Id, "pabili-order", cancellationToken);

        var detail = await LoadOrderDetailAsync(order.Id, customer.Id, cancellationToken);
        return Ok(detail);
    }

    [HttpGet("orders")]
    public async Task<ActionResult<IReadOnlyList<CustomerPabiliOrderDetail>>> MyOrders(CancellationToken cancellationToken)
    {
        var (customer, status, message) = await CustomerContext.RequireAsync(db, User, cancellationToken);
        if (customer is null)
        {
            return StatusCode(status, new { message });
        }

        var ids = await db.PabiliOrders.AsNoTracking()
            .Where(x => x.CustomerId == customer.Id)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => x.Id)
            .Take(40)
            .ToListAsync(cancellationToken);

        var list = new List<CustomerPabiliOrderDetail>();
        foreach (var id in ids)
        {
            var detail = await LoadOrderDetailAsync(id, customer.Id, cancellationToken);
            if (detail is not null)
            {
                list.Add(detail);
            }
        }

        return Ok(list);
    }

    [HttpGet("orders/{id:guid}")]
    public async Task<ActionResult<CustomerPabiliOrderDetail>> Order(Guid id, CancellationToken cancellationToken)
    {
        var (customer, status, message) = await CustomerContext.RequireAsync(db, User, cancellationToken);
        if (customer is null)
        {
            return StatusCode(status, new { message });
        }

        var detail = await LoadOrderDetailAsync(id, customer.Id, cancellationToken);
        return detail is null ? NotFound(new { message = "Order not found." }) : Ok(detail);
    }

    [HttpPost("orders/{id:guid}/cancel")]
    public async Task<ActionResult<CustomerPabiliOrderDetail>> Cancel(
        Guid id,
        [FromBody] CancelPabiliOrderRequest? request,
        CancellationToken cancellationToken)
    {
        var (customer, status, message) = await CustomerContext.RequireAsync(db, User, cancellationToken);
        if (customer is null)
        {
            return StatusCode(status, new { message });
        }

        var order = await db.PabiliOrders
            .Include(x => x.Offers)
            .FirstOrDefaultAsync(x => x.Id == id && x.CustomerId == customer.Id, cancellationToken);
        if (order is null)
        {
            return NotFound(new { message = "Order not found." });
        }

        if (order.Status is not (PabiliOrderStatus.Pending or PabiliOrderStatus.Waiting))
        {
            return BadRequest(new { message = "This order can no longer be cancelled." });
        }

        order.Status = PabiliOrderStatus.Cancelled;
        order.CancelledAtUtc = DateTime.UtcNow;
        order.CancelledBy = CancelledBy.Customer;
        order.CancelReason = string.IsNullOrWhiteSpace(request?.Reason) ? "Cancelled by customer." : request!.Reason!.Trim();
        order.UpdatedAtUtc = DateTime.UtcNow;
        foreach (var offer in order.Offers.Where(x => x.Status == OfferStatus.Offered))
        {
            offer.Status = OfferStatus.Expired;
            offer.UpdatedAtUtc = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);
        if (order.RiderId is Guid riderId)
        {
            await live.RiderChangedAsync(riderId, "pabili-cancelled", cancellationToken);
        }

        await live.CustomerChangedAsync(customer.Id, "pabili-cancelled", cancellationToken);
        return Ok(await LoadOrderDetailAsync(order.Id, customer.Id, cancellationToken));
    }

    private async Task<Operator?> ResolveOperatorAsync(
        Guid? barangayId,
        double lat,
        double lng,
        CancellationToken cancellationToken)
    {
        Barangay? barangay = null;
        if (barangayId is Guid id)
        {
            barangay = await db.Barangays.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        }

        if (barangay is null)
        {
            // Fall back: nearest covered operator by merchant proximity is fine for v1 —
            // resolve via any barangay match when GPS-only; prefer operators with merchants near the pin.
            return await db.Operators.AsNoTracking()
                .Where(x => x.IsActive && x.Merchants.Any(m => m.IsActive))
                .OrderBy(x => x.CompanyName)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return await db.Operators
            .Where(x => x.IsActive && (
                x.Areas.Any(a => a.BarangayId == barangay.Id)
                || x.Areas.Any(a => a.Barangay.MunicipalityId == barangay.MunicipalityId)))
            .OrderBy(x => x.CompanyName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<BuiltCart> BuildCartAsync(
        Guid merchantId,
        IReadOnlyList<CustomerPabiliCartItemRequest>? items,
        CancellationToken cancellationToken)
    {
        if (items is null || items.Count == 0)
        {
            return new BuiltCart { Error = "Add at least one item." };
        }

        var merchant = await db.Merchants
            .Include(x => x.OperatingHours)
            .Include(x => x.Products)
                .ThenInclude(x => x.AddonGroups)
                    .ThenInclude(x => x.Options)
            .Include(x => x.Products)
                .ThenInclude(x => x.AdoptedAddons)
                    .ThenInclude(x => x.AddonGroup)
                        .ThenInclude(g => g.Options)
            .FirstOrDefaultAsync(x => x.Id == merchantId && x.IsActive, cancellationToken);
        if (merchant is null)
        {
            return new BuiltCart { Error = "Store not found." };
        }

        decimal goodsSelling = 0;
        decimal goodsBase = 0;
        var lines = new List<CustomerPabiliQuoteLine>();
        var builtItems = new List<BuiltItem>();

        foreach (var req in items)
        {
            if (req.Quantity < 1 || req.Quantity > 99)
            {
                return new BuiltCart { Error = "Item quantity must be between 1 and 99." };
            }

            var product = merchant.Products.FirstOrDefault(x => x.Id == req.ProductId);
            if (product is null || !PabiliPricingService.IsProductAvailableNow(product))
            {
                return new BuiltCart { Error = "One or more products are unavailable." };
            }

            var addonLines = new List<CustomerPabiliQuoteAddonLine>();
            var builtAddons = new List<BuiltAddon>();
            decimal addonSellingPerUnit = 0;
            decimal addonBasePerUnit = 0;

            var groups = product.AddonGroups.Where(x => x.IsActive).ToList();
            groups.AddRange(product.AdoptedAddons
                .Where(x => x.AddonGroup.IsActive)
                .Select(x => new ProductAddonGroup
                {
                    Id = x.AddonGroup.Id,
                    Name = x.AddonGroup.Name,
                    MinSelect = x.AddonGroup.MinSelect,
                    MaxSelect = x.AddonGroup.MaxSelect,
                    IsActive = true,
                    Options = x.AddonGroup.Options
                        .Where(o => o.IsActive)
                        .Select(o => new ProductAddonOption
                        {
                            Id = o.Id,
                            Name = o.Name,
                            PriceDelta = o.SellingPrice,
                            IsActive = true
                        })
                        .ToList()
                }));

            var selected = req.Addons ?? Array.Empty<CustomerPabiliCartAddonRequest>();
            var selectedIds = selected.Select(x => x.OptionId).ToHashSet();

            foreach (var group in groups)
            {
                var activeOptions = group.Options.Where(x => x.IsActive).ToList();
                var picks = selected.Where(s => activeOptions.Any(o => o.Id == s.OptionId)).ToList();
                var pickCount = picks.Sum(x => Math.Max(1, x.Quantity));
                if (pickCount < group.MinSelect || (group.MaxSelect > 0 && pickCount > group.MaxSelect))
                {
                    return new BuiltCart { Error = $"Choose {group.MinSelect}–{group.MaxSelect} options for {group.Name}." };
                }

                foreach (var pick in picks)
                {
                    var option = activeOptions.First(x => x.Id == pick.OptionId);
                    var qty = Math.Max(1, pick.Quantity);
                    // Merchant library options carry selling/base via PriceDelta stand-in above; reload real prices when possible.
                    var merchantOption = product.AdoptedAddons
                        .SelectMany(x => x.AddonGroup.Options)
                        .FirstOrDefault(x => x.Id == option.Id);
                    var unitSell = merchantOption?.SellingPrice ?? option.PriceDelta;
                    var unitAddonBase = merchantOption?.BasePrice ?? 0;
                    addonSellingPerUnit += unitSell * qty;
                    addonBasePerUnit += unitAddonBase * qty;
                    addonLines.Add(new CustomerPabiliQuoteAddonLine(option.Name, unitSell * qty));
                    builtAddons.Add(new BuiltAddon(
                        option.Id,
                        option.Name,
                        qty,
                        unitAddonBase,
                        unitSell,
                        CommissionCut.Round(unitAddonBase * qty * req.Quantity),
                        CommissionCut.Round(unitSell * qty * req.Quantity)));
                }
            }

            if (selectedIds.Count > 0
                && selectedIds.Any(id => !groups.SelectMany(g => g.Options).Any(o => o.Id == id)))
            {
                return new BuiltCart { Error = "Invalid add-on selection." };
            }

            var unitSelling = CommissionCut.Round(product.SellingPrice + addonSellingPerUnit);
            var productUnitBase = CommissionCut.Round(product.BasePrice + addonBasePerUnit);
            var lineSell = CommissionCut.Round(unitSelling * req.Quantity);
            var lineBase = CommissionCut.Round(productUnitBase * req.Quantity);
            goodsSelling += lineSell;
            goodsBase += lineBase;
            lines.Add(new CustomerPabiliQuoteLine(
                product.Id,
                product.Name,
                req.Quantity,
                unitSelling,
                lineSell,
                addonLines));
            builtItems.Add(new BuiltItem(
                product.Id,
                product.Name,
                req.Quantity,
                productUnitBase,
                unitSelling,
                lineBase,
                lineSell,
                builtAddons));
        }

        return new BuiltCart
        {
            Merchant = merchant,
            GoodsSelling = CommissionCut.Round(goodsSelling),
            GoodsBase = CommissionCut.Round(goodsBase),
            Lines = lines,
            BuiltItems = builtItems
        };
    }

    private async Task<CustomerPabiliOrderDetail?> LoadOrderDetailAsync(
        Guid orderId,
        Guid customerId,
        CancellationToken cancellationToken)
    {
        var order = await db.PabiliOrders.AsNoTracking()
            .Include(x => x.Rider)!.ThenInclude(r => r!.AppUser)
            .Include(x => x.Items)
                .ThenInclude(i => i.Addons)
            .FirstOrDefaultAsync(x => x.Id == orderId && x.CustomerId == customerId, cancellationToken);
        return order is null ? null : MapOrder(order);
    }

    private static CustomerPabiliMerchantCard MapMerchantCard(Merchant merchant) =>
        new(
            merchant.Id,
            merchant.BusinessName,
            merchant.PinnedAddress,
            UploadUrls.FromPath(merchant.LogoPath),
            UploadUrls.FromPath(merchant.BackgroundPath),
            PabiliPricingService.IsMerchantOpen(merchant),
            merchant.Latitude,
            merchant.Longitude);

    private static CustomerPabiliProductCard MapProduct(MerchantProduct product)
    {
        var groups = new List<CustomerPabiliAddonGroup>();
        foreach (var group in product.AddonGroups.Where(x => x.IsActive).OrderBy(x => x.SortOrder))
        {
            groups.Add(new CustomerPabiliAddonGroup(
                group.Id,
                group.Name,
                group.MinSelect,
                group.MaxSelect,
                group.Options.Where(x => x.IsActive).OrderBy(x => x.SortOrder)
                    .Select(o => new CustomerPabiliAddonOption(o.Id, o.Name, o.PriceDelta, 0))
                    .ToList()));
        }

        foreach (var link in product.AdoptedAddons.Where(x => x.AddonGroup.IsActive).OrderBy(x => x.AddonGroup.SortOrder))
        {
            var g = link.AddonGroup;
            groups.Add(new CustomerPabiliAddonGroup(
                g.Id,
                g.Name,
                g.MinSelect,
                g.MaxSelect,
                g.Options.Where(x => x.IsActive).OrderBy(x => x.SortOrder)
                    .Select(o => new CustomerPabiliAddonOption(o.Id, o.Name, o.SellingPrice, o.BasePrice))
                    .ToList()));
        }

        return new CustomerPabiliProductCard(
            product.Id,
            product.CategoryId,
            product.Category?.Name ?? "Menu",
            product.Name,
            product.Description,
            product.SellingPrice,
            UploadUrls.FromPath(product.ImagePath),
            groups);
    }

    internal static CustomerPabiliOrderDetail MapOrder(PabiliOrder order) =>
        new(
            order.Id,
            order.Reference,
            order.Status.ToString(),
            order.MerchantName,
            order.PickupAddress,
            order.DropoffAddress,
            order.PickupLat,
            order.PickupLng,
            order.DropoffLat,
            order.DropoffLng,
            order.DistanceKm,
            order.GoodsSubtotal,
            order.DeliveryFee,
            order.SurchargeTotal,
            order.AdjustmentAmount,
            order.AdjustmentLabel,
            order.CustomerTotal,
            order.PaymentMethod.ToString(),
            order.Rider?.AppUser.FullName,
            order.Rider?.AppUser.PhoneNumber,
            order.Notes,
            DateTime.SpecifyKind(order.CreatedAtUtc, DateTimeKind.Utc),
            order.AcceptedAtUtc,
            order.PickedUpAtUtc,
            order.DeliveringAtUtc,
            order.CompletedAtUtc,
            order.CancelledAtUtc,
            order.Status is PabiliOrderStatus.Pending or PabiliOrderStatus.Waiting,
            order.Items.OrderBy(x => x.SortOrder).Select(i => new CustomerPabiliOrderLineItem(
                i.Id,
                i.Name,
                i.Quantity,
                i.UnitSellingPrice,
                i.LineSellingTotal,
                i.Addons.Select(a => new CustomerPabiliOrderAddonItem(
                    a.Id,
                    a.Name,
                    a.Quantity,
                    a.UnitSellingPrice,
                    a.LineSellingTotal)).ToList())).ToList());

    private sealed class BuiltCart
    {
        public Merchant? Merchant { get; set; }
        public decimal GoodsSelling { get; set; }
        public decimal GoodsBase { get; set; }
        public List<CustomerPabiliQuoteLine> Lines { get; set; } = [];
        public List<BuiltItem> BuiltItems { get; set; } = [];
        public string? Error { get; set; }
    }

    private sealed record BuiltItem(
        Guid ProductId,
        string Name,
        int Quantity,
        decimal UnitBase,
        decimal UnitSelling,
        decimal LineBase,
        decimal LineSelling,
        List<BuiltAddon> Addons);

    private sealed record BuiltAddon(
        Guid OptionId,
        string Name,
        int Quantity,
        decimal UnitBase,
        decimal UnitSelling,
        decimal LineBase,
        decimal LineSelling);
}
