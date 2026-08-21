using Example.QueueSystem.Domain;

namespace Example.QueueSystem.Domain.Tests;

public class TicketNumberingTests
{
    [Fact]
    public void Next_WhenNoCurrentTicket_ReturnsA0()
    {
        var result = TicketNumbering.Next(null);

        Assert.Equal(NextTicketOutcome.Issued, result.Outcome);
        Assert.Equal(new QueuePosition(0, 0), result.Position);
        Assert.Equal("A0", TicketNumbering.Format(result.Position));
    }

    [Fact]
    public void Next_IncrementsDigitWithinSameLetter()
    {
        var current = new QueuePosition(0, 3); // A3

        var result = TicketNumbering.Next(current);

        Assert.Equal(NextTicketOutcome.Issued, result.Outcome);
        Assert.Equal("A4", TicketNumbering.Format(result.Position));
    }

    [Fact]
    public void Next_WhenDigitIsNine_RollsOverToNextLetter()
    {
        var current = new QueuePosition(0, 9); // A9

        var result = TicketNumbering.Next(current);

        Assert.Equal(NextTicketOutcome.Issued, result.Outcome);
        Assert.Equal("B0", TicketNumbering.Format(result.Position));
    }

    [Fact]
    public void Next_WhenAtZ9_ReturnsExhausted()
    {
        var current = new QueuePosition(25, 9); // Z9

        var result = TicketNumbering.Next(current);

        Assert.Equal(NextTicketOutcome.Exhausted, result.Outcome);
        Assert.Null(result.Position);
    }

    [Fact]
    public void Format_WhenPositionIsNull_ReturnsZeroZero()
    {
        Assert.Equal("00", TicketNumbering.Format(null));
    }
}
