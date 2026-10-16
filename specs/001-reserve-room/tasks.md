# 001 — Tareas: reservar una sala

**Estado:** T01–T05 completadas. Evidencia en [acceptance.md](acceptance.md).
**Referencias:** [spec.md](spec.md) · [plan.md](plan.md).

## Forma de trabajo

Avanzar por incrementos explicados y revisados, no generar toda la aplicación de una vez. Para cada tarea: identificar reglas relevantes, escribir las pruebas aplicables, implementar lo mínimo, ejecutar la verificación y comunicar resultados.

Usaremos pruebas primero como técnica del ejercicio, no como una obligación universal de SDD. Una tarea solo se marca completada cuando se verifica su criterio de finalización; los fallos o comprobaciones omitidas se registran explícitamente.

## Tareas

- [x] **T01 — Crear el esqueleto verificable.**
  - Crear `global.json` con SDK 10.0.204 y `SalaReservas.slnx`.
  - Crear `src/SalaReservas.Api` y `tests/SalaReservas.Api.Tests`, ambos `net10.0`.
  - Configurar xUnit, referencia a la API y soporte de `WebApplicationFactory`.
  - Añadir una prueba de arranque del host, sin endpoints de negocio ni respuestas ficticias.
  - **Trazabilidad:** infraestructura para verificar A01–A10; todavía no satisface reglas de negocio.
  - **Finalización:** `dotnet build` correcto y prueba de arranque descubierta y superada con `dotnet test`.

- [x] **T02 — Implementar el contrato temporal.**
  - Escribir pruebas del formato documentado: UTC, offsets y fracciones; fechas sin zona en ambos campos, fechas y offsets inválidos.
  - Implementar el parser con cultura invariante y sin inferencia de zona local.
  - Comprobar equivalencia de instantes con offsets distintos.
  - **Trazabilidad:** R02, A09.
  - **Finalización:** pruebas del parser superadas; no depende de la zona horaria del equipo.

- [x] **T03 — Implementar las reglas y la operación atómica.**
  - Incorporar catálogo fijo A/B y servicio singleton en memoria.
  - Inyectar `TimeProvider`; usar reloj controlado en pruebas.
  - Validar sala, intervalo e inicio; comprobar solapamiento e insertar dentro de un único bloqueo.
  - Verificar los límites temporales y la independencia entre salas.
  - Añadir prueba de concurrencia coordinada y comprobar una sola inserción.
  - Comprobar que los rechazos preservan el estado.
  - **Trazabilidad:** R01, R03–R07; A01–A08 y A10 a nivel de servicio.
  - **Finalización:** pruebas del servicio superadas y revisión de atomicidad realizada.

- [x] **T04 — Exponer el contrato HTTP.**
  - Implementar `POST /reservations` con campos `roomId`, `start` y `end`.
  - Integrar parser y servicio; responder 201 con identificador y datos.
  - Traducir rechazos a 400, 404 o 409 con ProblemDetails y códigos estables.
  - Cubrir JSON inválido, campos ausentes, nulos y de tipo incorrecto.
  - No añadir rutas de consulta ni funcionalidades fuera de alcance.
  - **Trazabilidad:** entrada, resultados y rechazos de la especificación; contrato de transporte del plan.
  - **Finalización:** pruebas de contrato HTTP superadas, incluidos errores de binding.

- [x] **T05 — Completar aceptación y revisión de trazabilidad.**
  - Verificar A01–A10 mediante `WebApplicationFactory`, con estado y reloj aislados.
  - Completar adyacencia por ambos extremos, intervalos idénticos y contenidos, igualdad con ahora y un tick anterior.
  - Cubrir offsets equivalentes y ejemplos sin máximos artificiales de duración o antelación.
  - Comprobar estado final y conservación de reservas ante rechazos, sin endpoint de inspección.
  - Revisar que cada regla tiene pruebas y que no se exige precedencia entre errores múltiples.
  - Ejecutar compilación y suite completa.
  - Arrancar la API local y comprobar 201 seguido de 409 para el mismo intervalo futuro; detenerla al terminar.
  - **Trazabilidad:** R01–R07 y A01–A10.
  - **Finalización:** criterios verificados, resultados comunicados y límites de un único proceso documentados.

## Registro de ejecución

### T01 — Base verificable

- Creada solución con API y pruebas en `net10.0`, SDK fijado en 10.0.204.
- Configurados xUnit y `Microsoft.AspNetCore.Mvc.Testing` 10.0.13 con referencia a la API.
- Eliminada la prueba vacía de plantilla; añadida `HostStartupTests.HostStartsAndRespondsToHttpRequests`.
- Primera ejecución: 1 fallo, esperado 404 y obtenido 200 por el endpoint Hello World de la plantilla.
- Retirado ese endpoint; `Program` público y parcial permite usar `WebApplicationFactory<Program>`.
- Verificación final: `dotnet build SalaReservas.slnx --verbosity minimal` correcto, 0 errores y 0 advertencias.
- `dotnet test SalaReservas.slnx --no-build --verbosity minimal`: 1 prueba superada, 0 fallos y 0 omitidas.
- El host se ejecutó en el servidor de pruebas en memoria; no se arrancó un proceso Kestrel externo. La comprobación manual de la API de reservas corresponde a T05.
- No se han implementado reglas de negocio ni se han iniciado T02–T05.

