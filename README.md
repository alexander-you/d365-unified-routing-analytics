# Unified Routing Analytics

A Dynamics 365 Customer Service solution that provides visual routing diagnostics and an embedded AI assistant for investigating unified routing behavior.

<div style="padding:75% 0 0 0;position:relative;"><iframe src="https://player.vimeo.com/video/1182228641?badge=0&amp;autopause=0&amp;player_id=0&amp;app_id=58479&amp;muted=1" frameborder="0" allow="autoplay; fullscreen; picture-in-picture; clipboard-write; encrypted-media; web-share" referrerpolicy="strict-origin-when-cross-origin" style="position:absolute;top:0;left:0;width:100%;height:100%;" title="unified-routing-analytics-in-actionV2"></iframe></div><script src="https://player.vimeo.com/api/player.js"></script>

## Architecture

```
┌─────────────────────────────────────────────────────────┐
│  HTML Web Resource (routing-analytics.html)              │
│  ┌──────────────────────┐  ┌──────────────────────────┐ │
│  │  Timeline Dashboard   │  │  Copilot Studio Widget   │ │
│  │  • Summary Cards      │  │  • SSO via MSAL.js       │ │
│  │  • Expandable Steps   │  │  • M365 Agents SDK       │ │
│  │  • Color-coded Status │  │  • WebChat 4.18          │ │
│  └──────────┬───────────┘  └────────────┬─────────────┘ │
│             │                            │               │
│      Xrm.WebApi.execute()          Entra ID Token       │
└─────────────┼────────────────────────────┼───────────────┘
              │                            │
    ┌─────────▼─────────┐     ┌───────────▼────────────┐
    │  Custom API        │     │  Copilot Studio Agent   │
    │  GetRoutingDiag.   │     │  "Routing Insight"      │
    └─────────┬─────────┘     │  • Dataverse Tools      │
              │               │  • Web Search (MS Learn) │
    ┌─────────▼─────────┐     └────────────────────────┘
    │  Power Automate    │
    │  Flow              │
    └─────────┬─────────┘
              │
    ┌─────────▼─────────┐     ┌────────────────────────┐
    │  Azure Monitor     │     │  Dataverse              │
    │  Logs (KQL)        │     │  Staging Table          │
    └───────────────────┘     └────────────────────────┘
```

## Solution Components

| Component | Type | Description |
|-----------|------|-------------|
| `routing-analytics.html` | Web Resource | Main dashboard with timeline + copilot widget |
| `js/dataverseHelpers.js` | Web Resource | Xrm utility functions (env vars, tenant, user) |
| `js/agent.settings.js` | Web Resource | Copilot Studio connection settings |
| `js/acquireToken.js` | Web Resource | MSAL SSO token acquisition |
| `js/copilotInit.js` | Web Resource | Copilot Studio WebChat initialization |
| `alex_GetRoutingDiagnostics` | Custom API | Triggers the diagnostics flow |
| `CallCustomAPI_GetRoutingDiagnostics` | Cloud Flow | Queries Log Analytics, writes to staging table |
| `DeleteLogsEvery24Hours` | Cloud Flow | Cleanup of staging table |
| `alex_routing_diagnostics_result` | Table | Staging table for routing JSON |
| `alex_routing_insight` | Copilot Studio Agent | AI assistant for routing investigation |

## Environment Variables

| Variable | Description | Required |
|----------|-------------|----------|
| `alex_AppClientId` | Entra ID App Registration Client ID for SSO | Yes |
| `alex_LogAnalyticsSubscription` | Azure Subscription ID for Log Analytics | Yes |
| `alex_LogAnalyticsResourceGroup` | Resource Group containing Log Analytics workspace | Yes |
| `alex_LogAnalyticsWorkspaceName` | Log Analytics workspace name | Yes |

## Connection References

| Reference | Connector | Purpose |
|-----------|-----------|---------|
| `AzureMonitorLogs_Routing_flow` | Azure Monitor Logs | KQL queries against App Insights |
| `MicrosoftDataverse_Routing_flow` | Microsoft Dataverse | Write results to staging table |

## Prerequisites

