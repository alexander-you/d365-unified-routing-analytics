# Routing Diagnostics Summary — Aggregation KQL

This query aggregates routing diagnostic events from Application Insights into
one summary row per conversation, matching the fields in the
`alex_routingdiagnosticssummary` Dataverse table.

## Parameters

| Parameter | Description |
|---|---|
| `lookbackDays` | Number of days to look back (default: 7) |
| `existingIds` | Optional: comma-separated list of conversation IDs to exclude (already in Dataverse). Pass empty string to retrieve all. |

---

## Query

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

---

## Field Mapping to Dataverse Table

| KQL Output Column | Dataverse Field |
|---|---|
| `conversationId` | `alex_conversationid` |
| `diagnosticItem` | `alex_diagnosticitem` |
| `routingStatus` | `alex_routingstatus` |
| `routingStartedOn` | `alex_routingstartedon` |
| `routingDuration` | `alex_routingduration` |
| `workstream` | `alex_workstream` |
| `queue` | `alex_queue` |
| `assignedAgent` | `alex_assignedagent` |
| `channel` | `alex_channel` |
| `overallStatus` | `alex_overallstatus` |
| `stageDetails` | `alex_stagedetails` |

> `alex_name` (primary name) should be set to `diagnosticItem` when creating the record.
> `alex_retrievedon` should be set to `utcNow()` by the flow at write time.

---

## Variant: Refresh In-Progress Records Only

For the scheduled update flow (when `alex_DiagnosticsAutoUpdate = Yes`),
use the same query but add a filter for specific conversation IDs:

```kql
let targetIds = dynamic(["guid1", "guid2", "guid3"]);

// Insert after the rawEvents definition:
| where conversationId in (targetIds)
```

The flow reads all records from `alex_routingdiagnosticssummaries` where
`alex_overallstatus` is not `Completed` or `Failed`, collects their
`alex_conversationid` values, and passes them as `targetIds`.
