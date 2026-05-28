# Flow: Routing Diagnostics Summary — Retrieve

## Overview

This Flow is triggered when the Custom API `alex_GetRoutingDiagnosticsSummary` is
called from the UI. It queries Application Insights for routing diagnostic data,
persists the results into the Dataverse summary table, and returns the aggregated
JSON to the caller.

---

## Trigger

**When an action is performed** (Dataverse connector)

| Property    | Value                                |
| ----------- | ------------------------------------ |
| Catalog     | None                                 |
| Category    | None                                 |
| Table name  | None                                 |
| Action name | `alex_GetRoutingDiagnosticsSummary`   |

### Available Input Parameters from Trigger

| Parameter      | Type    | Expression Path                                              |
| -------------- | ------- | ------------------------------------------------------------ |
| `DateFrom`     | String  | `triggerOutputs()?['body/InputParameters/DateFrom']`          |
| `DateTo`       | String  | `triggerOutputs()?['body/InputParameters/DateTo']`            |
| `Workstream`   | String  | `triggerOutputs()?['body/InputParameters/Workstream']`        |
| `Queue`        | String  | `triggerOutputs()?['body/InputParameters/Queue']`             |
| `Status`       | String  | `triggerOutputs()?['body/InputParameters/Status']`            |
| `ForceRefresh` | Boolean | `triggerOutputs()?['body/InputParameters/ForceRefresh']`      |

---

## Step 1 — Initialize Variables

Create two variables to collect results for the response.

| Variable Name    | Type    | Initial Value |
| ---------------- | ------- | ------------- |
| `varResultArray` | Array   | `[]`          |
| `varRecordCount` | Integer | `0`           |

---

## Step 2 — Compose KQL Query

**Action:** Compose  
**Name:** `Build KQL Query`

Paste the full KQL query (see `src/webresources/kql-aggregation-query.md`), with
the following modification to inject the date parameters from the trigger.

### Original line (remove):

```kql
let lookbackDays = 7d;
```

And in the `rawEvents` block, replace:

```kql
| where timestamp > ago(lookbackDays)
```

### Replacement (dynamic dates):

```kql
| where timestamp >= datetime(@{if(empty(triggerOutputs()?['body/InputParameters/DateFrom']), '2026-01-01', triggerOutputs()?['body/InputParameters/DateFrom'])})
    and timestamp <= datetime(@{if(empty(triggerOutputs()?['body/InputParameters/DateTo']), '2099-12-31', triggerOutputs()?['body/InputParameters/DateTo'])})
```

> **Note:** The rest of the KQL query remains unchanged — aggregation,
> `mv-apply`, field derivation, and final projection are all kept as-is.

---

## Step 3 — Run KQL Against Application Insights

**Action:** Azure Monitor Logs — Run query and list results

| Property       | Value                              |
| -------------- | ---------------------------------- |
| Subscription   | *(your Azure subscription)*        |
| Resource Group | *(your App Insights resource group)*|
| Resource Name  | *(your App Insights instance)*     |
| Query          | Output of `Build KQL Query`        |
| Time Range     | Set in query                       |

---

## Step 4 — Apply to Each (Process Results)

**Action:** Apply to each  
**Input:** `value` from **Run query and list results**

> **Performance tip:** Set concurrency to **20** (under Settings) to speed up
> the upsert loop.

### Step 4a — Compose Record JSON

**Action:** Compose  
**Name:** `Record JSON`

```json
{
  "conversationId":   "@{items('Apply_to_each')?['conversationId']}",
  "diagnosticItem":   "@{items('Apply_to_each')?['diagnosticItem']}",
  "routingStatus":    "@{items('Apply_to_each')?['routingStatus']}",
  "routingStartedOn": "@{items('Apply_to_each')?['routingStartedOn']}",
  "routingDuration":  @{items('Apply_to_each')?['routingDuration']},
  "workstream":       "@{items('Apply_to_each')?['workstream']}",
  "queue":            "@{items('Apply_to_each')?['queue']}",
  "assignedAgent":    "@{items('Apply_to_each')?['assignedAgent']}",
  "channel":          "@{items('Apply_to_each')?['channel']}",
  "overallStatus":    "@{items('Apply_to_each')?['overallStatus']}",
  "stageDetails":     @{items('Apply_to_each')?['stageDetails']}
}
```

> `routingDuration` is numeric (no quotes).  
> `stageDetails` is raw JSON (no quotes) — it's already a JSON array string from
> the KQL output.

### Step 4b — Append to Result Array

**Action:** Append to array variable

| Property | Value                        |
| -------- | ---------------------------- |
| Name     | `varResultArray`             |
| Value    | Output of `Record JSON`      |

### Step 4c — Upsert to Dataverse

#### 4c-i. Check if record exists

**Action:** List rows (Dataverse)

| Property   | Value                                                                 |
| ---------- | --------------------------------------------------------------------- |
| Table name | `Routing Diagnostics Summaries`                                       |
| Filter     | `alex_conversationid eq '@{items('Apply_to_each')?['conversationId']}'` |
| Row count  | `1`                                                                   |

#### 4c-ii. Condition

**Expression:**  
`length(body('List_rows')?['value'])` is greater than `0`

#### YES branch — Update existing row

**Action:** Update a row (Dataverse)

