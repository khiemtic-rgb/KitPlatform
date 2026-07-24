namespace KitPlatform.Packs.FamilyOs;



/// <summary>

/// Late star tiers vs window_end + on_time_grace_minutes (family TZ). Half-open intervals:

/// on-time (late &lt;= 0) full; 0 &lt; late &lt;= T1 half; T1 &lt; late &lt;= T2 zero;

/// T2 &lt; late &lt;= T3 -half; late &gt; T3 -full. No window_end = on-time full.

/// Half stars: integer floor(star_reward * pct / 100).

/// </summary>

public static class FamilyStarTiers

{

    public const string OnTime = "on_time";

    public const string NoWindow = "no_window";

    public const string LateHalf = "late_half";

    public const string LateZero = "late_zero";

    public const string LatePenaltyHalf = "late_penalty_half";

    public const string LatePenaltyFull = "late_penalty_full";

}



public sealed record StarAwardResult(

    int Delta,

    string Tier,

    int? LateMinutes,

    string LabelVi);



public static class FamilyStarCalculator

{

    /// <summary>Half star value — integer division (floor).</summary>

    public static int HalfStars(int starReward) => starReward / 2;



    public static int InferStarReward(string? title)

    {

        var t = (title ?? "").Trim().ToLowerInvariant();

        if (t.Contains("bài") || t.Contains("học") || t.Contains("toán"))

            return 20;

        if (t.Contains("ngủ") || t.Contains("đánh răng"))

            return 15;

        return 10;

    }



    public static StarAwardResult Calculate(

        int starReward,

        DateTimeOffset? completedAt,

        TimeOnly? windowEnd,

        DateOnly flowDate,

        string? timezoneId,

        FamilyStarTierSettings? tierSettings = null,

        int onTimeGraceMinutes = 0)

    {

        var cfg = tierSettings ?? FamilyStarTierSettings.Default;

        var reward = starReward > 0 ? starReward : 10;



        if (completedAt is null || windowEnd is null)

        {

            var tier = windowEnd is null ? FamilyStarTiers.NoWindow : FamilyStarTiers.OnTime;

            return new StarAwardResult(reward, tier, null, FormatLabelVi(reward, null, tier));

        }



        var tz = FamilyTimeZones.Resolve(timezoneId);

        var local = TimeZoneInfo.ConvertTime(completedAt.Value, tz);

        var localDate = DateOnly.FromDateTime(local.DateTime);



        if (localDate < flowDate)

            return OnTimeResult(reward);



        var grace = Math.Max(0, onTimeGraceMinutes);
        var effectiveEnd = windowEnd.Value.AddMinutes(grace);
        var windowEndLocal = new DateTimeOffset(

            flowDate.Year,

            flowDate.Month,

            flowDate.Day,

            effectiveEnd.Hour,

            effectiveEnd.Minute,

            effectiveEnd.Second,

            local.Offset);



        var lateMinutes = (int)Math.Floor((local - windowEndLocal).TotalMinutes);

        if (lateMinutes <= 0)

            return OnTimeResult(reward);



        if (lateMinutes <= cfg.LateT1Minutes)

        {

            var delta = ApplyPct(reward, cfg.LateHalfPct);

            return new StarAwardResult(

                delta,

                FamilyStarTiers.LateHalf,

                lateMinutes,

                FormatLabelVi(delta, lateMinutes, FamilyStarTiers.LateHalf));

        }



        if (lateMinutes <= cfg.LateT2Minutes)

        {

            var delta = ApplyPct(reward, cfg.LateZeroPct);

            return new StarAwardResult(

                delta,

                FamilyStarTiers.LateZero,

                lateMinutes,

                FormatLabelVi(delta, lateMinutes, FamilyStarTiers.LateZero));

        }



        if (lateMinutes <= cfg.LateT3Minutes)

        {

            var delta = ApplyPct(reward, cfg.LatePenaltyHalfPct);

            return new StarAwardResult(

                delta,

                FamilyStarTiers.LatePenaltyHalf,

                lateMinutes,

                FormatLabelVi(delta, lateMinutes, FamilyStarTiers.LatePenaltyHalf));

        }



        var fullPenalty = ApplyPct(reward, cfg.LatePenaltyFullPct);

        return new StarAwardResult(

            fullPenalty,

            FamilyStarTiers.LatePenaltyFull,

            lateMinutes,

            FormatLabelVi(fullPenalty, lateMinutes, FamilyStarTiers.LatePenaltyFull));

    }



    internal static int ApplyPct(int starReward, int pct)

    {

        if (pct == 0) return 0;

        if (pct > 0)

            return (int)Math.Floor(starReward * pct / 100.0);

        return -(int)Math.Floor(starReward * Math.Abs(pct) / 100.0);

    }



    public static string FormatLateDuration(int minutes)
    {
        var n = Math.Max(0, minutes);
        if (n < 60) return $"{n} phút";
        var hrs = n / 60;
        var mins = n % 60;
        if (mins == 0) return $"{hrs} giờ";
        return $"{hrs} giờ {mins} phút";
    }

    public static string FormatLabelVi(int delta, int? lateMinutes, string tier)

    {

        if (tier is FamilyStarTiers.OnTime or FamilyStarTiers.NoWindow)

            return delta >= 0 ? $"Đúng giờ — +{delta}⭐" : $"{delta}⭐";



        var late = lateMinutes ?? 0;

        var prefix = $"Muộn {FormatLateDuration(late)} — ";

        if (delta > 0) return prefix + $"+{delta}⭐";

        if (delta < 0) return prefix + $"{delta}⭐";

        return prefix + "0⭐";

    }



    private static StarAwardResult OnTimeResult(int reward) =>

        new(reward, FamilyStarTiers.OnTime, 0, FormatLabelVi(reward, null, FamilyStarTiers.OnTime));

}


