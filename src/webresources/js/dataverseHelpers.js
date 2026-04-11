// ════════════════════════════════════════════════
// Dataverse Helper Functions
// Unified Routing Analytics
// ════════════════════════════════════════════════

export function getXrm() {
  if (typeof Xrm !== 'undefined') return Xrm;
  if (window.parent?.Xrm) return window.parent.Xrm;
  return null;
}

export function getEnvironmentId() {
  return Xrm.Utility.getGlobalContext().organizationSettings.bapEnvironmentId;
}

export function getTenantId() {
  return Xrm.Utility.getGlobalContext().organizationSettings.organizationTenant;
}

export async function getEnvironmentVariableValue(variableName) {
  var query = "?$filter=schemaname eq '" + variableName + "'&$select=value";
  const response = await Xrm.WebApi.retrieveMultipleRecords("environmentvariablevalue", query);
  if (response.entities.length === 0) return null;
  return response.entities[0].value;
}

export async function getUsername() {
  var userSettings = Xrm.Utility.getGlobalContext().userSettings;
  var id = userSettings.userId.replace("{", "").replace("}", "");
  var options = "?$select=domainname";
  var response = await Xrm.WebApi.retrieveRecord("systemuser", id, options);
  return response.domainname;
}
