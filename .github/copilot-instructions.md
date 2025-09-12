# Copilot Instructions for NestProxy

This repository contains a .NET 9 solution that proxies Google Nest (Device Access) RTSP(S) streams to a local RTSP server, providing a stable, debuggable relay with standardized logging, token refresh handling, and optional keep-alives. The solution includes a server (core logic), a client, shared models, and tests.

## Tech stack and targets
- Runtime: .NET 9 (TargetFramework: net9.0)
- Language: C# 13
- Server-side: Minimal services with custom RTSP implementation and a local RTSP server to restream.
- RTSP: Custom implementation in NestProxy.Rtsp.
- Logging: Microsoft.Extensions.Logging (Serilog enabled via configuration).
- Persistence: JSON file storage (config root via `ConfigStorage:RootPath`) for settings, credentials, and cameras.

Always prefer .NET 9 APIs and C# 13 language features where idiomatic and allowed by `.editorconfig`.

## Project structure
- src/NestProxy.Rtsp: Custom RTSP protocol implementation (transport, messages, protocol, SDP parsing)
- src/NestProxy.Server: Upstream relay + local RTSP server + orchestration services
- src/NestProxy.Client: Client UI assets
- src/NestProxy.Shared: Shared contracts/models
- test/NestProxy.Tests: Unit tests for all projects
- bench/NestProxy.Benchmarks: Performance benchmarks
- docs/: Documentation

When adding code, place files in the appropriate project and folder to match existing conventions:
- RTSP protocol code → NestProxy.Rtsp (Transport/, Messages/, Protocol/, Sdp/)
- Server logic → NestProxy.Server (Services/, Models/, Data/, Streaming/)
- Tests → NestProxy.Tests (organized by project/namespace)

## Core behaviors and domain rules
- RTSP upstream flow: OPTIONS → DESCRIBE → per-track SETUP(s) → PLAY.
- Interleaved RTP/RTCP channels are assigned per media track (0/1, 2/3, 4/5, …) in the upstream relay.
- DESCRIBE parsing captures SDP and track URIs. Local RTSP server synthesizes downstream SDP and rewrites c= and ports as needed.
- Token refresh (Extend stream):
  - Use the returned streamToken as the value in the auth query parameter of the upstream RTSP URLs.
  - Do NOT tear down the session.
  - Re-SETUP each media track using the updated URLs while including the same Session: <id> header to indicate continuity.
  - After all SETUPs complete, issue a PLAY using the same Session.
- Keep-alive:
  - Configuration keys:
    - Streaming:KeepaliveEnabled (default: false)
    - Streaming:KeepaliveMode (Options | GetParameter | Rtcp) (default: Options)
  - Behavior when enabled:
    - Options mode: send upstream OPTIONS periodically.
    - GetParameter mode: send upstream GET_PARAMETER for keep-alive; if rejected by the server, automatically fall back to OPTIONS.
    - Rtcp mode: send RTCP Receiver Report (RR) plus SDES CNAME as a compound RTCP packet over the upstream RTSP TCP interleaved RTCP control channel. Begin only after at least one track has been SETUP and the control channel is known; skip until available. Maintain consistent interleaved channel assignment per track across re-SETUP cycles.
- Logging policy:
  - Use consistent prefixes for RTSP logs:
    - Upstream (to Nest): "[cam-{CameraId}] …"
    - Downstream (to local clients): "[cam-{CameraId}] [client-{ClientId}] …"
  - When logging headers, only include headers that are present in the message.
  - Do not log message bodies for OPTIONS, SETUP, or PLAY. Only log the DESCRIBE response body (truncated to a reasonable size).
  - TEARDOWN responses are headers-only.

## Configuration
- appsettings.json keys (Server):
  - Streaming:RtspBasePort (default 8554)
  - Streaming:KeepaliveEnabled (default false)
  - Streaming:KeepaliveMode (Options | GetParameter | Rtcp; default Options)
- Bind and read configuration via IConfiguration/IOptions where appropriate.

## Coding standards (respect .editorconfig)
Mirror the repository’s `.editorconfig`. Key highlights:
- Indentation: 4 spaces; End of line: CRLF.
- Namespaces: file-scoped.
- Usings:
  - Group import directive blocks; do not force System directives first (dotnet_sort_system_directives_first=false).
  - Place usings outside the namespace (csharp_using_directive_placement=outside_namespace).
- Types and var usage:
  - Prefer explicit types. Do not use var for built-in or apparent types (csharp_style_var_* = false).
  - Use descriptive names; avoid abbreviations for variables, parameters, types, fields unless widely known.
- Expression-bodied members:
  - Allowed for accessors, indexers, properties, and lambdas per `.editorconfig` (methods/ctors generally not expression-bodied).
- Modern C# features (use when idiomatic and permitted by `.editorconfig`):
  - Primary constructors (csharp_style_prefer_primary_constructors = true)
  - Collection expressions (dotnet_style_prefer_collection_expression)
  - Pattern matching and switch expressions
  - Null propagation and coalescing
  - File-scoped namespaces; implicit object creation when type is apparent
- Braces and formatting:
  - Use braces; respect new line and spacing settings from `.editorconfig`.
- Nullability:
  - Enable and respect nullable reference types; avoid suppressions. Address warnings by design.
- Put each interface, class, record, struct, or enum in its own file named accordingly.

