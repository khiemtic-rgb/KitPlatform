using System.Text.RegularExpressions;
using Dapper;
using PharmaCore.Application.Abstractions;
using PharmaCore.Application.Catalog;
using PharmaCore.Infrastructure.Data;

namespace PharmaCore.Infrastructure.Catalog;

internal sealed class CatalogRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;

    public CatalogRepository(IDbConnectionFactory db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<(IReadOnlyList<ProductListItemDto> Items, int Total)> GetProductsAsync(
        ProductListFilter filter, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);
        var offset = (page - 1) * pageSize;

        var extra = new List<string>();
        if (filter.DrugTypes is { Length: > 0 })
            extra.Add("p.drug_type = ANY(@DrugTypes::smallint[])");
        if (filter.CategoryIds is { Length: > 0 })
            extra.Add("p.category_id = ANY(@CategoryIds::uuid[])");
        if (filter.BrandIds is { Length: > 0 })
            extra.Add("p.brand_id = ANY(@BrandIds::uuid[])");
        if (filter.Status is not null)
            extra.Add("p.status = @Status");
        if (filter.HasBarcode == true)
            extra.Add("EXISTS (SELECT 1 FROM product_barcodes b WHERE b.product_id = p.id AND b.status = 1)");
        if (filter.HasBarcode == false)
            extra.Add("NOT EXISTS (SELECT 1 FROM product_barcodes b WHERE b.product_id = p.id AND b.status = 1)");
        if (filter.HasPrice == true)
            extra.Add("""EXISTS (SELECT 1 FROM product_prices pr WHERE pr.product_id = p.id AND pr.price_type = 1 AND pr.status = 1 AND pr.effective_from <= NOW() AND (pr.effective_to IS NULL OR pr.effective_to > NOW()))""");
        if (filter.HasPrice == false)
            extra.Add("""NOT EXISTS (SELECT 1 FROM product_prices pr WHERE pr.product_id = p.id AND pr.price_type = 1 AND pr.status = 1 AND pr.effective_from <= NOW() AND (pr.effective_to IS NULL OR pr.effective_to > NOW()))""");
        if (filter.PriceMin is not null)
            extra.Add("""EXISTS (SELECT 1 FROM product_prices pr WHERE pr.product_id = p.id AND pr.price_type = 1 AND pr.status = 1 AND pr.effective_from <= NOW() AND (pr.effective_to IS NULL OR pr.effective_to > NOW()) AND pr.price >= @PriceMin)""");
        if (filter.PriceMax is not null)
            extra.Add("""EXISTS (SELECT 1 FROM product_prices pr WHERE pr.product_id = p.id AND pr.price_type = 1 AND pr.status = 1 AND pr.effective_from <= NOW() AND (pr.effective_to IS NULL OR pr.effective_to > NOW()) AND pr.price <= @PriceMax)""");

        var extraWhere = extra.Count > 0 ? " AND " + string.Join(" AND ", extra) : "";

        var where = $"""
            WHERE p.tenant_id = @TenantId AND p.deleted_at IS NULL
              AND (@Search IS NULL OR p.product_name ILIKE @SearchPattern
                   OR EXISTS (SELECT 1 FROM product_barcodes b WHERE b.product_id = p.id AND b.status = 1 AND b.barcode ILIKE @SearchPattern))
              {extraWhere}
            """;

        var sql = $"""
            SELECT COUNT(*)::int FROM products p {where};
            SELECT
                p.id AS Id,
                p.product_code AS ProductCode,
                p.product_name AS ProductName,
                p.generic_name AS GenericName,
                p.drug_type AS DrugType,
                c.category_name AS CategoryName,
                br.brand_name AS BrandName,
                (SELECT b.barcode FROM product_barcodes b
                 WHERE b.product_id = p.id AND b.is_primary = TRUE AND b.status = 1
                 LIMIT 1) AS PrimaryBarcode,
                (SELECT pr.price FROM product_prices pr
                 WHERE pr.product_id = p.id AND pr.price_type = 1 AND pr.status = 1
                   AND pr.effective_from <= NOW() AND (pr.effective_to IS NULL OR pr.effective_to > NOW())
                 ORDER BY pr.effective_from DESC LIMIT 1) AS RetailPrice,
                (SELECT pi.image_url FROM product_images pi
                 WHERE pi.product_id = p.id AND pi.is_primary = TRUE AND pi.status = 1
                 ORDER BY pi.sort_order LIMIT 1) AS PrimaryImageUrl,
                (SELECT u.unit_name FROM product_units u
                 WHERE u.product_id = p.id AND u.is_sale_unit = TRUE
                 ORDER BY u.is_base_unit DESC, u.unit_name LIMIT 1) AS SaleUnitName,
                p.status AS Status
            FROM products p
            LEFT JOIN product_categories c ON c.id = p.category_id
            LEFT JOIN product_brands br ON br.id = p.brand_id
            {where}
            ORDER BY p.product_name
            LIMIT @PageSize OFFSET @Offset
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await conn.QueryMultipleAsync(sql, new
        {
            TenantId = _tenant.TenantId,
            Search = string.IsNullOrWhiteSpace(filter.Search) ? null : filter.Search,
            SearchPattern = string.IsNullOrWhiteSpace(filter.Search) ? null : $"%{filter.Search.Trim()}%",
            filter.DrugTypes,
            filter.CategoryIds,
            filter.BrandIds,
            filter.Status,
            filter.PriceMin,
            filter.PriceMax,
            PageSize = pageSize,
            Offset = offset,
        });

        var total = await multi.ReadSingleAsync<int>();
        var items = (await multi.ReadAsync<ProductListItemDto>()).ToList();
        return (items, total);
    }

    public async Task<Guid?> ProductExistsAsync(Guid id, CancellationToken cancellationToken)
    {
        const string sql = "SELECT id FROM products WHERE id = @Id AND tenant_id = @TenantId AND deleted_at IS NULL";
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<Guid?>(sql, new { Id = id, TenantId = _tenant.TenantId });
    }

    public async Task<ProductDetailDto?> GetProductAsync(Guid id, CancellationToken cancellationToken)
    {
        const string productSql = """
            SELECT id AS Id, product_code AS ProductCode, product_name AS ProductName,
                   generic_name AS GenericName, drug_type AS DrugType,
                   category_id AS CategoryId, brand_id AS BrandId, description AS Description,
                   national_drug_id AS NationalDrugId,
                   national_registration_number AS NationalRegistrationNumber,
                   status AS Status, min_stock_qty AS MinStockQty
            FROM products
            WHERE id = @Id AND tenant_id = @TenantId AND deleted_at IS NULL
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var product = await conn.QuerySingleOrDefaultAsync<ProductDetailRow>(productSql, new { Id = id, TenantId = _tenant.TenantId });
        if (product is null) return null;

        const string unitsSql = """
            SELECT id AS Id, unit_name AS UnitName, conversion_factor AS ConversionFactor,
                   is_base_unit AS IsBaseUnit, is_sale_unit AS IsSaleUnit
            FROM product_units WHERE product_id = @ProductId AND status = 1
            ORDER BY is_base_unit DESC, unit_name
            """;
        const string barcodesSql = """
            SELECT id AS Id, barcode AS Barcode, barcode_type AS BarcodeType, is_primary AS IsPrimary
            FROM product_barcodes WHERE product_id = @ProductId AND status = 1 ORDER BY is_primary DESC
            """;
        const string pricesSql = """
            SELECT pr.id AS Id, pr.product_unit_id AS ProductUnitId, u.unit_name AS UnitName,
                   pr.price_type AS PriceType, pr.currency_code AS CurrencyCode, pr.price AS Price,
                   pr.effective_from AS EffectiveFrom, pr.effective_to AS EffectiveTo
            FROM product_prices pr
            INNER JOIN product_units u ON u.id = pr.product_unit_id
            WHERE pr.product_id = @ProductId AND pr.status = 1
            ORDER BY pr.price_type, pr.effective_from DESC
            """;
        const string imagesSql = """
            SELECT id AS Id, image_url AS ImageUrl, sort_order AS SortOrder, is_primary AS IsPrimary
            FROM product_images
            WHERE product_id = @ProductId AND status = 1
            ORDER BY is_primary DESC, sort_order, created_at
            """;
        const string ingredientsSql = """
            SELECT pi.id AS Id, pi.ingredient_id AS IngredientId,
                   ai.ingredient_code AS IngredientCode, ai.ingredient_name AS IngredientName,
                   pi.strength_value AS StrengthValue, pi.strength_unit AS StrengthUnit
            FROM product_ingredients pi
            INNER JOIN active_ingredients ai ON ai.id = pi.ingredient_id
            WHERE pi.product_id = @ProductId AND pi.tenant_id = @TenantId
            ORDER BY ai.ingredient_name
            """;

        var units = (await conn.QueryAsync<ProductUnitDto>(unitsSql, new { ProductId = id })).ToList();
        var barcodes = (await conn.QueryAsync<ProductBarcodeDto>(barcodesSql, new { ProductId = id })).ToList();
        var prices = (await conn.QueryAsync<ProductPriceDto>(pricesSql, new { ProductId = id })).ToList();
        var images = (await conn.QueryAsync<ProductImageDto>(imagesSql, new { ProductId = id })).ToList();
        var ingredients = (await conn.QueryAsync<ProductIngredientDto>(
            ingredientsSql,
            new { ProductId = id, TenantId = _tenant.TenantId })).ToList();
        var saleUnitName = units.FirstOrDefault(u => u.IsSaleUnit)?.UnitName
            ?? units.FirstOrDefault(u => u.IsBaseUnit)?.UnitName;

        return new ProductDetailDto(
            product.Id, product.ProductCode, product.ProductName, product.GenericName, product.DrugType,
            product.CategoryId, product.BrandId, product.Description,
            product.NationalDrugId, product.NationalRegistrationNumber,
            product.Status, product.MinStockQty,
            saleUnitName,
            units, barcodes, prices, images, ingredients);
    }

    public async Task<string> GenerateNextProductCodeAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COALESCE(MAX(CAST(SUBSTRING(product_code FROM 4) AS BIGINT)), 0) + 1
            FROM products
            WHERE tenant_id = @TenantId
              AND deleted_at IS NULL
              AND product_code ~* '^SP-[0-9]+$'
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var next = await conn.QuerySingleAsync<long>(sql, new { TenantId = _tenant.TenantId });
        if (next < 1)
            next = 1;
        return $"SP-{next:D6}";
    }

    public async Task<Guid> CreateProductAsync(CreateProductRequest request, CancellationToken cancellationToken)
    {
        var saleUnitName = string.IsNullOrWhiteSpace(request.SaleUnitName) ? "Viên" : request.SaleUnitName.Trim();
        var productCode = string.IsNullOrWhiteSpace(request.ProductCode)
            ? await GenerateNextProductCodeAsync(cancellationToken)
            : request.ProductCode.Trim();
        var bumpOnConflict = string.IsNullOrWhiteSpace(request.ProductCode) || IsAutoProductCode(productCode);

        const int maxAttempts = 5;
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            try
            {
                return await InsertProductAsync(request, productCode, saleUnitName, cancellationToken);
            }
            catch (Exception ex) when (bumpOnConflict && IsUniqueProductCodeViolation(ex))
            {
                productCode = await GenerateNextProductCodeAsync(cancellationToken);
            }
        }

        throw new InvalidOperationException("Không tạo được mã sản phẩm duy nhất. Vui lòng thử lại.");
    }

    private async Task<Guid> InsertProductAsync(
        CreateProductRequest request,
        string productCode,
        string saleUnitName,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO products (
                tenant_id, category_id, brand_id, product_code, product_name, product_name_normalized,
                generic_name, drug_type, description, national_drug_id, national_registration_number,
                min_stock_qty, status)
            VALUES (
                @TenantId, @CategoryId, @BrandId, @ProductCode, @ProductName, @ProductNameNormalized,
                @GenericName, @DrugType, @Description, @NationalDrugId, @NationalRegistrationNumber,
                @MinStockQty, @Status)
            RETURNING id
            """;
        const string unitSql = """
            INSERT INTO product_units (tenant_id, product_id, unit_name, conversion_factor, is_base_unit, is_sale_unit)
            VALUES (@TenantId, @ProductId, @UnitName, 1, TRUE, TRUE)
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var id = await conn.QuerySingleAsync<Guid>(sql, new
        {
            TenantId = _tenant.TenantId,
            request.CategoryId,
            request.BrandId,
            ProductCode = productCode,
            ProductName = request.ProductName.Trim(),
            ProductNameNormalized = ProductNameNormalizer.Normalize(request.ProductName),
            GenericName = request.GenericName?.Trim(),
            request.DrugType,
            request.Description,
            NationalDrugId = string.IsNullOrWhiteSpace(request.NationalDrugId) ? null : request.NationalDrugId.Trim(),
            NationalRegistrationNumber = string.IsNullOrWhiteSpace(request.NationalRegistrationNumber)
                ? null
                : request.NationalRegistrationNumber.Trim(),
            request.MinStockQty,
            request.Status,
        });
        await conn.ExecuteAsync(unitSql, new { TenantId = _tenant.TenantId, ProductId = id, UnitName = saleUnitName });
        return id;
    }

    private static bool IsAutoProductCode(string code) =>
        Regex.IsMatch(code, @"^SP-\d+$", RegexOptions.IgnoreCase);

    private static bool IsUniqueProductCodeViolation(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            var type = current.GetType();
            if (type.Name is "PostgresException" && type.GetProperty("SqlState")?.GetValue(current) is "23505")
                return true;
        }

        return false;
    }

    public async Task UpdateSaleUnitNameAsync(Guid productId, string saleUnitName, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE product_units SET unit_name = @UnitName, updated_at = NOW()
            WHERE product_id = @ProductId AND tenant_id = @TenantId AND is_base_unit = TRUE
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(sql, new
        {
            ProductId = productId,
            TenantId = _tenant.TenantId,
            UnitName = saleUnitName.Trim(),
        });
    }

    public async Task SyncProductBarcodesAsync(
        Guid productId,
        string? primaryBarcode,
        IReadOnlyList<ProductBarcodeItem>? extraBarcodes,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(primaryBarcode))
        {
            await conn.ExecuteAsync(
                """
                UPDATE product_barcodes SET is_primary = FALSE, updated_at = NOW()
                WHERE product_id = @ProductId AND tenant_id = @TenantId AND is_primary = TRUE AND status = 1
                """,
                new { ProductId = productId, TenantId = _tenant.TenantId });
        }
        else
        {
            var trimmedPrimary = primaryBarcode.Trim();
            var existingPrimary = await conn.QuerySingleOrDefaultAsync<PrimaryBarcodeRow>(
                """
                SELECT id AS Id, barcode AS Barcode
                FROM product_barcodes
                WHERE product_id = @ProductId AND tenant_id = @TenantId AND is_primary = TRUE AND status = 1
                LIMIT 1
                """,
                new { ProductId = productId, TenantId = _tenant.TenantId });

            if (existingPrimary is null)
            {
                await conn.ExecuteAsync(
                    """
                    INSERT INTO product_barcodes (tenant_id, product_id, barcode, barcode_type, is_primary)
                    VALUES (@TenantId, @ProductId, @Barcode, 1, TRUE)
                    """,
                    new { TenantId = _tenant.TenantId, ProductId = productId, Barcode = trimmedPrimary });
            }
            else if (!string.Equals(existingPrimary.Barcode, trimmedPrimary, StringComparison.Ordinal))
            {
                await conn.ExecuteAsync(
                    "UPDATE product_barcodes SET barcode = @Barcode, updated_at = NOW() WHERE id = @Id",
                    new { Id = existingPrimary.Id, Barcode = trimmedPrimary });
            }
        }

        var primary = primaryBarcode?.Trim();
        var desired = (extraBarcodes ?? [])
            .Select(b => b.Barcode.Trim())
            .Where(b => !string.IsNullOrWhiteSpace(b))
            .Where(b => !string.Equals(b, primary, StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        await conn.ExecuteAsync(
            """
            UPDATE product_barcodes SET status = 2, updated_at = NOW()
            WHERE product_id = @ProductId AND tenant_id = @TenantId AND is_primary = FALSE AND status = 1
            """,
            new { ProductId = productId, TenantId = _tenant.TenantId });

        foreach (var barcode in desired)
        {
            var reactivated = await conn.ExecuteAsync(
                """
                UPDATE product_barcodes SET status = 1, barcode_type = 1, updated_at = NOW()
                WHERE product_id = @ProductId AND tenant_id = @TenantId AND barcode = @Barcode
                """,
                new { ProductId = productId, TenantId = _tenant.TenantId, Barcode = barcode });
            if (reactivated == 0)
            {
                await conn.ExecuteAsync(
                    """
                    INSERT INTO product_barcodes (tenant_id, product_id, barcode, barcode_type, is_primary)
                    VALUES (@TenantId, @ProductId, @Barcode, 1, FALSE)
                    """,
                    new { TenantId = _tenant.TenantId, ProductId = productId, Barcode = barcode });
            }
        }
    }

    public async Task SyncProductPricesAsync(
        Guid productId,
        Guid defaultUnitId,
        Guid? retailUnitId,
        decimal? retailPrice,
        IReadOnlyList<ProductPriceItem>? extraPrices,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);

        var retailProductUnitId = retailUnitId ?? defaultUnitId;
        var desired = new Dictionary<(Guid UnitId, short PriceType), decimal>();

        if (retailPrice is > 0)
            desired[(retailProductUnitId, 1)] = retailPrice.Value;

        foreach (var item in extraPrices ?? [])
        {
            if (item.Price <= 0 || item.PriceType == 1)
                continue;

            var unitId = item.ProductUnitId ?? defaultUnitId;
            desired[(unitId, item.PriceType)] = item.Price;
        }

        await conn.ExecuteAsync(
            """
            UPDATE product_prices SET status = 2, updated_at = NOW()
            WHERE product_id = @ProductId AND tenant_id = @TenantId AND status = 1
            """,
            new { ProductId = productId, TenantId = _tenant.TenantId });

        foreach (var ((unitId, priceType), price) in desired)
        {
            await conn.ExecuteAsync(
                """
                INSERT INTO product_prices (tenant_id, product_id, product_unit_id, price_type, currency_code, price)
                VALUES (@TenantId, @ProductId, @UnitId, @PriceType, 'VND', @Price)
                """,
                new
                {
                    TenantId = _tenant.TenantId,
                    ProductId = productId,
                    UnitId = unitId,
                    PriceType = priceType,
                    Price = price,
                });
        }
    }

    public async Task SyncProductBarcodesListAsync(
        Guid productId,
        IReadOnlyList<ProductBarcodeSyncItem>? barcodes,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);

        var desired = (barcodes ?? [])
            .Where(b => !string.IsNullOrWhiteSpace(b.Barcode))
            .Select(b => new { Barcode = b.Barcode.Trim(), b.IsPrimary, BarcodeType = b.BarcodeType <= 0 ? (short)1 : b.BarcodeType })
            .ToList();

        await conn.ExecuteAsync(
            """
            UPDATE product_barcodes SET status = 2, updated_at = NOW()
            WHERE product_id = @ProductId AND tenant_id = @TenantId
            """,
            new { ProductId = productId, TenantId = _tenant.TenantId });

        foreach (var item in desired)
        {
            var updated = await conn.ExecuteAsync(
                """
                UPDATE product_barcodes
                SET status = 1, is_primary = @IsPrimary, barcode_type = @BarcodeType, updated_at = NOW()
                WHERE product_id = @ProductId AND tenant_id = @TenantId AND barcode = @Barcode
                """,
                new
                {
                    ProductId = productId,
                    TenantId = _tenant.TenantId,
                    item.Barcode,
                    item.IsPrimary,
                    item.BarcodeType,
                });
            if (updated == 0)
            {
                await conn.ExecuteAsync(
                    """
                    INSERT INTO product_barcodes (tenant_id, product_id, barcode, barcode_type, is_primary)
                    VALUES (@TenantId, @ProductId, @Barcode, @BarcodeType, @IsPrimary)
                    """,
                    new
                    {
                        TenantId = _tenant.TenantId,
                        ProductId = productId,
                        item.Barcode,
                        item.BarcodeType,
                        item.IsPrimary,
                    });
            }
        }

        if (desired.Count == 0)
            return;

        var primaryBarcode = desired.FirstOrDefault(d => d.IsPrimary)?.Barcode ?? desired[0].Barcode;
        await conn.ExecuteAsync(
            """
            UPDATE product_barcodes SET is_primary = (barcode = @PrimaryBarcode)
            WHERE product_id = @ProductId AND tenant_id = @TenantId AND status = 1
            """,
            new { ProductId = productId, TenantId = _tenant.TenantId, PrimaryBarcode = primaryBarcode });
    }

    public async Task SyncProductPricesListAsync(
        Guid productId,
        Guid defaultUnitId,
        IReadOnlyList<ProductPriceItem>? prices,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);

        var desired = (prices ?? [])
            .Where(p => p.Price > 0)
            .Select(p => new
            {
                UnitId = p.ProductUnitId ?? defaultUnitId,
                p.PriceType,
                p.Price,
            })
            .GroupBy(p => (p.UnitId, p.PriceType))
            .Select(g => g.Last())
            .ToList();

        await conn.ExecuteAsync(
            """
            UPDATE product_prices SET status = 2, updated_at = NOW()
            WHERE product_id = @ProductId AND tenant_id = @TenantId AND status = 1
            """,
            new { ProductId = productId, TenantId = _tenant.TenantId });

        foreach (var item in desired)
        {
            await conn.ExecuteAsync(
                """
                INSERT INTO product_prices (tenant_id, product_id, product_unit_id, price_type, currency_code, price)
                VALUES (@TenantId, @ProductId, @UnitId, @PriceType, 'VND', @Price)
                """,
                new
                {
                    TenantId = _tenant.TenantId,
                    ProductId = productId,
                    UnitId = item.UnitId,
                    PriceType = item.PriceType,
                    Price = item.Price,
                });
        }
    }

    public async Task SyncProductImagesAsync(
        Guid productId,
        IReadOnlyList<ProductImageItem>? images,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);

        var normalized = (images ?? [])
            .Where(i => !string.IsNullOrWhiteSpace(i.ImageUrl))
            .Select((img, index) => new { ImageUrl = img.ImageUrl.Trim(), img.IsPrimary, SortOrder = img.SortOrder >= 0 ? img.SortOrder : index })
            .ToList();

        if (normalized.Count == 0)
        {
            await conn.ExecuteAsync(
                "UPDATE product_images SET status = 2, updated_at = NOW() WHERE product_id = @ProductId AND tenant_id = @TenantId",
                new { ProductId = productId, TenantId = _tenant.TenantId });
            return;
        }

        var hasPrimary = normalized.Any(i => i.IsPrimary);
        if (!hasPrimary)
        {
            var first = normalized[0];
            normalized[0] = new { first.ImageUrl, IsPrimary = true, first.SortOrder };
        }

        await conn.ExecuteAsync(
            "UPDATE product_images SET status = 2, is_primary = FALSE, updated_at = NOW() WHERE product_id = @ProductId AND tenant_id = @TenantId",
            new { ProductId = productId, TenantId = _tenant.TenantId });

        foreach (var img in normalized)
        {
            var updated = await conn.ExecuteAsync(
                """
                UPDATE product_images
                SET image_url = @ImageUrl, sort_order = @SortOrder, is_primary = @IsPrimary, status = 1, updated_at = NOW()
                WHERE product_id = @ProductId AND tenant_id = @TenantId AND image_url = @ImageUrl
                """,
                new
                {
                    ProductId = productId,
                    TenantId = _tenant.TenantId,
                    img.ImageUrl,
                    img.SortOrder,
                    img.IsPrimary,
                });
            if (updated == 0)
            {
                await conn.ExecuteAsync(
                    """
                    INSERT INTO product_images (tenant_id, product_id, image_url, sort_order, is_primary, status)
                    VALUES (@TenantId, @ProductId, @ImageUrl, @SortOrder, @IsPrimary, 1)
                    """,
                    new
                    {
                        TenantId = _tenant.TenantId,
                        ProductId = productId,
                        img.ImageUrl,
                        img.SortOrder,
                        img.IsPrimary,
                    });
            }
        }

        var primaryUrl = normalized.FirstOrDefault(i => i.IsPrimary)?.ImageUrl ?? normalized[0].ImageUrl;
        await conn.ExecuteAsync(
            "UPDATE product_images SET is_primary = (image_url = @PrimaryUrl) WHERE product_id = @ProductId AND tenant_id = @TenantId AND status = 1",
            new { ProductId = productId, TenantId = _tenant.TenantId, PrimaryUrl = primaryUrl });
    }

    public async Task<bool> UpdateProductAsync(Guid id, UpdateProductRequest request, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE products SET
                category_id = @CategoryId, brand_id = @BrandId,
                product_name = @ProductName, product_name_normalized = @ProductNameNormalized,
                generic_name = @GenericName,
                drug_type = @DrugType, description = @Description,
                national_drug_id = @NationalDrugId,
                national_registration_number = @NationalRegistrationNumber,
                min_stock_qty = @MinStockQty,
                status = @Status,
                updated_at = NOW()
            WHERE id = @Id AND tenant_id = @TenantId AND deleted_at IS NULL
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.ExecuteAsync(sql, new
        {
            Id = id,
            TenantId = _tenant.TenantId,
            request.CategoryId,
            request.BrandId,
            ProductName = request.ProductName.Trim(),
            ProductNameNormalized = ProductNameNormalizer.Normalize(request.ProductName),
            GenericName = request.GenericName?.Trim(),
            request.DrugType,
            request.Description,
            NationalDrugId = string.IsNullOrWhiteSpace(request.NationalDrugId) ? null : request.NationalDrugId.Trim(),
            NationalRegistrationNumber = string.IsNullOrWhiteSpace(request.NationalRegistrationNumber)
                ? null
                : request.NationalRegistrationNumber.Trim(),
            request.MinStockQty,
            request.Status,
        });
        return rows > 0;
    }

    public async Task<bool> ProductCodeExistsAsync(string productCode, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT EXISTS(
                SELECT 1 FROM products
                WHERE tenant_id = @TenantId AND deleted_at IS NULL
                  AND UPPER(product_code) = UPPER(@ProductCode)
            )
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleAsync<bool>(sql, new { TenantId = _tenant.TenantId, ProductCode = productCode.Trim() });
    }

    public async Task<bool> BarcodeTakenAsync(string barcode, Guid? excludeProductId, CancellationToken cancellationToken)
    {
        var result = await CheckBarcodeAsync(barcode, excludeProductId, cancellationToken);
        return !result.IsAvailable;
    }

    public async Task<Dictionary<string, Guid>> GetCategoryCodeMapAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id AS Id, UPPER(category_code) AS Code
            FROM product_categories
            WHERE tenant_id = @TenantId AND deleted_at IS NULL
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<(Guid Id, string Code)>(sql, new { TenantId = _tenant.TenantId });
        return rows.ToDictionary(r => r.Code, r => r.Id, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<Dictionary<string, Guid>> GetBrandCodeMapAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id AS Id, UPPER(brand_code) AS Code
            FROM product_brands
            WHERE tenant_id = @TenantId AND deleted_at IS NULL
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<(Guid Id, string Code)>(sql, new { TenantId = _tenant.TenantId });
        return rows.ToDictionary(r => r.Code, r => r.Id, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<Guid?> ResolveProductIdByKeyAsync(string key, CancellationToken cancellationToken)
    {
        var trimmed = key.Trim();
        if (trimmed.Length == 0)
            return null;

        const string sql = """
            SELECT p.id
            FROM products p
            LEFT JOIN product_barcodes b
              ON b.product_id = p.id AND b.tenant_id = p.tenant_id AND b.status = 1
            WHERE p.tenant_id = @TenantId AND p.deleted_at IS NULL
              AND (UPPER(p.product_code) = UPPER(@Key) OR b.barcode = @Key)
            LIMIT 1
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<Guid?>(sql, new { TenantId = _tenant.TenantId, Key = trimmed });
    }

    public async Task<bool> SoftDeleteProductAsync(Guid id, CancellationToken cancellationToken)
    {
        const string sql = "UPDATE products SET deleted_at = NOW(), status = 2 WHERE id = @Id AND tenant_id = @TenantId AND deleted_at IS NULL";
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteAsync(sql, new { Id = id, TenantId = _tenant.TenantId }) > 0;
    }

    public async Task<int> BulkSoftDeleteProductsAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken)
    {
        if (ids.Count == 0) return 0;

        const string sql = """
            UPDATE products SET deleted_at = NOW(), status = 2
            WHERE id = ANY(@Ids) AND tenant_id = @TenantId AND deleted_at IS NULL
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteAsync(sql, new { Ids = ids.ToArray(), TenantId = _tenant.TenantId });
    }

    public async Task<ProductBarcodeDto?> AddBarcodeAsync(Guid productId, CreateBarcodeRequest request, CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        if (request.IsPrimary)
        {
            await conn.ExecuteAsync(
                "UPDATE product_barcodes SET is_primary = FALSE WHERE product_id = @ProductId AND tenant_id = @TenantId",
                new { ProductId = productId, TenantId = _tenant.TenantId });
        }

        const string sql = """
            INSERT INTO product_barcodes (tenant_id, product_id, barcode, barcode_type, is_primary)
            VALUES (@TenantId, @ProductId, @Barcode, @BarcodeType, @IsPrimary)
            RETURNING id AS Id, barcode AS Barcode, barcode_type AS BarcodeType, is_primary AS IsPrimary
            """;
        return await conn.QuerySingleOrDefaultAsync<ProductBarcodeDto>(sql, new
        {
            TenantId = _tenant.TenantId,
            ProductId = productId,
            Barcode = request.Barcode.Trim(),
            request.BarcodeType,
            request.IsPrimary,
        });
    }

    public async Task<ProductPriceDto?> AddPriceAsync(Guid productId, CreatePriceRequest request, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO product_prices (tenant_id, product_id, product_unit_id, price_type, currency_code, price)
            VALUES (@TenantId, @ProductId, @ProductUnitId, @PriceType, @CurrencyCode, @Price)
            RETURNING id
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var priceId = await conn.QuerySingleOrDefaultAsync<Guid?>(sql, new
        {
            TenantId = _tenant.TenantId,
            ProductId = productId,
            request.ProductUnitId,
            request.PriceType,
            request.CurrencyCode,
            request.Price,
        });
        if (priceId is null) return null;

        const string getSql = """
            SELECT pr.id AS Id, pr.product_unit_id AS ProductUnitId, u.unit_name AS UnitName,
                   pr.price_type AS PriceType, pr.currency_code AS CurrencyCode, pr.price AS Price,
                   pr.effective_from AS EffectiveFrom, pr.effective_to AS EffectiveTo
            FROM product_prices pr
            INNER JOIN product_units u ON u.id = pr.product_unit_id
            WHERE pr.id = @Id
            """;
        return await conn.QuerySingleAsync<ProductPriceDto>(getSql, new { Id = priceId });
    }

    public async Task<Guid?> GetBaseUnitIdAsync(Guid productId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id FROM product_units
            WHERE product_id = @ProductId AND is_base_unit = TRUE
            LIMIT 1
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<Guid?>(sql, new { ProductId = productId });
    }

    public async Task<ProductImageDto?> UpsertPrimaryImageAsync(Guid productId, string imageUrl, CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);

        var existingId = await conn.QuerySingleOrDefaultAsync<Guid?>(
            """
            SELECT id FROM product_images
            WHERE product_id = @ProductId AND tenant_id = @TenantId AND status = 1
            ORDER BY is_primary DESC, sort_order, created_at
            LIMIT 1
            """,
            new { ProductId = productId, TenantId = _tenant.TenantId });

        await conn.ExecuteAsync(
            "UPDATE product_images SET is_primary = FALSE WHERE product_id = @ProductId AND tenant_id = @TenantId",
            new { ProductId = productId, TenantId = _tenant.TenantId });

        if (existingId is not null)
        {
            const string updateSql = """
                UPDATE product_images SET image_url = @ImageUrl, is_primary = TRUE, status = 1, updated_at = NOW()
                WHERE id = @Id
                RETURNING id AS Id, image_url AS ImageUrl, sort_order AS SortOrder, is_primary AS IsPrimary
                """;
            return await conn.QuerySingleAsync<ProductImageDto>(updateSql, new
            {
                Id = existingId.Value,
                ImageUrl = imageUrl.Trim(),
            });
        }

        const string insertSql = """
            INSERT INTO product_images (tenant_id, product_id, image_url, sort_order, is_primary)
            VALUES (@TenantId, @ProductId, @ImageUrl, 0, TRUE)
            RETURNING id AS Id, image_url AS ImageUrl, sort_order AS SortOrder, is_primary AS IsPrimary
            """;
        return await conn.QuerySingleAsync<ProductImageDto>(insertSql, new
        {
            TenantId = _tenant.TenantId,
            ProductId = productId,
            ImageUrl = imageUrl.Trim(),
        });
    }

    public async Task UpsertPrimaryBarcodeAsync(Guid productId, string barcode, CancellationToken cancellationToken)
    {
        var trimmed = barcode.Trim();
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);

        var existing = await conn.QuerySingleOrDefaultAsync<PrimaryBarcodeRow>(
            """
            SELECT id AS Id, barcode AS Barcode
            FROM product_barcodes
            WHERE product_id = @ProductId AND tenant_id = @TenantId AND is_primary = TRUE AND status = 1
            LIMIT 1
            """,
            new { ProductId = productId, TenantId = _tenant.TenantId });

        if (existing is null)
        {
            await conn.ExecuteAsync(
                """
                INSERT INTO product_barcodes (tenant_id, product_id, barcode, barcode_type, is_primary)
                VALUES (@TenantId, @ProductId, @Barcode, 1, TRUE)
                """,
                new { TenantId = _tenant.TenantId, ProductId = productId, Barcode = trimmed });
            return;
        }

        if (string.Equals(existing.Barcode, trimmed, StringComparison.Ordinal))
            return;

        await conn.ExecuteAsync(
            "UPDATE product_barcodes SET barcode = @Barcode, updated_at = NOW() WHERE id = @Id",
            new { Id = existing.Id, Barcode = trimmed });
    }

    public async Task<bool> HasPrimaryBarcodeAsync(Guid productId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT EXISTS(
                SELECT 1 FROM product_barcodes
                WHERE product_id = @ProductId AND tenant_id = @TenantId AND is_primary = TRUE AND status = 1
            )
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleAsync<bool>(sql, new { ProductId = productId, TenantId = _tenant.TenantId });
    }

    public async Task UpsertRetailPriceAsync(Guid productId, Guid unitId, decimal price, CancellationToken cancellationToken)
    {
        const string deactivateSql = """
            UPDATE product_prices SET status = 2, updated_at = NOW()
            WHERE product_id = @ProductId AND product_unit_id = @UnitId AND price_type = 1 AND status = 1
              AND effective_from <= NOW() AND (effective_to IS NULL OR effective_to > NOW())
            """;
        const string insertSql = """
            INSERT INTO product_prices (tenant_id, product_id, product_unit_id, price_type, currency_code, price)
            VALUES (@TenantId, @ProductId, @UnitId, 1, 'VND', @Price)
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(deactivateSql, new { ProductId = productId, UnitId = unitId });
        await conn.ExecuteAsync(insertSql, new
        {
            TenantId = _tenant.TenantId,
            ProductId = productId,
            UnitId = unitId,
            Price = price,
        });
    }

    public async Task<SimilarProductNamesResult> FindSimilarProductNamesAsync(
        string productName,
        Guid? excludeProductId,
        double similarityThreshold,
        CancellationToken cancellationToken)
    {
        var rawName = productName.Trim();
        var normalized = ProductNameNormalizer.Normalize(productName);
        if (string.IsNullOrEmpty(normalized))
            return new SimilarProductNamesResult([], false);

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);

        var exactMatches = await FindExactNameMatchesAsync(conn, rawName, normalized, excludeProductId);
        var fuzzyMatches = await FindFuzzyNameMatchesAsync(conn, normalized, excludeProductId, similarityThreshold);

        var matches = exactMatches
            .Concat(fuzzyMatches.Where(f => exactMatches.All(e => e.Id != f.Id)))
            .Take(5)
            .ToList();

        var exact = exactMatches.Count > 0
            || await HasExactNormalizedNameAsync(conn, normalized, excludeProductId);

        return new SimilarProductNamesResult(matches, exact);
    }

    private async Task<List<SimilarProductNameDto>> FindExactNameMatchesAsync(
        System.Data.IDbConnection conn,
        string rawName,
        string normalized,
        Guid? excludeProductId)
    {
        const string sql = """
            SELECT id AS Id, product_code AS ProductCode, product_name AS ProductName
            FROM products
            WHERE tenant_id = @TenantId AND deleted_at IS NULL
              AND (
                lower(trim(product_name)) = lower(trim(@RawName))
                OR (COALESCE(product_name_normalized, '') <> '' AND product_name_normalized = @Normalized)
              )
              AND (@ExcludeId IS NULL OR id <> @ExcludeId)
            LIMIT 5
            """;
        var rows = (await conn.QueryAsync<SimilarProductRow>(sql, new
        {
            TenantId = _tenant.TenantId,
            RawName = rawName,
            Normalized = normalized,
            ExcludeId = excludeProductId,
        })).ToList();

        return rows
            .Select(r => new SimilarProductNameDto(r.Id, r.ProductCode, r.ProductName, 1.0))
            .ToList();
    }

    private async Task<List<SimilarProductNameDto>> FindFuzzyNameMatchesAsync(
        System.Data.IDbConnection conn,
        string normalized,
        Guid? excludeProductId,
        double similarityThreshold)
    {
        try
        {
            const string sql = """
                SELECT id AS Id, product_code AS ProductCode, product_name AS ProductName,
                       similarity(COALESCE(product_name_normalized, ''), @Normalized) AS SimilarityScore
                FROM products
                WHERE tenant_id = @TenantId AND deleted_at IS NULL
                  AND COALESCE(product_name_normalized, '') <> ''
                  AND similarity(COALESCE(product_name_normalized, ''), @Normalized) >= @Threshold
                  AND (@ExcludeId IS NULL OR id <> @ExcludeId)
                ORDER BY SimilarityScore DESC
                LIMIT 5
                """;
            var rows = (await conn.QueryAsync<SimilarProductRow>(sql, new
            {
                TenantId = _tenant.TenantId,
                Normalized = normalized,
                Threshold = similarityThreshold,
                ExcludeId = excludeProductId,
            })).ToList();

            return rows
                .Select(r => new SimilarProductNameDto(r.Id, r.ProductCode, r.ProductName, r.SimilarityScore))
                .ToList();
        }
        catch
        {
            var fallback = await FindSimilarProductNamesFallbackAsync(conn, normalized, excludeProductId, similarityThreshold);
            return fallback.Matches.ToList();
        }
    }

    private async Task<SimilarProductNamesResult> FindSimilarProductNamesFallbackAsync(
        System.Data.IDbConnection conn,
        string normalized,
        Guid? excludeProductId,
        double similarityThreshold)
    {
        const string sql = """
            SELECT id AS Id, product_code AS ProductCode, product_name AS ProductName,
                   COALESCE(product_name_normalized, '') AS ProductNameNormalized
            FROM products
            WHERE tenant_id = @TenantId AND deleted_at IS NULL
            """;
        var rows = (await conn.QueryAsync<SimilarProductCandidateRow>(sql, new { TenantId = _tenant.TenantId })).ToList();
        var matches = rows
            .Where(r => excludeProductId is null || r.Id != excludeProductId.Value)
            .Select(r =>
            {
                var candidate = string.IsNullOrWhiteSpace(r.ProductNameNormalized)
                    ? ProductNameNormalizer.Normalize(r.ProductName)
                    : r.ProductNameNormalized;
                var score = ComputeSimilarityScore(normalized, candidate);
                return new { Row = r, Score = score };
            })
            .Where(x => x.Score >= similarityThreshold)
            .OrderByDescending(x => x.Score)
            .Take(5)
            .Select(x => new SimilarProductNameDto(x.Row.Id, x.Row.ProductCode, x.Row.ProductName, x.Score))
            .ToList();

        var exact = rows.Any(r =>
            (excludeProductId is null || r.Id != excludeProductId.Value) &&
            string.Equals(
                string.IsNullOrWhiteSpace(r.ProductNameNormalized)
                    ? ProductNameNormalizer.Normalize(r.ProductName)
                    : r.ProductNameNormalized,
                normalized,
                StringComparison.Ordinal));

        return new SimilarProductNamesResult(matches, exact);
    }

    private async Task<bool> HasExactNormalizedNameAsync(
        System.Data.IDbConnection conn,
        string normalized,
        Guid? excludeProductId)
    {
        const string sql = """
            SELECT EXISTS(
                SELECT 1 FROM products
                WHERE tenant_id = @TenantId AND deleted_at IS NULL
                  AND product_name_normalized = @Normalized
                  AND (@ExcludeId IS NULL OR id <> @ExcludeId)
            )
            """;
        return await conn.QuerySingleAsync<bool>(sql, new
        {
            TenantId = _tenant.TenantId,
            Normalized = normalized,
            ExcludeId = excludeProductId,
        });
    }

    private static double ComputeSimilarityScore(string left, string right)
    {
        if (string.IsNullOrEmpty(left) || string.IsNullOrEmpty(right))
            return 0;
        if (string.Equals(left, right, StringComparison.Ordinal))
            return 1;

        var distance = LevenshteinDistance(left, right);
        return 1.0 - (double)distance / Math.Max(left.Length, right.Length);
    }

    private static int LevenshteinDistance(string left, string right)
    {
        if (left == right) return 0;
        if (left.Length == 0) return right.Length;
        if (right.Length == 0) return left.Length;

        var previous = new int[right.Length + 1];
        var current = new int[right.Length + 1];
        for (var j = 0; j < previous.Length; j++)
            previous[j] = j;

        for (var i = 0; i < left.Length; i++)
        {
            current[0] = i + 1;
            for (var j = 0; j < right.Length; j++)
            {
                var cost = left[i] == right[j] ? 0 : 1;
                current[j + 1] = Math.Min(
                    Math.Min(current[j] + 1, previous[j + 1] + 1),
                    previous[j] + cost);
            }

            (previous, current) = (current, previous);
        }

        return previous[right.Length];
    }

    public async Task<IReadOnlyList<LookupItemDto>> GetCategoriesAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id AS Id, category_code AS Code, category_name AS Name
            FROM product_categories
            WHERE tenant_id = @TenantId AND deleted_at IS NULL AND status = 1
            ORDER BY sort_order, category_name
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return (await conn.QueryAsync<LookupItemDto>(sql, new { TenantId = _tenant.TenantId })).ToList();
    }

    public async Task<IReadOnlyList<LookupItemDto>> GetBrandsAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id AS Id, brand_code AS Code, brand_name AS Name
            FROM product_brands
            WHERE tenant_id = @TenantId AND deleted_at IS NULL AND status = 1
            ORDER BY brand_name
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return (await conn.QueryAsync<LookupItemDto>(sql, new { TenantId = _tenant.TenantId })).ToList();
    }

    public async Task<IReadOnlyList<LookupItemDto>> GetIngredientsAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id AS Id, ingredient_code AS Code, ingredient_name AS Name
            FROM active_ingredients
            WHERE status = 1
            ORDER BY ingredient_name
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return (await conn.QueryAsync<LookupItemDto>(sql)).ToList();
    }

    public async Task SyncProductUnitsListAsync(
        Guid productId,
        IReadOnlyList<ProductUnitSyncItem> units,
        CancellationToken cancellationToken)
    {
        var desired = units
            .Where(u => !string.IsNullOrWhiteSpace(u.UnitName))
            .Select(u => new
            {
                u.Id,
                UnitName = u.UnitName.Trim(),
                ConversionFactor = u.ConversionFactor <= 0 ? 1m : u.ConversionFactor,
                u.IsBaseUnit,
                u.IsSaleUnit,
            })
            .ToList();

        if (desired.Count == 0)
            throw new InvalidOperationException("Sản phẩm phải có ít nhất một đơn vị tính.");

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);

        var unitsWithPrices = (await conn.QueryAsync<Guid>(
            """
            SELECT DISTINCT product_unit_id FROM product_prices
            WHERE product_id = @ProductId AND tenant_id = @TenantId AND status = 1
            """,
            new { ProductId = productId, TenantId = _tenant.TenantId })).ToHashSet();

        var existing = (await conn.QueryAsync<(Guid Id, string UnitName)>(
            """
            SELECT id, unit_name FROM product_units
            WHERE product_id = @ProductId AND tenant_id = @TenantId AND status = 1
            """,
            new { ProductId = productId, TenantId = _tenant.TenantId })).ToList();

        var keepIds = new HashSet<Guid>();
        foreach (var item in desired)
        {
            if (item.Id is Guid id)
                keepIds.Add(id);
            else
            {
                var byName = existing.FirstOrDefault(e =>
                    string.Equals(e.UnitName, item.UnitName, StringComparison.OrdinalIgnoreCase));
                if (byName.Id != default)
                    keepIds.Add(byName.Id);
            }
        }

        foreach (var row in existing)
        {
            if (keepIds.Contains(row.Id))
                continue;
            if (unitsWithPrices.Contains(row.Id))
                throw new InvalidOperationException($"Không xóa được đơn vị \"{row.UnitName}\" vì đang có giá bán.");
            await conn.ExecuteAsync(
                """
                UPDATE product_units SET status = 2, updated_at = NOW()
                WHERE id = @Id AND product_id = @ProductId AND tenant_id = @TenantId
                """,
                new { Id = row.Id, ProductId = productId, TenantId = _tenant.TenantId });
        }

        foreach (var item in desired)
        {
            var updated = 0;
            if (item.Id is Guid id)
            {
                updated = await conn.ExecuteAsync(
                    """
                    UPDATE product_units
                    SET unit_name = @UnitName, conversion_factor = @ConversionFactor,
                        is_base_unit = @IsBaseUnit, is_sale_unit = @IsSaleUnit,
                        status = 1, updated_at = NOW()
                    WHERE id = @Id AND product_id = @ProductId AND tenant_id = @TenantId
                    """,
                    new
                    {
                        Id = id,
                        ProductId = productId,
                        TenantId = _tenant.TenantId,
                        item.UnitName,
                        item.ConversionFactor,
                        item.IsBaseUnit,
                        item.IsSaleUnit,
                    });
            }

            if (updated == 0)
            {
                updated = await conn.ExecuteAsync(
                    """
                    UPDATE product_units
                    SET conversion_factor = @ConversionFactor, is_base_unit = @IsBaseUnit,
                        is_sale_unit = @IsSaleUnit, status = 1, updated_at = NOW()
                    WHERE product_id = @ProductId AND tenant_id = @TenantId AND unit_name = @UnitName
                    """,
                    new
                    {
                        ProductId = productId,
                        TenantId = _tenant.TenantId,
                        item.UnitName,
                        item.ConversionFactor,
                        item.IsBaseUnit,
                        item.IsSaleUnit,
                    });
            }

            if (updated == 0)
            {
                await conn.ExecuteAsync(
                    """
                    INSERT INTO product_units (tenant_id, product_id, unit_name, conversion_factor, is_base_unit, is_sale_unit)
                    VALUES (@TenantId, @ProductId, @UnitName, @ConversionFactor, @IsBaseUnit, @IsSaleUnit)
                    """,
                    new
                    {
                        TenantId = _tenant.TenantId,
                        ProductId = productId,
                        item.UnitName,
                        item.ConversionFactor,
                        item.IsBaseUnit,
                        item.IsSaleUnit,
                    });
            }
        }
    }

    public async Task SyncProductIngredientsListAsync(
        Guid productId,
        IReadOnlyList<ProductIngredientSyncItem>? ingredients,
        CancellationToken cancellationToken)
    {
        var desired = (ingredients ?? [])
            .GroupBy(i => i.IngredientId)
            .Select(g => g.Last())
            .ToList();

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(
            "DELETE FROM product_ingredients WHERE product_id = @ProductId AND tenant_id = @TenantId",
            new { ProductId = productId, TenantId = _tenant.TenantId });

        foreach (var item in desired)
        {
            await conn.ExecuteAsync(
                """
                INSERT INTO product_ingredients (tenant_id, product_id, ingredient_id, strength_value, strength_unit)
                VALUES (@TenantId, @ProductId, @IngredientId, @StrengthValue, @StrengthUnit)
                """,
                new
                {
                    TenantId = _tenant.TenantId,
                    ProductId = productId,
                    item.IngredientId,
                    item.StrengthValue,
                    StrengthUnit = string.IsNullOrWhiteSpace(item.StrengthUnit) ? null : item.StrengthUnit.Trim(),
                });
        }
    }

    public async Task<BarcodeCheckResult> CheckBarcodeAsync(
        string barcode,
        Guid? excludeProductId,
        CancellationToken cancellationToken)
    {
        var trimmed = barcode.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return new BarcodeCheckResult(true, null, null, null);

        const string sql = """
            SELECT p.id AS ExistingProductId, p.product_code AS ExistingProductCode, p.product_name AS ExistingProductName
            FROM product_barcodes b
            INNER JOIN products p ON p.id = b.product_id AND p.deleted_at IS NULL
            WHERE b.tenant_id = @TenantId AND b.barcode = @Barcode AND b.status = 1
              AND (@ExcludeProductId IS NULL OR b.product_id <> @ExcludeProductId)
            LIMIT 1
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var row = await conn.QuerySingleOrDefaultAsync<BarcodeCheckRow>(sql, new
        {
            TenantId = _tenant.TenantId,
            Barcode = trimmed,
            ExcludeProductId = excludeProductId,
        });

        return row is null
            ? new BarcodeCheckResult(true, null, null, null)
            : new BarcodeCheckResult(false, row.ExistingProductId, row.ExistingProductCode, row.ExistingProductName);
    }

    private sealed class BarcodeCheckRow
    {
        public Guid ExistingProductId { get; init; }
        public string ExistingProductCode { get; init; } = "";
        public string ExistingProductName { get; init; } = "";
    }

    private sealed class SimilarProductRow
    {
        public Guid Id { get; init; }
        public string ProductCode { get; init; } = "";
        public string ProductName { get; init; } = "";
        public double SimilarityScore { get; init; }
    }

    private sealed class SimilarProductCandidateRow
    {
        public Guid Id { get; init; }
        public string ProductCode { get; init; } = "";
        public string ProductName { get; init; } = "";
        public string ProductNameNormalized { get; init; } = "";
    }

    private sealed class PrimaryBarcodeRow
    {
        public Guid Id { get; init; }
        public string Barcode { get; init; } = "";
    }

    private sealed class ProductDetailRow
    {
        public Guid Id { get; init; }
        public string ProductCode { get; init; } = "";
        public string ProductName { get; init; } = "";
        public string? GenericName { get; init; }
        public short DrugType { get; init; }
        public Guid? CategoryId { get; init; }
        public Guid? BrandId { get; init; }
        public string? Description { get; init; }
        public string? NationalDrugId { get; init; }
        public string? NationalRegistrationNumber { get; init; }
        public short Status { get; init; }
        public decimal? MinStockQty { get; init; }
    }
}
