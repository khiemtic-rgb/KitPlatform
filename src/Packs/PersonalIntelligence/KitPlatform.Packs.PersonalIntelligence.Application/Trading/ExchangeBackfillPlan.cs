namespace KitPlatform.Packs.PersonalIntelligence.Trading;

public static class ExchangeMarket
{
    public const string Spot = "SPOT";
    public const string Futures = "FUTURES";
    public const long DayMs = 24L * 60 * 60 * 1000;
    public static readonly DateTimeOffset BinanceSpotFloor = DateTimeOffset.Parse("2017-07-14T00:00:00Z");

    public static (string MarketType, string ProductSubtype) ForSubtype(string exchange, string subtype) =>
        (exchange, subtype) switch
        {
            ("BYBIT", "spot") => (Spot, "spot"),
            ("BYBIT", "linear") => (Futures, "linear"),
            ("BYBIT", "inverse") => (Futures, "inverse"),
            ("BINANCE", "spot") => (Spot, "spot"),
            ("BINANCE", "usd_m") => (Futures, "usd_m"),
            ("BINANCE", "coin_m") => (Futures, "coin_m"),
            ("OKX", "spot") => (Spot, "spot"),
            ("OKX", "swap") => (Futures, "swap"),
            ("OKX", "expiry") => (Futures, "expiry"),
            _ => throw new ExchangeConnectionException(400, "unsupported_category", $"Chưa có mapping SPOT/FUTURES cho {exchange}/{subtype}."),
        };
}

public sealed class ExchangeStreamDefinition
{
    public string ExchangeCode { get; init; } = "";
    public string MarketType { get; init; } = "";
    public string ProductSubtype { get; init; } = "";
    public string Endpoint { get; init; } = "";
    public long WindowMs { get; init; }
    public long RetentionMs { get; init; }
    public bool RetentionLimited { get; init; }
    public string RetentionNote { get; init; } = "";
    public bool RequiresSymbol { get; init; }
    public string Pagination { get; init; } = "cursor";
    public string Discover { get; init; } = "none";
    public int PageSize { get; init; } = 100;
}

public sealed class ExchangeCatalogEntry
{
    public string ExchangeCode { get; set; } = "";
    public bool RequiresPassphrase { get; set; }
    public bool ReadOnlyVerifiable { get; set; }
    public string HistoryNote { get; set; } = "";
    public List<ExchangeStreamDefinition> Streams { get; set; } = [];
    public List<string> UnsupportedProducts { get; set; } = [];
}

public static class ExchangeCatalog
{
    public static IReadOnlyList<ExchangeCatalogEntry> Describe(int? bybitUta = null) =>
    [
        new ExchangeCatalogEntry
        {
            ExchangeCode = "BYBIT",
            ReadOnlyVerifiable = true,
            HistoryNote = "GET /v5/execution/list. Mỗi lần gọi tối đa 7 ngày. UTA giữ khoảng 730 ngày. Tài khoản classic hoặc khi thiếu uta chỉ lấy 7 ngày.",
            UnsupportedProducts = ["option", "margin"],
            Streams = BybitStreams(bybitUta),
        },
        new ExchangeCatalogEntry
        {
            ExchangeCode = "BINANCE",
            ReadOnlyVerifiable = true,
            HistoryNote = "Spot dùng GET /api/v3/myTrades khi apiRestrictions chứng minh không bật giao dịch Spot. USD-M và COIN-M không được gọi: Enable Futures cho phép đặt lệnh và không có quyền chỉ đọc tách biệt.",
            UnsupportedProducts = ["margin", "options", "portfolio margin"],
            Streams = BinanceStreams(),
        },
        new ExchangeCatalogEntry
        {
            ExchangeCode = "OKX",
            RequiresPassphrase = true,
            ReadOnlyVerifiable = true,
            HistoryNote = "GET /api/v5/trade/fills-history. SPOT, SWAP và FUTURES được phân trang bằng tradeId. Endpoint này chỉ giữ khoảng 3 tháng. GET /api/v5/account/config trả perm. Chỉ perm đúng read_only được xác minh. Thiếu hoặc lạ thì chưa xác minh và không đồng bộ lịch sử. trade hoặc withdraw bị từ chối.",
            UnsupportedProducts = ["MARGIN", "OPTION"],
            Streams = OkxStreams(),
        },
    ];

    public static ExchangeStreamDefinition Require(string exchange, string subtype, int? bybitUta = null)
    {
        var stream = Describe(bybitUta).SelectMany(entry => entry.Streams)
            .FirstOrDefault(item => item.ExchangeCode == exchange && item.ProductSubtype == subtype);
        return stream ?? throw new ExchangeConnectionException(400, "unsupported_category", $"Không có luồng đồng bộ cho {exchange}/{subtype}.");
    }

