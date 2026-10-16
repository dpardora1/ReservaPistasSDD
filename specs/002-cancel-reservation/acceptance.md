# 002 — Evidencia de aceptación de cancelación

**Estado:** criterios del incremento verificados.
**Referencias:** [spec.md](spec.md), [plan.md](plan.md), [tasks.md](tasks.md).

## Trazabilidad

Pruebas HTTP en `tests/SalaReservas.Api.Tests/CancellationEndpointTests.cs`, con estado y reloj aislados por caso.

| Ejemplo | Regla | Prueba |
|---|---|---|
| B01 | C02 | C02_B01_DeleteReturns204AndPreservesReservationData |
| B02 | C03 | C03_B02_CancelAllowsReplacementAndOldIdCannotCancelIt |
| B03 | C01 | InvalidOrUnknownIdReturnsProblemWithoutChangingState, GUIDs válidos inexistentes |
| B04 | C04 | C04_B04_RepeatedDeleteReturns204EvenAfterStart, antes/después del inicio |
| B05 | C05 | C05_B05_StartedReservationReturns409WithoutChangingState, igualdad/un tick posterior/tras finalización |
| B06 | C06 | C06_B06_ConcurrentCreateAndCancelRespectEitherSerialOrder |

Cobertura adicional: éxito un tick antes del inicio; identificadores malformados frente a inexistentes; 204 sin cuerpo; errores ProblemDetails con códigos estables; dos DELETE concurrentes exitosos; conservación del reemplazo al repetir cancelación del id antiguo.

Las pruebas del servicio en CancellationServiceTests verifican conservación de datos y snapshots, reloj y bloqueo a ese nivel. CreationHttpContractDoesNotExposeInternalState protege los cuatro campos originales del POST. Las 113 pruebas anteriores al incremento siguen pasando sin cambios de expectativas.

## Concurrencia

B06 usa dos clientes que esperan una señal común. DELETE debe producir 204; POST puede producir 201 si observa la cancelación o 409 reservation_overlap si observa aún la reserva activa. Se comprueba el original cancelado y ninguna duplicación activa. Si POST obtuvo conflicto, un reintento posterior debe crear la reserva.

Dos DELETE simultáneos devuelven 204 y dejan un único registro cancelado. Las pruebas coordinan salida, pero no fuerzan todos los intercalados posibles del scheduler. Se complementan con la revisión estructural del mismo bloqueo alrededor de Reserve, Cancel y GetSnapshot. No son pruebas de carga ni garantías entre procesos.

## Ejecución automatizada

```text
dotnet build SalaReservas.slnx --verbosity minimal
0 errores, 0 advertencias

dotnet test SalaReservas.slnx --no-build --verbosity minimal
139 superadas, 0 fallos, 0 omitidas
```

T03 añadió dos pruebas HTTP de concurrencia que pasaron sobre la implementación existente. No se modificó producción ni las reglas aprobadas.

## Servidor real

Arranque:

```text
dotnet src/SalaReservas.Api/bin/Debug/net10.0/SalaReservas.Api.dll --urls http://127.0.0.1:5217
```

Con curl, reloj real e intervalo futuro de sala A `2026-10-17T10:00:00Z`–`2026-10-17T11:00:00Z`:

1. POST: 201 con identificador original.
2. DELETE del original: 204 sin cuerpo.
3. Repetir DELETE: 204 sin cuerpo.
4. POST mismo intervalo: 201 con otro identificador.
5. Repetir DELETE del original: 204.
6. POST mismo intervalo: 409 reservation_overlap; el reemplazo sigue bloqueando disponibilidad.

Se detuvo el proceso creado para aceptación y se confirmó conexión rechazada en el puerto. Para repetir, arrancar un proceso nuevo y escoger fechas futuras. Los límites temporales y la carrera concurrente se verificaron con pruebas automatizadas, no con esperas en el servidor real.

## Límites

Sin persistencia, autenticación, consultas, restauración ni finalización anticipada. El estado se pierde al reiniciar. La exclusión depende de un único servicio singleton en un único proceso; no es una certificación de preparación para producción.
