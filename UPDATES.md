# Unified Routing Analytics — Updates Overview

This document summarises all updates made to the solution since the initial release
(v1.0.1 / tag `Unified`), with emphasis on the new Routing Diagnostics Grid
introduced in v1.0.3.  The updated KQL aggregation query is reproduced in full at
the end.

---

## Table of Contents

1. [What Changed at a Glance](#what-changed-at-a-glance)
2. [v1.0.3 — Routing Diagnostics Grid](#v103--routing-diagnostics-grid)
   - [Grid Overview](#grid-overview)
   - [Filtering and Retrieval](#filtering-and-retrieval)
   - [Dataverse Caching Layer](#dataverse-caching-layer)
   - [Enhanced Routing Story Panel](#enhanced-routing-story-panel)
   - [Copilot Integration](#copilot-integration)
   - [Settings Panel](#settings-panel)
3. [v1.0.2 — Routing Insight Bot Improvements](#v102--routing-insight-bot-improvements)
   - [New Bot Capabilities](#new-bot-capabilities)
   - [Bug Fix — Panel Connectivity](#bug-fix--panel-connectivity)
   - [UI Improvements — Assistant Panel](#ui-improvements--assistant-panel)
4. [New Infrastructure Components](#new-infrastructure-components)
   - [Custom API](#custom-api)
   - [Power Automate Flow](#power-automate-flow)
   - [Dataverse Table](#dataverse-table)
5. [Updated KQL Query](#updated-kql-query)
6. [Environment Variables Reference](#environment-variables-reference)

---

## What Changed at a Glance

| Area | v1.0.1 (baseline) | v1.0.2 | v1.0.3 |
|------|--------------------|--------|--------|
| Entry point | Single-conversation search bar | ← same | **New grid — all conversations at once** |
| Data source | Live KQL only | ← same | **Cached Dataverse table + live KQL sync** |
| Routing Story | Timeline cards | ← same | **+ Stepper, summary, AI narrative, Copilot buttons per card** |
| Copilot panel | Basic open/close | **Minimize, Restart, version badge, connectivity fix** | **Grid-row icon, contextual Explain buttons, working indicator** |
| Infrastructure | `alex_GetRoutingDiagnostics` flow | ← same | **New `alex_GetRoutingDiagnosticsSummary` flow + Dataverse table** |
| Settings | None | None | **4-option Settings panel (env vars)** |

---

## v1.0.3 — Routing Diagnostics Grid

### Grid Overview

The centrepiece of this release is the **Routing Diagnostics Grid** — a sortable,
filterable table that presents *all* routed conversations at once, replacing the
need to analyse conversations one at a time.

Each row shows:

| Column | Description |
|--------|-------------|
| Diagnostic Item | Record title or conversation ID |
| Status | Human-readable routing outcome (e.g. "Agent assignment – completed") |
| Started | UTC start time of the routing process |
| Duration | Total routing duration in seconds |
| Workstream | Live workstream name |
| Queue | Destination queue |
| Agent | Assigned agent name |

- Column headers are clickable — clicking sorts ascending/descending; the grid defaults to newest-first.
- Rows animate in with staggered fade-up transitions.
- Clicking a row opens the full **Routing Story** detail panel for that conversation.
- A **statistics bar** below the grid shows live totals: total records, completed, failed, and average routing duration.

### Filtering and Retrieval

The grid toolbar contains four filter controls and a **Retrieve** button:

| Control | Options |
|---------|---------|
| Date range | Today / Yesterday / Last 7 Days / This Month / Last 30 Days / Last 90 Days / Custom / **All Cached** (default) |
| Workstream | Pre-populated from environment's active workstreams |
| Queue | Pre-populated from environment's active queues |
| Status | Completed / In Progress / Failed |

Selecting **All Cached** skips date filtering and loads everything already stored
in Dataverse instantly — useful for a broad overview without a live KQL call.

### Dataverse Caching Layer

Routing diagnostics data is now persisted in the Dataverse table
`alex_routingdiagnosticssummary`.  This enables a two-phase load strategy:

1. **Instant results from cache** — On Retrieve, the grid immediately renders any
   matching records already in Dataverse.
2. **Background live sync** — In parallel, the system calls the Custom API
   (`alex_GetRoutingDiagnosticsSummary`), which runs the KQL aggregation query
   against Application Insights and upserts fresh records back into Dataverse.
3. **Automatic polling** — A polling loop checks for new records every few seconds
   and updates the grid without requiring a manual refresh.  The loop stops when
   record counts stabilise across three consecutive polls, or after a 2-minute
   safety timeout.

This means the grid is never empty while waiting for a live query — cached data
appears first, and fresh data fills in silently behind the scenes.

### Enhanced Routing Story Panel

The per-conversation detail panel received substantial improvements:

#### Progress Tracker (Stepper)

A visual stepper at the top of the panel maps the routing lifecycle to a sequence
of stage nodes: **Intake → Classification → Queue Routing → Assignment**.

- 🟢 Green checkmark — stage completed successfully
- 🔴 Red cross — stage failed
- 🟠 Pulsing orange — stage in progress
- ⚫ Grey — stage not yet reached

Connector lines between nodes turn green as stages complete, providing an instant
sense of how far routing progressed and where it stopped.

#### Summary Section

Below the stepper, a hero section displays key routing facts in a structured grid:

- Workstream, routing duration, queue, assigned agent, channel, assignment method
- When a routing issue is detected (failed assignment, missing queue, classification
  error), a highlighted warning row appears with the finding and its severity.

An auto-generated **story summary** provides a short narrative of what happened:
which queue was targeted, whether classification rules applied, and how the
conversation was resolved.

#### Timeline Event Cards

Each routing event renders as an expandable card with:

- **Icon + label** — identifies the event type (e.g. 🏷 Classification, 👤 Agent Selected)
- **Result chip** — outcome (e.g. "Rule applied," "Failed," "Completed")
- **Meaning chip** — contextual detail (e.g. rule name, queue selected)
- **Timestamp + delta** — elapsed time since the previous event
- **Narrative** — a dynamically generated explanation incorporating actual names,
  rules, and outcomes from the raw data
- **Technical details** — expandable evidence section with raw fields; JSON renders
  as nested grids, FetchXML is pretty-printed, comma-separated values display as
  tag lists, and status codes map to human-readable badges

Supported event types (13):

> Conversation Started · Intake · Classification · Record Identification ·
> Line of Business · Queue Routing · Agent Selection · Agent Acceptance ·
> Bot Assignment · Bot Escalation · Conversation Ended · Session Closed ·
> Agent Rejoined

### Copilot Integration

The **Routing Insight** AI assistant is now available at every level of the
diagnostics experience:

| Surface | Trigger | Behaviour |
|---------|---------|-----------|
| **Grid row icon** | Hover over any row, click Copilot icon | Sends conversation context to assistant without opening the detail panel |
| **Panel "Ask Copilot" button** | Hero section of the Routing Story | Sends full routing context for in-depth analysis |
| **Inline "Explain with Copilot"** | Timeline cards flagged as errors/warnings | Sends a scenario-specific prompt tailored to the event type |
| **Working indicator** | While assistant is processing | Cycles through "Analysing…" → "Still working…" → "This is taking longer than expected…" |
| **Seamless handoff** | Any Copilot action when panel is closed | Panel opens automatically, initialises the connection, delivers the queued message |

Each contextual prompt is tailored to the event type — for example, a failed agent
assignment generates a prompt asking about capacity and skill-matching implications,
while a classification failure asks about rule configuration.

### Settings Panel

A **Settings** icon in the grid toolbar opens a settings panel.  All four settings
are stored as Dataverse environment variables:

| Setting | Environment Variable | Effect |
|---------|----------------------|--------|
| Auto-Update In-Progress Records | `alex_DiagnosticsAutoUpdate` | Periodically refreshes incomplete records from the last 7 days |
| Auto-Retrieve Diagnostics | `alex_DiagnosticsAutoRetrieve` | Calls the Custom API on a schedule without a manual Retrieve click |
| Show Copilot | `alex_show_copilot` | Toggles all Copilot UI surfaces on or off |
| Open Copilot by Default | `alex_show_copilot_default` | Opens the Copilot panel automatically on page load |

---

## v1.0.2 — Routing Insight Bot Improvements

### New Bot Capabilities

- **Direct record lookup** — The bot can retrieve a specific Dataverse record by ID,
  producing faster and more accurate answers for targeted questions.
- **Broader data retrieval** — Additional query variants give the bot more
  flexibility when gathering diagnostic context across different environments.
- **Graceful error handling** — A dedicated error-handling flow ensures the bot
  responds helpfully if something goes wrong mid-conversation.
- **Natural conversation closure** — The bot recognises gratitude and completion
  signals and responds appropriately before closing.

### Bug Fix — Panel Connectivity

**Problem:** After closing and reopening the Routing Insight assistant panel, a red
error *"Failed to connect: interaction is currently in progress"* appeared, preventing
the bot from loading.

**Root cause:** Calling `restartCopilot()` inside `closeCopilot()` started a new
MSAL authentication flow while the panel was hidden, leaving a stale
`interaction.status` lock in `sessionStorage`.

**Fix:** `closeCopilot()` now tears down the DOM and resets `copilotInitialized`
without triggering an MSAL flow.  The `restartCopilot()` function explicitly clears
any stale `interaction.status` keys from `sessionStorage` before constructing a new
`PublicClientApplication` instance.  A new `minimizeCopilot()` function hides the
panel without resetting the conversation, so the existing chat session is preserved
when the user reopens the panel.

### UI Improvements — Assistant Panel

| Button | Behaviour |
|--------|-----------|
| **Minimize (−)** | Hides the panel without ending the conversation; reopening resumes exactly where the user left off |
| **Close (×)** | Closes the panel and resets the conversation so the next open starts fresh |
| **Restart (↺)** | Resets the conversation while keeping the panel open |
| **Version (*i*)** | Small italic button at the far right of the search bar; shows current solution version on click as a floating popup |

---

## New Infrastructure Components

### Custom API

A new Dataverse Custom API was added alongside the existing one:

| | Existing | New |
|--|----------|-----|
| **Name** | `alex_GetRoutingDiagnostics` | `alex_GetRoutingDiagnosticsSummary` |
| **Purpose** | Fetch full timeline for a single conversation | Fetch aggregated summary rows for all conversations |
| **Inputs** | `ConversationId` | `DateFrom`, `DateTo`, `Workstream`, `Queue`, `Status`, `ForceRefresh` |
| **Outputs** | `ResultJson` | `ResultJson`, `RecordCount` |

### Power Automate Flow

**Flow name:** `Routing Diagnostics Summary — Retrieve`

Triggered by `alex_GetRoutingDiagnosticsSummary`, this flow:

1. Injects `DateFrom` / `DateTo` filter parameters into the KQL aggregation query.
2. Runs the query against Azure Monitor Logs (Application Insights).
3. Iterates over results (concurrency: 20) and upserts each record into Dataverse:
   - Checks whether a record with the same `alex_conversationid` already exists.
   - Updates the existing row, or creates a new one.
   - Sets `alex_retrievedon` to `utcNow()` on every write.
4. Returns `ResultJson` (the full result array as a JSON string) and `RecordCount`
   to the Custom API caller.

> **Timeout note:** Synchronous flows have a 2-minute execution limit.  With
> concurrency set to 20, the flow handles ~200 records comfortably within this
> window.  For larger datasets, increase the date range and rely on the polling
> loop to accumulate results across multiple calls.

Full flow documentation: [`src/flow/Flow-Routing-Diagnostics-Summary-Retrieve.md`](src/flow/Flow-Routing-Diagnostics-Summary-Retrieve.md)

### Dataverse Table

**Schema name:** `alex_routingdiagnosticssummary`  
**Display name:** Routing Diagnostics Summary

| Column (schema name) | Type | Description |
|----------------------|------|-------------|
| `alex_conversationid` | Single line of text | Lowercase GUID — natural key, used for upsert matching |
| `alex_name` | Single line of text (primary) | Same as `alex_diagnosticitem` |
| `alex_diagnosticitem` | Single line of text | Record title or conversation ID |
| `alex_routingstatus` | Single line of text | Human-readable routing outcome |
| `alex_routingstartedon` | Date and Time | UTC start of the routing process |
| `alex_routingduration` | Whole number | Duration in seconds |
| `alex_workstream` | Single line of text | Workstream name |
| `alex_queue` | Single line of text | Destination queue name |
| `alex_assignedagent` | Single line of text | Assigned agent display name |
| `alex_channel` | Single line of text | Omnichannel channel type |
| `alex_overallstatus` | Single line of text | `Completed` / `In Progress` / `Failed` |
| `alex_stagedetails` | Multiple lines of text | Full stage array as a JSON string |
| `alex_retrievedon` | Date and Time | UTC timestamp of the last upsert by the flow |

---

## Updated KQL Query

The query below aggregates raw `ConversationDiagnosticsScenario` traces from
Application Insights into one summary row per conversation.  It is the query used
by the Power Automate flow; the `lookbackDays` variable is replaced at runtime with
dynamic date parameters from the Custom API trigger.

```kql
// ── CONFIG ─────────────────────────────────────────────────────────────────
let lookbackDays = 7d;

// ── RAW EVENTS ─────────────────────────────────────────────────────────────
// Pull all ConversationDiagnosticsScenario traces within the lookback window,
// extract the key fields per event, and group by conversation.
let rawEvents =
    traces
    | where timestamp > ago(lookbackDays)
    | extend cd = parse_json(customDimensions)
    | where tostring(cd["powerplatform.analytics.scenario"]) == "ConversationDiagnosticsScenario"
    | extend
        conversationId  = tolower(tostring(cd["powerplatform.analytics.resource.id"])),
        stage           = tostring(cd["powerplatform.analytics.subscenario"]),
        description     = tostring(cd["powerplatform.analytics.diagnostics.description"]),
        channel         = tostring(cd["omnichannel.channel.type"]),
        result          = tostring(cd["omnichannel.result"]),
        assignmentStatus = tostring(cd["omnichannel.assignment.status"]),
        assignmentMethod = tostring(cd["omnichannel.assignment.method"]),
        agentName       = tostring(cd["omnichannel.initiator_agent.name"]),
        additionalInfo  = tostring(cd["omnichannel.additional_info"]),
        rulesExecuted   = tostring(cd["omnichannel.rules_executed_info"]),
        workItemDetails = tostring(cd["omnichannel.work_item.details"]),
        recordIdDetails = tostring(cd["omnichannel.record_identification_details"]),
        previousState   = tostring(cd["omnichannel.previous_state"]),
        newState        = tostring(cd["omnichannel.new_state"])
    | project
        timestamp,
        conversationId,
        stage,
        description,
        channel,
        result,
        assignmentStatus,
        assignmentMethod,
        agentName,
        additionalInfo,
        rulesExecuted,
        workItemDetails,
        recordIdDetails,
        previousState,
        newState;

// ── PER-CONVERSATION AGGREGATION ───────────────────────────────────────────
rawEvents
| summarize
    routingStartedOn   = min(timestamp),
    routingEndedOn     = max(timestamp),
    stages             = make_list(pack(
                            "stage", stage,
                            "timestamp", timestamp,
                            "description", description,
                            "result", result,
                            "assignmentStatus", assignmentStatus,
                            "assignmentMethod", assignmentMethod,
                            "agentName", agentName,
                            "rulesExecuted", rulesExecuted,
                            "workItemDetails", workItemDetails,
                            "recordIdDetails", recordIdDetails,
                            "additionalInfo", additionalInfo,
                            "previousState", previousState,
                            "newState", newState
                         )),
    // Channel: take first non-empty value
    channel            = take_any(channel),
    // Agent name: take from CSRAccepted event if present
    agentFromAccepted  = take_anyif(agentName, stage == "CSRAccepted"),
    // Queue: extract from RouteToQueue result
    queueResult        = take_anyif(result, stage == "RouteToQueue"),
    // Workstream: extract from ConversationDetails additionalInfo
    workstreamInfo     = take_anyif(additionalInfo, stage == "ConversationDetails"),
    // Description from ConversationCreated (contains record title for cases)
    createdDesc        = take_anyif(description, stage == "ConversationCreated"),
    // Record title from RecordDetails
    recordDesc         = take_anyif(description, stage == "RecordDetails"),
    // Assignment method
    method             = take_anyif(assignmentMethod, stage == "CSRAssignment")
    by conversationId

// ── DERIVE LAST STAGE ──────────────────────────────────────────────────────
| mv-apply stg = stages on (
    top 1 by todatetime(tostring(stg.timestamp)) desc
    | project lastStageName = tostring(stg.stage)
)

// ── FIELD DERIVATION ───────────────────────────────────────────────────────
| extend
    // Routing duration in seconds
    routingDuration = datetime_diff('second', routingEndedOn, routingStartedOn),

    // Queue name: parse the result JSON from RouteToQueue
    queue = coalesce(
        tostring(parse_json(queueResult).DisplayName),
        ""
    ),

    // Workstream name: parse additionalInfo from ConversationDetails
    workstream = coalesce(
        tostring(parse_json(workstreamInfo).LiveWorkStreamName),
        ""
    ),

    // Assigned agent
    assignedAgent = coalesce(agentFromAccepted, ""),

    // Overall status: derive from last stage
    overallStatus = case(
        lastStageName in ("CSRClosedSession", "ConversationClosed", "SessionClosed"), "Completed",
        lastStageName == "CSREndedConversation", "Completed",
        lastStageName == "CSRAccepted", "Completed",
        lastStageName == "CSRAssignment" and isnotempty(agentFromAccepted), "Completed",
        lastStageName == "CSRAssignment" and isempty(agentFromAccepted), "Failed",
        "In Progress"
    ),

    // Routing status: human-readable label
    routingStatus = case(
        lastStageName in ("CSRClosedSession", "ConversationClosed", "SessionClosed"),
            "Agent assignment - completed",
        lastStageName == "CSREndedConversation",
            "Agent assignment - completed",
        lastStageName == "CSRAccepted",
            "Agent assignment - completed",
        lastStageName == "CSRAssignment" and isnotempty(agentFromAccepted),
            "Agent assignment - completed",
        lastStageName == "CSRAssignment" and isempty(agentFromAccepted),
            "Assignment - failed",
        lastStageName == "RouteToQueue",
            "Workstream: Route-to-queue rules",
        lastStageName == "RecordIdentification",
            "Workstream: Classification rules",
        lastStageName == "ConversationCreated",
            "Intake rules - completed",
        lastStageName == "ConversationDetails",
            "Intake rules - completed",
        lastStageName == "RecordDetails",
            "Intake rules - completed",
        strcat(lastStageName, " - in progress")
    ),

    // Diagnostic item: record title or conversation description
    diagnosticItem = coalesce(
        // RecordDetails description contains "Record Id primary name attribute: <field> and its value : <value>"
        extract(@"its value\s*:\s*(.+)$", 1, recordDesc),
        // ConversationCreated description may contain "Record Id : <guid> for Conversation"
        extract(@"Record Id\s*:\s*([a-f0-9\-]+)", 1, createdDesc),
        conversationId
    )

// ── FINAL PROJECTION ───────────────────────────────────────────────────────
| project
    conversationId,
    diagnosticItem,
    routingStatus,
    routingStartedOn,
    routingDuration,
    workstream,
    queue,
    assignedAgent,
    channel,
    overallStatus,
    stageDetails = tostring(stages)
| order by routingStartedOn desc
```

### Dynamic Date Variant (Power Automate)

When the flow calls this query, the static `let lookbackDays = 7d;` line and the
`where timestamp > ago(lookbackDays)` clause are replaced with the date parameters
supplied by the Custom API trigger:

```kql
| where timestamp >= datetime(@{if(empty(triggerOutputs()?['body/InputParameters/DateFrom']), '2026-01-01', triggerOutputs()?['body/InputParameters/DateFrom'])})
    and timestamp <= datetime(@{if(empty(triggerOutputs()?['body/InputParameters/DateTo']), '2099-12-31', triggerOutputs()?['body/InputParameters/DateTo'])})
```

### Variant: Refresh In-Progress Records Only

For the scheduled auto-update flow (when `alex_DiagnosticsAutoUpdate = Yes`), add a
filter for specific conversation IDs after the `rawEvents` definition:

```kql
let targetIds = dynamic(["guid1", "guid2", "guid3"]);

// Add immediately after "let rawEvents = ... ;"
| where conversationId in (targetIds)
```

The flow reads all records from `alex_routingdiagnosticssummaries` where
`alex_overallstatus` is not `Completed` or `Failed`, collects their
`alex_conversationid` values, and passes them as `targetIds`.

---

## Environment Variables Reference

| Variable | Type | Purpose |
|----------|------|---------|
| `alex_show_copilot` | Boolean | Show / hide all Copilot UI surfaces |
| `alex_show_copilot_default` | Boolean | Open Copilot panel automatically on page load |
| `alex_DiagnosticsAutoUpdate` | Boolean | Auto-refresh in-progress records on a schedule |
| `alex_DiagnosticsAutoRetrieve` | Boolean | Auto-call the Custom API without a manual Retrieve click |
| `alex_AppInsightsWorkspaceId` | Text | Azure Application Insights workspace ID for KQL queries |
| `alex_CopilotStudioBotId` | Text | Copilot Studio bot schema name for Routing Insight |
| `alex_TenantId` | Text | Azure AD tenant ID for MSAL authentication |
