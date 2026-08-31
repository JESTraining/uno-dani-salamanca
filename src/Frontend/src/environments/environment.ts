// Phase 5: the frontend now calls only the API Gateway, which proxies to
// Order/Payment/Inventory Service and forwards the SignalR hub's WebSocket
// traffic - see CLAUDE.md, "Architecture: Non-Negotiable Rules". This
// collapses the three base URLs used during the temporary Phase 4 exception
// into one, with no other frontend code changes.
export const environment = {
  production: false,
  gatewayUrl: 'http://localhost:5013',
  orderHubUrl: 'http://localhost:5013/hubs/orders',
};
