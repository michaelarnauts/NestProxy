# NestProxy Architecture

## Overview
NestProxy provides a local RTSP server that restreams Google Nest (Device Access) camera video/audio while handling Google stream token lifecycles, keep-alives, and per-client RTP/RTCP adaptation.

```
        +--------------------+          +-------------------------------+
        |  Google SDM API    |          |  Upstream Nest RTSP Endpoints |
        +----------+---------+          +-------------------------------+
                   ^                                   ^
                   | REST (OAuth2 / device & stream)   | RTSP (OPTIONS/DESCRIBE/SETUP/PLAY/EXTEND)
                   |                                   |
        +----------+---------+                +--------+--------------+
        |  GoogleAuthService |<--tokens-------| Upstream Orchestrator |
        +----------+---------+                +----+------------------+
                   |                               |           |
                   | JSON storage (config/*.json)  |           |
        +----------v---------+       RTP/RTCP      |           | RTP/RTCP (interleaved or UDP)
        |  File repositories |<--------------------+           |
        +--------------------+                                 |
                                                               v
                                                   +-----------+------------------+
                                                   |   LocalRtspServer            |
                                                   |  (RtspRequestDispatcher)     |
                                                   +-------------+----------------+
                                                                 |
                                      +--------------------------+--------------------------+
                                      |                          |                          |
                         RTSP SETUP/PLAY/TEARDOWN      RTP/RTCP forwarding          Metrics / Future
                                      v                          v                          v
                             +--------+-------+         +--------+-------+          +-------+-------+
                             | Client Session |         | Client Session |  ...     | Observability |
                             +----------------+         +----------------+          +---------------+
```

## Core Components

### GoogleAuthService / GoogleSdmService
- Manage OAuth2 flows and SDM device & stream lifecycle calls.
- Persist credentials and camera metadata via JSON file repositories (`config/credentials.json`, `config/google-settings.json`, `config/cameras/{deviceId}.json`).

### CameraProxyRunner & Supervisor
- One runner per enabled camera; obtains RTSP stream URLs and manages token extension.
- Supervisor polls camera repository to start/stop runners as cameras are enabled/disabled.

### Upstream Orchestrator
- Establishes and maintains the upstream RTSP session (OPTIONS → DESCRIBE → multi-SETUP → PLAY).
- Performs token refresh (Extend stream) by re-issuing SETUP for each track with the new auth token while preserving the Session header, then PLAY (no TEARDOWN).
- Owns the keep-alive scheduling loop and delegates the concrete action to a pluggable keep-alive strategy (see Keep-Alive Strategies).

### LocalRtspServer
- Accepts downstream RTSP clients, delegates SDP synthesis to `DescribeSynthesizer`.
- Uses a lightweight request dispatcher to handle OPTIONS / DESCRIBE / SETUP / PLAY / TEARDOWN (and future EXTEND mapping if exposed downstream).
- Delegates all upstream transport negotiation for downstream SETUP requests to `TransportSetupService` (extracted from prior inline logic) for clearer separation of concerns and targeted unit testing.
- Delegates listener→(camera, session) correlation for log prefix generation to `ListenerIndex` instead of maintaining ad-hoc dictionaries internally.
- Maintains per-session transport bindings (TCP interleaved or UDP) produced by `TransportSetupService`.
- Forwards RTP/RTCP packets transparently (no sequence/timestamp rewriting) acting as an RTP "transport translator" per RFC 3550 §7.1 (only transport encapsulation may change, packet headers are preserved).

### DescribeSynthesizer
- Isolated service responsible for building downstream SDP strings from stored upstream SDP plus captured per-track media lines (video + audio).
- Normalizes connection line to `c=IN IP4 0.0.0.0` and zeroes media ports.
- Enables independent unit testing of SDP composition and future enhancements (bandwidth modifiers, attribute filtering) without editing the monolithic server class.

### ControlMapBuilder
- Extracted logic for generating the control attribute (e.g. `trackID=0`) to payload type mapping from SDP.
- Keeps `LocalRtspServer` free of parsing details; unit-tested separately.

### StreamForwarder
- Handles RTP/RTCP forwarding for a camera path to active client sessions.
- Encapsulates SSRC-first binding resolution and payload-type bootstrap heuristic.
- Manages buffer rental (thread scratch / large pool / ArrayPool fallback) independent of server orchestration logic.
- Future: extract session removal callback to avoid placeholder method.

### BindingSelector
- Maps SSRC → TrackBinding. Payload type acts only as a bootstrap hint: if a single binding exists for a payload type when a new SSRC is first observed it is associated; otherwise (multiple bindings share the payload type) association is deferred until a disambiguating signal appears. No round-robin selection is performed.

