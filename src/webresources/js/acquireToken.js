// ════════════════════════════════════════════════
// MSAL Token Acquisition (SSO)
// Unified Routing Analytics
//
// Attempts token acquisition in this order:
// 1. Silent (cached account)
// 2. SSO Silent (leverages existing D365 session)
// 3. Interactive popup (fallback)
// ════════════════════════════════════════════════

import { getUsername } from './dataverseHelpers.js';

export async function acquireToken(settings) {
  console.log('[RoutingInsight] Acquiring token with settings:', settings);

  const msalInstance = new window.msal.PublicClientApplication({
    auth: {
      clientId: settings.appClientId,
      authority: `https://login.microsoftonline.com/${settings.tenantId}`,
      redirectUri: window.location.origin,
      navigateToLoginRequestUrl: false
    },
    cache: {
      cacheLocation: 'memory',
      storeAuthStateInCookie: true
    }
  });

  await msalInstance.initialize();

  const username = await getUsername();
  console.log('[RoutingInsight] Acquired username:', username);

  const loginRequest = {
    scopes: ['https://api.powerplatform.com/.default'],
    redirectUri: window.location.origin,
    loginHint: username
  };

  // ── Attempt 1: Silent token (cached account) ──
  try {
    const account = await msalInstance.getAccountByUsername(username);
    if (account != null) {
      console.log('[RoutingInsight] Attempting silent token acquisition');
      const response = await msalInstance.acquireTokenSilent({
        ...loginRequest,
        account: account
      });
      if (response?.accessToken) {
        console.log('[RoutingInsight] Silent token acquired successfully');
        return response.accessToken;
      }
    }
  } catch (e) {
    console.warn('[RoutingInsight] acquireTokenSilent failed:', e);
    if (!(e instanceof window.msal.InteractionRequiredAuthError)) {
      throw e;
    }
  }

  // ── Attempt 2: SSO Silent ──
  try {
    console.log('[RoutingInsight] Falling back to ssoSilent');
    const response = await msalInstance.ssoSilent(loginRequest);
    if (response?.accessToken) {
      console.log('[RoutingInsight] ssoSilent token acquired successfully');
      return response.accessToken;
    }
  } catch (e) {
    console.warn('[RoutingInsight] ssoSilent failed:', e);
    if (!(e instanceof window.msal.InteractionRequiredAuthError)) {
      throw e;
    }
  }

  // ── Attempt 3: Interactive popup ──
  console.log('[RoutingInsight] Falling back to interactive login');
  const response = await msalInstance.loginPopup(loginRequest);
  if (response?.accessToken) {
    console.log('[RoutingInsight] Interactive token acquired successfully');
    return response.accessToken;
  }

  throw new Error('Failed to acquire SSO token through any method.');
}
