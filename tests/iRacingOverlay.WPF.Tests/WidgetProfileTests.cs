using iRacingOverlay.WPF.Models;

namespace iRacingOverlay.WPF.Tests;

/// <summary>
/// Profile matching decides which layout appears when a session starts. The
/// documented order is session+class, then session, then class, then none.
/// </summary>
public class WidgetProfileTests
{
    private static WidgetProfile Bound(SessionCategory? session, string? carClass) =>
        new() { SessionBinding = session, CarClassBinding = carClass };

    [Fact]
    public void BothBound_OutscoresEitherAlone()
    {
        var both = Bound(SessionCategory.Race, "GT3");
        var sessionOnly = Bound(SessionCategory.Race, null);
        var classOnly = Bound(null, "GT3");

        int b = both.MatchScore(SessionCategory.Race, "GT3");
        int s = sessionOnly.MatchScore(SessionCategory.Race, "GT3");
        int c = classOnly.MatchScore(SessionCategory.Race, "GT3");

        Assert.True(b > s && b > c, $"both={b} session={s} class={c}");
    }

    [Fact]
    public void WrongBinding_DoesNotMatchAtAll()
    {
        Assert.Equal(-1, Bound(SessionCategory.Race, null).MatchScore(SessionCategory.Practice, "GT3"));
        Assert.Equal(-1, Bound(null, "LMP2").MatchScore(SessionCategory.Race, "GT3"));
    }

    [Fact]
    public void ClassMatch_IgnoresCase()
    {
        Assert.True(Bound(null, "gt3").MatchScore(SessionCategory.Race, "GT3") > 0);
    }

    [Fact]
    public void Unbound_MatchesEverythingWithZeroScore()
    {
        // Zero is "matches, but only as a last resort" — FindBestMatch requires > 0,
        // so a catch-all profile is never auto-applied over doing nothing.
        Assert.Equal(0, Bound(null, null).MatchScore(SessionCategory.Race, "GT3"));
    }

    [Fact]
    public void HasLayout_TracksTheCapturedList()
    {
        var p = new WidgetProfile();
        Assert.False(p.HasLayout);

        p.Layout.Add(new WidgetConfig { Type = WidgetType.MRTOne });
        Assert.True(p.HasLayout);
    }
}

/// <summary>
/// Three global hotkeys share one conflict check; the profile one is optional.
/// </summary>
public class HotkeyConflictTests
{
    private static AppSettings With(string lockKey, string visKey, string profileKey) => new()
    {
        ToggleLockModifier = "Ctrl", ToggleLockKey = lockKey,
        ToggleVisibilityModifier = "Ctrl", ToggleVisibilityKey = visKey,
        CycleProfileModifier = "Ctrl", CycleProfileKey = profileKey,
    };

    [Fact]
    public void Distinct_NoConflict() => Assert.False(With("L", "H", "P").HotkeysConflict());

    [Fact]
    public void LockAndVisibilitySame_Conflicts() => Assert.True(With("L", "L", "").HotkeysConflict());

    [Fact]
    public void ProfileSameAsLock_Conflicts() => Assert.True(With("L", "H", "L").HotkeysConflict());

    [Fact]
    public void UnboundProfile_NeverConflicts() => Assert.False(With("L", "H", "").HotkeysConflict());

    [Fact]
    public void ConflictCheck_IgnoresCase()
    {
        var s = With("L", "H", "l");
        Assert.True(s.HotkeysConflict());
    }
}
