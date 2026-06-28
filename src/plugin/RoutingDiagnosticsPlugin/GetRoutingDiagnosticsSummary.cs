using System;
using System.Text;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace RoutingDiagnosticsPlugin
{
    /// <summary>
    /// Custom API plugin: alex_GetRoutingDiagnosticsSummary
    ///
    /// Reads the alex_routingdiagnosticssummary table and returns matching
    /// records as a JSON array based on optional filter parameters.
    ///
    /// Input Parameters:
    ///   - DateFrom       (String, optional)  ISO 8601 date string for start of range
    ///   - DateTo         (String, optional)  ISO 8601 date string for end of range
    ///   - Workstream     (String, optional)  Exact workstream name filter
    ///   - Queue          (String, optional)  Exact queue name filter
    ///   - Status         (String, optional)  Overall status filter (Completed/Failed/In Progress)
    ///   - ForceRefresh   (Boolean, optional) If true, triggers re-fetch logic (future use)
    ///
    /// Output Parameters:
    ///   - ResultJson     (String) JSON array of summary records
    ///   - RecordCount    (Integer) Number of records returned
    /// </summary>
    public class GetRoutingDiagnosticsSummary : IPlugin
    {
        private const string TableName = "alex_routingdiagnosticssummary";
        private const int MaxRecords = 500;

        public void Execute(IServiceProvider serviceProvider)
        {
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            var tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));
            var serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            var service = serviceFactory.CreateOrganizationService(context.UserId);

            try
            {
                // ── Read input parameters ──────────────────────────────────
                string dateFrom = GetInputParam<string>(context, "DateFrom");
                string dateTo = GetInputParam<string>(context, "DateTo");
                string workstream = GetInputParam<string>(context, "Workstream");
                string queue = GetInputParam<string>(context, "Queue");
                string status = GetInputParam<string>(context, "Status");
                bool forceRefresh = GetInputParam<bool>(context, "ForceRefresh");

                tracingService.Trace("GetRoutingDiagnosticsSummary: DateFrom={0}, DateTo={1}, Workstream={2}, Queue={3}, Status={4}, ForceRefresh={5}",
                    dateFrom ?? "(null)", dateTo ?? "(null)", workstream ?? "(null)", queue ?? "(null)", status ?? "(null)", forceRefresh);

                // ── Build query ────────────────────────────────────────────
                var query = new QueryExpression(TableName)
                {
                    ColumnSet = new ColumnSet(
                        "alex_conversationid",
                        "alex_diagnosticitem",
                        "alex_routingstatus",
                        "alex_routingstartedon",
                        "alex_routingduration",
                        "alex_workstream",
                        "alex_queue",
                        "alex_assignedagent",
                        "alex_channel",
                        "alex_stagedetails",
                        "alex_overallstatus",
                        "alex_retrievedon"
                    ),
                    TopCount = MaxRecords,
                    Orders =
                    {
                        new OrderExpression("alex_routingstartedon", OrderType.Descending)
                    }
                };

                // Date range filter
                if (!string.IsNullOrEmpty(dateFrom))
                {
                    if (DateTime.TryParse(dateFrom, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dtFrom))
                    {
                        query.Criteria.AddCondition("alex_routingstartedon", ConditionOperator.OnOrAfter, dtFrom);
                        tracingService.Trace("Filter: DateFrom >= {0}", dtFrom.ToString("o"));
                    }
                }

                if (!string.IsNullOrEmpty(dateTo))
                {
                    if (DateTime.TryParse(dateTo, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dtTo))
                    {
                        // Include the full end day
                        var endOfDay = dtTo.Date.AddDays(1).AddMilliseconds(-1);
                        query.Criteria.AddCondition("alex_routingstartedon", ConditionOperator.OnOrBefore, endOfDay);
                        tracingService.Trace("Filter: DateTo <= {0}", endOfDay.ToString("o"));
                    }
                }

                // Workstream filter
                if (!string.IsNullOrEmpty(workstream))
                {
                    query.Criteria.AddCondition("alex_workstream", ConditionOperator.Equal, workstream);
                    tracingService.Trace("Filter: Workstream = {0}", workstream);
                }

                // Queue filter
                if (!string.IsNullOrEmpty(queue))
                {
                    query.Criteria.AddCondition("alex_queue", ConditionOperator.Equal, queue);
                    tracingService.Trace("Filter: Queue = {0}", queue);
                }

                // Overall status filter
                if (!string.IsNullOrEmpty(status))
                {
                    query.Criteria.AddCondition("alex_overallstatus", ConditionOperator.Equal, status);
                    tracingService.Trace("Filter: Status = {0}", status);
                }

                // ── Execute query ──────────────────────────────────────────
                var results = service.RetrieveMultiple(query);
                tracingService.Trace("Retrieved {0} records", results.Entities.Count);

                // ── Build JSON response ────────────────────────────────────
                var json = new StringBuilder();
                json.Append("[");

                for (int i = 0; i < results.Entities.Count; i++)
                {
                    if (i > 0) json.Append(",");
                    json.Append(EntityToJson(results.Entities[i]));
                }

                json.Append("]");

                // ── Set output parameters ──────────────────────────────────
                context.OutputParameters["ResultJson"] = json.ToString();
                context.OutputParameters["RecordCount"] = results.Entities.Count;

                tracingService.Trace("GetRoutingDiagnosticsSummary completed. Records: {0}", results.Entities.Count);
            }
            catch (Exception ex)
            {
                tracingService.Trace("GetRoutingDiagnosticsSummary error: {0}", ex.ToString());
                throw new InvalidPluginExecutionException($"Error retrieving routing diagnostics summary: {ex.Message}", ex);
            }
        }

        private static T GetInputParam<T>(IPluginExecutionContext context, string name)
        {
            if (context.InputParameters.Contains(name) && context.InputParameters[name] != null)
            {
                return (T)context.InputParameters[name];
            }
            return default;
        }

        private static string EntityToJson(Entity entity)
        {
            var sb = new StringBuilder();
            sb.Append("{");

            AppendJsonField(sb, "conversationId", entity.GetAttributeValue<string>("alex_conversationid"));
            sb.Append(",");
            AppendJsonField(sb, "diagnosticItem", entity.GetAttributeValue<string>("alex_diagnosticitem"));
            sb.Append(",");
            AppendJsonField(sb, "routingStatus", entity.GetAttributeValue<string>("alex_routingstatus"));
            sb.Append(",");

            var startedOn = entity.GetAttributeValue<DateTime?>("alex_routingstartedon");
            AppendJsonField(sb, "routingStartedOn", startedOn?.ToString("o"));
            sb.Append(",");

            var duration = entity.GetAttributeValue<int?>("alex_routingduration");
            sb.AppendFormat("\"routingDuration\":{0}", duration.HasValue ? duration.Value.ToString() : "null");
            sb.Append(",");

            AppendJsonField(sb, "workstream", entity.GetAttributeValue<string>("alex_workstream"));
            sb.Append(",");
            AppendJsonField(sb, "queue", entity.GetAttributeValue<string>("alex_queue"));
            sb.Append(",");
            AppendJsonField(sb, "assignedAgent", entity.GetAttributeValue<string>("alex_assignedagent"));
            sb.Append(",");
            AppendJsonField(sb, "channel", entity.GetAttributeValue<string>("alex_channel"));
            sb.Append(",");
            AppendJsonField(sb, "overallStatus", entity.GetAttributeValue<string>("alex_overallstatus"));
            sb.Append(",");

            // stageDetails is already JSON — embed it raw
            var stageDetails = entity.GetAttributeValue<string>("alex_stagedetails");
            sb.AppendFormat("\"stageDetails\":{0}", string.IsNullOrEmpty(stageDetails) ? "[]" : stageDetails);
            sb.Append(",");

            var retrievedOn = entity.GetAttributeValue<DateTime?>("alex_retrievedon");
            AppendJsonField(sb, "retrievedOn", retrievedOn?.ToString("o"));

            sb.Append("}");
            return sb.ToString();
        }

        private static void AppendJsonField(StringBuilder sb, string name, string value)
        {
            sb.AppendFormat("\"{0}\":", name);
            if (value == null)
            {
                sb.Append("null");
            }
            else
            {
                sb.Append("\"");
                sb.Append(EscapeJson(value));
                sb.Append("\"");
            }
        }

        private static string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
        }
    }
}
