# Changelog

All notable changes to this project are documented in this file.

---

## [1.0.4] — 2026-06-30

### Routing Story — Grouped "Rules Executed" in the Assignment card

The **Rules Executed** evidence inside the *Agent Selected* (assignment) card is now organized to mirror the queue's **Assignment method** structure, instead of being shown as a flat list of raw fields. The rules are split into up to three ordered sub-headings that follow the actual routing lifecycle:

1. **Prioritization** *(configured on queue)* — the prioritization ruleset that set the conversation's order within the queue (`PrioritizationRuleExecuted`).
2. **Selection** — the selection ruleset that determined which agents were eligible (`SelectionRuleExecuted`).
3. **Assignment** — the assignment rule / ruleset that picked the final agent (`AssignmentRuleExecuted`, `AssignmentRuleSetExecuted`).

Each rule continues to resolve its GUID to the human-readable rule name, condition, and owning ruleset. Empty groups are hidden automatically — so conversations whose telemetry did not emit a prioritization or selection rule simply omit that sub-heading rather than showing a blank or zero-GUID entry. This makes it immediately clear *why* a conversation was prioritized and assigned the way it was, without changing the timeline or inventing telemetry events that the platform does not emit.

### Grid performance — pagination

The diagnostics grid now paginates results (50 / 100 / 250 / 500 per page) with Prev/Next navigation and a live record-count indicator. Previously, retrieving large result sets (~2,000 records) rendered every row at once with cumulative entry animations, which could freeze the browser. Row animation delays are now capped, and only the current page is rendered, keeping the grid responsive on large environments.

### Data hygiene

- Zero-GUIDs (`00000000-0000-0000-0000-000000000000`) are now treated as empty and hidden across the detail panel.
- Assignment ruleset GUIDs resolve to their ruleset name (typed as *Rule Set*) via an expanded rule index.

> Web resource internal version: **v3.4.1**. Solution version: **1.0.0.4**.

---

## [1.0.3] — 2026-05-24

### Routing Diagnostics Grid — A New Way to Investigate Routing

This release introduces a fundamentally new experience for investigating unified routing. Instead of analyzing one conversation at a time, the **Routing Diagnostics Grid** presents all routed conversations in a sortable, filterable table — giving supervisors and administrators an at-a-glance view of routing activity across the organization.

Each row in the grid displays the key facts for a routed conversation: the diagnostic item name, routing status, start time, duration, workstream, destination queue, and assigned agent. Clicking any column header sorts the grid ascending or descending — newest-first by default. Rows animate in with staggered transitions for a polished, responsive feel.

Clicking any row opens the full **Routing Story** detail panel for that conversation, with the complete step-by-step timeline. The previous capability to analyze routing for an individual conversation remains fully available and unchanged — the grid is a new entry point that makes it easier to find and navigate between conversations.

### Filtering and Retrieval

The grid header includes a filter bar with four controls that let you narrow results before retrieval:

- **Date range** — Choose from preset ranges (Today, Yesterday, Last 7 Days, This Month, Last 30 Days, Last 90 Days) or specify a custom date range. The default option, "All Cached," retrieves everything already stored in Dataverse without date bounds.
- **Workstream** — Filter to a specific workstream. The dropdown is pre-populated from your environment's active workstreams.
- **Queue** — Filter to a specific queue. Pre-populated from active queues in your environment.
- **Status** — Filter by routing outcome: Completed, In Progress, or Failed.

A **Retrieve** button triggers data loading with the selected filters. A statistics bar below the grid shows live totals — total records, completed, failed, and average routing duration — updating as data arrives.

### Cached Dataverse Diagnostics Records

Routing diagnostics data is now cached in a dedicated Dataverse table (`alex_routingdiagnosticssummary`). This changes the data flow in an important way:

1. **Immediate results from cache** — When you click Retrieve, the grid first loads any matching records already stored in Dataverse, so results appear instantly.
2. **Background sync from source** — In parallel, the system calls the Custom API (`alex_GetRoutingDiagnosticsSummary`) to fetch fresh diagnostics from Azure Monitor. As new records arrive, the grid updates automatically via a polling loop — no manual refresh needed.
3. **Stability detection** — The polling loop monitors record counts and stops automatically once the data stabilizes (three consecutive polls with no new records) or after a two-minute safety timeout.

This two-phase approach means the grid is never empty while waiting for a live query — cached data is always shown first, and fresh data fills in behind the scenes.

### Routing Story — Enhanced Timeline and Analysis