### Retrospectiva T01

- **Observación:** las plantillas añadieron un endpoint de demostración y una prueba vacía que no formaban parte del entregable.
- **Aprendizaje:** generar un proyecto no demuestra que cumpla el alcance de la tarea; hay que revisar también el código de plantilla.
- **Ajuste:** en futuros incrementos que utilicen scaffolding, revisar los artefactos generados contra el criterio de finalización y sustituir pruebas vacías por comprobaciones observables.
- **Destino:** registro del proceso en este documento. No se modifica la especificación ni se duplica este resultado en la memoria del asistente.

### T02 — Contrato temporal

- Añadidas primero las pruebas `InstantParserTests`, trazadas a R02 y A09.
- Primera ejecución: compilación fallida con CS0103 porque `InstantParser` todavía no existía; las pruebas no llegaron a ejecutarse en esa fase.
- Implementado `InstantParser.TryParse`: validación de sintaxis completa, parseo exacto con cultura invariante, offset obligatorio y normalización UTC. No se consulta el reloj ni se infiere la zona local.
- Cubiertos offsets positivos y negativos (incluidos cambios de día y ±14:00), fracciones de uno a siete dígitos, precisión de un tick, fechas de calendario válidas e inválidas, rango UTC, zona ausente, espacios y sufijos extra.
- Comprobado el resultado bajo culturas es-ES, en-US y ar-SA, restaurando la cultura después de cada prueba. No se cambió la zona horaria del sistema; la independencia de zona se apoya en el offset obligatorio y el parseo exacto.
- Verificación: `dotnet build SalaReservas.slnx --verbosity minimal` correcto, 0 errores y 0 advertencias.
- `dotnet test SalaReservas.slnx --no-build --verbosity minimal`: 50 pruebas superadas (49 del parser y 1 de arranque), 0 fallos y 0 omitidas.
- R02/A09 quedan cubiertos a nivel de parser, no todavía de aceptación HTTP. Probar ausencia de zona por separado en `start` y `end` requiere la integración de T04 y la aceptación de T05.
- No se implementaron reglas de intervalo, pasado, catálogo, reservas ni endpoints.

### Retrospectiva T02

- **Observación:** R02 requiere instantes inequívocos; el plan concreta además una sintaxis de transporte. Las pruebas distinguen ambas responsabilidades y comprueban calendario, precisión y normalización.
- **Aprendizaje:** completar una pieza técnica no equivale a verificar el requisito de extremo a extremo; un parser correcto todavía puede integrarse incorrectamente en un campo HTTP.
- **Ajuste:** registrar el nivel de verificación alcanzado y mantener visibles las comprobaciones de integración pendientes, sin marcar A09 como aceptado por HTTP.
- **Destino:** seguimiento de la funcionalidad en este documento; no se duplica en memoria ni se modifica la especificación.

### T03 — Reglas y operación atómica

- Añadidas primero 22 pruebas en `ReservationServiceTests`, con reloj controlado y referencias a R01, R03–R07 y A01–A08/A10.
- Fase roja: contratos y servicio provisional con `NotImplementedException`; 22 fallos y 50 pruebas anteriores superadas. La compilación provisional avisaba de `timeProvider` sin usar.
- Implementados resultados tipados de creación/rechazo y reservas inmutables; catálogo fijo de identificadores A y B.
- `ReservationService.Reserve` valida y crea bajo un único `System.Threading.Lock`. Lee el reloj una vez al evaluar el inicio; compara instantes y almacena UTC.
- Registrados servicio y `TimeProvider.System` como singleton. Prueba de resolución confirma la misma instancia de servicio en scopes distintos.
- Cubiertos solapamientos parciales, contenido, envolvente e idéntico; adyacencia por ambos extremos, independencia de salas, offsets equivalentes, inicio igual a ahora y un tick anterior, duración de un tick y ausencia de máximos artificiales.
- Verificada conservación del estado en rechazos mediante snapshot interno copiado, protegido por el mismo bloqueo y accesible al ensamblado de pruebas. No se expone endpoint ni colección mutable.
- Concurrencia coordinada mediante barrera y dos trabajadores dedicados, sin sleeps: una creación, un conflicto y exactamente una reserva almacenada.
- Revisión de atomicidad: todas las lecturas/escrituras de la colección están dentro del mismo bloqueo; no hay await, callbacks de aplicación ni separación entre comprobación e inserción. El reloj se consulta dentro de la sección crítica. La garantía es local a una instancia singleton en un proceso.
- Verificación final: `dotnet build SalaReservas.slnx --verbosity minimal`, 0 errores y 0 advertencias; `dotnet test SalaReservas.slnx --no-build --verbosity minimal`, 72 superadas, 0 fallos y 0 omitidas.
- No se han implementado endpoints de reservas ni aceptación HTTP de negocio. T04 y T05 siguen pendientes.