    public static List<ExchangeStreamDefinition> BybitStreams(int? uta)
    {
        var utaKnown = uta == 1;
        var retention = (utaKnown ? 730L : 7L) * ExchangeMarket.DayMs;
        var note = utaKnown
            ? "Bybit ghi execution của Unified Trading Account khoảng 730 ngày trên GET /v5/execution/list. Đây không phải toàn bộ đời tài khoản nếu tài khoản cũ hơn."
            : uta == 0
                ? "Bybit classic (uta=0) chỉ trả execution khoảng 7 ngày trên GET /v5/execution/list."
                : "Kết nối không có uta. Không giả định 730 ngày. Phạm vi execution là 7 ngày.";
        return
        [
            Stream("BYBIT", "spot", "GET /v5/execution/list", 7 * ExchangeMarket.DayMs, retention, note, false, "cursor", "none", 100),
            Stream("BYBIT", "linear", "GET /v5/execution/list", 7 * ExchangeMarket.DayMs, retention, note, false, "cursor", "none", 100),
            Stream("BYBIT", "inverse", "GET /v5/execution/list", 7 * ExchangeMarket.DayMs, retention, note, false, "cursor", "none", 100),
        ];
    }

    public static List<ExchangeStreamDefinition> BinanceStreams()
    {
        var futuresNote = "Binance USD-M/COIN-M userTrades chỉ truy vấn giao dịch trong 6 tháng, từng khoảng tối đa 7 ngày, và bắt buộc symbol. Symbol được lấy từ income history, không ghi income thành giao dịch. Symbol không xuất hiện trong income nằm ngoài phạm vi.";
        var spotNote = "Binance GET /api/v3/myTrades bắt buộc symbol và khoảng startTime-endTime tối đa 24 giờ. Symbol lấy từ số dư hiện tại ghép với USDT, USDC, FDUSD và BTC. Symbol đã bán hết, số dư bằng 0 và chưa có trong sổ sẽ không được phát hiện. Mốc sàn dùng để dừng là 2017-07-14.";
        return
        [
            Stream("BINANCE", "spot", "GET /api/v3/myTrades", ExchangeMarket.DayMs, 0, spotNote, true, "time", "balances", 1000),
            Stream("BINANCE", "usd_m", "GET /fapi/v1/userTrades", 7 * ExchangeMarket.DayMs, 183 * ExchangeMarket.DayMs, futuresNote, true, "time", "income", 1000),
            Stream("BINANCE", "coin_m", "GET /dapi/v1/userTrades", 7 * ExchangeMarket.DayMs, 183 * ExchangeMarket.DayMs, futuresNote, true, "time", "income", 1000),
        ];
    }

    public static List<ExchangeStreamDefinition> OkxStreams()
    {
        var note = "OKX GET /api/v5/trade/fills-history chỉ trả giao dịch khoảng 3 tháng. SWAP là perpetual. FUTURES là hợp đồng có kỳ hạn và được lưu subtype expiry. MARGIN và OPTION không được ghi vào SPOT hay FUTURES.";
        return
        [
            Stream("OKX", "spot", "GET /api/v5/trade/fills-history", 7 * ExchangeMarket.DayMs, 90 * ExchangeMarket.DayMs, note, false, "cursor", "none", 100),
            Stream("OKX", "swap", "GET /api/v5/trade/fills-history", 7 * ExchangeMarket.DayMs, 90 * ExchangeMarket.DayMs, note, false, "cursor", "none", 100),
            Stream("OKX", "expiry", "GET /api/v5/trade/fills-history", 7 * ExchangeMarket.DayMs, 90 * ExchangeMarket.DayMs, note, false, "cursor", "none", 100),
        ];
    }

    static ExchangeStreamDefinition Stream(
        string exchange, string subtype, string endpoint, long windowMs, long retentionMs,
        string note, bool requiresSymbol, string pagination, string discover, int pageSize)
    {
        var (market, _) = ExchangeMarket.ForSubtype(exchange, subtype);
        return new ExchangeStreamDefinition
        {
            ExchangeCode = exchange,
            MarketType = market,
            ProductSubtype = subtype,
            Endpoint = endpoint,
            WindowMs = windowMs,
            RetentionMs = retentionMs,
            RetentionLimited = true,
            RetentionNote = note,
            RequiresSymbol = requiresSymbol,
            Pagination = pagination,
            Discover = discover,
            PageSize = pageSize,
        };
    }
}

public sealed class BackfillWindow
{
    public long Start { get; set; }
    public long End { get; set; }
}

