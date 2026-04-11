# Seamless SSO for Embedded Copilot Studio: A Deep Dive into Zero-Click Authentication

When embedding a Copilot Studio agent into a custom web portal or a Dynamics 365 Web Resource, the biggest friction point for users is authentication. By default, if your bot needs to access Dataverse data using **End user credentials**, it prompts the user with a "Log In" button.

In this article, I will explain how I achieved a **Zero-Click SSO** experience in the **Unified Routing Analytics** project, leveraging the latest Microsoft SDKs and MSAL.js.

---

## The Challenge
Standard web chat implementations are anonymous. To perform actions like querying routing diagnostics in Dataverse, the bot must act as the logged-in user. The traditional "Manual Authentication" flow requires a multi-step handshake that interrupts the user experience. Our goal was to leverage the existing Dynamics 365 session to authenticate the bot silently.

## The Architecture
The solution relies on three main components working in harmony:

### 1. Identity Discovery (Dataverse Context)
First, we need to know who the user is. Instead of asking, we query the Dynamics 365 environment directly.
The `dataverseHelpers.js` utility uses the `Xrm` context to retrieve the user's unique ID and then queries the `systemuser` table to get their **User Principal Name (UPN)**. This UPN acts as our `loginHint` for the next step.

### 2. Silent Token Acquisition (MSAL.js)
Using the **Microsoft Authentication Library (MSAL.js)**, we attempt to get an access token for the Power Platform API without any UI popups:
* **Silent Cache:** We first check if a valid token already exists in the browser's memory.
* **SSO Silent:** If not, we use the `ssoSilent` method. By passing the UPN we discovered earlier as a `loginHint`, MSAL can often leverage the existing session cookie from the user's Dynamics 365 login to issue a new token for our App Registration.

### 3. Token Injection (The Modern SDK)
This is where the magic happens. Rather than waiting for the bot to ask for a login, we provide the identity upfront.
Using the new **`@microsoft/agents-copilotstudio-client`** library, we initialize the `CopilotStudioClient` by passing both the connection settings and the freshly acquired SSO token. 

When the `directLine` connection is created, it carries this identity. When a user asks a question, the bot already "knows" who they are and can immediately execute Dataverse Tools using their credentials.

---

## Inspiration & References
This implementation pattern was heavily inspired by the technical advancements shared in the **[Microsoft Copilot Studio Samples (Pull Request #427)](https://github.com/microsoft/CopilotStudioSamples/pull/427)**. 

The sample demonstrated how the new Agent SDK allows developers to bypass traditional OAuth prompts by handling the token exchange on the client-side and passing the token directly to the back-channel.

## Key Takeaways for Developers
* **Environment Variables:** Store your Entra ID `Client ID` in a Dataverse Environment Variable. This allows your code to remain generic and deployable across different tenants without hardcoding IDs.
* **First Run Consent:** Even with SSO, the very first time a user interacts with the bot, they may need to click "Allow" to grant the bot permission to access Dataverse.
* **Manual Auth is Still Required:** Even with client-side SSO, the bot's security settings in Copilot Studio must be set to **"Authenticate manually"** to allow the backend to validate the tokens you are sending.

---

## Conclusion
By combining the Power Platform's native identity with MSAL.js and the new Agent SDK, we can create AI assistants that feel like a native part of the enterprise ecosystem. No more login buttons—just immediate, intelligent assistance.

**Check out the full source code on GitHub: https://github.com/microsoft/CopilotStudioSamples/pull/427/changes/09e56ecca5febb17f4cb4d70dcef65f0c1edcdc6
