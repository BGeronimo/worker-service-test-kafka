# Copilot Instructions

## Directrices del proyecto
- Prefiere arquitectura con estructura de carpetas y responsabilidades claras por flujo de negocio, no solo separación técnica por tipo de archivo.
- Prefiere modo estricto de composición en todos los ambientes: si no existe compositor específico por evento/canal, debe fallar con error controlado y trazabilidad en logs.