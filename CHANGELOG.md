# Changelog

All notable changes to this project are documented in this file.

---

## [1.0.3a] — 2026-05-02

### New Feature — MCS Bot Insights

The routing timeline now optionally displays **MCS (Microsoft Copilot Studio) bot telemetry** alongside the standard OC routing events:

- **MCS Insights toggle** — A checkbox next to the *Analyze* button enables bot telemetry. When checked, the Custom API also queries MCS events and merges them into the unified timeline.
- **Grouped bot segments** — Consecutive MCS events are collapsed into logical topic groups (e.g. "Bot Greeted Customer", "Escalate to Agent", "OC Context Received") rather than one row per raw telemetry event, keeping the timeline readable.
- **Color-coded source pills** — Each timeline card shows an *OC* or *MCS* pill and a left-border accent (blue for OC, purple for MCS) so the two event streams are visually distinct.
- **MCS summary cards** — When bot data is present, four extra summary cards appear: *Bot Outcome*, *Topics Visited*, *Bot Duration*, and *Bridge* (correlation link status).
- **Chat message view** — Bot cards expand to show the actual customer/bot message exchange and any action error codes.

### Bug Fix — Black Screen (JavaScript SyntaxError)

A stray closing brace `}` introduced in the MCS rendering code caused a `SyntaxError` that silently prevented the entire script block from running. As a result:

- The `initTheme()` IIFE never executed, leaving the page in its default CSS dark-mode state (`#0f1117` background — visible as a black screen).
- All UI interactions (Analyze button, theme toggle, Copilot panel) stopped responding.

The extra brace has been removed; the script now parses and executes correctly.

### Bug Fix — Theme Icon on Dark-Mode Reload

When the page loaded in **dark mode** (localStorage `ura-theme = 'dark'`), the theme-toggle button incorrectly showed 🌙 instead of ☀️. The `initTheme` function now sets the correct icon (☀️) when restoring a dark-mode preference.

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
