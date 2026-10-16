# 002 — Tareas de cancelación

**Estado:** T01–T03 completadas. Evidencia en [acceptance.md](acceptance.md).
**Referencias:** [spec.md](spec.md), [plan.md](plan.md).

- [x] **T01 — Estado y cancelación en el servicio.**
  - Añadir estado interno, sin alterar el contrato HTTP de creación.
  - Cancelar bajo el bloqueo compartido con Reserve; preservar datos y omitir canceladas en conflictos.
  - Probar C01–C06, límites del reloj, repetición tras inicio, conservación de estado y concurrencia a nivel de servicio.
  - Finalización: pruebas nuevas y las 113 existentes superadas; compilación limpia y revisión de atomicidad.
- [x] **T02 — Endpoint de cancelación.**
  - DELETE /reservations/{id}, 204/400/404/409 y ProblemDetails.
  - Probar formato de identificador, resultados HTTP y ausencia de mutación en rechazos.
  - Finalización: contrato HTTP verificado sin duplicar reglas del servicio.
- [x] **T03 — Aceptación y regresión.**
  - Matriz B01–B06, concurrencia HTTP y regresión de creación.
  - Compilación y suite completas; servidor real para crear, cancelar, repetir y volver a reservar; detener servidor.
  - Finalización: evidencias registradas y retrospectiva, sin ampliar alcance.

## Registro

### T01 — Resultados

- Plan y reglas aprobados; implementada solo T01, sin endpoint DELETE.
- Añadido IsCancelled interno e inmutable al registro Reservation. No se serializa como parte de la respuesta HTTP; prueba explícita verifica los cuatro campos originales.
- Cancel devuelve Cancelled, NotFound o AlreadyStarted. Conserva id, sala e intervalo mediante una copia del registro; Reserve omite canceladas al buscar conflictos.
- C01/B03: identificador inexistente conserva estado.
- C02/B01: cancelación conserva datos y otras reservas; snapshots anteriores permanecen inmutables.
- C03/B02: mismo intervalo puede reservarse de nuevo con otro id; repetir cancelación del id original no cancela su reemplazo.
- C04/B04: repetición antes y después del inicio, sin consultar el reloj ni cambiar estado.
- C05/B05: rechazo en igualdad, un tick después y tras finalización; éxito un tick antes. Lectura única del reloj cuando corresponde.
- C06/B06: carrera coordinada de creación/cancelación en el servicio; admite éxito o conflicto de creación según el orden y verifica el estado final. No demuestra un intercalado específico del scheduler.
- Revisión estructural: búsqueda, lectura temporal y sustitución comparten el bloqueo de Reserve y GetSnapshot; no hay exposición mutable ni operaciones await dentro de la sección crítica. Garantía limitada al singleton de un proceso.
- Fase roja: 10 fallos nuevos y 114 éxitos (113 originales y prueba de contrato POST).
- Verificación final: dotnet build correcto, 0 errores y 0 advertencias; dotnet test, 124 superadas, 0 fallos y 0 omitidas. No se modificaron expectativas de las 113 pruebas originales.
- Aceptación HTTP de cancelación y servidor real pendientes de T02/T03.

### Retrospectiva T01

- **Observación:** el cambio de estado afecta al cálculo de disponibilidad, pero no requiere ampliar la respuesta de creación.
- **Aprendizaje:** separar estado interno y contrato externo permite evolucionar el comportamiento sin cambios de transporte accidentales.
- **Ajuste:** comprobar explícitamente la forma del JSON en evoluciones del modelo y conservar la precedencia idempotencia → reloj al conectar el endpoint.
- **Destino:** seguimiento del incremento, sin duplicar requisitos en memoria ni modificar el contrato histórico 001.

### T02 — Resultados

- Añadidas primero 13 pruebas HTTP en CancellationEndpointTests con reloj controlado y host aislado por caso.
- Fase roja: 13 fallos nuevos y 124 pruebas anteriores superadas.
- Implementada ruta DELETE /reservations/{id} sin restricción de GUID en routing: parseo explícito con Guid.TryParse para devolver 400 invalid_reservation_id en ProblemDetails.
- Reutilizado Cancel del singleton y el constructor existente de ProblemDetails. Sin cambios en las reglas del servicio.
- Cancelación y repetición devuelven 204 sin cuerpo; inexistente devuelve 404 reservation_not_found; activa iniciada devuelve 409 reservation_started.
- Comprobados datos conservados, estado sin cambios ante rechazos, GUID vacío bien formado pero inexistente, repetición después del inicio, igualdad y límites de un tick.
- Flujo POST → DELETE → POST acepta reemplazo; repetir DELETE del id original deja el reemplazo intacto.
- Verificación: dotnet build, 0 errores y 0 advertencias; dotnet test, 137 superadas, 0 fallos y 0 omitidas.
- Pruebas mediante TestServer; concurrencia HTTP de cancelación y comprobación Kestrel real permanecen en T03.

### Retrospectiva T02

- **Observación:** una restricción {id:guid} impediría controlar el error de identificador malformado desde el handler.
- **Aprendizaje:** routing y binding forman parte del contrato observable de errores, no son solo detalles internos.
- **Ajuste:** probar identificadores malformados y válidos pero inexistentes por separado cuando el contrato distingue 400 y 404.
- **Destino:** registro del incremento; se conserva la especificación aprobada y no se duplica lógica de negocio en HTTP.

### T03 — Resultados

- Añadida carrera HTTP POST/DELETE con señal de salida común, admitiendo ambos órdenes seriales de B06 y verificando estado final. Si POST encuentra conflicto, un reintento posterior crea la reserva.
- Añadidos dos DELETE concurrentes: ambos 204, una sola reserva cancelada.
- Verificación final: build con 0 errores y 0 advertencias; 139 pruebas superadas, 0 fallos y 0 omitidas. No hubo cambios de producción en T03.
- Matriz B01–B06 y límites de evidencia en acceptance.md.
- Kestrel real en 127.0.0.1:5217: crear 201 → cancelar 204 → repetir 204 → reemplazar 201 con otro id → repetir cancelación antigua 204 → conflicto 409 al intentar ocupar el reemplazo.
- Servidor detenido; conexión rechazada confirmada tras el cierre.

### Retrospectiva T03

- **Observación:** el resultado de POST durante una cancelación depende del orden de aplicación, y ambos resultados permitidos mantienen la regla.
- **Aprendizaje:** una prueba concurrente debe verificar resultados permitidos e invariantes, no imponer un ganador accidental del scheduler.
- **Ajuste:** para futuras operaciones concurrentes, especificar primero los órdenes válidos y comprobar estado además de códigos HTTP.
- **Destino:** contrato B06 y evidencia en acceptance.md; no se afirma cobertura de todos los intercalados ni garantía distribuida.