- Dynamics 365 Customer Service with Unified Routing enabled
- Application Insights connected to D365 (diagnostic settings)
- Log Analytics workspace receiving D365 telemetry
- Power Automate Premium license (Azure Monitor Logs connector)
- Copilot Studio license

## Deployment Steps

### 1. Import the Managed Solution

Import the solution into the target environment. You'll be prompted to configure connection references.

### 2. Register an Entra ID App

1. Go to **Azure Portal → Entra ID → App registrations → New registration**
2. Name: `Unified Routing Analytics`
3. Redirect URI: `https://<your-org>.crm.dynamics.com` (Single-page application)
4. Under **API Permissions**, add:
   - `https://api.powerplatform.com/.default` (delegated)
5. Under **Expose an API**:
   - Set Application ID URI
   - Add scope: `access_as_user`
6. Copy the **Client ID**

### 3. Configure Copilot Studio Bot

1. Open the **Routing Insight** agent in Copilot Studio
2. Go to **Settings → Security → Authentication → Authenticate manually**
3. Enter the Client ID, Client Secret, and Token Exchange URL from the Entra ID App
4. Set tool authentication to **End user credentials**
5. **Publish** the agent

### 4. Set Environment Variables

| Variable | Value |
|----------|-------|
| `alex_AppClientId` | Client ID from step 2 |
| `alex_LogAnalyticsSubscription` | Your Azure Subscription ID |
| `alex_LogAnalyticsResourceGroup` | Resource Group name |
| `alex_LogAnalyticsWorkspaceName` | Workspace name |

### 5. Verify

1. Navigate to the Routing Diagnostic page in D365
2. Enter a conversation GUID → timeline should render
3. Click the floating bot button → Copilot should connect via SSO

## File Structure

```
src/
└── webresources/
    ├── routing-analytics.html    # Main dashboard
    └── js/
        ├── dataverseHelpers.js   # Xrm utility functions
        ├── agent.settings.js     # Copilot connection config
        ├── acquireToken.js       # MSAL SSO token flow
        └── copilotInit.js        # WebChat initialization
```

## Security Notes

- **No secrets in client-side code** — all authentication uses MSAL with the user's existing D365 session
- **Entra ID App Registration** is required per tenant — each customer registers their own app
- **Connection References** are tenant-scoped — each customer creates local connections on import
- **No hardcoded URLs** — all tenant/environment-specific values come from environment variables or Xrm context

## KQL Query

The flow uses this query against Log Analytics:

```kql
traces
| where timestamp > ago(30d)
| extend cd = parse_json(customDimensions)
| where tostring(cd["powerplatform.analytics.resource.id"]) == "{conversationId}"
| where tostring(cd["powerplatform.analytics.scenario"]) == "ConversationDiagnosticsScenario"
| extend
    stage = tostring(cd["powerplatform.analytics.subscenario"]),
    eventType = tostring(cd.type),
    channel = tostring(cd["omnichannel.channel.type"]),
    description = tostring(cd["omnichannel.description"]),
    additionalInfo = tostring(cd["omnichannel.additional_info"]),
    result = tostring(cd["omnichannel.result"]),
    assignmentStatus = tostring(cd["omnichannel.assignment.status"]),
    assignmentMethod = tostring(cd["omnichannel.assignment.method"]),
    rulesExecuted = tostring(cd["omnichannel.rules_executed_info"]),
    workItemDetails = tostring(cd["omnichannel.work_item.details"]),
    recordIdentification = tostring(cd["omnichannel.record_identification_details"]),
    agentName = tostring(cd["omnichannel.initiator_agent.name"]),
    previousState = tostring(cd["omnichannel.previous_state"]),
    newState = tostring(cd["omnichannel.new_state"]),
    targetAgentId = tostring(cd["omnichannel.target_agent.id"]),
    targetQueueId = tostring(cd["omnichannel.target_queue.id"])
| project timestamp, stage, eventType, channel, description, additionalInfo, result,
    assignmentStatus, assignmentMethod, rulesExecuted, workItemDetails,
    recordIdentification, agentName, previousState, newState, targetAgentId, targetQueueId
| order by timestamp asc
```

## License

MIT
