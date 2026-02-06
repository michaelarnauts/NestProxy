# Quickstart

This guide walks you through using the NestProxy application UI to configure Google Device Access credentials, authorize with your Google account, discover cameras, enable streaming, and obtain local RTSP URLs. Instructions for cloning, building, hosting, or running the server are documented separately.

> NOTE: Throughout this document, "Base URL" refers to the root address where the application is already running (e.g. `https://localhost:8081` or your deployed domain). Replace it accordingly when navigating.

> **IMPORTANT**: Google OAuth and Device Access credentials are stored on disk under the configured `ConfigStorage` root (default `config`). Settings live in `config/google-settings.json`; credentials are written to `config/credentials.json` after authorization. Camera metadata is stored per-camera in `config/cameras/*.json`.

## Navigation Overview
Once the application is running:
- Open the application in a browser at the Base URL.
- Use the top-level navigation (or directly append the routes) to reach:
  - `/settings` – Configure Google OAuth & Device Access Project settings (**required before first use**).
  - `/cameras` – Authorize / refresh Google access, list cameras, enable/disable local relays, and view local RTSP URLs.

## 1. Configure Google OAuth & Device Access (Settings Page)
Navigate to: `BaseURL/settings`

**First-time setup is mandatory**: The application will not function until you configure these settings through the web UI.

Populate the following fields, then click **Save**:
- **Client ID** – From your Google Cloud OAuth 2.0 Web Client.
- **Client Secret** – From the same OAuth client (keep private).
- **Project ID** – The Device Access Project ID (from the Device Access Console, not the Cloud numeric project number).
- **Redirect URI** – Must exactly match one of the Authorized redirect URIs configured in the OAuth client (examples: `https://localhost:8081/api/auth/callback`, or a public domain callback path when deployed).

> **Note**: These settings are persisted to `config/google-settings.json` (under the `ConfigStorage:RootPath` directory). They are **not** stored in `appsettings.json` or environment variables.

After saving, proceed to the Cameras page to authenticate. If you change credentials later, you must re-authenticate.

### Google Cloud & Device Access Setup Guide
The steps below mirror the in-app collapsible guide for completeness:

