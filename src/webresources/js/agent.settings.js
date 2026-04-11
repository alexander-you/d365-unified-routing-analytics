// ════════════════════════════════════════════════
// Copilot Studio Agent Settings
// Unified Routing Analytics
// ════════════════════════════════════════════════

import { ConnectionSettings } from 'https://unpkg.com/@microsoft/agents-copilotstudio-client@1.0.15/dist/src/browser.mjs';
import { getEnvironmentId, getTenantId, getEnvironmentVariableValue } from './dataverseHelpers.js';

// Bot schema name — travels with the solution
const BOT_SCHEMA_NAME = 'alex_routing_insight';

// Client ID from Entra ID App Registration — stored in environment variable per tenant
const appClientId = await getEnvironmentVariableValue("alex_AppClientId");

if (!appClientId) {
  throw new Error("Environment variable 'alex_AppClientId' is not configured. Register an App in Entra ID and store the Client ID in this variable.");
}

export const settings = new ConnectionSettings({
  appClientId: appClientId,
  tenantId: getTenantId(),
  environmentId: getEnvironmentId(),
  agentIdentifier: BOT_SCHEMA_NAME
});
