# 001 — Reservar una sala

**Estado:** aprobado

## Objetivo

Permitir reservar una sala durante un intervalo y evitar que una misma sala tenga reservas solapadas.

## Alcance

Esta funcionalidad permite crear una reserva sobre una sala existente.

No incluye:
- Cancelación o modificación de reservas.
- Reservas recurrentes.
- Gestión del catálogo de salas.
- Autenticación, permisos o identificación de quien reserva.
- Consulta de disponibilidad.
- Persistencia entre reinicios.

## Datos de entrada y resultado

La solicitud contiene:
- Identificador de sala.
- Instante de inicio.
- Instante de fin.

Cuando se acepta, se devuelve un identificador único de reserva y los datos de la reserva creada.

Cuando se rechaza, se informa del motivo y no se crea ninguna reserva.

## Reglas de negocio

| ID | Regla |
|---|---|
| R01 | La sala debe existir en el catálogo disponible. |
| R02 | Inicio y fin deben identificar instantes inequívocos: deben incluir UTC o un desplazamiento horario explícito. |
| R03 | El fin debe ser estrictamente posterior al inicio. |
| R04 | El inicio no puede ser anterior al instante en que el sistema evalúa la solicitud. |
| R05 | No puede haber reservas solapadas para la misma sala. Los intervalos se interpretan como `[inicio, fin)`: se permite que una reserva empiece exactamente cuando termina otra. |
| R06 | Las reservas de salas diferentes no interfieren entre sí. |
| R07 | Ante solicitudes simultáneas que compiten por un mismo intervalo libre, solo una puede aceptarse. |

No se impone una duración máxima ni una antelación máxima en esta versión.

## Ejemplos de aceptación

Salvo indicación contraria, la sala existe y los intervalos están en el futuro. Los horarios de cada ejemplo corresponden al mismo día y están expresados en UTC.

| ID | Situación y solicitud | Resultado esperado |
|---|---|---|
| A01 | Sala libre; reservar 10:00–11:00. | Reserva aceptada con identificador. |
| A02 | Existe 10:00–11:00; solicitar 10:30–11:30 en la misma sala. | Rechazo por solapamiento. |
| A03 | Existe 10:00–11:00; solicitar 11:00–12:00 en la misma sala. | Reserva aceptada. |
| A04 | Existe 10:00–11:00; solicitar 09:00–12:00 en la misma sala. | Rechazo por solapamiento. |
| A05 | Existe 10:00–11:00 en A; solicitar ese intervalo en B, que está libre. | Reserva aceptada. |
| A06 | Solicitar una reserva con inicio igual o posterior al fin. | Rechazo por intervalo inválido. |
| A07 | Solicitar una reserva cuyo inicio está en el pasado. | Rechazo por inicio pasado. |
| A08 | Solicitar una reserva para una sala inexistente. | Rechazo por sala inexistente. |
| A09 | Solicitar una reserva sin zona o desplazamiento horario explícito. | Rechazo por fecha ambigua. |
| A10 | Dos solicitudes simultáneas para la misma sala e intervalo libre. | Exactamente una aceptada; la otra rechazada por solapamiento. |

En todos los rechazos, las reservas existentes permanecen sin cambios.

Si una solicitud incumple varias reglas, esta versión no garantiza qué motivo se comunica primero.
