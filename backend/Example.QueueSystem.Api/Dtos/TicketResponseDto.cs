namespace Example.QueueSystem.Api.Dtos;

public record TicketResponseDto(string TicketNumber, DateTimeOffset? IssuedAt);
