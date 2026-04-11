// ════════════════════════════════════════════════
// Copilot Studio WebChat Initialization (SSO)
// Unified Routing Analytics
//
// Uses the M365 Agents SDK to establish an
// authenticated connection to the Copilot Studio
// bot, with SSO via MSAL.
// ════════════════════════════════════════════════

import {
  CopilotStudioClient,
  CopilotStudioWebChat
} from 'https://unpkg.com/@microsoft/agents-copilotstudio-client@1.0.15/dist/src/browser.mjs';

import { acquireToken } from './acquireToken.js';
import { settings } from './agent.settings.js';

export async function initCopilotModule() {
  const container = document.getElementById('copilotWebChat');
  container.innerHTML = '<div class="copilot-loading"><div class="spinner"></div>Connecting to Routing Insight (SSO)...</div>';

  try {
    // 1. Acquire SSO token
    const token = await acquireToken(settings);

    // 2. Connect to Copilot Studio
    const client = new CopilotStudioClient(settings, token);
    await client.startConversationAsync(true);

    const directLine = CopilotStudioWebChat.createConnection(client, {
      typingIndicator: true
    });

    // 3. Render WebChat
    container.innerHTML = '';
    container.style.height = '100%';

    const isLight = document.documentElement.classList.contains('light');

    window.WebChat.renderWebChat({
      directLine: directLine,
      locale: 'en-US',
      styleOptions: {
        accent: '#4a6cf7',
        backgroundColor: isLight ? '#ffffff' : '#1a1d27',
        bubbleBackground: isLight ? '#f0f1f4' : '#222633',
        bubbleBorderColor: isLight ? '#e0e2e8' : '#2a2e3d',
        bubbleBorderRadius: 12,
        bubbleFromUserBackground: '#4a6cf7',
        bubbleFromUserBorderColor: '#4a6cf7',
        bubbleFromUserBorderRadius: 12,
        bubbleFromUserTextColor: '#ffffff',
        bubbleTextColor: isLight ? '#1a1d27' : '#e2e4ea',
        sendBoxBackground: isLight ? '#f4f5f7' : '#0f1117',
        sendBoxTextColor: isLight ? '#1a1d27' : '#e2e4ea',
        sendBoxBorderTop: `1px solid ${isLight ? '#d8dbe3' : '#2a2e3d'}`,
        hideUploadButton: true,
        botAvatarInitials: 'RI',
        botAvatarBackgroundColor: '#4a6cf7',
        userAvatarInitials: 'Me',
        rootHeight: '100%',
        rootWidth: '100%'
      }
    }, container);

    console.log('[RoutingInsight] Copilot Studio WebChat with SSO rendered successfully');
    window.copilotInitialized = true;

  } catch (err) {
    console.error('[RoutingInsight] Failed to initialize Copilot:', err);
    container.innerHTML = `<div class="copilot-error">Failed to connect:<br>${err.message}</div>`;
  }
}

// Expose globally for the main HTML
window.initCopilotModule = initCopilotModule;
