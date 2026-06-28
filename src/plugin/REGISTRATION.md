## Plugin Registration — alex_GetRoutingDiagnosticsSummary

### 1. Register the Assembly

1. Open the **Plugin Registration Tool**
2. Click **Register** → **Register New Assembly**
3. Browse to: `src\plugin\RoutingDiagnosticsPlugin\bin\Release\net462\RoutingDiagnosticsPlugin.dll`
4. Isolation Mode: **Sandbox**
5. Location: **Database**
6. Click **Register Selected Plugins**

### 2. Create the Custom API

In the Dataverse maker portal (or via API), create the following:

**Custom API:**

| Property | Value |
|---|---|
| Unique Name | `alex_GetRoutingDiagnosticsSummary` |
| Name | `GetRoutingDiagnosticsSummary` |
| Display Name | `Get Routing Diagnostics Summary` |
| Description | Retrieves filtered routing diagnostic summary records from the alex_routingdiagnosticssummary table |
| Binding Type | Global |
| Is Function | No (Action) |
| Is Private | No |
| Plugin Type | `RoutingDiagnosticsPlugin.GetRoutingDiagnosticsSummary` |
| Allowed Custom Processing Step Type | None |

**Request Parameters:**

| Unique Name | Display Name | Type | Required | Description |
|---|---|---|---|---|
| `DateFrom` | Date From | String | No | ISO 8601 date string — start of date range |
| `DateTo` | Date To | String | No | ISO 8601 date string — end of date range |
| `Workstream` | Workstream | String | No | Filter by exact workstream name |
| `Queue` | Queue | String | No | Filter by exact queue name |
| `Status` | Status | String | No | Filter by overall status (Completed / Failed / In Progress) |
| `ForceRefresh` | Force Refresh | Boolean | No | Reserved for future use — triggers re-fetch from App Insights |

**Response Properties:**

| Unique Name | Display Name | Type | Description |
|---|---|---|---|
| `ResultJson` | Result JSON | String | JSON array of summary records |
| `RecordCount` | Record Count | Integer | Number of records returned |

### 3. Test the Custom API

From the browser console (on a model-driven app page) or via Postman:

```javascript
// Browser console test
var req = {
    getMetadata: function() {
        return {
            boundParameter: null,
            operationName: "alex_GetRoutingDiagnosticsSummary",
            operationType: 0,
            parameterTypes: {
                DateFrom:     { typeName: "Edm.String",  structuralProperty: 1 },
                DateTo:       { typeName: "Edm.String",  structuralProperty: 1 },
                Workstream:   { typeName: "Edm.String",  structuralProperty: 1 },
                Queue:        { typeName: "Edm.String",  structuralProperty: 1 },
                Status:       { typeName: "Edm.String",  structuralProperty: 1 },
                ForceRefresh: { typeName: "Edm.Boolean", structuralProperty: 1 }
            }
        };
    },
    DateFrom: "2026-05-01",
    DateTo: "2026-05-21"
};

Xrm.WebApi.online.execute(req).then(
    function(response) { return response.json(); }
).then(function(data) {
    console.log("Records:", data.RecordCount);
    console.log("Data:", JSON.parse(data.ResultJson));
});
```

### 4. Output JSON Format

Each record in the `ResultJson` array has this shape:

```json
{
  "conversationId": "edb95d19-68f6-4837-9a5e-257f530b04f4",
  "diagnosticItem": "My Coffee Maker has stopped working",
  "routingStatus": "Agent assignment - completed",
  "routingStartedOn": "2026-05-19T04:15:00.000Z",
  "routingDuration": 55,
  "workstream": "CCD Record Workstream",
  "queue": "CCD USA Entity Support Queue",
  "assignedAgent": "Eugene Spelmann",
  "channel": "Record",
  "overallStatus": "Completed",
  "stageDetails": [ ... ],
  "retrievedOn": "2026-05-21T10:00:00.000Z"
}
```
