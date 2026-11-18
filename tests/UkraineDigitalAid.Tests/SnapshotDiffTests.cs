using UkraineDigitalAid.History;

namespace UkraineDigitalAid.Tests;

public class SnapshotDiffTests
{
    private static readonly DateOnly Before = new(2026, 11, 11), Now = new(2026, 11, 18);

    private static SnapshotEntry Entry(string id, int? status = 2, DateOnly? end = null, bool direct = true) =>
        new(id, status, end ?? new DateOnly(2028, 12, 31), direct);

    private static Changes Compare(SnapshotEntry[] before, SnapshotEntry[] now) =>
        SnapshotDiff.Compare(new Snapshot(Before, before), new Snapshot(Now, now));

    [Fact]
    public void Record_missing_from_the_previous_snapshot_is_new()
    {
        var changes = Compare([Entry("SE-0-1")], [Entry("SE-0-1"), Entry("CH-4-2026-7")]);

        Assert.Equal(["CH-4-2026-7"], changes.Added);
        Assert.Empty(changes.Ended);
        Assert.Equal((Before, Now), (changes.From, changes.To));
    }

    [Fact]
    public void Status_change_to_closed_or_cancelled_is_an_end()
    {
        var changes = Compare(
            [Entry("A", status: 2), Entry("B", status: 1), Entry("C", status: 2)],
            [Entry("A", status: 4), Entry("B", status: 5), Entry("C", status: 6)]);

        // 6 «призупинено» ще не завершення
        Assert.Equal([new Ended("A", EndReason.Status), new Ended("B", EndReason.Status)], changes.Ended);
    }

    [Fact]
    public void End_date_passed_between_snapshots_is_an_end_even_if_the_status_stayed()
    {
        var changes = Compare([Entry("A", end: new DateOnly(2026, 11, 15))], [Entry("A", end: new DateOnly(2026, 11, 15))]);

        Assert.Equal([new Ended("A", EndReason.EndDate)], changes.Ended);
    }

    [Theory]
    [InlineData(2026, 11, 10)] // дата минула ще до попереднього зрізу
    [InlineData(2026, 11, 18)] // завершується в день нового зрізу, ще не минула
    public void End_date_outside_the_interval_is_not_an_end(int year, int month, int day)
    {
        var end = new DateOnly(year, month, day);

        Assert.Empty(Compare([Entry("A", end: end)], [Entry("A", end: end)]).Ended);
    }

    [Fact]
    public void Record_already_closed_does_not_end_again()
    {
        var changes = Compare([Entry("A", status: 4, end: new DateOnly(2026, 11, 15))], [Entry("A", status: 4, end: new DateOnly(2026, 11, 15))]);

        Assert.Empty(changes.Ended);
    }

    [Fact]
    public void Record_missing_from_the_new_snapshot_is_gone_with_its_kind()
    {
        var changes = Compare([Entry("A"), Entry("B", direct: false)], [Entry("A")]);

        Assert.Equal([Entry("B", direct: false)], changes.Gone);
        Assert.Empty(changes.Added);
    }

    [Fact]
    public void Duplicate_ids_in_a_published_snapshot_do_not_break_the_comparison()
    {
        var changes = Compare([Entry("A"), Entry("A")], [Entry("A")]);

        Assert.Empty(changes.Added);
        Assert.Empty(changes.Gone);
    }
}
