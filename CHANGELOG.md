# Changelog

All notable changes to this project are documented in this file.

---

## [1.0.3f] — 2026-05-02

### Chronological diagnostic timeline — full rewrite

This version replaces the monolithic transcript block with a true **chronological diagnostic timeline** where each event is its own row, ordered by real occurrence time.

#### Customer messages are now individual timeline rows
- Each real `BotMessageReceived` event (customer input) renders as its own **CUSTOMER** row with:
  - Amber `CUSTOMER` source pill + `👤` dot to visually distinguish from bot events
  - Right-aligned customer chat bubble (or placeholder if text not in telemetry)
  - If the message triggered an error loop, a **"TOPIC TRIGGERED"** badge is shown on the row

#### Bot responses are now individual timeline rows
- The initial bot greeting burst (≤100 ms from first send) renders as **"Bot Greeted Customer"**
- Subsequent `BotMessageSend` clusters each render as a **"Bot Response"** row

#### Error retry loop attempts now show rich diagnostics
Each attempt in a diagnostic cycle now includes:
- **Attempt 1**: Expanded by default with a full error detail panel showing:
  - Error code (`FlowActionBadRequest`, `AIModelActionBadRequest`, etc.)
  - Action type (InvokeFlowAction / InvokeAIBuilderModelAction)
  - Human-readable error description
  - Root cause explanation and impact
  - **Execution trace** panel (`✗ action → error code` + `○ On Error → CancelAllDialogs`)
  - **Bot error message** sent to the customer in the OnError group
- **Attempt 2+**: Compact rows with **customer message badge** showing what the user said before retrying

#### Item ordering
- `expandMessageGroups()` runs AFTER `mergeDiagnosticCycles` so retry pairs are correctly detected before any group is split into individual rows
- The last `customer-msg` row immediately before a `_cycle` block is annotated with a **"TOPIC TRIGGERED"** warning badge

#### New CSS classes
- `.tl-dot.customer-dot` — amber-outlined dot for customer rows
- `.source-pill.cust` — amber CUSTOMER pill
- `.tl-card.customer-msg-card` — amber left border
- `.error-detail-panel` / `.edp-*` — error information box inside attempt 1
- `.trace-line.error` / `.trace-line.neutral` — monospace execution trace entries
- `.cycle-attempt`, `.attempt-row` — attempt wrapper and row inside diagnostic cycle block
- `.badge-cust` — amber customer message badge

---

## [1.0.3e] — 2026-05-02

### Transcript ordering fix + KQL guidance for message text

#### Transcript event ordering fixed
The conversation transcript was incorrectly showing a **Customer bubble as the very first item**, before any bot greetings. Root cause: Copilot Studio fires a `BotMessageReceived` event 0.3 ms before the first `BotMessageSend` — this is the OC handoff trigger that activates the bot, not a real customer message. The fix filters out any `BotMessageReceived` event that arrives at or before the first `BotMessageSend` in the same group.

#### Message text placeholder improved
When message text is empty in the telemetry, the placeholder now reads:
- `📤 Bot response — text not logged (update KQL to capture activityText)` 
- `📥 Customer input — text not logged (update KQL to capture activityText)`

This makes clear that the fix is a KQL change, not a data issue.

#### KQL fix needed for message text (apply manually in Power Automate flow)
In the **MCS Bot Events** section of the query, change:
```kql
msgText = tostring(cd["Text"]),
```
to:
```kql
msgText = coalesce(
  iff(isnotempty(tostring(cd["Text"])),         tostring(cd["Text"]),         ""),
  iff(isnotempty(tostring(cd["activityText"])), tostring(cd["activityText"]), ""),
  iff(isnotempty(tostring(cd["ActivityText"])), tostring(cd["ActivityText"]), "")
),
```
For LCW/OmniChannel integrations, customer message text is often stored in `activityText` rather than `Text`. The fallback chain ensures the correct field is used regardless of how the channel populates custom dimensions.

---



### MCS Error Detection & Diagnostic Cycle Grouping

Fixes the core MCS error pipeline: errors were silently showing 0 because the code was checking `ActionExecuted` events instead of `OnErrorLog` events. Additionally, repeated `[trigger → error]` retry patterns are now collapsed into a single expandable **Retry Loop** block in the timeline.

#### Error detection fixed
- **`buildMcsGroup`**: `hasErrors` and the `errors[]` array now use `eventName === 'OnErrorLog' && text` (was `ActionExecuted`). Error codes such as `AIModelActionBadRequest` and `FlowActionBadRequest` are now correctly surfaced.
- **`getStageStatus`**: MCS `OnErrorLog` events with a code are now classified as `'error'` status.
- **`diagnoseConversation`**: `allErrors` filter changed from `ActionExecuted` to `OnErrorLog`. Error counts in outcome diagnosis are now accurate.
- Error detail label changed from "Action Execution Failed" to "On Error Handler Fired".

