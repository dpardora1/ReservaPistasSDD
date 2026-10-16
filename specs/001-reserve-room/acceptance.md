# 001 — Evidencia de aceptación

**Estado:** criterios del incremento verificados.
**Referencias:** [spec.md](spec.md), [plan.md](plan.md), [tasks.md](tasks.md).

## Trazabilidad HTTP

Pruebas en `tests/SalaReservas.Api.Tests/ReservationEndpointTests.cs`. Cada caso tiene host, estado y reloj aislados. Las tablas de InlineData distinguen escenarios dentro de un método parametrizado.

| Ejemplo | Regla | Prueba HTTP |
|---|---|---|
| A01 | Creación con identificador y datos | `ValidRequestReturns201WithStoredReservationAndUtcDates` |
| A02 | R05 | `R05_R06_IntervalAcceptanceThroughHttp`, escenario A02 |
| A03 | R05 | `R05_R06_IntervalAcceptanceThroughHttp`, escenarios A03 y A03-before |
| A04 | R05 | `R05_R06_IntervalAcceptanceThroughHttp`, escenario A04 |
| A05 | R06 | `R05_R06_IntervalAcceptanceThroughHttp`, escenario A05 |
| A06 | R03 | `RejectionsMapToStableHttpErrorsAndPreserveState`, filas invalid_interval |
| A07 | R04 | `RejectionsMapToStableHttpErrorsAndPreserveState`, fila start_in_past |
| A08 | R01 | `RejectionsMapToStableHttpErrorsAndPreserveState`, fila room_not_found |
| A09 | R02 | `RejectionsMapToStableHttpErrorsAndPreserveState`, ausencia de zona por separado en start y end |
| A10 | R07 | `R07_A10_ConcurrentHttpRequestsCreateExactlyOneReservation` |

## Límites y garantías adicionales

- `R05_R06_IntervalAcceptanceThroughHttp`: intervalos contenidos e idénticos, offsets equivalentes, adyacencia por ambos extremos, identificadores distintos y conservación de las reservas previas.
- `R03_R04_TimeBoundariesThroughHttp`: inicio igual a ahora y duración de un tick, inicio un tick anterior, duración de 50 años y antelación de 100 años. Verifica también los instantes devueltos.
- Pruebas de rechazos: comparan estado antes/después. No hay endpoint de inspección; se usa un snapshot interno copiado bajo bloqueo.
- `MalformedOrIncompleteJsonReturnsProblemDetailsWithoutMutation`: errores de transporte sin mutación.
- `RepeatedRequestsShareStateAndReturnConflict`: estado compartido entre peticiones sucesivas.
- Ninguna prueba exige prioridad de un motivo de rechazo sobre otro cuando fallan varias reglas.
- Las 49 pruebas de parser y 22 de servicio complementan la aceptación HTTP. La prueba de arranque sigue pasando.

## Concurrencia: alcance de la evidencia

Dos clientes HTTP esperan una señal común antes de enviar sus solicitudes. Se verifica exactamente un 201, un 409 con código reservation_overlap y una reserva almacenada coincidente con la respuesta aceptada.

Esto ejercita peticiones concurrentes, pero no garantiza un intercalado concreto del scheduler ni demuestra por sí solo ausencia de carreras. La revisión estructural de T03 confirma que comprobación e inserción comparten bloqueo y que el servicio es singleton. Garantía limitada a un proceso; no es una prueba de carga ni de despliegue distribuido.

## Ejecución final

```text
dotnet build SalaReservas.slnx --verbosity minimal
0 errores, 0 advertencias

dotnet test SalaReservas.slnx --no-build --verbosity minimal
113 superadas, 0 fallos, 0 omitidas
```

Las 13 pruebas nuevas pasaron sobre la implementación existente. Se corrigieron dos advertencias xUnit2031 de las pruebas antes de la ejecución final; no se cambió código de producción en T05.

## Comprobación con Kestrel real

Se ejecutó la DLL compilada mediante:

```text
dotnet src/SalaReservas.Api/bin/Debug/net10.0/SalaReservas.Api.dll --urls http://127.0.0.1:5217
```

Dos POST idénticos con sala A e intervalo `2026-10-17T10:00:00Z`–`2026-10-17T11:00:00Z`, futuro respecto al reloj real de la ejecución:

1. **201**: reserva con identificador y fechas UTC.
2. **409**: ProblemDetails con `code: reservation_overlap`.

Después se detuvo el proceso iniciado para esta prueba y se confirmó conexión rechazada en el puerto. La prueba usó curl contra el socket real, sin sustituir el reloj ni utilizar TestServer.

Para repetir en otra fecha, elegir un intervalo futuro y un proceso nuevo: las reservas se pierden al reiniciar, por diseño.

## Exclusiones que se mantienen

Sin persistencia, autenticación, cancelación, consultas ni gestión de salas. La funcionalidad satisface los criterios verificados de este ejercicio; no implica preparación para producción.