## Architectural and design guidance
- Dependency Injection: Use constructor injection for services and options. Register services in the host composition root.
- Cancellation tokens: Accept and flow CancellationToken for async operations, especially loops, I/O, and network calls.
- Threading and state:
  - Avoid static mutable state. Use instance fields with clear ownership and synchronization.
  - Use async/await throughout; avoid blocking waits.
- Error handling:
  - Use structured logging with context (camera id, client id). Handle transient failures with backoff where relevant.
  - In long-running loops, treat TaskCanceledException as a normal shutdown path.
  - For fire-and-forget tasks, capture and log exceptions; cancel via CTS.

## RTSP-specific implementation tips
- Only send headers required by the server (CSeq, Session, Transport, Range, etc.).
- Preserve and reuse the Session ID between SETUP/PLAY/teardown phases as designed.
- Compute absolute track URIs from SDP control attributes; otherwise fallback to /trackID=n (1-based).
- Keep interleaved channel assignment consistent per track during a session or re-setup cycle.
- When updating tokens, do not re-DESCRIBE unless necessary. Prefer updating auth on the known base URL and stored track URIs.

## Testing
- Unit tests should avoid requiring a live RTSP endpoint. Use mocks/fakes around transport and listener interfaces.
- Mark any live-integration placeholders as [Skip] with a clear reason (as done in the repo).
- When changing observable behavior or public APIs, add/adjust targeted tests.
- After any code edit that affects logic (services, protocol handling, token refresh, keep-alive, SDP parsing, EF models), run the relevant existing tests. If tests are absent for the changed surface, add at least one focused unit test demonstrating the expected behavior or edge-case fixed.
- Prefer minimal, fast tests (no real network). Mock RTSP transport and timeouts; assert sequence (OPTIONS → DESCRIBE → SETUP → PLAY) and session continuity during token refresh.
- If a bug is fixed, first reproduce with a failing test, then fix and confirm it turns green.

## Documentation
- Update README and docs when changing configuration keys, token handling, logging format, or protocol sequences.
- Keep comments concise and accurate; add remarks for non-obvious behavior.
- Use XML doc comments for public APIs. Wrap tags to 100 characters.
- Always put `<summary>`, `</summary>`, `<remarks>`, `</remarks>`, `<returns>` and `</returns>` tags on their own line.

## Iteration Summary Requirement
Purpose: Provide an immediate, in-chat micro-recap of what the Copilot agent just did (edits, file additions, config changes, tests run) so the user can track progress without scrolling through diffs.

When to emit: After every actionable iteration (any file edit, creation, deletion, test/build execution, or configuration/documentation change). If the response was purely explanatory with no action taken (no tool calls, no edits), you may omit it.

## Performance Considerations
- Hot paths: The upstream read / downstream relay loops must avoid unnecessary allocations. Favor Span<byte>, Memory<byte>, and pooled buffers (ArrayPool<byte>.Shared) for RTP/RTCP packet handling instead of always allocating new arrays.
- String handling: Avoid string concatenation in per-packet logging; guard logs with logger.IsEnabled(LogLevel.Debug) before formatting complex messages.
- Async I/O: Do not block on network or database calls; always use async. Never use Task.Result or .Wait() inside hot loops.
- Logging volume: Keep per-packet logs at Debug or Trace; Info level should summarize session lifecycle events (DESCRIBE done, track SETUP complete, token refreshed, keep-alive mode active, teardown).
- Timers: Use a single hosted service or a channel-based scheduler for periodic keep-alives instead of multiple per-track timers.
- Cancellation: Check cancellation tokens in long-running loops (read loops, keep-alive send loops) at least once per iteration to ensure timely shutdown.
- Exceptions: Avoid using exceptions for control flow in packet parsing; pre-validate headers/lengths.
- Buffer reuse: Maintain consistent interleaved channel assignment without reallocating tracking structures; reuse collections (e.g., List<T>.Clear()) when safe.
- Time measurement: Use Stopwatch.GetTimestamp() or ValueStopwatch (if added) for measuring intervals in hot paths; avoid DateTime.UtcNow in tight loops.

## NestProxy.Rtsp Library
The custom RTSP implementation is organized into:
- **Transport/** - Low-level TCP/SSL transport (IRtspTransport, RtspTcpTransport, RtspSslTransport)
- **Messages/** - RTSP message models (RtspRequest, RtspResponse, method-specific requests)
- **Protocol/** - Message parsing and serialization (RtspMessageParser, RtspMessageSerializer)
- **Sdp/** - SDP file parsing (SdpParser, SdpFile, SdpMedia, MediaType)

When working with RTSP:
- Transport layer handles TCP/SSL connections and provides byte-level I/O
- Message layer provides strongly-typed RTSP request/response objects
- Protocol layer handles parsing/serialization between bytes and messages
- SDP layer parses session descriptions from DESCRIBE responses

## References
Google Nest Device Access API: https://developers.google.com/nest/device-access
Google Nest Camera traits: https://developers.google.com/nest/device-access/traits/device/camera-live-stream
RTSP: https://www.rfc-editor.org/rfc/rfc2326
RTP RTCP: https://www.rfc-editor.org/rfc/rfc3550
SDP: https://www.rfc-editor.org/rfc/rfc4566

---

By following this guide, Copilot should generate code and edits that fit NestProxy’s architecture, respect the enforced coding style from `.editorconfig`, and preserve correct RTSP behavior under token refresh and keep-alive scenarios.
