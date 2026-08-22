// Phase 4: the frontend calls Order/Payment/Inventory Service directly,
// since the API Gateway (Phase 5) does not exist yet - see CLAUDE.md,
// "Architecture: Non-Negotiable Rules" for the documented interim
// exception. Once the Gateway exists, these three URLs collapse to one
// gatewayUrl with no service/component changes elsewhere in the app.
export const environment = {
  production: false,
  orderServiceUrl: 'http://localhost:5290',
  paymentServiceUrl: 'http://localhost:5033',
  inventoryServiceUrl: 'http://localhost:5225',
  orderHubUrl: 'http://localhost:5290/hubs/orders',
};