| Property                | Value                                                              |
| ----------------------- | ------------------------------------------------------------------ |
| Table name              | `Routing Diagnostics Summaries`                                    |
| Row ID                  | `first(body('List_rows')?['value'])?['alex_routingdiagnosticssummaryid']` |
| `alex_name`             | `items('Apply_to_each')?['diagnosticItem']`                        |
| `alex_diagnosticitem`   | `items('Apply_to_each')?['diagnosticItem']`                        |
| `alex_routingstatus`    | `items('Apply_to_each')?['routingStatus']`                         |
| `alex_routingstartedon` | `items('Apply_to_each')?['routingStartedOn']`                      |
| `alex_routingduration`  | `items('Apply_to_each')?['routingDuration']`                       |
| `alex_workstream`       | `items('Apply_to_each')?['workstream']`                            |
| `alex_queue`            | `items('Apply_to_each')?['queue']`                                 |
| `alex_assignedagent`    | `items('Apply_to_each')?['assignedAgent']`                         |
| `alex_channel`          | `items('Apply_to_each')?['channel']`                               |
| `alex_overallstatus`    | `items('Apply_to_each')?['overallStatus']`                         |
| `alex_stagedetails`     | `items('Apply_to_each')?['stageDetails']`                          |
| `alex_retrievedon`      | `utcNow()`                                                         |

> **Do NOT update** `alex_conversationid` — it is the match key.

#### NO branch — Create new row

**Action:** Add a new row (Dataverse)

| Property                | Value                                                  |
| ----------------------- | ------------------------------------------------------ |
| Table name              | `Routing Diagnostics Summaries`                        |
| `alex_conversationid`   | `items('Apply_to_each')?['conversationId']`             |
| `alex_name`             | `items('Apply_to_each')?['diagnosticItem']`             |
| `alex_diagnosticitem`   | `items('Apply_to_each')?['diagnosticItem']`             |
| `alex_routingstatus`    | `items('Apply_to_each')?['routingStatus']`              |
| `alex_routingstartedon` | `items('Apply_to_each')?['routingStartedOn']`           |
| `alex_routingduration`  | `items('Apply_to_each')?['routingDuration']`            |
| `alex_workstream`       | `items('Apply_to_each')?['workstream']`                 |
| `alex_queue`            | `items('Apply_to_each')?['queue']`                      |
| `alex_assignedagent`    | `items('Apply_to_each')?['assignedAgent']`              |
| `alex_channel`          | `items('Apply_to_each')?['channel']`                    |
| `alex_overallstatus`    | `items('Apply_to_each')?['overallStatus']`              |
| `alex_stagedetails`     | `items('Apply_to_each')?['stageDetails']`               |
| `alex_retrievedon`      | `utcNow()`                                              |

---

## Step 5 — Set Record Count

**Action:** Set variable

| Property | Value                               |
| -------- | ----------------------------------- |
| Name     | `varRecordCount`                    |
| Value    | `length(variables('varResultArray'))` |

---

## Step 6 — Return Response to Custom API

**Action:** Return value(s) to the caller (Dataverse connector)

> This action maps to the Custom API's response properties.

| Output Parameter | Value                                  |
| ---------------- | -------------------------------------- |
| `ResultJson`     | `string(variables('varResultArray'))`  |
| `RecordCount`    | `variables('varRecordCount')`          |

---

## Flow Diagram

```
┌─────────────────────────────────────────┐
│  Trigger: When action is performed      │
│  Action: alex_GetRoutingDiagnostics...  │
│  Inputs: DateFrom, DateTo, Workstream,  │
│          Queue, Status, ForceRefresh    │
└──────────────────┬──────────────────────┘
                   │
     ┌─────────────▼─────────────┐
     │  Step 1: Initialize vars  │
     │  varResultArray = []      │
     │  varRecordCount = 0       │
     └─────────────┬─────────────┘
                   │
     ┌─────────────▼─────────────┐
     │  Step 2: Compose KQL      │
     │  (inject date params)     │
     └─────────────┬─────────────┘
                   │
     ┌─────────────▼─────────────┐
     │  Step 3: Run query and    │
     │  list results             │
     │  (Application Insights)   │
     └─────────────┬─────────────┘
                   │
     ┌─────────────▼─────────────┐
     │  Step 4: Apply to each    │
     │  ┌──────────────────────┐ │
     │  │ 4a. Compose JSON     │ │
     │  │ 4b. Append to array  │ │
     │  │ 4c. Upsert to DV    │ │
     │  │   ├─ List rows       │ │
     │  │   ├─ Condition       │ │
     │  │   ├─ Yes: Update     │ │
     │  │   └─ No:  Create     │ │
     │  └──────────────────────┘ │
     └─────────────┬─────────────┘
                   │
     ┌─────────────▼─────────────┐
     │  Step 5: Set record count │
     └─────────────┬─────────────┘
                   │
     ┌─────────────▼─────────────┐
     │  Step 6: Return values    │
     │  ResultJson + RecordCount │
     └───────────────────────────┘
```

---

## Constraints & Notes

| Concern            | Detail                                                            |
| ------------------ | ----------------------------------------------------------------- |
| **Timeout**        | Synchronous flows have a **2-minute** execution limit.            |
| **Concurrency**    | Set Apply to each concurrency to **20** for parallel upserts.     |
| **Record limit**   | KQL returns up to 10,000 rows by default; practical limit ~200 for the 2-min window. |
| **Conversation ID**| Never updated on existing records — used as the match key.        |
| **Retrieved On**   | Set to `utcNow()` on both create and update.                     |
| **stageDetails**   | Stored as raw JSON string in the Memo field — no parsing needed.  |
