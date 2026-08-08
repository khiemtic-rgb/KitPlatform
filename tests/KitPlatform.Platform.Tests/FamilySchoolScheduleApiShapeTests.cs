using KitPlatform.Packs.FamilyOs;
using Xunit;

namespace KitPlatform.Platform.Tests;

public sealed class FamilySchoolScheduleApiShapeTests
{
    [Fact]
    public void ToPayloadDto_round_trips_core_fields()
    {
        var schedule = new FamilySchoolScheduleV1(
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

        var payload = FamilySchoolSchedule.ToPayloadDto(schedule);
        Assert.NotNull(payload);
        Assert.Equal(1, payload!.SchemaVersion);
        Assert.True(payload.SeasonOn);
        Assert.Equal("full", payload.Mode);
        Assert.Equal("18:30", payload.ExtraEnd);
        Assert.Null(FamilySchoolSchedule.ToPayloadDto(null));
    }
}
