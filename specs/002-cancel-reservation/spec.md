# 002 — Cancelar una reserva

**Estado:** aprobado.
**Base:** [001 — Reservar una sala](../001-reserve-room/spec.md).

## Objetivo

Permitir cancelar una reserva y liberar su intervalo para nuevas reservas de la misma sala.

## Alcance y evolución del contrato

Este incremento incorpora la cancelación, excluida del alcance de 001. La especificación de 001 se conserva como contrato histórico de ese incremento.

En el comportamiento conjunto, R05 de 001 se interpreta como: no puede haber reservas **activas** solapadas para una misma sala. Una nueva reserva se crea activa. Las reservas canceladas conservan sus datos, pero no bloquean disponibilidad. El resto de las reglas de creación sigue vigente.

## Entrada y resultado

La solicitud identifica la reserva que se quiere cancelar.

El resultado informa éxito o el motivo de rechazo. Los rechazos no alteran el estado. No se decide aquí el verbo HTTP ni la representación de transporte.

## Reglas de negocio

| ID | Regla |
|---|---|
| C01 | La reserva debe existir; si no existe, se rechaza la solicitud indicando ese motivo. |
| C02 | Cancelar una reserva activa la deja en estado cancelada, conservando su identificador y datos originales. |
| C03 | Las reservas canceladas no bloquean nuevas reservas. |
| C04 | Cancelar de nuevo una reserva ya cancelada se considera éxito y no produce cambios adicionales. |
| C05 | Solo se puede cancelar una reserva activa cuyo inicio sea posterior al instante de evaluación. Si ya ha empezado —incluida la igualdad con ahora—, se rechaza sin cambios. |
| C06 | Cancelación y creación deben mantener la exclusión de reservas activas solapadas, también ante solicitudes simultáneas. |

Precedencia explícita: para una reserva ya cancelada se aplica C04 aunque haya pasado su fecha de inicio. Un reintento no cambia de resultado por el paso del tiempo.

## Ejemplos de aceptación

Salvo indicación contraria, el reloj está fijado a las 08:00; horarios del mismo día, expresados en UTC. Las reservas mencionadas existen antes de evaluar la cancelación.

| ID | Situación | Resultado |
|---|---|---|
| B01 | Cancelar una reserva de 10:00–11:00. | Éxito; queda cancelada. |
| B02 | Después de B01, reservar la misma sala de 10:00–11:00. | Nueva reserva aceptada. |
| B03 | Cancelar un identificador inexistente. | Rechazo; estado sin cambios. |
| B04 | Cancelar otra vez la reserva de B01. | Éxito; estado sin cambios. |
| B05 | Cancelar una reserva activa cuyo inicio es exactamente a las 08:00 o anterior. | Rechazo; estado sin cambios. |
| B06 | Cancelar una reserva mientras otra petición intenta ocupar su intervalo. | La nueva petición puede encontrar conflicto o reservar si la cancelación ya se aplicó; nunca quedan dos reservas activas solapadas. |

B04 también debe mantenerse al avanzar el reloj más allá del inicio de la reserva cancelada.

## Fuera de alcance

Sin autenticación, persistencia ni consultas. No se añaden restauración de reservas canceladas, cancelaciones masivas, modificación de horarios ni finalización anticipada de reservas en curso.

Se mantiene el límite técnico del ejercicio: un único proceso con estado en memoria.
