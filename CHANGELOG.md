# Changelog

All notable changes to this project are documented in this file.

---

## [1.0.2] — 2026-04-26

### Routing Insight Bot — New Capabilities

The **Routing Insight** AI assistant has been significantly expanded with new tools and improved conversation handling:

- **Direct record lookup** — The bot can now retrieve a specific Dataverse record by its ID. When you ask about a particular conversation, queue, or agent, the bot pinpoints the exact record rather than scanning a list, producing faster and more accurate answers.
- **Broader data retrieval** — Additional query variants for listing Dataverse rows give the bot more flexibility when gathering diagnostic context across different environments and scenarios.
- **Graceful error handling** — A new dedicated error-handling flow ensures the bot responds helpfully if something goes wrong mid-conversation, rather than going silent or ending the session unexpectedly.
- **Natural conversation closure** — The bot now recognises when users express gratitude or indicate they are done, and responds appropriately before closing — creating a more natural, professional interaction.

### Bug Fix — Assistant Panel Connectivity

**What users experienced:** After closing the Routing Insight assistant panel and reopening it, a red error message appeared — *"Failed to connect: interaction is currently in progress"* — preventing the bot from loading.

**What was fixed:** The panel now connects reliably every time it is opened. Closing and reopening the assistant, even in quick succession, no longer triggers this error.

### UI Improvements — Assistant Panel

- **Minimize (−)** — Collapses the panel without ending the conversation. Users can return and continue exactly where they left off, with no reconnection required.
- **Close (×)** — Closes the panel and resets the conversation so the next open starts fresh.
- **Restart (↺)** — Resets the conversation while keeping the panel open.
- **Version indicator** — An *i* button in the top-right corner of the toolbar shows the current solution version on click.

---

## [1.0.1] — Initial Release

- Visual routing timeline dashboard for Dynamics 365 Unified Routing
- Expandable step-by-step routing lifecycle visualization with color-coded status indicators
- Embedded **Routing Insight** Copilot Studio agent with SSO authentication (MSAL + M365 Agents SDK)
- Custom API (`alex_GetRoutingDiagnostics`) backed by a Power Automate flow querying Azure Monitor Logs (KQL)
- Automated staging table cleanup (24-hour log rotation)
- Light/dark theme toggle
- Environment variable–driven configuration (no hardcoded tenant values)
