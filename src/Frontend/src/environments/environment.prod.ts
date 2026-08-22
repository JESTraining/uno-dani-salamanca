// Placeholder production values - real hosts are wired in Phase 5 (Docker
// Compose service names / the API Gateway URL, once it exists).
export const environment = {
  production: true,
  orderServiceUrl: 'http://order-service:8080',
  paymentServiceUrl: 'http://payment-service:8080',
  inventoryServiceUrl: 'http://inventory-service:8080',
  orderHubUrl: 'http://order-service:8080/hubs/orders',
};
