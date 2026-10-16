# 002 — Plan técnico de cancelación

**Estado:** implementado y verificado en T01–T03. Evidencia en [acceptance.md](acceptance.md).
**Contrato:** [spec.md](spec.md). **Tareas:** [tasks.md](tasks.md).

## Diseño

- Reutilizar ReservationService singleton, TimeProvider y su bloqueo. No añadir repositorios ni almacenamiento externo.
- Representar internamente el estado activa/cancelada conservando id, sala e intervalo. Mantener la respuesta POST con sus cuatro campos actuales, sin exponer el estado interno.
- Reserve debe ignorar reservas canceladas al comprobar solapamientos.
- Cancel(id) usa el mismo bloqueo que Reserve: buscar; rechazar si no existe; éxito inmediato si ya está cancelada; leer reloj una vez; rechazar si Start <= ahora; sustituir la reserva por una copia cancelada.
- El orden de estas comprobaciones preserva C04 después del inicio. Los rechazos no alteran el estado.
- Snapshot interno copiado bajo bloqueo para pruebas; sin endpoint de consulta.
- Atomicidad limitada a un proceso. En carrera creación/cancelación, se admiten ambos órdenes seriales: conflicto antes de cancelar o éxito después. Nunca dos reservas activas solapadas.

## HTTP (T02, no T01)

DELETE /reservations/{id}:

| Resultado | HTTP | Código |
|---|---|---|
| Cancelada o ya cancelada | 204 | — |
| Identificador malformado | 400 | invalid_reservation_id |
| Inexistente | 404 | reservation_not_found |
| Activa iniciada | 409 | reservation_started |

Errores ProblemDetails. Parseo explícito del identificador para controlar el formato de error. DELETE no elimina los datos históricos.

## Archivos principales

- src/SalaReservas.Api/Reservation.cs: estado interno inmutable.
- src/SalaReservas.Api/ReservationService.cs: cancelar e ignorar canceladas al reservar.
- src/SalaReservas.Api/CancellationResult.cs: resultado de cancelación independiente de HTTP.
- tests/SalaReservas.Api.Tests/CancellationServiceTests.cs: reglas y límites con reloj controlado.
- En T02: ReservationEndpoint.cs, Program.cs y pruebas HTTP.

## Verificación

Mantener las 113 pruebas existentes sin cambiar sus expectativas. Añadir pruebas C01–C06/B01–B06 al nivel apropiado, conservación de datos/estado, repetición tras inicio, inicio igual a ahora y un tick posterior, y carreras coordinadas sin sleeps. Comprobar explícitamente que el POST conserva sus campos.

T01 verifica servicio; T02 verifica transporte; T03 completa matriz de aceptación y prueba con Kestrel real. Compilar y ejecutar suite completa en cada incremento. Una prueba concurrente favorable complementa, pero no sustituye, la revisión del bloqueo compartido.
