# 003 — Plan y seguimiento

Plan aprobado. Alcance: infraestructura de entrega, sin cambios en producción ni expectativas de tests.

## Diseño

Reutilizar SDK fijado, solución, 139 pruebas y coverlet ya instalado. Directory.Build.props habilita lockfiles revisados; verify.ps1 usa restore bloqueado y configura Release/TRX/cobertura. smoke.ps1 arranca su propia DLL en puerto dinámico, verifica contratos existentes y limpia su proceso en finally.

CI usa el mismo script, permisos contents:read, acciones fijadas, matriz Windows/Linux y artefactos con retención limitada. No hay agentes remotos, secretos ni despliegues. spec/current.md consolida comportamiento histórico; CLAUDE.md y plantilla PR hacen visibles límites y evidencia sin pretender imponer control de acceso.

## Checklist

- [x] Revisar publicación y verificar base; bootstrap main 924704c580e282106b39ff87dc7bd0c45043f5d7.
- [x] Implementar contrato vigente, política e interfaz única de verificación.
- [x] Verificar localmente 139 pruebas, cobertura, smoke y fallo inducido con limpieza.
- [x] Revisar de forma independiente scripts/CI/documentación y resolver bloqueos.
- [ ] Publicar rama y abrir PR; observar jobs Windows/Linux del candidato final.
- [ ] Vincular SHA/run/evidencias en PR y entregar sin fusionar.

## Registro local

La primera versión del wrapper ocultaba el ejecutable dotnet por coincidencia de nombre en PowerShell; se corrigió a Invoke-Dotnet. El parser JSON de PowerShell convertía fechas automáticamente; el smoke usa JsonDocument para preservar cadenas y precisión. No se cambiaron reglas del producto ni aserciones de negocio.

El presupuesto de corrección limita tres intentos por el mismo fallo recurrente, no errores distintos diagnosticados. Aceptación humana y configuración de protección no están incluidas en la automatización.
