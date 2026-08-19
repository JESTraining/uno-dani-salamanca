# CLAUDE.md

Este archivo es la fuente de reglas obligatorias para cualquier agente (Claude Code u otro) que trabaje sobre este repositorio. Antes de escribir o modificar código, cualquier agente debe leer este documento completo.

## Proposito del Repositorio

Este repositorio implementa la prueba tecnica "Full-Stack Microservices Exercise: Real-time Order Processing System": un sistema de procesamiento de pedidos basado en microservicios (C#/.NET, SQL, RabbitMQ) con frontend en React o Angular y actualizaciones de estado en tiempo real via SignalR.

## Estado Actual del Repositorio

La Fase 1 esta completa: los esquemas de base de datos de los tres servicios existen (`orderdb` via migraciones EF Core, `paymentdb` e `inventorydb` via los scripts en `scripts/`) y el Order Service esta implementado end-to-end en `src/OrderService/` (API REST, EF Core, RabbitMQ, 38 pruebas automatizadas en verde). Payment Service, Inventory Service, API Gateway, Frontend e infraestructura Docker todavia no existen.

Este resumen se actualiza al cierre de cada fase, pero puede desactualizarse entre sesiones. Cualquier agente debe verificar el estado real con las herramientas de busqueda del repositorio antes de asumir que algo esta o no esta implementado.

## Fuente de Verdad

[`docs/technical-exercise.md`](docs/technical-exercise.md) contiene el enunciado completo de la prueba tecnica, en ingles, organizado en seis fases (Phase 1 a Phase 6) con sus tareas, reglas de negocio, criterios de evaluacion y retos bonus. Es la especificacion fuente del proyecto; ante cualquier ambiguedad, prevalece sobre cualquier otra interpretacion. `README.md`, en la raiz, es la presentacion publica del proyecto (para quien lo evalue o lo clone), no la especificacion — no debe usarse como fuente de requisitos.

Antes de iniciar el trabajo de una fase, un agente debe leer completa la seccion correspondiente de `docs/technical-exercise.md`. No se debe avanzar a la fase siguiente sin haber cubierto sus requisitos (o haber acordado explicitamente con el usuario una excepcion).

Este archivo (`CLAUDE.md`) funciona como tablero de estado del proyecto: la seccion "Estado Actual del Repositorio" se actualiza al cierre de cada fase.

## Arquitectura: Reglas No Negociables

- **Base de datos por servicio.** Order Service, Payment Service e Inventory Service tienen cada uno su propia base de datos, sin excepcion. Ningun servicio accede directamente a la base de datos de otro servicio, ni mediante conexion directa ni mediante vistas compartidas.
- **Comunicacion sincrona solo via API Gateway o llamadas REST explicitas documentadas.** El frontend nunca llama directamente a un microservicio; siempre pasa por el API Gateway.
- **Comunicacion asincrona solo via RabbitMQ**, usando exactamente estos contratos de evento (formato JSON):

  | Evento | Campos |
  |--------|--------|
  | `OrderCreatedEvent` | OrderId, CustomerId, TotalAmount, Items, Timestamp |
  | `PaymentProcessedEvent` | OrderId, TransactionId, Amount, Timestamp |
  | `PaymentFailedEvent` | OrderId, Reason, Timestamp |
  | `InventoryReservedEvent` | OrderId, Items, Timestamp |
  | `InventoryFailedEvent` | OrderId, Reason, Timestamp |
  | `OrderCompletedEvent` | OrderId, Status, Timestamp |
  | `OrderStatusChangedEvent` | OrderId, PreviousStatus, NewStatus, Timestamp |

  `OrderStatusChangedEvent` no esta en el enunciado original: se agrego en la Fase 1 porque el Order Service publica eventos en cada cambio de estado (no solo en la creacion) y el contrato original solo cubria creacion y cierre del pedido. Implementado en `OrderService.Application.IntegrationEvents`. Si un agente necesita agregar un campo o un evento nuevo, debe actualizar esta tabla en el mismo cambio, no dejar el codigo y la documentacion desincronizados.

- **El patron Saga gobierna el flujo completo del pedido** (creacion, pago, reserva de inventario, cierre o compensacion). Ninguna implementacion puede introducir un camino alterno que complete un pedido sin pasar por el flujo de Saga descrito.
- **Idempotencia obligatoria** en la creacion de pedidos y en el procesamiento de pagos. Todo manejador de evento debe ser idempotente (reprocesar el mismo evento dos veces no debe duplicar efectos).

## Stack Tecnologico

No cambiar estas elecciones sin confirmarlo explicitamente con el usuario:

- **Backend:** C# sobre .NET 10 (LTS vigente; el enunciado original del ejercicio menciona .NET 8, pero el proyecto adopta la LTS actual instalada en el entorno de desarrollo).
- **Acceso a datos:** Entity Framework Core (decision tomada en la Fase 1; no usar Dapper, para mantener un unico patron de acceso a datos entre servicios), con patron Repository y Unit of Work. Nomenclatura de columnas en snake_case via el paquete `EFCore.NamingConventions` (`UseSnakeCaseNamingConvention()` en cada `DbContext`), para que coincida con los scripts SQL escritos a mano.
- **Bases de datos:** PostgreSQL (decision tomada en la Fase 1). El enunciado original permite SQL Server o MySQL, pero ya existe un unico contenedor `orders-postgres` con una base de datos logica y un rol dedicado por servicio (`orderdb`/`order_service`, `paymentdb`/`payment_service`, `inventorydb`/`inventory_service` — ver `scripts/setup-databases.sql`). Mantener Postgres para Payment e Inventory Service salvo que el usuario pida explicitamente cambiar de motor.
- **Concurrencia optimista:** columna de sistema `xmin` de PostgreSQL como concurrency token (shadow property `uint xmin` con `IsConcurrencyToken()` + `HasColumnType("xid")`), sin agregar columnas de version manuales salvo que el motor no sea Postgres. Ver `OrderConfiguration` en `OrderService.Infrastructure` como referencia.
- **Mensajeria:** RabbitMQ via MassTransit, **fijado en la version 8.x** (`MassTransit.RabbitMQ` 8.5.10 o cualquier 8.x posterior; version exacta, no floating). **Nunca actualizar a MassTransit 9 o superior**: esas versiones exigen una licencia comercial (`SetLicense`/`SetLicenseLocation` o variables `MT_LICENSE`/`MT_LICENSE_PATH`) y la aplicacion falla al iniciar sin ella (`MassTransit.ConfigurationException` en el arranque del host). Exchanges topic o direct, dead letter queues y reintento con backoff exponencial (3 intentos), a definir en la Fase 3.
- **Frontend:** Angular (decision tomada). No usar React en este proyecto. UI con Angular Material, PrimeNG o ng-bootstrap; estado con NgRx.
- **Tiempo real:** SignalR (hub en el Order Service, cliente en el frontend).
- **Contenedores:** Docker con Dockerfiles multi-etapa, orquestados con Docker Compose.
- **Logging:** Serilog, con sinks configurables (Console, File, Seq).
- **Observabilidad:** OpenTelemetry (trazas) y Prometheus (metricas) a partir de la Fase 5.
- **Autenticacion:** JWT con autorizacion basada en roles, a partir de la Fase 5.

## Convenciones de Codigo

- Seguir principios SOLID y DRY. No crear abstracciones ni capas adicionales que el requisito actual no necesite.
- Usar async/await para toda operacion de entrada/salida (base de datos, HTTP, mensajeria).
- Usar inyeccion de dependencias del contenedor nativo de .NET; evitar patrones de localizador de servicios (service locator).
- Nomenclatura: PascalCase para clases, metodos y propiedades publicas en C#; camelCase para variables locales y parametros; camelCase para variables y funciones en JavaScript/TypeScript, PascalCase para componentes de React.
- Identificadores de codigo, nombres de variables, nombres de clases y mensajes de commit se escriben en ingles, siguiendo la convencion tecnica habitual, aun cuando la documentacion del proyecto (este archivo) este en espanol.
- No agregar comentarios que describan que hace el codigo cuando el nombre de la funcion o variable ya lo deja claro. Solo comentar cuando exista una razon no obvia (una restriccion externa, un workaround puntual, una decision que sorprenderia a quien lea el codigo despues).
- No dejar implementaciones a medio terminar ni funcionalidad detras de flags "por si acaso". Si una tarea se marca como completa, debe estar terminada y probada.
- Los mensajes de commit deben ser descriptivos y en ingles, preferentemente en formato tipo Conventional Commits (`feat:`, `fix:`, `docs:`, `test:`, `chore:`).
- En DTOs definidos como `record` con constructor primario, los atributos de validacion (`[Required]`, `[MaxLength]`, `[Range]`, etc.) van directo sobre el parametro, sin el prefijo `[property: ...]`. Con `property:` ASP.NET Core lanza `InvalidOperationException` en tiempo de ejecucion ("validation metadata defined on property... must be associated with the constructor parameter") en lugar de devolver 400. Ver `OrderService.Application/Contracts/OrderDtos.cs`.

## Patron Arquitectonico Establecido (replicar en Payment e Inventory Service)

Order Service (Fase 1, en `src/OrderService/`) fija el patron que Payment Service e Inventory Service deben seguir en la Fase 2, para que los tres servicios sean consistentes:

- Cinco proyectos por servicio: `<Servicio>.Domain` (entidades y reglas de negocio como metodos, sin setters publicos), `<Servicio>.Application` (DTOs en `Contracts/`, interfaces en `Abstractions/`, casos de uso en `Services/`, eventos de integracion en `IntegrationEvents/`, excepciones en `Exceptions/`), `<Servicio>.Infrastructure` (`DbContext` y configuraciones EF Core en `Persistence/`, publicador de eventos en `Messaging/`, clientes HTTP salientes en `ExternalServices/`), `<Servicio>.API` (`Controllers/`, `Middleware/`, `Program.cs`), `<Servicio>.Tests` (subcarpetas `Domain/`, `Application/`, `Unit/`, `Integration/`).
- Manejo de errores centralizado en un `ExceptionHandlingMiddleware` por servicio, que traduce excepciones de dominio/aplicacion a codigos HTTP (404 no encontrado, 409 conflicto de estado o de concurrencia, 422 regla de negocio incumplida, 503 dependencia externa no disponible). No usar `try/catch` repetido en cada controller.
- Idempotencia via header `Idempotency-Key`, con una tabla dedicada (`idempotency_keys` en Order Service) cuyo commit ocurre en la misma transaccion que la operacion principal (mismo `SaveChangesAsync`), no en un paso separado.
- Pruebas de integracion con `WebApplicationFactory` + `Testcontainers.PostgreSql` (base de datos real efimera), reemplazando en el `CustomWebApplicationFactory` unicamente las dependencias hacia servicios externos que todavia no existen o no conviene levantar en el test (ver `AlwaysAvailableInventoryChecker` en `OrderService.Tests`).

## Reglas de Negocio Criticas (nunca deben violarse)

Estas reglas provienen del enunciado original y deben quedar reflejadas en validaciones reales del codigo, no solo en la documentacion:

**Order Service**
- Un pedido solo puede cancelarse si esta en estado `Pending` o `PaymentProcessing`.
- Los pedidos en estado `Shipped` o `Delivered` son inmutables.
- El monto total debe ser mayor que 0.
- Todo pedido requiere al menos un articulo.

**Payment Service (gateway simulado)**
- Pagos superiores a $10,000: siempre fallan (deteccion de fraude).
- Pagos cuyo monto termina en `.99`: 20% de probabilidad de fallo, aleatoria.
- Resto de los pagos: exitosos.
- Simular una demora de procesamiento de 2 a 5 segundos con `Task.Delay`.

**Inventory Service**
- El stock se reserva en el momento en que el pago se procesa exitosamente (evento `PaymentProcessed`).
- Si la reserva no se confirma dentro de 5 minutos, se libera automaticamente y se dispara un evento de fallo.
- Toda operacion de stock debe ser atomica (transaccion SQL) y proteger contra sobreventa mediante bloqueo a nivel de fila o concurrencia optimista.

Ver el enunciado completo en [`docs/technical-exercise.md`](docs/technical-exercise.md) antes de implementar la logica correspondiente.

## Seguridad

- Todas las consultas SQL deben ser parametrizadas. Prohibido concatenar valores de entrada del usuario directamente en una consulta o comando.
- Validar toda entrada en el limite de la API (payloads de request), no confiar en validaciones del frontend como unica barrera.
- Nunca commitear secretos, cadenas de conexion con credenciales reales, ni claves de API. Usar variables de entorno y `appsettings.json` solo para configuracion no sensible.
- JWT obligatorio en endpoints que no sean explicitamente publicos; autorizacion basada en roles donde el enunciado lo indique (por ejemplo, alta de productos restringida a administrador).
- CORS debe configurarse con origenes explicitos; no usar wildcard (`*`) en configuraciones destinadas a produccion.

## Testing

- Cobertura minima de pruebas unitarias: 70%, medida sobre la logica de servicios, modelos de dominio y manejadores de eventos.
- Pruebas unitarias con xUnit o NUnit en el backend; Jest y React Testing Library en el frontend (si se elige React).
- Pruebas de integracion de base de datos con TestContainers o base de datos en memoria.
- Pruebas de integracion de endpoints con `WebApplicationFactory`.
- Toda regla de negocio critica listada arriba debe tener al menos una prueba que la cubra explicitamente, incluyendo sus casos limite (por ejemplo, un pago de exactamente $10,000, un monto que termina en `.99`).
- Ninguna tarea se considera terminada si el codigo que la implementa no tiene pruebas asociadas.

## Docker y DevOps

- Cada microservicio y el frontend tienen su propio Dockerfile con build multi-etapa.
- Cada contenedor expone un health check funcional.
- La configuracion depende de variables de entorno; no hardcodear hosts, puertos ni credenciales en el codigo o en las imagenes.
- `docker-compose.yml` debe permitir levantar el sistema completo (todos los servicios, RabbitMQ, bases de datos) con un unico comando, en un entorno limpio.

## Flujo de Trabajo para Agentes

1. Leer este archivo completo antes de tocar codigo.
2. Identificar en que fase esta el proyecto revisando la seccion "Estado Actual del Repositorio" de este archivo.
3. Leer la seccion correspondiente de `docs/technical-exercise.md` completa antes de empezar a escribir codigo.
4. Implementar unicamente lo que esa fase pide; no adelantar trabajo de fases posteriores ni de los retos bonus sin que el usuario lo pida explicitamente.
5. Al completar una fase, actualizar la seccion "Estado Actual del Repositorio" de este archivo.
6. Si se descubre una ambiguedad en el enunciado, resolverla con el criterio que mejor se ajuste al resto de las reglas de este archivo, y dejar constancia de la interpretacion tomada en el codigo o en el mensaje de commit correspondiente.
7. Ejecutar las pruebas relevantes antes de dar una tarea por concluida.

## Estilo de Documentacion y Comunicacion

- Prohibido el uso de emojis en cualquier archivo del repositorio: codigo, comentarios, commits, documentacion o salida hacia el usuario. El tono debe ser serio y profesional en todo momento.
- Toda la documentacion de arquitectura del proyecto (ADRs, este archivo) se redacta en espanol. El codigo fuente y los mensajes de commit se redactan en ingles.
- No crear archivos de documentacion adicionales fuera de la estructura ya definida (`docs/`, este `CLAUDE.md`) salvo que el usuario lo solicite.

## Prohibiciones Explicitas

- No acoplar servicios compartiendo base de datos o llamando directamente a la base de datos de otro servicio.
- No introducir un camino que complete o modifique un pedido sin pasar por las reglas de negocio y el flujo de Saga descritos.
- No omitir la idempotencia en creacion de pedidos ni en procesamiento de pagos.
- No commitear secretos, cadenas de conexion reales ni archivos `.env` con valores sensibles.
- No implementar retos bonus (ver la seccion "Bonus Challenges" de `docs/technical-exercise.md`) antes de completar las seis fases principales, salvo pedido explicito del usuario.
- No usar `git push --force`, `git reset --hard` ni comandos destructivos similares sin autorizacion explicita del usuario para esa accion puntual.

## Estructura de Carpetas Objetivo

Mismo arbol que la seccion "Suggested Folder Structure" de `docs/technical-exercise.md`, mas los archivos que ya existen fuera de esa lista original:

```
/
├── src/
│   ├── OrderService/       (Fase 1 — implementado: Domain/Application/Infrastructure/API/Tests)
│   ├── PaymentService/     (Fase 2 — pendiente)
│   ├── InventoryService/   (Fase 2 — pendiente)
│   ├── ApiGateway/         (Fase 5 — pendiente)
│   └── Frontend/           (Fase 4 — pendiente, Angular)
├── docker/                 (Fase 5 — pendiente)
├── docs/
│   ├── api/                (implementado: coleccion Postman por servicio)
│   ├── technical-exercise.md  (implementado: enunciado original, movido desde README.md)
│   ├── architecture.md     (Fase 6 — pendiente)
│   └── adr/                (Fase 6 — pendiente)
├── scripts/                (implementado: setup-databases.sql, payment-service-schema.sql, inventory-service-schema.sql)
├── CLAUDE.md
├── .gitignore
└── README.md               (presentacion publica del proyecto, no la especificacion)
```

`CLAUDE.md`, `.gitignore`, `scripts/`, `docs/api/`, `docs/technical-exercise.md` y `src/OrderService/` ya existen. El resto de la estructura se construye de forma incremental, fase por fase.