public sealed class BackfillCheckpoint
{
    public string Mode { get; set; } = "FULL";
    public string Pagination { get; set; } = "cursor";
    public string Discover { get; set; } = "none";
    public bool RequiresSymbol { get; set; }
    public bool RetentionLimited { get; set; }
    public string RetentionNote { get; set; } = "";
    public long VenueFloorMs { get; set; }
    public bool DiscoveryComplete { get; set; } = true;
    public int DiscoverWindowIndex { get; set; }
    public long? DiscoverCursorMs { get; set; }
    public List<string> Symbols { get; set; } = [];
    public int SymbolIndex { get; set; }
    public List<BackfillWindow> Windows { get; set; } = [];
    public int WindowIndex { get; set; }
    public long? ActiveWindowEnd { get; set; }
    public string? Cursor { get; set; }
    public string? Note { get; set; }
}

public sealed class BackfillStep
{
    public BackfillCheckpoint Checkpoint { get; init; } = new();
    public bool Terminal { get; init; }
    public string? TerminalStatus { get; init; }
    public string? CutoffReason { get; init; }
    public long? CutoffMs { get; init; }
}

public static class ExchangeBackfillMachine
{
    public static List<BackfillWindow> Windows(long floorMs, long ceilingMs, long windowMs)
    {
        if (windowMs <= 0)
            throw new ExchangeConnectionException(400, "window", "Khoảng truy vấn không hợp lệ.");
        if (ceilingMs <= floorMs)
            throw new ExchangeConnectionException(400, "window", "Mốc kết thúc phải sau mốc bắt đầu.");
        var windows = new List<BackfillWindow>();
        var end = ceilingMs;
        while (end > floorMs)
        {
            var start = Math.Max(floorMs, end - windowMs);
            windows.Add(new BackfillWindow { Start = start, End = end });
            if (start == floorMs)
                break;
            end = start;
        }

        return windows;
    }

    public static BackfillCheckpoint Start(ExchangeStreamDefinition stream, DateTimeOffset now, DateTimeOffset? watermark, string mode)
    {
        var ceiling = now.ToUnixTimeMilliseconds();
        var venueFloor = stream.ProductSubtype == "spot" && stream.ExchangeCode == "BINANCE"
            ? ExchangeMarket.BinanceSpotFloor.ToUnixTimeMilliseconds()
            : ceiling - stream.RetentionMs;
        if (stream.ExchangeCode == "BYBIT" || stream.ExchangeCode == "OKX" || stream.Discover == "income")
            venueFloor = ceiling - stream.RetentionMs;
        var floor = venueFloor;
        if (string.Equals(mode, "INCREMENTAL", StringComparison.Ordinal) && watermark is not null)
            floor = Math.Max(venueFloor, watermark.Value.ToUnixTimeMilliseconds() - stream.WindowMs);
        if (floor >= ceiling)
            floor = ceiling - 1;
        return new BackfillCheckpoint
        {
            Mode = mode,
            Pagination = stream.Pagination,
            Discover = stream.Discover,
            RequiresSymbol = stream.RequiresSymbol,
            RetentionLimited = stream.RetentionLimited,
            RetentionNote = stream.RetentionNote,
            VenueFloorMs = venueFloor,
            DiscoveryComplete = stream.Discover == "none",
            Windows = Windows(floor, ceiling, stream.WindowMs),
        };
    }

    public static ExchangeHistoryQuery Query(BackfillCheckpoint checkpoint, string subtype, int limit)
    {
        if (!checkpoint.DiscoveryComplete)
        {
            var discover = checkpoint.Windows[Math.Min(checkpoint.DiscoverWindowIndex, checkpoint.Windows.Count - 1)];
            var start = checkpoint.DiscoverCursorMs is long cursor && cursor > discover.Start ? cursor : discover.Start;
            return new ExchangeHistoryQuery
            {
                ProductSubtype = subtype,
                Phase = "discover",
                WindowStartMs = start,
                WindowEndMs = discover.End,
                Limit = limit,
            };
        }

        var window = checkpoint.Windows[checkpoint.WindowIndex];
        return new ExchangeHistoryQuery
        {
            ProductSubtype = subtype,
            Phase = "fills",
            WindowStartMs = window.Start,
            WindowEndMs = checkpoint.ActiveWindowEnd ?? window.End,
            Cursor = checkpoint.Cursor,
            Symbol = checkpoint.RequiresSymbol ? checkpoint.Symbols.ElementAtOrDefault(checkpoint.SymbolIndex) : null,
            Limit = limit,
        };
    }

