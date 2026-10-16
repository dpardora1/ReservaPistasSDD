# 003 — Evidencia y revisión

**Aceptación humana: pendiente.** La revisión técnica del agente no la sustituye.

| Criterio | Evidencia |
|---|---|
| L01 | Base publicada tras revisión independiente de 29 archivos staged; sin hallazgos de secretos/datos locales. SHA bootstrap 924704c580e282106b39ff87dc7bd0c45043f5d7. |
| L02 | specs/current.md conserva referencias 001/002, precedencia de cancelación y contratos HTTP. Revisar diff como parte de la PR. |
| L03 | verify.ps1 ejecutado localmente: 139 pruebas, 0 omitidas/fallidas, compilación limpia, TRX y Cobertura generados. |
| L04 | Workflow de dos plataformas; resultado remoto final y artefactos se vinculan en PR. No inferir éxito remoto del local. |
| L05 | Smoke local exitoso y proceso detenido; fallo intencional sale no cero, registra motivo y confirma stopped en smoke.json. |
| L06 | PR debe vincular SHA, ref probado, run/intento y revisión. Esos datos viven fuera del commit para evitar autorreferencia del SHA. |
| L07 | Entrega sin merge, despliegue o cambios de permisos/protecciones. |

## Cómo reproducir

`pwsh -NoLogo -NoProfile -File scripts/verify.ps1`

Consultar artifacts/verify-*/verify.log, tests/*.trx, tests/**/coverage.cobertura.xml, smoke/smoke.json, stdout/stderr del servidor y smoke-cleanup-check/smoke.json. Cada ejecución crea un directorio nuevo: no reutiliza informes antiguos para declarar éxito. Los logs locales pueden incluir rutas/nombres del equipo y no se publican; CI produce su propia evidencia en runners hosted.

## Paquete final

El cuerpo de la PR debe registrar el candidato concreto, ambos jobs, enlaces de artefactos, revisión técnica y limitaciones. Si cambian commits, la evidencia del candidato anterior no aprueba el nuevo. El humano revisa el SHA final y decide la fusión manual.

## Límites

Misma identidad GitHub ADMIN y ausencia de rulesets: no hay separación técnica de permisos. No se ejecuta un agente alojado en GitHub ni se han configurado hooks automáticos. Solo se automatizan verificaciones en cada push/PR. Imágenes hosted variables y runtimes seleccionados se registran, no se promete build hermético. El producto sigue siendo un ejercicio en memoria sin seguridad de producción.
