# Nest Camera RTSP Proxy

## Introduction
This project started out as a vibe-coded proof-of-concept in hope of finding a solution for Nest RTSP streams expiring after 5 minutes. This issue affects many awesome open-source projects like [NestMTX](https://nestmtx.com/), [go2rtc](https://github.com/AlexxIT/go2rtc) and [Home Assistant's Google Nest integration](https://www.home-assistant.io/integrations/nest/), regardless of them using the Google Smart Device Management API [ExtendRtspStream](https://developers.google.com/nest/device-access/traits/device/camera-live-stream#extendrtspstream) command to extend the stream lifetime.

The `ExtendRtspStream` command does not actually extend the existing RTSP session, but instead issues a new access token to be used in the RTSP URL. Iterating on the proof-of-concept, many approaches were attempted to work around this limitation, including:
* Sending an RTSP `OPTIONS` or `GET_PARAMETER` keepalive with the new access token on the existing RTSP session.
* Re-issuing the `PLAY` command with the new access token on the existing RTSP session.
* Re-negotiating the transport for each media track with `SETUP` to switch to the new access token.
* Re-establishing the entire RTSP session starting from `OPTIONS` and `DESCRIBE` all the way through to `PLAY` on the existing TCP connection.

All to no avail; even managing to get the server to issue a new RTSP [Session Identifier](https://www.rfc-editor.org/rfc/rfc2326#section-3.4), the stream still expired 5 minutes after initial setup. Having tried all RTSP-compliant methods to extend the session without success, and then some, it became clear that the only viable solution to provide a continuous RTSP stream is to establish an entirely new RTSP connection when the access token nears expiry.

Hence the inception of the **NestProxy** project: a simple local proxy for Nest devices.

## Goal
The goal of this project is to provide a simple proxy server for RTSP-based Nest camera devices, providing a continuous RTSP stream for live streaming or feeding into other software (e.g. [Frigate NVR](https://frigate.video/), [Home Assistant](https://www.home-assistant.io/)). The proxy handles token refresh and session re-establishment transparently, so clients can maintain a stable connection without worrying about the underlying token expiry.

## Features
* Google OAuth (Device Access scope) – obtain refresh + access tokens.
* Camera discovery & persistence (JSON files per camera under `config/cameras`).
* Enable/disable camera proxying (per-camera in-process RTSP relay).
* Automatic stream extension before expiry; seamless refresh and switchover.
* Serilog console logging (Docker friendly).
* Transparent RTP translator: forwards RTP/RTCP without rewriting sequence numbers or timestamps ([RFC 3550 §7.1](https://www.rfc-editor.org/rfc/rfc3550#section-7.1) transport translator semantics).

## Quickstart

#### Build & Run

Using .NET SDK:
```sh
dotnet restore NestProxy.sln
dotnet build NestProxy.sln
dotnet run --project src/NestProxy.Server
```

Using Docker:
```sh
docker build -t nestproxy:latest .
docker run --rm -d -p 5000:8080 -p 5001:8081 -p 8554-8564:8554-8564 -v ./config:/app/config --name nestproxy nestproxy:latest
```

#### Initial Setup
Navigate to http://localhost:5000/ and refer to [docs/quickstart.md](docs/quickstart.md) for setup and usage instructions.

#### Configuration
Google auth/settings are stored in `config/google-settings.json` and credentials in `config/credentials.json` (created after auth). Config root defaults to `config` and can be overridden via appsettings:
```
"ConfigStorage": {
  "RootPath": "config"
}
```

Streaming settings (in `appsettings.json`):
```
"Streaming": {
  "RtspBasePort": 8554,
  "KeepaliveEnabled": true,
  "KeepaliveMode": "Options",
  "KeepaliveIntervalSeconds": 20,
  "StreamExtensionOffsetSeconds": 30,
  "PacketChannelCapacity": 1000
}
```
Notes:
- KeepaliveEnabled: toggles upstream keepalive loop (default true).
- KeepaliveMode: Options | GetParameter | Rtcp (default Options)
  - Options: send periodic RTSP OPTIONS.
  - GetParameter: prefer RTSP GET_PARAMETER with Session; if rejected (e.g., 405/501), auto-fallback to OPTIONS.
  - Rtcp: send periodic minimal RTCP Receiver Reports over the first control interleaved channel.
- KeepaliveIntervalSeconds: interval between keepalive messages (default 20).
- StreamExtensionOffsetSeconds: how many seconds before expiry to extend stream (default 30).
- PacketChannelCapacity: channel buffer size for packet forwarding (default 1000).

#### Docker compose (example)
```yaml
  nestproxy:
    container_name: nestproxy
    image: nestproxy
    build:
      context: ./NestProxy
      dockerfile: Dockerfile
    ports:
      - "9080:8080"
      - "9081:8081"
      - "9554-9564:8554-8564"
    environment:
      - Streaming__RtspBasePort=8554
    volumes:
      - ./config:/app/config
    restart: unless-stopped
    logging:
      driver: "json-file"
      options:
        max-size: "10m"
        max-file: "5"
```

## Architecture
When an upstream token refresh is required, the server establishes a new RTSP TCP “staging” connection (OPTIONS → DESCRIBE → SETUP for each track → PLAY). As soon as the first RTP packet is received on the staging connection, forwarding switches over atomically and a TEARDOWN is sent upstream to the old session. A synthetic RTCP BYE is sent downstream to indicate the old session's end. This avoids reuse of the old Session ID and minimizes glitches.

Refer to [docs/architecture.md](docs/architecture.md) for a deeper technical dive into the architecture and design decisions.