### SessionManager (ISessionRegistry)
- Tracks client sessions, updates activity timestamps, expires idle sessions via `PeriodicTimer`.
- Implements `ISessionRegistry` abstraction used by `LocalRtspServer` and downstream components.

### TransportSetupService
- Encapsulates downstream SETUP handling and construction of upstream Transport headers.
- Chooses interleaved channel allocation (TCP) versus UDP port negotiations and constructs appropriate header strings.
- Returns negotiated binding info to `LocalRtspServer` which persists it for subsequent PLAY and forwarding.

### ListenerIndex
- Thread-safe mapping from accepted TCP listener sockets to (cameraId, session) for consistent log prefixes.
- Replaces repeated dictionary lookups previously embedded in `LocalRtspServer`.

### Keep-Alive Strategies
- Interface: `IKeepAliveStrategy` with `TrySend(IUpstreamSession)`.
- Implementations:
        - Options: sends upstream OPTIONS.
        - GetParameter: attempts GET_PARAMETER; on first exception/failure flips permanently to Options for resilience.
        - Rtcp: sends compound RTCP Receiver Report + SDES CNAME over the first available control channel once interleaved assignment is known.
- Factory selects mode based on `Streaming:KeepaliveMode` configuration when `Streaming:KeepaliveEnabled` is true.

### StatisticsReporter
- Aggregates per (camera, session, payload) RTP/RTCP counts in fixed windows, emitting structured logs.
- Provides snapshot APIs for the metrics endpoint (`api/Metrics`), which exposes per-camera/session RTP/RTCP counters for observability.

### Models / Persistence
- Camera, credentials, and Google settings are stored as JSON files under `config/` using file-based repositories. Each camera is kept in `config/cameras/<deviceId>.json`.

## Data Flows
1. Enable camera → Supervisor starts runner → Runner creates RTSP stream → Upstream SDP stored in camera JSON and cached.
2. Downstream client DESCRIBE → Local SDP synthesized (`c=` rewritten, ports zeroed) → client issues SETUP(s) → PLAY.
3. Incoming upstream RTP/RTCP → Forwarded to `StreamForwarder` → Distributed to matched client bindings (headers preserved).
4. Token approaching expiry → Runner extends stream (ExtendRtspStream) → Orchestrator re-SETUPs tracks with new auth (same Session) → PLAY (no teardown).
5. Keep-alive interval elapses (if enabled) → Orchestrator invokes strategy `TrySend` (OPTIONS / GET_PARAMETER / RTCP) to keep session active.

## Concurrency & Performance
- ConcurrentDictionary for camera & session registries.
- Per-session lock only over snapshot enumeration; minimal locking in hot RTP path using copy + ArrayPool buffer.
- For very large packets, buffer rental falls back to a dedicated `LargeBufferPool` before using `ArrayPool<byte>.Shared`.
- PeriodicTimer for session sweeping to reduce wake-ups.

## Recent Enhancements
- Pre-parsed SDP control → payload map via `ControlMapBuilder`.
- Async-friendly forwarding path (foundation for `ValueTask` adoption) via `StreamForwarder`.
- Extracted services: `DescribeSynthesizer`, `ControlMapBuilder`, `StreamForwarder`, `TransportSetupService`, `ListenerIndex`.
- Unified session tracking through `ISessionRegistry` (`SessionManager`).
- Keep-alive strategy pattern introduced (OPTIONS / GET_PARAMETER with fallback / RTCP) replacing ad-hoc loop logic in orchestrator.
- Dedicated `ListenerAcceptLoop` hosted service decoupling socket accepts.
- Benchmark project (`bench/NestProxy.Benchmarks`) for allocation profiling and performance analysis.
- File-based persistence layer for credentials, Google settings, and cameras (configurable root via `ConfigStorage:RootPath`).

## Security Considerations
- Token secrets never logged in full; refresh tokens masked in API responses.
- Future phases: path validation, SSRC randomness strengthening, rate limiting.

## Extensibility
- Additional protocols (WebRTC) could reuse camera registry & RTP forwarding surfaces.
- Recording pipeline can subscribe to same forwarding events.

## Blazor Client
- Consumes REST APIs for camera list, status, settings; can embed rtsp-over-wasm player (future) or rely on external players.

## Logging Format
- Downstream: [{CameraId}] [client-{ShortSession}] ...
- Upstream: [{CameraId}] ... (no client).
- DESCRIBE body truncated; other request bodies omitted.

---
This document will be iteratively updated as Phase B/C/D enhancements land.
