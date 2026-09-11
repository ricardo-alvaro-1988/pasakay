using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YaPasakay.Api.Services;
using YaPasakay.Application.Admin;
using YaPasakay.Application.Common;
using YaPasakay.Domain.Entities;
using YaPasakay.Domain.Enums;
using YaPasakay.Infrastructure.Auth;
using YaPasakay.Infrastructure.Persistence;

namespace YaPasakay.Api.Controllers;

[ApiController]
[Authorize(Roles = "Operator")]
[ServiceFilter(typeof(OperatorAccessFilter))]
[Route("api/operator/merchants")]
public class OperatorMerchantsController(AppDbContext db, UploadStore uploads) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<MerchantListResponse>> List(CancellationToken cancellationToken)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        var rows = await db.Merchants
            .AsNoTracking()
            .Where(x => x.OperatorId == op.Id)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.BusinessName)
            .ToListAsync(cancellationToken);
        return Ok(new MerchantListResponse(rows.Select(MapList).ToList()));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MerchantDetailItem>> Get(Guid id, CancellationToken cancellationToken)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        var row = await LoadMerchantAsync(op.Id, id, cancellationToken);
        return row is null ? NotFound(new { message = "Merchant not found." }) : Ok(MapDetail(row));
    }

    [HttpPost]
    public async Task<ActionResult<MerchantDetailItem>> Create(
        [FromBody] SaveMerchantRequest request,
        CancellationToken cancellationToken)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        var error = ValidateMerchant(request, requirePassword: request.ManagedByMerchant);
        if (error is not null)
        {
            return BadRequest(new { message = error });
        }

        var hoursError = ValidateHours(request.OperatingHours);
        if (hoursError is not null)
        {
            return BadRequest(new { message = hoursError });
        }

        var mobile = NormalizeOptionalPhone(request.Mobile);
        var email = (request.Email ?? string.Empty).Trim();

        var merchant = new Merchant
        {
            OperatorId = op.Id,
            BusinessName = request.BusinessName.Trim(),
            ContactPerson = request.ContactPerson.Trim(),
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            PinnedAddress = request.PinnedAddress.Trim(),
            ManagedByMerchant = request.ManagedByMerchant,
            Mobile = mobile,
            Email = email,
            IsActive = request.IsActive,
            SortOrder = request.SortOrder,
        };

        if (request.ManagedByMerchant)
        {
            var accountError = await EnsureMerchantAccountAsync(
                merchant,
                op.Id,
                mobile,
                email,
                request.Password,
                requirePassword: true,
                excludeUserId: null,
                cancellationToken);
            if (accountError is not null)
            {
                return BadRequest(new { message = accountError });
            }
        }

        ApplyHours(merchant, request.OperatingHours);
        db.Merchants.Add(merchant);
        await db.SaveChangesAsync(cancellationToken);

        if (merchant.AppUser is not null)
        {
            merchant.AppUser.MerchantId = merchant.Id;
            await db.SaveChangesAsync(cancellationToken);
        }

        var loaded = await LoadMerchantAsync(op.Id, merchant.Id, cancellationToken);
        return Ok(MapDetail(loaded!));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<MerchantDetailItem>> Update(
        Guid id,
        [FromBody] SaveMerchantRequest request,
        CancellationToken cancellationToken)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        var error = ValidateMerchant(request, requirePassword: false);
        if (error is not null)
        {
            return BadRequest(new { message = error });
        }

        var hoursError = ValidateHours(request.OperatingHours);
        if (hoursError is not null)
        {
            return BadRequest(new { message = hoursError });
        }

        var merchant = await db.Merchants
            .Include(x => x.AppUser)
            .Include(x => x.OperatingHours)
            .FirstOrDefaultAsync(x => x.Id == id && x.OperatorId == op.Id, cancellationToken);
        if (merchant is null)
        {
            return NotFound(new { message = "Merchant not found." });
        }

        var mobile = NormalizeOptionalPhone(request.Mobile);
        var email = (request.Email ?? string.Empty).Trim();

        merchant.BusinessName = request.BusinessName.Trim();
        merchant.ContactPerson = request.ContactPerson.Trim();
        merchant.Latitude = request.Latitude;
        merchant.Longitude = request.Longitude;
        merchant.PinnedAddress = request.PinnedAddress.Trim();
        merchant.Mobile = mobile;
        merchant.Email = email;
        merchant.IsActive = request.IsActive;
        merchant.SortOrder = request.SortOrder;
        merchant.UpdatedAtUtc = DateTime.UtcNow;

        if (request.ManagedByMerchant)
        {
            merchant.ManagedByMerchant = true;
            var accountError = await EnsureMerchantAccountAsync(
                merchant,
                op.Id,
                mobile,
                email,
                request.Password,
                requirePassword: merchant.AppUserId is null,
                excludeUserId: merchant.AppUserId,
                cancellationToken);
            if (accountError is not null)
            {
                return BadRequest(new { message = accountError });
            }
        }
        else
        {
            merchant.ManagedByMerchant = false;
            await DetachMerchantAccountAsync(merchant, cancellationToken);
        }

        ApplyHours(merchant, request.OperatingHours);
        await db.SaveChangesAsync(cancellationToken);

        var loaded = await LoadMerchantAsync(op.Id, merchant.Id, cancellationToken);
        return Ok(MapDetail(loaded!));
    }

    [HttpPost("{id:guid}/toggle")]
    public async Task<ActionResult<MerchantDetailItem>> Toggle(Guid id, CancellationToken cancellationToken)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        var merchant = await db.Merchants
            .Include(x => x.OperatingHours)
            .FirstOrDefaultAsync(x => x.Id == id && x.OperatorId == op.Id, cancellationToken);
        if (merchant is null)
        {
            return NotFound(new { message = "Merchant not found." });
        }

        merchant.IsActive = !merchant.IsActive;
        merchant.UpdatedAtUtc = DateTime.UtcNow;
        if (merchant.AppUserId is Guid userId)
        {
            var user = await db.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
            if (user is not null)
            {
                user.IsActive = merchant.IsActive && merchant.ManagedByMerchant;
                user.UpdatedAtUtc = DateTime.UtcNow;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return Ok(MapDetail(merchant));
    }

    [HttpPut("{id:guid}/hours")]
    public async Task<ActionResult<MerchantDetailItem>> SaveHours(
        Guid id,
        [FromBody] IReadOnlyList<MerchantOperatingHourItem> hours,
        CancellationToken cancellationToken)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        var hoursError = ValidateHours(hours);
        if (hoursError is not null)
        {
            return BadRequest(new { message = hoursError });
        }

        var merchant = await db.Merchants
            .Include(x => x.OperatingHours)
            .FirstOrDefaultAsync(x => x.Id == id && x.OperatorId == op.Id, cancellationToken);
        if (merchant is null)
        {
            return NotFound(new { message = "Merchant not found." });
        }

        ApplyHours(merchant, hours);
        merchant.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(MapDetail(merchant));
    }

    [HttpPost("{id:guid}/logo")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<MerchantDetailItem>> UploadLogo(
        Guid id,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        return await UploadImageAsync(id, file, logo: true, cancellationToken);
    }

    [HttpPost("{id:guid}/background")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<MerchantDetailItem>> UploadBackground(
        Guid id,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        return await UploadImageAsync(id, file, logo: false, cancellationToken);
    }

    [HttpGet("{merchantId:guid}/categories")]
    public async Task<ActionResult<MerchantCategoryListResponse>> ListCategories(
        Guid merchantId,
        CancellationToken cancellationToken)
    {
        var (op, merchant, fail) = await RequireMerchantAsync(merchantId, cancellationToken);
        if (fail is not null)
        {
            return fail;
        }

        var rows = await db.MerchantProductCategories
            .AsNoTracking()
            .Where(x => x.MerchantId == merchant!.Id)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);
        return Ok(new MerchantCategoryListResponse(rows.Select(MapCategory).ToList()));
    }

    [HttpPost("{merchantId:guid}/categories")]
    public async Task<ActionResult<MerchantProductCategoryItem>> CreateCategory(
        Guid merchantId,
        [FromBody] SaveMerchantProductCategoryRequest request,
        CancellationToken cancellationToken)
    {
        var (op, merchant, fail) = await RequireMerchantAsync(merchantId, cancellationToken);
        if (fail is not null)
        {
            return fail;
        }

        var name = (request.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return BadRequest(new { message = "Category name is required." });
        }

        var row = new MerchantProductCategory
        {
            MerchantId = merchant!.Id,
            Name = name,
            SortOrder = request.SortOrder,
            IsActive = request.IsActive,
        };
        db.MerchantProductCategories.Add(row);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(MapCategory(row));
    }

    [HttpPut("{merchantId:guid}/categories/{id:guid}")]
    public async Task<ActionResult<MerchantProductCategoryItem>> UpdateCategory(
        Guid merchantId,
        Guid id,
        [FromBody] SaveMerchantProductCategoryRequest request,
        CancellationToken cancellationToken)
    {
        var (op, merchant, fail) = await RequireMerchantAsync(merchantId, cancellationToken);
        if (fail is not null)
        {
            return fail;
        }

        var name = (request.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return BadRequest(new { message = "Category name is required." });
        }

        var row = await db.MerchantProductCategories
            .FirstOrDefaultAsync(x => x.Id == id && x.MerchantId == merchant!.Id, cancellationToken);
        if (row is null)
        {
            return NotFound(new { message = "Category not found." });
        }

        row.Name = name;
        row.SortOrder = request.SortOrder;
        row.IsActive = request.IsActive;
        row.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(MapCategory(row));
    }

    [HttpDelete("{merchantId:guid}/categories/{id:guid}")]
    public async Task<IActionResult> DeleteCategory(
        Guid merchantId,
        Guid id,
        CancellationToken cancellationToken)
    {
        var (op, merchant, fail) = await RequireMerchantAsync(merchantId, cancellationToken);
        if (fail is not null)
        {
            return fail;
        }

        var row = await db.MerchantProductCategories
            .FirstOrDefaultAsync(x => x.Id == id && x.MerchantId == merchant!.Id, cancellationToken);
        if (row is null)
        {
            return NotFound(new { message = "Category not found." });
        }

        var products = await db.MerchantProducts.Where(x => x.CategoryId == id).ToListAsync(cancellationToken);
        foreach (var product in products)
        {
            product.CategoryId = null;
            product.UpdatedAtUtc = DateTime.UtcNow;
        }

        db.MerchantProductCategories.Remove(row);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpGet("{merchantId:guid}/addon-groups")]
    public async Task<ActionResult<MerchantAddonGroupListResponse>> ListAddonLibrary(
        Guid merchantId,
        CancellationToken cancellationToken)
    {
        var (op, merchant, fail) = await RequireMerchantAsync(merchantId, cancellationToken);
        if (fail is not null)
        {
            return fail;
        }

        var rows = await db.MerchantAddonGroups
            .AsNoTracking()
            .Include(x => x.Options)
            .Where(x => x.MerchantId == merchant!.Id)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);
        return Ok(new MerchantAddonGroupListResponse(rows.Select(MapLibraryGroup).ToList()));
    }

    [HttpPost("{merchantId:guid}/addon-groups")]
    public async Task<ActionResult<ProductAddonGroupItem>> CreateAddonLibraryGroup(
        Guid merchantId,
        [FromBody] ProductAddonGroupItem request,
        CancellationToken cancellationToken)
    {
        var (op, merchant, fail) = await RequireMerchantAsync(merchantId, cancellationToken);
        if (fail is not null)
        {
            return fail;
        }

        var error = ValidateAddons([request]);
        if (error is not null)
        {
            return BadRequest(new { message = error });
        }

        var group = new MerchantAddonGroup
        {
            MerchantId = merchant!.Id,
            Name = request.Name.Trim(),
            MinSelect = request.MinSelect,
            MaxSelect = request.MaxSelect,
            SortOrder = request.SortOrder,
            IsActive = request.IsActive,
        };
        foreach (var option in request.Options ?? [])
        {
            group.Options.Add(new MerchantAddonOption
            {
                Name = option.Name.Trim(),
                PriceDelta = option.PriceDelta,
                SortOrder = option.SortOrder,
                IsActive = option.IsActive,
            });
        }

        db.MerchantAddonGroups.Add(group);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(MapLibraryGroup(group));
    }

    [HttpPut("{merchantId:guid}/addon-groups/{id:guid}")]
    public async Task<ActionResult<ProductAddonGroupItem>> UpdateAddonLibraryGroup(
        Guid merchantId,
        Guid id,
        [FromBody] ProductAddonGroupItem request,
        CancellationToken cancellationToken)
    {
        var (op, merchant, fail) = await RequireMerchantAsync(merchantId, cancellationToken);
        if (fail is not null)
        {
            return fail;
        }

        var error = ValidateAddons([request]);
        if (error is not null)
        {
            return BadRequest(new { message = error });
        }

        var group = await db.MerchantAddonGroups
            .Include(x => x.Options)
            .FirstOrDefaultAsync(x => x.Id == id && x.MerchantId == merchant!.Id, cancellationToken);
        if (group is null)
        {
            return NotFound(new { message = "Add-on group not found." });
        }

        group.Name = request.Name.Trim();
        group.MinSelect = request.MinSelect;
        group.MaxSelect = request.MaxSelect;
        group.SortOrder = request.SortOrder;
        group.IsActive = request.IsActive;
        group.UpdatedAtUtc = DateTime.UtcNow;
        db.MerchantAddonOptions.RemoveRange(group.Options);
        group.Options.Clear();
        foreach (var option in request.Options ?? [])
        {
            group.Options.Add(new MerchantAddonOption
            {
                Name = option.Name.Trim(),
                PriceDelta = option.PriceDelta,
                SortOrder = option.SortOrder,
                IsActive = option.IsActive,
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        return Ok(MapLibraryGroup(group));
    }

    [HttpDelete("{merchantId:guid}/addon-groups/{id:guid}")]
    public async Task<IActionResult> DeleteAddonLibraryGroup(
        Guid merchantId,
        Guid id,
        CancellationToken cancellationToken)
    {
        var (op, merchant, fail) = await RequireMerchantAsync(merchantId, cancellationToken);
        if (fail is not null)
        {
            return fail;
        }

        var group = await db.MerchantAddonGroups
            .Include(x => x.Options)
            .Include(x => x.ProductLinks)
            .FirstOrDefaultAsync(x => x.Id == id && x.MerchantId == merchant!.Id, cancellationToken);
        if (group is null)
        {
            return NotFound(new { message = "Add-on group not found." });
        }

        db.MerchantProductAddons.RemoveRange(group.ProductLinks);
        db.MerchantAddonGroups.Remove(group);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpGet("{merchantId:guid}/products")]
    public async Task<ActionResult<MerchantProductListResponse>> ListProducts(
        Guid merchantId,
        CancellationToken cancellationToken)
    {
        var (op, merchant, fail) = await RequireMerchantAsync(merchantId, cancellationToken);
        if (fail is not null)
        {
            return fail;
        }

        var rows = await db.MerchantProducts
            .AsNoTracking()
            .Include(x => x.Category)
            .Include(x => x.AdoptedAddons)
                .ThenInclude(x => x.AddonGroup)
                    .ThenInclude(x => x.Options)
            .Where(x => x.MerchantId == merchant!.Id)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);
        return Ok(new MerchantProductListResponse(rows.Select(MapProduct).ToList()));
    }

    [HttpGet("{merchantId:guid}/products/{id:guid}")]
    public async Task<ActionResult<MerchantProductItem>> GetProduct(
        Guid merchantId,
        Guid id,
        CancellationToken cancellationToken)
    {
        var (op, merchant, fail) = await RequireMerchantAsync(merchantId, cancellationToken);
        if (fail is not null)
        {
            return fail;
        }

        var row = await LoadProductAsync(merchant!.Id, id, cancellationToken);
        return row is null ? NotFound(new { message = "Product not found." }) : Ok(MapProduct(row));
    }

    [HttpPost("{merchantId:guid}/products")]
    public async Task<ActionResult<MerchantProductItem>> CreateProduct(
        Guid merchantId,
        [FromBody] SaveMerchantProductRequest request,
        CancellationToken cancellationToken)
    {
        var (op, merchant, fail) = await RequireMerchantAsync(merchantId, cancellationToken);
        if (fail is not null)
        {
            return fail;
        }

        var error = await ValidateProductAsync(merchant!.Id, request, cancellationToken);
        if (error is not null)
        {
            return BadRequest(new { message = error });
        }

        var row = new MerchantProduct
        {
            MerchantId = merchant.Id,
            CategoryId = request.CategoryId,
            Name = request.Name.Trim(),
            Description = (request.Description ?? string.Empty).Trim(),
            BasePrice = request.SellingPrice,
            AvailableOnStorefront = request.AvailableOnStorefront,
            AvailableFromTime = request.AvailableAllDay ? null : ParseTime(request.AvailableFromTime),
            AvailableToTime = request.AvailableAllDay ? null : ParseTime(request.AvailableToTime),
            SortOrder = request.SortOrder,
        };
        db.MerchantProducts.Add(row);
        await db.SaveChangesAsync(cancellationToken);
        var loaded = await LoadProductAsync(merchant.Id, row.Id, cancellationToken);
        return Ok(MapProduct(loaded!));
    }

    [HttpPut("{merchantId:guid}/products/{id:guid}")]
    public async Task<ActionResult<MerchantProductItem>> UpdateProduct(
        Guid merchantId,
        Guid id,
        [FromBody] SaveMerchantProductRequest request,
        CancellationToken cancellationToken)
    {
        var (op, merchant, fail) = await RequireMerchantAsync(merchantId, cancellationToken);
        if (fail is not null)
        {
            return fail;
        }

        var error = await ValidateProductAsync(merchant!.Id, request, cancellationToken);
        if (error is not null)
        {
            return BadRequest(new { message = error });
        }

        var row = await db.MerchantProducts
            .FirstOrDefaultAsync(x => x.Id == id && x.MerchantId == merchant.Id, cancellationToken);
        if (row is null)
        {
            return NotFound(new { message = "Product not found." });
        }

        row.CategoryId = request.CategoryId;
        row.Name = request.Name.Trim();
        row.Description = (request.Description ?? string.Empty).Trim();
        row.BasePrice = request.SellingPrice;
        row.AvailableOnStorefront = request.AvailableOnStorefront;
        row.AvailableFromTime = request.AvailableAllDay ? null : ParseTime(request.AvailableFromTime);
        row.AvailableToTime = request.AvailableAllDay ? null : ParseTime(request.AvailableToTime);
        row.SortOrder = request.SortOrder;
        row.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        var loaded = await LoadProductAsync(merchant.Id, row.Id, cancellationToken);
        return Ok(MapProduct(loaded!));
    }

    [HttpPost("{merchantId:guid}/products/{id:guid}/image")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<MerchantProductItem>> UploadProductImage(
        Guid merchantId,
        Guid id,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        var (op, merchant, fail) = await RequireMerchantAsync(merchantId, cancellationToken);
        if (fail is not null)
        {
            return fail;
        }

        var row = await db.MerchantProducts
            .FirstOrDefaultAsync(x => x.Id == id && x.MerchantId == merchant!.Id, cancellationToken);
        if (row is null)
        {
            return NotFound(new { message = "Product not found." });
        }

        try
        {
            var path = await uploads.SaveAsync(
                file,
                $"merchants/{merchant!.Id}/products",
                $"{row.Id:N}",
                cancellationToken);
            if (string.IsNullOrWhiteSpace(path))
            {
                return BadRequest(new { message = "Image file is required." });
            }

            row.ImagePath = path;
            row.UpdatedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            var loaded = await LoadProductAsync(merchant.Id, row.Id, cancellationToken);
            return Ok(MapProduct(loaded!));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{merchantId:guid}/products/{id:guid}/storefront")]
    public async Task<ActionResult<MerchantProductItem>> ToggleStorefront(
        Guid merchantId,
        Guid id,
        CancellationToken cancellationToken)
    {
        var (op, merchant, fail) = await RequireMerchantAsync(merchantId, cancellationToken);
        if (fail is not null)
        {
            return fail;
        }

        var row = await db.MerchantProducts
            .FirstOrDefaultAsync(x => x.Id == id && x.MerchantId == merchant!.Id, cancellationToken);
        if (row is null)
        {
            return NotFound(new { message = "Product not found." });
        }

        row.AvailableOnStorefront = !row.AvailableOnStorefront;
        row.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        var loaded = await LoadProductAsync(merchant!.Id, row.Id, cancellationToken);
        return Ok(MapProduct(loaded!));
    }

    [HttpDelete("{merchantId:guid}/products/{id:guid}")]
    public async Task<IActionResult> DeleteProduct(
        Guid merchantId,
        Guid id,
        CancellationToken cancellationToken)
    {
        var (op, merchant, fail) = await RequireMerchantAsync(merchantId, cancellationToken);
        if (fail is not null)
        {
            return fail;
        }

        var row = await db.MerchantProducts
            .Include(x => x.AdoptedAddons)
            .Include(x => x.AddonGroups)
                .ThenInclude(x => x.Options)
            .FirstOrDefaultAsync(x => x.Id == id && x.MerchantId == merchant!.Id, cancellationToken);
        if (row is null)
        {
            return NotFound(new { message = "Product not found." });
        }

        db.MerchantProducts.Remove(row);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPut("{merchantId:guid}/products/{id:guid}/addons")]
    public async Task<ActionResult<MerchantProductItem>> AdoptAddonGroups(
        Guid merchantId,
        Guid id,
        [FromBody] AdoptProductAddonGroupsRequest request,
        CancellationToken cancellationToken)
    {
        var (op, merchant, fail) = await RequireMerchantAsync(merchantId, cancellationToken);
        if (fail is not null)
        {
            return fail;
        }

        var row = await db.MerchantProducts
            .Include(x => x.AdoptedAddons)
            .FirstOrDefaultAsync(x => x.Id == id && x.MerchantId == merchant!.Id, cancellationToken);
        if (row is null)
        {
            return NotFound(new { message = "Product not found." });
        }

        var ids = (request.AddonGroupIds ?? []).Distinct().ToList();
        if (ids.Count > 0)
        {
            var validCount = await db.MerchantAddonGroups.CountAsync(
                x => x.MerchantId == merchant!.Id && ids.Contains(x.Id),
                cancellationToken);
            if (validCount != ids.Count)
            {
                return BadRequest(new { message = "One or more add-on groups are not in this merchant library." });
            }
        }

        db.MerchantProductAddons.RemoveRange(row.AdoptedAddons);
        row.AdoptedAddons.Clear();
        for (var i = 0; i < ids.Count; i++)
        {
            row.AdoptedAddons.Add(new MerchantProductAddon
            {
                ProductId = row.Id,
                AddonGroupId = ids[i],
                SortOrder = i,
            });
        }

        row.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        var loaded = await LoadProductAsync(merchant!.Id, row.Id, cancellationToken);
        return Ok(MapProduct(loaded!));
    }

    async Task<ActionResult<MerchantDetailItem>> UploadImageAsync(
        Guid id,
        IFormFile file,
        bool logo,
        CancellationToken cancellationToken)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return StatusCode(status, new { message });
        }

        var merchant = await db.Merchants
            .Include(x => x.OperatingHours)
            .FirstOrDefaultAsync(x => x.Id == id && x.OperatorId == op.Id, cancellationToken);
        if (merchant is null)
        {
            return NotFound(new { message = "Merchant not found." });
        }

        try
        {
            var path = await uploads.SaveAsync(
                file,
                $"merchants/{merchant.Id}",
                logo ? "logo" : "background",
                cancellationToken);
            if (string.IsNullOrWhiteSpace(path))
            {
                return BadRequest(new { message = "Image file is required." });
            }

            if (logo)
            {
                merchant.LogoPath = path;
            }
            else
            {
                merchant.BackgroundPath = path;
            }

            merchant.UpdatedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            return Ok(MapDetail(merchant));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    async Task<(Operator? Op, Merchant? Merchant, ActionResult? Fail)> RequireMerchantAsync(
        Guid merchantId,
        CancellationToken cancellationToken)
    {
        var (op, status, message) = await OperatorContext.RequireAsync(db, User, cancellationToken);
        if (op is null)
        {
            return (null, null, StatusCode(status, new { message }));
        }

        var merchant = await db.Merchants.FirstOrDefaultAsync(
            x => x.Id == merchantId && x.OperatorId == op.Id,
            cancellationToken);
        if (merchant is null)
        {
            return (op, null, NotFound(new { message = "Merchant not found." }));
        }

        return (op, merchant, null);
    }

    async Task<Merchant?> LoadMerchantAsync(Guid operatorId, Guid id, CancellationToken cancellationToken) =>
        await db.Merchants
            .AsNoTracking()
            .Include(x => x.OperatingHours)
            .FirstOrDefaultAsync(x => x.Id == id && x.OperatorId == operatorId, cancellationToken);

    async Task<MerchantProduct?> LoadProductAsync(Guid merchantId, Guid id, CancellationToken cancellationToken) =>
        await db.MerchantProducts
            .AsNoTracking()
            .Include(x => x.Category)
            .Include(x => x.AdoptedAddons)
                .ThenInclude(x => x.AddonGroup)
                    .ThenInclude(x => x.Options)
            .FirstOrDefaultAsync(x => x.Id == id && x.MerchantId == merchantId, cancellationToken);

    async Task<string?> EnsureMerchantAccountAsync(
        Merchant merchant,
        Guid operatorId,
        string mobile,
        string email,
        string? password,
        bool requirePassword,
        Guid? excludeUserId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(mobile))
        {
            return "Mobile is required when the merchant manages their own account.";
        }

        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
        {
            return "A valid email is required when the merchant manages their own account.";
        }

        if (requirePassword || !string.IsNullOrWhiteSpace(password))
        {
            if (!SecretHasher.IsStrongPassword(password ?? string.Empty))
            {
                return "Set a password of at least 6 characters.";
            }
        }

        var phoneTaken = await db.Users.AnyAsync(
            x => x.PhoneNumber == mobile && (excludeUserId == null || x.Id != excludeUserId),
            cancellationToken);
        if (phoneTaken)
        {
            return "That mobile number is already in use.";
        }

        var emailTaken = await db.Users.AnyAsync(
            x => x.Email != null
                && x.Email.ToLower() == email.ToLower()
                && (excludeUserId == null || x.Id != excludeUserId),
            cancellationToken);
        if (emailTaken)
        {
            return "That email is already in use.";
        }

        if (merchant.AppUser is null && merchant.AppUserId is Guid existingId)
        {
            merchant.AppUser = await db.Users.FirstOrDefaultAsync(x => x.Id == existingId, cancellationToken);
        }

        if (merchant.AppUser is null)
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                return "Password is required when enabling merchant login.";
            }

            var user = new AppUser
            {
                FullName = merchant.ContactPerson.Trim(),
                PhoneNumber = mobile,
                Email = email,
                PasswordHash = SecretHasher.Hash(password.Trim()),
                Role = UserRole.Merchant,
                OperatorId = operatorId,
                MerchantId = merchant.Id == Guid.Empty ? null : merchant.Id,
                IsActive = true,
            };
            db.Users.Add(user);
            merchant.AppUser = user;
            merchant.AppUserId = user.Id;
        }
        else
        {
            merchant.AppUser.FullName = merchant.ContactPerson.Trim();
            merchant.AppUser.PhoneNumber = mobile;
            merchant.AppUser.Email = email;
            merchant.AppUser.Role = UserRole.Merchant;
            merchant.AppUser.OperatorId = operatorId;
            merchant.AppUser.MerchantId = merchant.Id;
            merchant.AppUser.IsActive = true;
            merchant.AppUser.UpdatedAtUtc = DateTime.UtcNow;
            if (!string.IsNullOrWhiteSpace(password))
            {
                merchant.AppUser.PasswordHash = SecretHasher.Hash(password.Trim());
            }
        }

        return null;
    }

    async Task DetachMerchantAccountAsync(Merchant merchant, CancellationToken cancellationToken)
    {
        if (merchant.AppUser is null && merchant.AppUserId is Guid userId)
        {
            merchant.AppUser = await db.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
        }

        if (merchant.AppUser is null)
        {
            merchant.AppUserId = null;
            return;
        }

        merchant.AppUser.IsActive = false;
        merchant.AppUser.MerchantId = null;
        merchant.AppUser.UpdatedAtUtc = DateTime.UtcNow;
        merchant.AppUserId = null;
        merchant.AppUser = null;
    }

    static void ApplyHours(Merchant merchant, IReadOnlyList<MerchantOperatingHourItem>? hours)
    {
        var byDay = (hours ?? [])
            .GroupBy(x => x.DayOfWeek)
            .ToDictionary(x => x.Key, x => x.Last());

        for (var day = 0; day <= 6; day++)
        {
            byDay.TryGetValue(day, out var item);
            var existing = merchant.OperatingHours.FirstOrDefault(x => x.DayOfWeek == day);
            var isClosed = item?.IsClosed ?? true;
            TimeSpan? open = null;
            TimeSpan? close = null;
            if (!isClosed && item is not null)
            {
                open = ParseTime(item.OpenTime);
                close = ParseTime(item.CloseTime);
            }

            if (existing is null)
            {
                merchant.OperatingHours.Add(new MerchantOperatingHour
                {
                    DayOfWeek = day,
                    IsClosed = isClosed,
                    OpenTime = open,
                    CloseTime = close,
                });
            }
            else
            {
                existing.IsClosed = isClosed;
                existing.OpenTime = open;
                existing.CloseTime = close;
                existing.UpdatedAtUtc = DateTime.UtcNow;
            }
        }
    }

    static string? ValidateMerchant(SaveMerchantRequest request, bool requirePassword)
    {
        if (string.IsNullOrWhiteSpace(request.BusinessName))
        {
            return "Business name is required.";
        }

        if (string.IsNullOrWhiteSpace(request.ContactPerson))
        {
            return "Contact person is required.";
        }

        if (string.IsNullOrWhiteSpace(request.PinnedAddress))
        {
            return "Pinned address is required.";
        }

        if (request.Latitude is < -90 or > 90 || request.Longitude is < -180 or > 180)
        {
            return "Map pin coordinates are invalid.";
        }

        if (request.ManagedByMerchant)
        {
            if (string.IsNullOrWhiteSpace(request.Mobile))
            {
                return "Mobile is required when the merchant manages their own account.";
            }

            if (string.IsNullOrWhiteSpace(request.Email))
            {
                return "Email is required when the merchant manages their own account.";
            }

            if (requirePassword && string.IsNullOrWhiteSpace(request.Password))
            {
                return "Password is required when the merchant manages their own account.";
            }
        }
        else if (!string.IsNullOrWhiteSpace(request.Password))
        {
            return "Password is only used when manage by merchant is enabled.";
        }

        return null;
    }

    static string? ValidateHours(IReadOnlyList<MerchantOperatingHourItem>? hours)
    {
        if (hours is null)
        {
            return null;
        }

        foreach (var item in hours)
        {
            if (item.DayOfWeek is < 0 or > 6)
            {
                return "Day of week must be between 0 (Sunday) and 6 (Saturday).";
            }

            if (item.IsClosed)
            {
                continue;
            }

            var open = ParseTime(item.OpenTime);
            var close = ParseTime(item.CloseTime);
            if (open is null || close is null)
            {
                return "Open and close times are required for open days.";
            }

            if (open >= close)
            {
                return "Open time must be before close time.";
            }
        }

        return null;
    }

    async Task<string?> ValidateProductAsync(
        Guid merchantId,
        SaveMerchantProductRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return "Product name is required.";
        }

        if (request.SellingPrice < 0)
        {
            return "Selling price cannot be negative.";
        }

        if (!request.AvailableAllDay)
        {
            var from = ParseTime(request.AvailableFromTime);
            var to = ParseTime(request.AvailableToTime);
            if (from is null || to is null)
            {
                return "Available hours require both from and to times.";
            }

            if (from >= to)
            {
                return "Available-from time must be before available-to time.";
            }
        }

        if (request.CategoryId is Guid categoryId)
        {
            var exists = await db.MerchantProductCategories.AnyAsync(
                x => x.Id == categoryId && x.MerchantId == merchantId,
                cancellationToken);
            if (!exists)
            {
                return "Category not found for this merchant.";
            }
        }

        return null;
    }

    static string? ValidateAddons(IReadOnlyList<ProductAddonGroupItem>? groups)
    {
        foreach (var group in groups ?? [])
        {
            if (string.IsNullOrWhiteSpace(group.Name))
            {
                return "Addon group name is required.";
            }

            if (group.MinSelect < 0 || group.MaxSelect < 1 || group.MinSelect > group.MaxSelect)
            {
                return "Addon group min/max select is invalid.";
            }

            if (group.Options is null || group.Options.Count == 0)
            {
                return $"Addon group '{group.Name.Trim()}' needs at least one option.";
            }

            foreach (var option in group.Options)
            {
                if (string.IsNullOrWhiteSpace(option.Name))
                {
                    return "Addon option name is required.";
                }

                if (option.PriceDelta < 0)
                {
                    return "Addon option price cannot be negative.";
                }
            }
        }

        return null;
    }

    static string NormalizeOptionalPhone(string? mobile)
    {
        var raw = (mobile ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var normalized = PhoneNormalizer.Normalize(raw);
        return string.IsNullOrWhiteSpace(normalized) ? raw : normalized;
    }

    static TimeSpan? ParseTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return TimeSpan.TryParse(value.Trim(), out var span) ? span : null;
    }

    static string? FormatTime(TimeSpan? value) =>
        value is TimeSpan span ? span.ToString(@"hh\:mm") : null;

    static MerchantListItem MapList(Merchant row) =>
        new(
            row.Id,
            row.BusinessName,
            row.ContactPerson,
            row.PinnedAddress,
            row.ManagedByMerchant,
            row.Mobile,
            row.Email,
            UploadUrls.FromPath(row.LogoPath),
            UploadUrls.FromPath(row.BackgroundPath),
            row.IsActive,
            row.SortOrder,
            DateTime.SpecifyKind(row.CreatedAtUtc, DateTimeKind.Utc));

    static MerchantDetailItem MapDetail(Merchant row) =>
        new(
            row.Id,
            row.BusinessName,
            row.ContactPerson,
            row.Latitude,
            row.Longitude,
            row.PinnedAddress,
            row.ManagedByMerchant,
            row.Mobile,
            row.Email,
            UploadUrls.FromPath(row.LogoPath),
            UploadUrls.FromPath(row.BackgroundPath),
            row.IsActive,
            row.SortOrder,
            row.OperatingHours
                .OrderBy(x => x.DayOfWeek)
                .Select(x => new MerchantOperatingHourItem(
                    x.DayOfWeek,
                    x.IsClosed,
                    FormatTime(x.OpenTime),
                    FormatTime(x.CloseTime)))
                .ToList(),
            DateTime.SpecifyKind(row.CreatedAtUtc, DateTimeKind.Utc));

    static MerchantProductCategoryItem MapCategory(MerchantProductCategory row) =>
        new(row.Id, row.Name, row.SortOrder, row.IsActive);

    static ProductAddonGroupItem MapLibraryGroup(MerchantAddonGroup g) =>
        new(
            g.Id,
            g.Name,
            g.MinSelect,
            g.MaxSelect,
            g.SortOrder,
            g.IsActive,
            g.Options
                .OrderBy(o => o.SortOrder)
                .ThenBy(o => o.Name)
                .Select(o => new ProductAddonOptionItem(
                    o.Id,
                    o.Name,
                    o.PriceDelta,
                    o.SortOrder,
                    o.IsActive))
                .ToList());

    static MerchantProductItem MapProduct(MerchantProduct row)
    {
        var adopted = row.AdoptedAddons
            .OrderBy(x => x.SortOrder)
            .Where(x => x.AddonGroup is not null)
            .ToList();
        return new(
            row.Id,
            row.MerchantId,
            row.CategoryId,
            row.Category?.Name,
            row.Name,
            row.Description,
            row.BasePrice,
            row.AvailableOnStorefront,
            row.AvailableFromTime is null && row.AvailableToTime is null,
            FormatTime(row.AvailableFromTime),
            FormatTime(row.AvailableToTime),
            row.SortOrder,
            UploadUrls.FromPath(row.ImagePath),
            adopted.Select(x => x.AddonGroupId).ToList(),
            adopted.Select(x => MapLibraryGroup(x.AddonGroup)).ToList());
    }
}
