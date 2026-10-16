# 003 — Ejecución autónoma hasta revisión humana

**Estado:** alcance y plan aprobados; aceptación humana del resultado pendiente.

## Objetivo

Un agente ejecuta un cambio aprobado hasta entregar una PR verificable. El humano conserva decisiones de producto, aceptación y fusión. Este incremento prepara infraestructura local/CI, no incorpora agentes remotos ni IA al producto.

## Criterios

| ID | Resultado exigido |
|---|---|
| L01 | Publicar solo fuente, configuración publicable y documentación revisadas; excluir secretos, memoria y resultados locales. |
| L02 | Un contrato vigente consolida 001/002 con procedencia y sin cambiar comportamiento. |
| L03 | Un comando reproduce restore bloqueado, build Release, pruebas con TRX/cobertura y comprobación real de API. |
| L04 | CI Windows/Linux usa el mismo comando, permisos de lectura, acciones fijadas, timeout y artefactos aun con fallo. |
| L05 | Smoke usa servidor propio, puerto dinámico, fechas futuras y limpieza tanto en éxito como en fallo. |
| L06 | PR enlaza revisión técnica, SHA/ref realmente probado, run/intento y evidencia; aceptación humana permanece pendiente. |
| L07 | No se fusiona, despliega ni cambian permisos/protecciones automáticamente. |

## Autonomía y paradas

Dentro de un alcance explícitamente autorizado: implementar, probar, corregir, preparar commits, enviar rama y crear/actualizar PR cuando estas acciones estén autorizadas para ese incremento. Una aprobación de requisitos no autoriza por sí sola publicar o desplegar.

Detener y explicar ante secretos, ambigüedad de producto, cambio incompatible no aprobado, permisos nuevos, historia remota inesperada o verificación que no pueda completarse. Hasta tres ciclos de corrección por fallo recurrente; no loops ilimitados. No eliminar tests, aflojar aserciones o debilitar CI para obtener verde. Cambios a políticas requieren revisión específica.

El agente no autoaprueba, no fusiona ni activa auto-merge. El revisor técnico separado es apoyo, no sustituto del humano. Un cambio posterior al SHA revisado requiere nueva evidencia y revisión del cambio.

## Seguridad real y límites

La cuenta de esta sesión es la misma cuenta ADMIN del propietario. No hay separación técnica entre agente y humano ni rulesets configurados. La política es procedimental, no un control de acceso. El autor no puede aprobar su propia PR en GitHub; el humano revisa el SHA final y decide la fusión manual.

Mejora posterior: identidad de automatización restringida y protecciones, con autorización separada. No se crean credenciales, hooks de permisos o despliegues en 003.

## Entrega inicial

Se autoriza excepción de bootstrap: publicar base 001/002 verificada en main del remoto vacío y desarrollar 003 en rama. No futuras entregas directas a main. El repositorio público se llama ReservaPistasSDD; no se renombra el dominio de salas en este incremento.