    public static BackfillStep Advance(BackfillCheckpoint checkpoint, ExchangeHistoryPage page)
    {
        var next = Clone(checkpoint);
        if (!next.DiscoveryComplete)
            return AdvanceDiscovery(next, page);
        if (next.RequiresSymbol && next.SymbolIndex >= next.Symbols.Count)
            return Finish(next, next.Symbols.Count == 0
                ? "Không phát hiện symbol nào để đọc lịch sử. Không tạo giao dịch giả."
                : null);

        if (next.Pagination == "cursor")
        {
            if (!string.IsNullOrEmpty(page.NextCursor) && !string.Equals(page.NextCursor, next.Cursor, StringComparison.Ordinal))
            {
                next.Cursor = page.NextCursor;
                return new BackfillStep { Checkpoint = next };
            }

            if (page.PageFull && string.IsNullOrEmpty(page.NextCursor))
                next.Note = Append(next.Note, "Một trang đầy không có cursor tiếp theo. Khoảng này dừng tại trang đã nhận.");
            return AdvanceWindow(next);
        }

        if (page.PageFull && page.OldestMs is long oldest)
        {
            var window = next.Windows[next.WindowIndex];
            var newEnd = oldest >= (next.ActiveWindowEnd ?? window.End) ? oldest - 1 : oldest;
            if (newEnd > window.Start)
            {
                next.ActiveWindowEnd = newEnd;
                next.Cursor = null;
                return new BackfillStep { Checkpoint = next };
            }

            next.Note = Append(next.Note, "Nhiều execution trùng một timestamp ở biên khoảng. Bản ghi đã lưu không bị ghi đè.");
        }

        return AdvanceWindow(next);
    }

    static BackfillStep AdvanceDiscovery(BackfillCheckpoint next, ExchangeHistoryPage page)
    {
        foreach (var symbol in page.DiscoveredSymbols)
        {
            if (!string.IsNullOrWhiteSpace(symbol) && !next.Symbols.Contains(symbol, StringComparer.Ordinal))
                next.Symbols.Add(symbol.Trim().ToUpperInvariant());
        }

        next.Symbols.Sort(StringComparer.Ordinal);
        if (next.Discover == "balances")
        {
            next.DiscoveryComplete = true;
            next.Note = Append(next.Note, "Danh sách symbol Spot lấy từ số dư hiện tại. Symbol đã bán hết không nằm trong danh sách này.");
        }
        else if (!string.IsNullOrEmpty(page.NextCursor)
            && long.TryParse(page.NextCursor, out var cursor)
            && cursor > (next.DiscoverCursorMs ?? next.Windows[next.DiscoverWindowIndex].Start))
        {
            next.DiscoverCursorMs = cursor;
        }
        else
        {
            next.DiscoverCursorMs = null;
            next.DiscoverWindowIndex++;
            if (next.DiscoverWindowIndex >= next.Windows.Count)
            {
                next.DiscoveryComplete = true;
                next.Note = Append(next.Note, "Symbol hợp đồng lấy từ income history. Income không được ghi thành giao dịch.");
            }
        }

        if (next.DiscoveryComplete && next.Symbols.Count == 0)
            return Finish(next, "Không phát hiện symbol nào trong phạm vi API cho phép. Không suy ra giao dịch từ số dư hay income.");
        return new BackfillStep { Checkpoint = next };
    }

    static BackfillStep AdvanceWindow(BackfillCheckpoint next)
    {
        next.Cursor = null;
        next.ActiveWindowEnd = null;
        next.WindowIndex++;
        if (next.WindowIndex < next.Windows.Count)
            return new BackfillStep { Checkpoint = next };
        next.WindowIndex = 0;
        if (!next.RequiresSymbol)
            return Finish(next, null);
        next.SymbolIndex++;
        if (next.SymbolIndex >= next.Symbols.Count)
            return Finish(next, null);
        return new BackfillStep { Checkpoint = next };
    }

    static BackfillStep Finish(BackfillCheckpoint next, string? extra)
    {
        var reason = Append(next.RetentionNote, extra);
        reason = Append(reason, next.Note);
        var incremental = string.Equals(next.Mode, "INCREMENTAL", StringComparison.Ordinal);
        if (incremental)
            reason = Append("Đồng bộ tăng dần đã đi hết khoảng chồng lấn. Mốc cutoff của API vẫn còn hiệu lực.", reason);
        var status = incremental && string.IsNullOrWhiteSpace(extra) ? "COMPLETED" : "PARTIAL";
        if (!next.RetentionLimited && string.IsNullOrWhiteSpace(extra) && !next.RequiresSymbol)
            status = "COMPLETED";
        return new BackfillStep
        {
            Checkpoint = next,
            Terminal = true,
            TerminalStatus = status,
            CutoffReason = reason,
            CutoffMs = next.VenueFloorMs,
        };
    }

    static BackfillCheckpoint Clone(BackfillCheckpoint checkpoint)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(checkpoint);
        return System.Text.Json.JsonSerializer.Deserialize<BackfillCheckpoint>(json) ?? new BackfillCheckpoint();
    }

    static string Append(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left))
            return right?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(right))
            return left.Trim();
        return left.Trim() + " " + right.Trim();
    }
}
