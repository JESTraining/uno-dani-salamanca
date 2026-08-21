# Postman Collections

Each microservice has its own Postman collection in this folder, named `<Service>.postman_collection.json` (Postman Collection v2.1 format). There is no combined collection for all services: each one is imported and used independently, in line with the service separation used throughout the rest of the project.

## Existing Collections

| Service | File | Phase |
|----------|---------|------|
| Order Service | [OrderService.postman_collection.json](OrderService.postman_collection.json) | 1 |
| Payment Service | [PaymentService.postman_collection.json](PaymentService.postman_collection.json) | 2 |
| Inventory Service | [InventoryService.postman_collection.json](InventoryService.postman_collection.json) | 2 |
| API Gateway | pending | 5 |

## Convention

- Each collection defines its own `baseUrl` variable pointing at the service's local port.
- Every request includes a description of the business rules it applies and the relevant response codes (success and the most common errors), with at least one saved response example per case.
- When a flow depends on an id created earlier (for example, operating on an order after creating it), the creation request fills a collection variable via a test script, so the following requests can be chained without copying values by hand.
- When a new service is built, its collection is added here in the same change and the table above is updated.