1. Open the [Google Cloud Console](https://console.cloud.google.com/) and (if needed) create a Project (top bar – *Select a project* – **NEW PROJECT**).
2. Ensure the intended Project is selected (top bar selector).
3. Enable the **Smart Device Management API** (APIs & Services – Library – search "Smart Device Management" – Enable).
4. Configure the [OAuth Consent Screen](https://console.cloud.google.com/apis/credentials/consent): choose *External*, fill required app info, add your Google account as a Test User, then Finish.
5. Create an [OAuth 2.0 Client](https://console.cloud.google.com/auth/clients): type *Web application*. Add an Authorized redirect URI matching what you will enter in the Settings page.
6. Copy the generated **Client ID** and **Client Secret** into the Settings page. If you added a custom Redirect URI, copy that too.
7. Open the [Google Auth Platform Audience](https://console.cloud.google.com/auth/audience) page and publish the app if required.
8. Visit the [Device Access Console](https://console.nest.google.com/device-access); create a Device Access Project (if not present) and link it to your OAuth Client ID. (Events enablement is optional.)
9. Copy the **Project ID** from the Device Access Console to the Settings page and **Save**.
10. Proceed to `/cameras` and click **Connect / Refresh** to start the OAuth authorization flow.

Keep your Client Secret private. Regenerate the credentials if compromised.

## 2. Authorize & Discover Cameras (Cameras Page)
Navigate to: `BaseURL/cameras`

1. Click **Connect / Refresh** to launch the Google authorization flow in a new window/tab (Google OAuth + Device Access consent). Complete the prompts.
2. After returning, the table will display any cameras the API exposes. A warning banner appears if none are found.
3. Each row shows: Name, Room, Enabled status, Local RTSP URL (when enabled), and current Runner Status (Running / Idle).
4. Click **Enable** to start the local proxy/relay for that camera. The status will transition to *Running* once the upstream session establishes.
5. Use the displayed RTSP URL in a compatible player (e.g. VLC or `ffplay -rtsp_transport tcp <url>`). If you need the URL again, revisit the `/cameras` page.
6. Click **Disable** to stop relaying a camera; the session and downstream sockets are released.

### Partner Connections Manager Setup (After Authenticating)
If cameras do not appear or lack capabilities:

1. After a successful **Connect / Refresh**, open the [Partner Connections Manager](https://nestservices.google.com/partnerconnections).
2. Select the same Device Access Project you configured on the **Settings** page (it must match the Project ID field exactly).
3. Open the project details (Manage / Configure) and enable all available options / capabilities (events, streaming, structures, etc.).
4. Return to the `/cameras` page and click **Connect / Refresh** again to pull updated capabilities.
5. If devices still do not populate, verify:
   - The OAuth client ID in the Device Access Console matches the one on the Settings page.
   - Your Google account is listed as a Test User on the OAuth consent screen.

## 3. Working With Local RTSP Streams
Once a camera is Enabled:
- The local RTSP server assigns a stable URL of the form: `rtsp://<host>:<port>/<path>` (e.g. `rtsp://localhost:8554/living-room`).
- Use TCP transport for best reliability (`-rtsp_transport tcp` in ffplay) unless you have explicitly configured UDP.
- Streams auto-extend before token expiry; the URL does not change during extension.

### Optional Upstream Keep-Alives
By default keep-alives are disabled. Enable them if you observe upstream idle timeouts (some networks or longer token refresh intervals). Configuration keys:

- `Streaming:KeepaliveEnabled` (bool, default false)
- `Streaming:KeepaliveMode` (Options | GetParameter | Rtcp)

Modes:
- Options: periodic OPTIONS requests.
- GetParameter: sends GET_PARAMETER; on first failure permanently falls back to OPTIONS.
- Rtcp: sends RTCP Receiver Report + SDES over the first control channel (after track SETUP completes).

Example (PowerShell) before launching the server:
```
$env:Streaming__KeepaliveEnabled = 'true'
$env:Streaming__KeepaliveMode = 'Rtcp'
```

## 4. Session & Metrics Visibility
- Per-session activity and RTP/RTCP counters are periodically logged (every configured stats window; default 10s) with the prefix: `[<camera-id>] [client-<shortSession>]`.
- An aggregate metrics endpoint (if exposed at `api/metrics`) includes per-camera/session payload counters.

## 5. Updating Credentials
If you rotate the Client Secret, Project ID, or Redirect URI:
1. Update fields on `/settings` and **Save**.
2. Go to `/cameras` and click **Connect / Refresh** to re-authorize.
3. Re-enable any cameras as needed.

## 6. Troubleshooting Quick Tips
| Symptom | Action |
|--------|--------|
| No cameras listed | Confirm Partner Connections Manager configuration and that authorization completed; click **Connect / Refresh** again. |
| RTSP player cannot connect | Ensure the camera is Enabled and the RTSP base port is reachable (firewall). Use TCP transport first. |
| Frequent reconnects | Check network stability and clock sync; review logs for upstream token refresh failures or consider enabling keep-alives. |
| Missing video or audio track | Verify the Device Access Project capabilities and re-run **Connect / Refresh**. |
| 401 / auth failures after update | Re-enter credentials on `/settings` and re-authenticate. |

## 7. Security Considerations
- Treat Client Secret and Project ID as sensitive. Do not commit them to source control.
- Restrict access to the running instance; RTSP URLs are bearer-style and allow stream access.
- Monitor logs for unexpected session churn that could indicate misuse.

## 8. Next Steps
- Consult `docs/architecture.md` for deeper technical detail (token refresh flow, interleaved channel assignment, session lifecycle).
- Refer to the operations document (separate) for deployment, hosting, scaling, and build instructions.

---
This document focuses purely on in-app usage and configuration. Operational setup (build/run/hosting) is intentionally omitted.
