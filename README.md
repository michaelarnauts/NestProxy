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

## License
This project is licensed under the MIT License. See [LICENSE.txt](LICENSE.txt) for details.

Third-party dependencies and their licenses are listed in [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

## Quickstart

### Install & Run with Docker (Recommended)

You can run NestProxy directly using Docker from the GitHub Container Registry:

```sh
docker pull ghcr.io/basvbeek/nestproxy:latest
docker run --rm -d -p 8080:8080 -p 8554-8564:8554-8564 -v ./config:/app/config ghcr.io/basvbeek/nestproxy:latest
```

To run with Docker Compose, use the following example:

```yaml
services:
  nestproxy:
    container_name: nestproxy
    image: ghcr.io/basvbeek/nestproxy:latest
    ports:
      - "8080:8080"
      - "8554-8564:8554-8564"
    volumes:
      - ./config:/app/config
    restart: unless-stopped
    logging:
      driver: "json-file"
      options:
        max-size: "10m"
        max-file: "5"
```

### Build & Run

Using .NET SDK:
```sh
dotnet restore NestProxy.sln
dotnet build NestProxy.sln
dotnet run --project src/NestProxy.Server
```

Using Docker:
```sh
docker build -t nestproxy:latest .
docker run --rm -d -p 8080:8080 -p 8554-8564:8554-8564 -v ./config:/app/config --name nestproxy nestproxy:latest
```

### Initial Setup
Navigate to http://localhost:8080/ and refer to [docs/quickstart.md](docs/quickstart.md) for setup and usage instructions.

### Configuration
Google auth/settings are stored in `config/google-settings.json` and credentials in `config/credentials.json` (created after auth). Config root defaults to `config` and can be overridden via appsettings:
```json
"ConfigStorage": {
  "RootPath": "config"
}
```

Streaming settings (in `appsettings.json`):
```json
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
- RtspBasePort: TCP port server listens on for RTSP streams (default 8554).
- KeepaliveEnabled: toggles upstream keepalive loop (default true).
- KeepaliveMode: Options | GetParameter | Rtcp (default Options)
  - Options: send periodic RTSP OPTIONS.
  - GetParameter: prefer RTSP GET_PARAMETER with Session; if rejected (e.g., 405/501), auto-fallback to OPTIONS.
  - Rtcp: send periodic minimal RTCP Receiver Reports over the first control interleaved channel.
- KeepaliveIntervalSeconds: interval between keepalive messages (default 20).
- StreamExtensionOffsetSeconds: how many seconds before expiry to extend stream (default 30).
- PacketChannelCapacity: channel buffer size for packet forwarding (default 1000).

### Environment Variables
The following environment variables can be used to override configuration settings in `appsettings.json`:
- `ConfigStorage__RootPath`: Overrides `ConfigStorage:RootPath`.
- `Streaming__RtspBasePort`: Overrides `Streaming:RtspBasePort`.
- `Streaming__KeepaliveEnabled`: Overrides `Streaming:KeepaliveEnabled`.
- `Streaming__KeepaliveMode`: Overrides `Streaming:KeepaliveMode`.
- `Streaming__KeepaliveIntervalSeconds`: Overrides `Streaming:KeepaliveIntervalSeconds`.
- `Streaming__StreamExtensionOffsetSeconds`: Overrides `Streaming:StreamExtensionOffsetSeconds`.
- `Streaming__PacketChannelCapacity`: Overrides `Streaming:PacketChannelCapacity`.
- `Logging__LogLevel__Default`: Overrides default log level (e.g., Debug, Information, Warning, Error).

### Configure HTTPS (SSL Certificates)

#### Creating a Self-Signed Certificate

You can create a self-signed certificate for development or testing using the .NET CLI:

**Using .NET CLI (cross-platform):**

```sh
dotnet dev-certs https --export-path mycert.pfx --password yourpassword
```

This will generate a `mycert.pfx` file you can use for HTTPS as shown below.

**Using OpenSSL (Linux/macOS):**

```sh
openssl req -x509 -newkey rsa:4096 -keyout key.pem -out cert.pem -days 365 -nodes -subj "/CN=localhost"
openssl pkcs12 -export -out mycert.pfx -inkey key.pem -in cert.pem -password pass:yourpassword
```

You can now use `mycert.pfx` as shown below.

#### Using Your Certificate in Docker

To enable HTTPS with your own SSL certificate, mount your certificate file into the container and set the required environment variables for .NET/Kestrel.

**Example with Docker:**

```sh
docker run --rm -d \
  -p 8080:8080 \
  -p 8081:8081 \
  -p 8554-8564:8554-8564 \
  -v ./config:/app/config \
  -v ./mycert.pfx:/https/mycert.pfx:ro \
  -e ASPNETCORE_HTTPS_PORTS=8081 \
  -e ASPNETCORE_Kestrel__Certificates__Default__Path=/https/mycert.pfx \
  -e ASPNETCORE_Kestrel__Certificates__Default__Password=yourpassword \
  ghcr.io/basvbeek/nestproxy:latest
```

**Example with Docker Compose:**

```yaml
services:
  nestproxy:
    container_name: nestproxy
    image: ghcr.io/basvbeek/nestproxy:latest
    ports:
      - "8080:8080"
      - "8081:8081"
      - "8554-8564:8554-8564"
    volumes:
      - ./config:/app/config
      - ./mycert.pfx:/https/mycert.pfx:ro
    environment:
      - ASPNETCORE_HTTPS_PORTS=8081
      - ASPNETCORE_Kestrel__Certificates__Default__Path=/https/mycert.pfx
      - ASPNETCORE_Kestrel__Certificates__Default__Password=yourpassword
    restart: unless-stopped
    logging:
      driver: "json-file"
      options:
        max-size: "10m"
        max-file: "5"
```

**Notes:**
- Use a `.pfx` file for Kestrel (default for .NET).
- Adjust the path and password as needed.

## Architecture
When an upstream token refresh is required, the server establishes a new RTSP TCP “staging” connection (OPTIONS → DESCRIBE → SETUP for each track → PLAY). As soon as the first RTP packet is received on the staging connection, forwarding switches over atomically and a TEARDOWN is sent upstream to the old session. A synthetic RTCP BYE is sent downstream to indicate the old session's end. This avoids reuse of the old Session ID and minimizes glitches.

Refer to [docs/architecture.md](docs/architecture.md) for a deeper technical dive into the architecture and design decisions.