### Retrospectiva T03

- **Observación:** además del resultado de cada operación, las pruebas comprueban el estado final y que el contenedor comparte la instancia del servicio.
- **Aprendizaje:** la exclusión depende tanto de la sección crítica como del ciclo de vida del estado; un bloqueo en servicios independientes no coordinaría peticiones.
- **Ajuste:** al integrar HTTP en T04, resolver el singleton por DI y reutilizar las comprobaciones de conservación del estado y concurrencia en aceptación.
- **Destino:** seguimiento en este documento. No se modifica la especificación ni se considera la prueba concurrente una demostración suficiente por sí sola.

### T04 — Contrato HTTP

- Añadidas primero 28 pruebas en `ReservationEndpointTests`, con reloj fijo y host/estado aislados por escenario.
- Fase roja: 28 pruebas nuevas fallidas y 72 anteriores superadas, antes de registrar el endpoint.
- Implementado `POST /reservations` mediante `ReservationEndpoint.Create`: lectura explícita de JSON, validación de campos, uso de `InstantParser` en start/end y llamada al servicio singleton existente.
- Respuesta 201 con identificador y datos UTC; rechazos 400/404/409 con `application/problem+json`, status, title, detail y extensión code estable.
- JSON malformado, cuerpos vacíos/null/array, campos ausentes, nulos, vacíos o de tipo incorrecto producen invalid_request sin mutación. El contenido no JSON se rechaza también como 400 invalid_request.
- Verificados invalid_instant por ausencia de zona en cada campo y por calendario inválido; invalid_interval, start_in_past, room_not_found y reservation_overlap.
- Comprobada conservación del estado en rechazos y persistencia entre peticiones del mismo host: primer POST 201 y repetición 409.
- Verificación: `dotnet build SalaReservas.slnx --verbosity minimal`, 0 errores y 0 advertencias; `dotnet test SalaReservas.slnx --no-build --verbosity minimal`, 100 superadas, 0 fallos y 0 omitidas.
- Ejecución HTTP en TestServer mediante WebApplicationFactory. No se arrancó Kestrel externo; suite completa de aceptación, concurrencia HTTP y prueba manual siguen en T05.

### Retrospectiva T04

- **Observación:** las comprobaciones del parser se integraron explícitamente para ambos campos y los errores de deserialización se probaron desde HTTP.
- **Aprendizaje:** el contrato de errores no queda garantizado por tener DTOs tipados; el binding automático puede responder antes de que se ejecute la validación de la aplicación.
- **Ajuste:** verificar en la frontera HTTP tanto errores de transporte como rechazos de negocio, conservando el mismo formato y comprobando que no haya mutación.
- **Destino:** registro de esta funcionalidad. A09 ya cuenta con pruebas HTTP para ambos campos; no se da por terminada toda la aceptación de T05.

### T05 — Aceptación y trazabilidad

- Añadidos 13 casos HTTP: adyacencia, intervalos contenidos/envolventes/idénticos, offsets equivalentes, salas distintas, límites temporales y concurrencia con dos clientes coordinados.
- Los casos nuevos pasaron sin cambios en producción. Se corrigieron dos advertencias xUnit2031 en las aserciones de concurrencia.
- Resultado final: compilación con 0 errores y 0 advertencias; 113 pruebas superadas, 0 fallos y 0 omitidas.
- Documentada trazabilidad R01–R07/A01–A10 y límites de la evidencia en [acceptance.md](acceptance.md).
- Arrancado Kestrel en 127.0.0.1:5217 con reloj real. Dos POST idénticos de un intervalo futuro devolvieron 201 y 409 con reservation_overlap.
- Detenido el proceso de prueba y confirmada conexión rechazada al puerto.
- No se añadieron funcionalidades fuera de alcance. La garantía de concurrencia sigue limitada a un proceso.

### Retrospectiva T05

- **Observación:** las pruebas finales no necesitaron cambiar la implementación, pero sí completar la evidencia HTTP y comprobar el servidor real.
- **Aprendizaje:** terminar tareas de código no equivale a terminar la aceptación; la revisión debe partir de cada criterio, no solo del número de pruebas verdes.
- **Ajuste:** en el siguiente incremento, mantener una matriz breve criterio → prueba → nivel de ejecución y distinguir evidencia automatizada de comprobación con servidor real.
- **Destino:** matriz en acceptance.md y retrospectiva aquí. No se duplica el contrato en memoria ni se cambia la especificación para justificar el código.
