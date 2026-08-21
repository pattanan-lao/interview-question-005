namespace Example.QueueSystem.Domain;

public readonly record struct QueuePosition(int LetterIndex, int Digit);

public enum NextTicketOutcome
{
    Issued,
    Exhausted,
}

public readonly record struct NextTicketResult(NextTicketOutcome Outcome, QueuePosition? Position);

public static class TicketNumbering
{
    public const int MinLetterIndex = 0;
    public const int MaxLetterIndex = 25; // 'Z'
    public const int MinDigit = 0;
    public const int MaxDigit = 9;

    public static NextTicketResult Next(QueuePosition? current)
    {
        if (current is null)
        {
            return new NextTicketResult(NextTicketOutcome.Issued, new QueuePosition(MinLetterIndex, MinDigit));
        }

        var position = current.Value;

        if (position.Digit < MaxDigit)
        {
            return new NextTicketResult(NextTicketOutcome.Issued, position with { Digit = position.Digit + 1 });
        }

        if (position.LetterIndex < MaxLetterIndex)
        {
            return new NextTicketResult(NextTicketOutcome.Issued, new QueuePosition(position.LetterIndex + 1, MinDigit));
        }

        return new NextTicketResult(NextTicketOutcome.Exhausted, null);
    }

    public static string Format(QueuePosition? position)
    {
        if (position is null)
        {
            return "00";
        }

        var letter = (char)('A' + position.Value.LetterIndex);
        return $"{letter}{position.Value.Digit}";
    }
}