#### Diagnostic cycle grouping (`mergeDiagnosticCycles`)
New function detects consecutive `[triggerTopic + Bot Error Handling]` pairs and collapses ≥2 pairs with the same trigger label into a `_cycle` block:
- Rendered as a **Retry Loop** accordion (e.g. "billing — Retry Loop (×3) — AIModelActionBadRequest")
- Each attempt shows its own trigger + error row inside the expanded block
- Applies to both AI action retries (`billing + On Error`) and flow retries (`Escalate + On Error`)

#### Message placeholder text
- `BotMessageSend` / `BotMessageReceived` events with empty `text` now render a placeholder: *"(Bot response — content not captured in telemetry)"* instead of a blank bubble
- All message events are now counted toward the `💬 N` badge, even when text is absent

#### Bot Processing rows suppressed
- `buildMcsGroup` now marks groups with no named topic as `_lowValue: true`
- `renderMcsGroupItem` silently drops `_lowValue` groups that have no messages and no errors, removing noise from the timeline

---

## [1.0.3c] — 2026-05-02

### MCS Insights Stabilisation

Fixes an issue where enabling MCS Insights caused core Omnichannel routing data (channel, queue, agent, latency) to disappear from the summary cards. MCS Insights is now strictly additive — it enriches the routing analysis without replacing or hiding it.

#### Routing summary cards now use OC-only data
All routing-derived summary values (Channel, Queue, Assigned Agent, Assignment Method, Routing Latency) are now calculated exclusively from OC routing events. MCS events in the merged array no longer interfere with these derivations.

- **Channel** detection changed from `events[0]?.channel` (which picked up the first MCS event) to `ocEvents.find(e => e.channel)?.channel`, scanning all OC events
- **Queue** detection now falls back to `queueName`, `queue`, `selectedQueue`, `outputQueue` in addition to `result.DisplayName`, so the queue is shown even when the API returns it in a non-standard field
- **Agent, Method, Routing Latency** all now search only OC events

#### OC completeness warning
When MCS Insights is enabled, the UI now validates that core OC routing stages are present in the result:
- If **zero OC events** are returned (MCS-only payload), a red warning banner appears: "MCS telemetry was returned, but no OC routing events are present in the result."
- If `ConversationCreated`, `RouteToQueue`, or `CSRAssignment` are missing while OC events exist, an amber warning names which stages are absent

#### Conversation transcript bubbles
- Bot and customer messages now render in a proper `chat-thread` flex container with distinct visual alignment: bot messages left, customer messages right
- Each bubble shows the speaker label (`Bot` / `Customer`) above the message
- MCS group rows that contain messages now show a `💬 N` badge in the collapsed header, making it clear that transcript data is available without expanding

---

## [1.0.4] — 2026-05-02

### Diagnostic UI Overhaul

The routing timeline now acts as a **diagnostic tool**, not just a data viewer. A support engineer can understand the conversation outcome within seconds.

#### Outcome Banner
A colour-coded banner now appears at the top of every analysis result:
- **Green** — routing completed / agent assigned
- **Amber** — customer abandoned, or conversation in progress
- **Red** — conversation failed (bot errors, missing escalation, assignment failure)

The banner states what happened in plain language and, where determinable, the **root cause** (e.g. "Bot tool / action failure", "OC did not receive escalation signal").

#### 14 KPI Summary Cards
The summary row is expanded to 14 cards: Channel, Queue, Assigned Agent, Assignment Method, Status, Bot Outcome, Total Duration, Bot Duration, Routing Latency, OC Events, MCS Events, Errors, Escalation Attempts, Bridge.

Missing values no longer show `—` or `Unknown` silently. Instead they explain the reason:
- *"Not available — routing event missing"*
- *"None — escalation did not complete"*
- *"N/A — no assignment"*

#### Error Loop Grouping
When 3 or more consecutive MCS error / escalation items appear in the timeline (e.g. repeated `AIModelActionBadRequest` or `FlowActionBadRequest` cycles), they are collapsed into a single **error-loop block**. Each block shows the error code, attempt count, and the dominant topic. Individual attempts are visible inside the expanded block.

#### Missing Step Detection
After the timeline, the UI now highlights routing steps that were **expected but never received**:
- Bot escalated, but OC escalation event was never received
- OC received escalation, but queue routing did not complete
- Queue matched, but agent assignment was not attempted

These appear as dashed amber rows with explanatory text.

#### Legend Improvements
The legend dynamically adds "Bot Error Loop" and "Missing Step" entries when those item types are present in the current result.

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