The per-conversation detail panel has been significantly enhanced with new visualization and narrative capabilities:

**Progress Tracker (Stepper)**
A visual stepper at the top of the panel shows the routing lifecycle as a sequence of stage nodes — from Intake through Classification, Queue Routing, and Assignment. Each node is color-coded by status: green checkmark for completed, red cross for failed, pulsing orange for in-progress, and grey for pending. Connector lines between nodes turn green as stages complete, giving an immediate sense of how far routing progressed and where it stopped.

**Summary Section**
Below the stepper, a hero section displays key routing facts in a structured grid: workstream, routing duration, queue, assigned agent, channel, and assignment method. When the system detects a routing issue — a failed assignment, a missing queue, or a classification error — a highlighted warning row appears at the top with the finding and its severity.

An auto-generated **story summary** provides a one-to-three sentence narrative of what happened during routing: which queue was targeted, whether classification rules applied, and how the conversation was ultimately resolved. Warnings are called out explicitly (e.g., "⚠ Attention needed at: Assignment, Queue Routing").

**Timeline Event Cards**
Each routing event is rendered as an expandable card with:
- An **icon and label** identifying the event type (e.g., 🏷 Classification, 👤 Agent Selected, 🤖 Bot Assigned)
- A **result chip** showing the outcome (e.g., "Rule applied," "Failed," "Completed")
- A **meaning chip** with contextual detail (e.g., the name of the rule that fired, the queue selected)
- A **timestamp** and **delta** showing elapsed time since the previous event
- A **narrative** — a dynamically generated explanation of what happened at this stage, incorporating actual names, rules, and outcomes from the raw data
- **Technical details** — an expandable evidence section showing all raw fields from the diagnostic record, with intelligent formatting: JSON objects render as nested grids, FetchXML is pretty-printed, comma-separated values display as tag lists, and status codes are mapped to human-readable badges

The timeline supports 13 distinct event types covering the full routing lifecycle: conversation started, intake, classification, record identification, line of business, queue routing, agent selection, agent acceptance, bot assignment, bot escalation, conversation ended, session closed, and agent rejoined.

### Copilot Integration

The **Routing Insight** AI assistant is now embedded throughout the diagnostics experience, available at every level — from the grid, the detail panel, and individual timeline cards:

- **Grid row icon** — Each row in the grid shows a Copilot icon on hover. Clicking it sends the routing conversation context directly to the assistant without opening the detail panel, enabling quick triage across multiple conversations.
- **Panel "Ask Copilot" button** — When viewing a conversation's routing story, a button in the hero section sends the full routing context to Copilot for in-depth analysis.
- **Contextual "Explain with Copilot"** — Timeline cards flagged as errors or warnings display an inline button that sends a scenario-specific prompt to the assistant. Each prompt is tailored to the event type — for example, a failed agent assignment generates a prompt asking the bot to explain capacity and skill-matching implications, while a classification failure asks about rule configuration. This turns every problem card into a one-click investigation.
- **Working indicator** — A progress indicator with contextual messages appears in the Copilot panel while the assistant is processing: "Analyzing…" initially, advancing to "Still working…" and "This is taking longer than expected…" for longer operations. The indicator hides automatically when the bot responds.
- **Seamless handoff** — If the Copilot panel is closed when a user clicks any Copilot action, the panel opens automatically, initializes the connection, and delivers the queued message once ready. No manual setup required.

### Copilot Visibility and Settings

All Copilot surfaces — the panel toggle button, grid row icons, Ask Copilot buttons, and Explain with Copilot links — are controlled by a single environment variable (`alex_show_copilot`). When disabled, every Copilot element is hidden and all action handlers are inert. A companion setting (`alex_show_copilot_default`) controls whether the Copilot panel opens automatically on page load.

These settings, along with two additional options for auto-refreshing diagnostics data, are configurable through a **Settings panel** accessible from the grid toolbar:

| Setting | Effect |
|---------|--------|
| Auto-Update In-Progress Records | Periodically refreshes incomplete routing records from the last 7 days |
| Auto-Retrieve Diagnostics | Calls the Custom API on a schedule without requiring a manual Retrieve click |
| Show Copilot | Toggles all Copilot UI on or off |
| Open Copilot by Default | Automatically opens the Copilot panel when the page loads |

All settings are stored as Dataverse environment variables and apply across the environment.

### Solution Packages

Both solution packages are attached:

- **Managed solution** — for production environments
- **Unmanaged solution** — for development and customization

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
