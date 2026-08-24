namespace ApiGateway.API.Services;

/// Minimal demo auth (Phase 5): the project has no user-management system in
/// any service, and the exercise statement does not specify where JWT users
/// come from. This is a deliberately small, hardcoded user list - not a
/// production identity store - scoped to the one endpoint CLAUDE.md's
/// Security section anchors as needing role-based authorization ("product
/// creation restricted to administrators").
public sealed record DemoUser(string Username, string PasswordHash, string Role);
