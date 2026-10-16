using System.Text.Json;

namespace SalaReservas.Api;

public static class ReservationEndpoint
{
    public static async Task<IResult> Create(HttpRequest request, ReservationService service,
        CancellationToken cancellationToken)
    {
        if (!request.HasJsonContentType())
        {
            return InvalidRequest();
        }

        CreateReservationRequest? input;
        try
        {
            input = await request.ReadFromJsonAsync<CreateReservationRequest>(cancellationToken);
        }
        catch (JsonException)
        {
            return InvalidRequest();
        }

        if (input is null || string.IsNullOrWhiteSpace(input.RoomId)
            || string.IsNullOrWhiteSpace(input.Start) || string.IsNullOrWhiteSpace(input.End))
        {
            return InvalidRequest();
        }

        if (!InstantParser.TryParse(input.Start, out var start)
            || !InstantParser.TryParse(input.End, out var end))
        {
            return Problem(400, "invalid_instant", "Fecha inválida",
                "start y end deben ser fechas ISO 8601 válidas con segundos y zona explícita (Z o ±HH:mm).");
        }

        return service.Reserve(input.RoomId, start, end) switch
        {
            ReservationResult.Created created => Results.Json(created.Reservation, statusCode: 201),
            ReservationResult.Rejected rejected => rejected.Error switch
            {
                ReservationError.RoomNotFound => Problem(404, "room_not_found", "Sala inexistente",
                    "La sala solicitada no existe."),
                ReservationError.InvalidInterval => Problem(400, "invalid_interval", "Intervalo inválido",
                    "El fin debe ser estrictamente posterior al inicio."),
                ReservationError.StartInPast => Problem(400, "start_in_past", "Inicio pasado",
                    "El inicio no puede ser anterior al instante de evaluación."),
                ReservationError.Overlap => Problem(409, "reservation_overlap", "Reserva solapada",
                    "La sala ya tiene una reserva que se solapa con el intervalo solicitado."),
                _ => throw new InvalidOperationException("Unknown reservation error.")
            },
            _ => throw new InvalidOperationException("Unknown reservation result.")
        };
    }

    public static IResult Cancel(string id, ReservationService service)
    {
        if (!Guid.TryParse(id, out var reservationId))
        {
            return Problem(400, "invalid_reservation_id", "Identificador inválido",
                "El identificador de reserva debe ser un GUID válido.");
        }

        return service.Cancel(reservationId) switch
        {
            CancellationResult.Cancelled => Results.NoContent(),
            CancellationResult.NotFound => Problem(404, "reservation_not_found", "Reserva inexistente",
                "La reserva solicitada no existe."),
            CancellationResult.AlreadyStarted => Problem(409, "reservation_started", "Reserva iniciada",
                "No se puede cancelar una reserva activa cuyo inicio ya ha llegado."),
            _ => throw new InvalidOperationException("Unknown cancellation result.")
        };
    }

    private static IResult InvalidRequest() => Problem(400, "invalid_request", "Solicitud inválida",
        "Se requiere un objeto JSON con roomId, start y end como cadenas no vacías.");

    private static IResult Problem(int status, string code, string title, string detail) =>
        Results.Problem(statusCode: status, title: title, detail: detail,
            extensions: new Dictionary<string, object?> { ["code"] = code });
}

public sealed record CreateReservationRequest(string? RoomId, string? Start, string? End);
