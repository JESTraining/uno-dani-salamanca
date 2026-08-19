# Colecciones Postman

Cada microservicio tiene su propia coleccion Postman en esta carpeta, nombrada `<Servicio>.postman_collection.json` (formato Postman Collection v2.1). No existe una coleccion combinada para todos los servicios: cada una se importa y se usa de forma independiente, en linea con la separacion de servicios del resto del proyecto.

## Colecciones existentes

| Servicio | Archivo | Fase |
|----------|---------|------|
| Order Service | [OrderService.postman_collection.json](OrderService.postman_collection.json) | 1 |
| Payment Service | pendiente | 2 |
| Inventory Service | pendiente | 2 |
| API Gateway | pendiente | 5 |

## Convencion

- Cada coleccion define su propia variable `baseUrl` apuntando al puerto local del servicio.
- Cada request incluye una descripcion con las reglas de negocio que aplica y los codigos de respuesta relevantes (exito y errores mas comunes), con al menos un ejemplo de respuesta guardado por caso.
- Cuando un flujo depende de un id creado previamente (por ejemplo, operar sobre un pedido despues de crearlo), la request de creacion completa una variable de coleccion via un test script, para poder encadenar las siguientes peticiones sin copiar valores a mano.
- Al construir un servicio nuevo, se agrega su coleccion aqui en el mismo cambio y se actualiza la tabla de arriba.
