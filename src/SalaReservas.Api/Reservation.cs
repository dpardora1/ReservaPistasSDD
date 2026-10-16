namespace SalaReservas.Api;

public sealed record Reservation(Guid Id, string RoomId, DateTimeOffset Start, DateTimeOffset End)
{
    internal bool IsCancelled { get; init; }
}

public enum ReservationError
{
    RoomNotFound,
    InvalidInterval,
    StartInPast,
    Overlap
}

public abstract record ReservationResult
{
    private ReservationResult() { }

    public sealed record Created(Reservation Reservation) : ReservationResult;
    public sealed record Rejected(ReservationError Error) : ReservationResult;
}
