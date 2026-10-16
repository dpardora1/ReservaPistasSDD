# 001 — Plan técnico: reservar una sala

**Estado:** implementado y verificado por incrementos T01–T05. Evidencia en [acceptance.md](acceptance.md).
**Contrato de comportamiento:** [spec.md](spec.md).
**Trabajo desglosado:** [tasks.md](tasks.md).

## Objetivo y límites

Implementar R01–R07 y verificar A01–A10 con una Minimal API .NET 10. El ejercicio busca practicar SDD por incrementos, no diseñar una arquitectura de producción.

No se amplía el alcance de la especificación. No se incluyen persistencia, autenticación, cancelación, consultas ni gestión de salas. La aprobación de este plan permite documentar plan y tareas; no implica generar toda la aplicación de una vez.

## Estructura

- SDK 10.0.204 fijado mediante `global.json`.
- Solución `SalaReservas.slnx`.
- `src/SalaReservas.Api`: Minimal API `net10.0`, contratos HTTP, parser de instantes y servicio de reservas.
- `tests/SalaReservas.Api.Tests`: xUnit, pruebas del parser y servicio, aceptación HTTP mediante `WebApplicationFactory`.

Sin CQRS, MediatR, repositorios genéricos ni proyectos de capas adicionales. No existe código previo que reutilizar.

## Decisiones técnicas

### Estado y atomicidad — R01, R03–R07

Un servicio singleton encapsula el catálogo fijo de salas A y B y las reservas en memoria. Un único bloqueo protege toda operación sobre las reservas.

Tras validar y convertir la entrada de transporte, la creación ejecuta dentro del bloqueo:

1. Validación de sala e intervalo.
2. Lectura única de `TimeProvider.GetUtcNow()` y validación del inicio.
3. Comprobación de solapamientos para la sala solicitada.
4. Generación de identificador e inserción, únicamente si todo lo anterior se cumple.

No hay operaciones asíncronas dentro del bloqueo. Un rechazo no modifica reservas. La comprobación y la inserción no pueden separarse en operaciones públicas independientes.

La condición de solapamiento es:

```text
nuevoInicio < existenteFin && existenteInicio < nuevoFin
```

Permite reservas contiguas por ambos extremos. Las comparaciones se realizan sobre instantes UTC y solo entre reservas de la misma sala.

Esta garantía cubre un único proceso: varias instancias tendrían estados y bloqueos independientes.

### Tiempo y formato de transporte — R02–R04

`start` y `end` se reciben como cadenas, no mediante binding directo a `DateTimeOffset`. Se comprueba explícitamente que la zona esté presente y que la fecha completa sea válida con cultura invariante.

Formato admitido:

```text
AAAA-MM-DDTHH:mm:ss[.fffffff](Z|±HH:mm)
```

Los segundos son obligatorios; la fracción es opcional, de uno a siete dígitos. Se exige `T` como separador y `Z` o desplazamiento explícito. Ejemplos válidos:

```text
2030-06-01T10:00:00Z
2030-06-01T12:00:00+02:00
2030-06-01T10:00:00.1234567Z
```

Los ejemplos muestran la sintaxis; que un intervalo sea futuro se evalúa por separado. No se infiere la zona local ni basta con comprobar el sufijo. Tras parsear, se normaliza a UTC para comparar y responder.

Se inyecta `TimeProvider`: reloj del sistema en ejecución y reloj controlado en pruebas. El instante de evaluación se captura una vez dentro del bloqueo. Inicio igual a ese instante es válido; un inicio anterior no.

No se añaden máximos de duración o antelación. El rango y precisión representables por .NET son límites técnicos.

### Contrato HTTP

`POST /reservations`, con cuerpo JSON:

```json
{
  "roomId": "A",
  "start": "2030-06-01T10:00:00Z",
  "end": "2030-06-01T11:00:00Z"
}
```

`roomId`, `start` y `end` son obligatorios y deben ser cadenas no vacías. El catálogo contiene identificadores exactos `A` y `B`.

| Resultado | Estado | Código de error |
|---|---|---|
| Reserva creada | 201 | — |
| JSON o campos inválidos | 400 | `invalid_request` |
| Fecha sin zona o formato inválido | 400 | `invalid_instant` |
| Fin igual o anterior al inicio | 400 | `invalid_interval` |
| Inicio pasado | 400 | `start_in_past` |
| Sala inexistente | 404 | `room_not_found` |
| Solapamiento | 409 | `reservation_overlap` |

Éxito: respuesta con `id`, `roomId`, `start` y `end`; identificador único y fechas UTC. No se inventa un endpoint GET para añadir una cabecera Location.

Rechazos: ProblemDetails con extensión `code` estable y explicación del motivo. Se cubren explícitamente los errores de JSON y campos ausentes, sin depender de que el binding devuelva ese formato por defecto. No se garantiza qué error se informa primero si fallan varias reglas.

## Estrategia de verificación

| Regla o resultado | Ejemplos | Cobertura adicional |
|---|---|---|
| Creación con identificador y datos | A01 | Identificadores distintos entre reservas aceptadas |
| R01 | A08 | Rechazo sin mutación |
| R02 | A09 | Ausencia de zona en cada fecha, formatos inválidos, offsets equivalentes |
| R03 | A06 | Inicio igual o posterior al fin; intervalo de un tick |
| R04 | A07 | Inicio igual a ahora; un tick anterior |
| R05 | A02–A04 | Adyacencia por ambos extremos, intervalo idéntico y contenido |
| R06 | A05 | Mismo intervalo en salas diferentes |
| R07 | A10 | Un éxito, un conflicto y una sola inserción |

- Pruebas focalizadas del parser y del servicio; A01–A10 también a nivel HTTP.
- Reloj fijo y estado aislado por escenario, sin dependencia de la hora real.
- Prueba concurrente con arranque coordinado, sin `Thread.Sleep`; inspección del estado interno mediante un mecanismo seguro de pruebas, sin exponer un endpoint de consulta.
- Revisión del bloqueo además de la prueba: una ejecución favorable no demuestra por sí sola atomicidad.
- Conservación del estado ante todos los rechazos y ejemplos con duraciones y antelaciones grandes.
- Ejecutar `dotnet build` y `dotnet test` sobre la solución cuando exista.
- Al finalizar, arrancar localmente la API y enviar dos POST del mismo intervalo futuro: esperar 201 y 409. Detener el proceso al terminar.

## Límites y mantenimiento

Memoria volátil, crecimiento sin límite, búsqueda lineal y bloqueo global son concesiones deliberadas del ejercicio.

La especificación no se cambia para justificar código existente. Si aparece una ambigüedad de negocio, se resuelve antes de modificarla. Las decisiones técnicas pueden evolucionar mientras sigan cumpliendo el contrato.
