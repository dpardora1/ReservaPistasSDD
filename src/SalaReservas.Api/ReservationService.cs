namespace SalaReservas.Api;

public sealed class ReservationService(TimeProvider timeProvider)
{
    private readonly Lock gate = new();
    private readonly List<Reservation> reservations = [];

    public ReservationResult Reserve(string roomId, DateTimeOffset start, DateTimeOffset end)
    {
        lock (gate)
        {
            if (roomId is not ("A" or "B"))
            {
                return new ReservationResult.Rejected(ReservationError.RoomNotFound);
            }

            if (end <= start)
            {
                return new ReservationResult.Rejected(ReservationError.InvalidInterval);
            }

            var now = timeProvider.GetUtcNow();
            if (start < now)
            {
                return new ReservationResult.Rejected(ReservationError.StartInPast);
            }

            if (reservations.Any(existing => !existing.IsCancelled && existing.RoomId == roomId
                && start < existing.End && existing.Start < end))
            {
                return new ReservationResult.Rejected(ReservationError.Overlap);
            }

            var reservation = new Reservation(Guid.NewGuid(), roomId, start.ToUniversalTime(), end.ToUniversalTime());
            reservations.Add(reservation);
            return new ReservationResult.Created(reservation);
        }
    }

    public CancellationResult Cancel(Guid id)
    {
        lock (gate)
        {
            var index = reservations.FindIndex(reservation => reservation.Id == id);
            if (index < 0)
            {
                return CancellationResult.NotFound;
            }

            var reservation = reservations[index];
            if (reservation.IsCancelled)
            {
                return CancellationResult.Cancelled;
            }

            var now = timeProvider.GetUtcNow();
            if (reservation.Start <= now)
            {
                return CancellationResult.AlreadyStarted;
            }

            reservations[index] = reservation with { IsCancelled = true };
            return CancellationResult.Cancelled;
        }
    }

    internal Reservation[] GetSnapshot()
    {
        lock (gate)
        {
            return reservations.ToArray();
        }
    }
}
