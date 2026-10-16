# ReservaPistasSDD

Ejercicio de Spec-Driven Development con .NET 10. El dominio implementado es reserva y cancelación de **salas**; el nombre del repositorio no implica un cambio a pistas deportivas.

## Empezar

Requisitos: Git, SDK **10.0.204** (global.json, sin roll-forward), PowerShell **7.4 o superior** y acceso a NuGet para la primera restauración.

```powershell
pwsh -NoLogo -NoProfile -File scripts/verify.ps1
```

El comando resuelve rutas desde el script, restaura dependencias bloqueadas, compila Release con advertencias como errores, ejecuta pruebas con TRX y cobertura y levanta un Kestrel temporal para el flujo de reservas. También provoca un fallo de smoke para comprobar limpieza. Un fallo devuelve salida no cero. Evidencia en `artifacts/verify-<id>/`, fuera de Git.

Para arrancar manualmente después de compilar:

```powershell
dotnet src/SalaReservas.Api/bin/Release/net10.0/SalaReservas.Api.dll --urls http://127.0.0.1:5206
```

POST `/reservations` recibe roomId A/B, start y end con fechas futuras ISO 8601 y zona explícita. DELETE `/reservations/{id}` cancela. No hay interfaz web ni GET de consulta; GET `/` devuelve 404. Detener con Ctrl+C.

## Contrato y proceso

- [Contrato vigente](specs/current.md): reglas y transporte actuales, con procedencia.
- [001: creación](specs/001-reserve-room/spec.md) y [002: cancelación](specs/002-cancel-reservation/spec.md): historial de aprendizaje.
- [003: autonomía y revisión humana](specs/003-development-lifecycle/spec.md), [plan](specs/003-development-lifecycle/plan.md), [evidencia](specs/003-development-lifecycle/acceptance.md).

Flujo: acordar cambio → ejecutar alcance aprobado en rama → verificación local/CI → revisión técnica independiente → revisión humana del SHA final → fusión manual. Las reglas de autonomía no otorgan permisos por sí solas.

GitHub Actions ejecuta el mismo comando en Ubuntu 24.04 y Windows 2025, y conserva artefactos 14 días. Las acciones están fijadas a SHAs y el token del workflow solo lee contenido. No ejecuta agentes remotos, no usa claves de IA ni despliega. Los lockfiles fijan resolución NuGet; su actualización es deliberada mediante restore --force-evaluate y revisión del diff, no durante verify.

## Revisión humana en solitario

El autor no puede aprobar su propia PR. Revisar el diff y la evidencia del SHA final y dejar, si se desea, un comentario humano con la decisión antes del merge manual. El agente no debe escribir esa aceptación en nombre del humano.

Actualmente el agente usa la misma cuenta ADMIN y no se han configurado protecciones: la separación es procedimental, no técnica. Una identidad de automatización limitada y reglas de rama requieren una configuración posterior explícita.

## Límites

Sin autenticación, persistencia, autorización, auditoría ni consultas. Estado en memoria, crecimiento sin límite y exclusión por bloqueo de un singleton en un proceso. Reiniciar pierde los datos. No desplegar como servicio de producción.

SDK y dependencias fijados mejoran reproducibilidad; las imágenes hosted y runtimes pueden cambiar. No se afirma hermeticidad ni ausencia de defectos por pasar las pruebas.
