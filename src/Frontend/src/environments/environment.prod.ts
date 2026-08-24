// This Docker Compose setup is a local development/demo environment, not a
// genuine multi-host production deployment - the Gateway is reachable from
// the browser at the same host-mapped port as local (non-Docker) dev (see
// docker/docker-compose.yml). A real production deployment would replace
// this with the Gateway's real public URL.
export const environment = {
  production: true,
  gatewayUrl: 'http://localhost:5013',
  orderHubUrl: 'http://localhost:5013/hubs/orders',
};
