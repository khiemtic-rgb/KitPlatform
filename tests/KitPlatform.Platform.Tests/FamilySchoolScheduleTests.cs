using System.Text.Json;
using KitPlatform.Packs.FamilyOs;
using Xunit;

namespace KitPlatform.Platform.Tests;

/// <summary>
/// Parity with client/family-app/scripts/check-school-season.mjs + school-season.ts.
/// </summary>
public sealed class FamilySchoolScheduleTests
{
    private static readonly FamilySchoolScheduleV1 Full = new(
        SchemaVersion: 1,
        SeasonOn: true,
        Mode: FamilySchoolDayModes.Full,
        Weekdays: [1, 2, 3, 4, 5],
        SchoolStart: "07:00",
        SchoolEnd: "16:30",
        HasExtraClass: true,
        ExtraEnd: "18:30",
        Source: "parent_settings",
        UpdatedAt: "2026-08-08T08:00:00+07:00");

    private static TimeOnly T(string hhmm)
    {
        var parts = hhmm.Split(':');
        return new TimeOnly(int.Parse(parts[0]), int.Parse(parts[1]));
    }

    [Theory]
    [InlineData(6, "10:00", FamilySchoolPhases.Weekend)]
    [InlineData(1, "06:30", FamilySchoolPhases.BeforeSchool)]
    [InlineData(1, "09:00", FamilySchoolPhases.AtSchool)]
    [InlineData(1, "17:00", FamilySchoolPhases.AtSchool)] // still in học thêm
    [InlineData(1, "18:45", FamilySchoolPhases.AfterSchool)]
    [InlineData(1, "20:00", FamilySchoolPhases.Evening)]
    public void Phase_matches_check_school_season_mjs(int weekday, string hhmm, string expected)
    {
        Assert.Equal(expected, FamilySchoolSchedule.ResolvePhase(Full, weekday, T(hhmm)));
    }

    [Fact]
    public void Season_off_is_not_quiet()
    {
        var off = Full with { SeasonOn = false };
        Assert.Equal(
            FamilySchoolPhases.SeasonOff,
            FamilySchoolSchedule.ResolvePhase(off, 1, T("09:00")));
        Assert.False(FamilySchoolSchedule.IsQuietNow(off, 1, T("09:00")));
    }

    [Fact]
    public void Mode_off_is_season_off()
    {
        var off = Full with { Mode = FamilySchoolDayModes.Off, SeasonOn = false };
        Assert.Equal(
            FamilySchoolPhases.SeasonOff,
            FamilySchoolSchedule.ResolvePhase(off, 1, T("09:00")));
    }

    [Fact]
    public void Morning_mode_quiet_ends_at_school_end()
    {
        var morning = Full with
        {
            Mode = FamilySchoolDayModes.Morning,
            HasExtraClass = false,
            ExtraEnd = null,
        };
        Assert.Equal("16:30", FamilySchoolSchedule.EffectiveQuietEnd(morning));
        Assert.True(FamilySchoolSchedule.IsQuietNow(morning, 1, T("12:00")));
        Assert.False(FamilySchoolSchedule.IsQuietNow(morning, 1, T("16:30")));
        Assert.Equal(
            FamilySchoolPhases.AfterSchool,
            FamilySchoolSchedule.ResolvePhase(morning, 1, T("16:45")));
    }

    [Fact]
    public void Full_without_extra_uses_school_end_as_quiet()
    {
        var noExtra = Full with { HasExtraClass = false, ExtraEnd = null };
        Assert.Equal("16:30", FamilySchoolSchedule.EffectiveQuietEnd(noExtra));
        Assert.True(FamilySchoolSchedule.IsQuietNow(noExtra, 1, T("15:00")));
        Assert.Equal(
            FamilySchoolPhases.AfterSchool,
            FamilySchoolSchedule.ResolvePhase(noExtra, 1, T("16:45")));
    }

    [Fact]
    public void ReadMemberSchedule_round_trips_layers_json()
    {
        var childId = Guid.Parse("01900000-0000-7000-8000-000000000001");
        var layers = $$"""
            {
              "members": {
                "{{childId}}": {
                  "praiseStyle": "gentle",
                  "schoolSchedule": {
                    "schemaVersion": 1,
                    "seasonOn": true,
                    "mode": "full",
                    "weekdays": [1, 2, 3, 4, 5],
                    "schoolStart": "07:00",
                    "schoolEnd": "16:30",
                    "hasExtraClass": true,
                    "extraEnd": "18:30",
                    "source": "parent_settings",
                    "updatedAt": "2026-08-08T08:00:00+07:00"
                  }
                }
              }
            }
            """;

        var schedule = FamilySchoolSchedule.ReadMemberSchedule(layers, childId);
        Assert.NotNull(schedule);
        Assert.Equal(FamilySchoolDayModes.Full, schedule!.Mode);
        Assert.True(schedule.HasExtraClass);
        Assert.Equal("18:30", schedule.ExtraEnd);
        Assert.Equal("gentle", JsonDocument.Parse(layers).RootElement
            .GetProperty("members").GetProperty(childId.ToString()).GetProperty("praiseStyle").GetString());
    }

    [Fact]
    public void QuietMap_marks_at_school_true()
    {
        var childA = Guid.Parse("01900000-0000-7000-8000-0000000000aa");
        var childB = Guid.Parse("01900000-0000-7000-8000-0000000000bb");
        var layers = $$"""
            {
              "members": {
                "{{childA}}": {
                  "schoolSchedule": {
                    "schemaVersion": 1,
                    "seasonOn": true,
                    "mode": "full",
                    "weekdays": [1, 2, 3, 4, 5],
                    "schoolStart": "07:00",
                    "schoolEnd": "16:30",
                    "hasExtraClass": true,
                    "extraEnd": "18:30",
                    "source": "parent_settings",
                    "updatedAt": "2026-08-08T08:00:00+07:00"
                  }
                },
                "{{childB}}": {
                  "schoolSchedule": {
                    "schemaVersion": 1,
                    "seasonOn": false,
                    "mode": "off",
                    "weekdays": [1, 2, 3, 4, 5],
                    "schoolStart": "07:00",
                    "schoolEnd": "16:30",
                    "hasExtraClass": false,
                    "source": "parent_settings",
                    "updatedAt": "2026-08-08T08:00:00+07:00"
                  }
                }
              }
            }
            """;

        // Monday 2026-08-10 10:00 +07
        var asOf = new DateTimeOffset(2026, 8, 10, 10, 0, 0, TimeSpan.FromHours(7));
        var map = FamilySchoolSchedule.QuietMap(layers, asOf, "Asia/Ho_Chi_Minh");
        Assert.True(map[childA]);
        Assert.False(map[childB]);

        var evening = new DateTimeOffset(2026, 8, 10, 19, 0, 0, TimeSpan.FromHours(7));
        var mapEve = FamilySchoolSchedule.QuietMap(layers, evening, "Asia/Ho_Chi_Minh");
        Assert.False(mapEve[childA]);
    }

    [Fact]
    public void Derive_exposes_phase_and_quiet_end()
    {
        var asOf = new DateTimeOffset(2026, 8, 10, 17, 0, 0, TimeSpan.FromHours(7));
        var d = FamilySchoolSchedule.Derive(Full, asOf, "Asia/Ho_Chi_Minh");
        Assert.Equal(FamilySchoolPhases.AtSchool, d.Phase);
        Assert.True(d.QuietNow);
        Assert.Equal("18:30", d.QuietEnd);
        Assert.Equal("Asia/Ho_Chi_Minh", d.TimeZone);
    }
}
