# Contrato vigente — reservas de salas

Referencia actual del producto. Consolida los incrementos históricos [001](001-reserve-room/spec.md) y [002](002-cancel-reservation/spec.md), que se conservan como historial. Un cambio de comportamiento debe actualizar esta referencia junto a su propuesta aprobada y pruebas; no basta con añadir una excepción en otro documento.

## Creación

| Procedencia | Regla vigente |
|---|---|
| 001/R01 | La sala debe existir: catálogo del ejercicio A y B, identificadores exactos. |
| 001/R02 | Inicio y fin deben representar instantes inequívocos con zona explícita. |
| 001/R03 | Fin estrictamente posterior al inicio. |
| 001/R04 | Inicio no anterior al instante de evaluación; igualdad válida. |
| 001/R05 + 002/C03 | No hay reservas **activas** solapadas para una sala. Intervalos [inicio, fin); adyacencia válida por ambos extremos. |
| 001/R06 | Las reservas de salas distintas son independientes. |
| 001/R07 + 002/C06 | Creaciones y cancelaciones concurrentes preservan exclusión de activas; comprobación e inserción atómicas en un proceso. |

Cada reserva nueva es activa, tiene identificador único y conserva sala e intervalo. No hay máximo de duración o antelación de negocio; rigen los límites representables por .NET. Rechazos no modifican reservas. No se garantiza precedencia entre errores múltiples de creación.

## Cancelación

| Procedencia | Regla vigente |
|---|---|
| 002/C01 | Identificador inexistente: rechazo sin cambios. |
| 002/C02 | Cancelar conserva id, sala e intervalo y deja estado cancelado. |
| 002/C03 | Canceladas no bloquean disponibilidad; no se borran sus datos. |
| 002/C04 | Repetir cancelación tiene éxito sin cambios, incluso después del inicio. |
| 002/C05 | Una activa solo se cancela antes del inicio; igualdad o pasado se rechazan. |
| 002/C06 | Ante creación/cancelación simultánea, crear puede encontrar conflicto o aceptar después de cancelar, nunca duplicar activas solapadas. |

Precedencia: inexistencia → ya cancelada → comprobación del reloj → transición. No hay restauración ni finalización anticipada.

## Contrato HTTP existente

De los planes aprobados [001](001-reserve-room/plan.md) y [002](002-cancel-reservation/plan.md):

POST /reservations, objeto JSON con roomId, start y end como cadenas no vacías. Fechas `AAAA-MM-DDTHH:mm:ss[.fffffff](Z|±HH:mm)`; segundos obligatorios y fracción opcional de 1–7 dígitos. Cultura invariante, sin inferir zona local. Éxito 201 con exactamente id, roomId, start, end; fechas UTC. Estado de cancelación no expuesto en esa respuesta.

DELETE /reservations/{id}, GUID validado explícitamente. Éxito 204 sin cuerpo para cancelación inicial o repetida.

| Operación | Error | HTTP/code |
|---|---|---|
| POST | JSON, campos o contenido no JSON inválidos | 400 / invalid_request |
| POST | Fecha inválida o sin zona | 400 / invalid_instant |
| POST | Intervalo inválido | 400 / invalid_interval |
| POST | Inicio pasado | 400 / start_in_past |
| POST | Sala inexistente | 404 / room_not_found |
| POST | Solapamiento activo | 409 / reservation_overlap |
| DELETE | Identificador malformado | 400 / invalid_reservation_id |
| DELETE | Identificador válido inexistente | 404 / reservation_not_found |
| DELETE | Activa iniciada | 409 / reservation_started |

Errores de estas operaciones usan application/problem+json, status, title, detail y extensión code. No se promete este formato para rutas ajenas; GET / responde 404 y no es un health endpoint.

## Límites deliberados

Un proceso, singleton y memoria volátil; sin autenticación, autorización, persistencia, consultas ni administración de salas. No es un servicio preparado para producción. Los nombres de solución/dominio siguen siendo salas aunque el repositorio se llame ReservaPistasSDD.

Evidencia histórica: [001](001-reserve-room/acceptance.md), [002](002-cancel-reservation/acceptance.md). Evidencia del candidato actual: ejecución CI y SHA enlazados en su PR, no los resultados históricos.
