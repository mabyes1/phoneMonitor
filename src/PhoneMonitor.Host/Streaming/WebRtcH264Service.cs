using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PhoneMonitor.Host.Diagnostics;
using SIPSorcery.Net;
using SIPSorceryMedia.Abstractions;

namespace PhoneMonitor.Host.Streaming
{
    /// <summary>
    /// Owns browser WebRTC sessions for the virtual display. Signalling is a
    /// single HTTPS offer/answer request; media uses the best nominated
    /// DTLS/SRTP path (direct when possible, TURN relay when configured).
    /// </summary>
    public sealed class WebRtcH264Service
    {
        private readonly H264AnnexBStreamer h264;
        private readonly CloudflareTurnCredentialService turnCredentials;
        private readonly AuditTrailService audit;
        private readonly ConcurrentDictionary<Guid, WebRtcSession> sessions = new ConcurrentDictionary<Guid, WebRtcSession>();

        public WebRtcH264Service(
            H264AnnexBStreamer h264,
            CloudflareTurnCredentialService turnCredentials,
            AuditTrailService audit)
        {
            this.h264 = h264;
            this.turnCredentials = turnCredentials;
            this.audit = audit;
        }

        public bool IsAvailable => h264.IsAvailable;

        public async Task<WebRtcOfferAnswer> CreateAnswerAsync(
            string offerSdp,
            string deviceName,
            int fps,
            int quality,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(offerSdp))
            {
                throw new ArgumentException("WebRTC offer SDP is required.", nameof(offerSdp));
            }

            IceServerConfiguration iceConfiguration;
            try
            {
                iceConfiguration = await turnCredentials.CreateIceServersAsync(
                    "host-" + Guid.NewGuid().ToString("N").Substring(0, 12),
                    cancellationToken);
            }
            catch (TurnCredentialException error)
            {
                iceConfiguration = turnCredentials.GetStunOnlyConfiguration();
                iceConfiguration.TurnConfigured = true;
                iceConfiguration.Warning = error.Code;
                audit.Record(
                    "warning",
                    "stream",
                    "turn-host-credentials",
                    "fallback-stun",
                    subject: deviceName,
                    details: new Dictionary<string, string> { ["code"] = error.Code });
            }

            var configuration = new RTCConfiguration
            {
                // Include local addresses for LAN use, and STUN/TURN candidates
                // for cross-network clients. TURN credentials are short lived.
                X_ICEIncludeAllInterfaceAddresses = true,
                iceServers = ToSipsIceServers(iceConfiguration.IceServers)
            };
            var peer = new RTCPeerConnection(configuration);
            var track = new MediaStreamTrack(
                SDPMediaTypesEnum.video,
                false,
                new List<SDPAudioVideoMediaFormat>
                {
                    new SDPAudioVideoMediaFormat(new VideoFormat(
                        VideoCodecsEnum.H264,
                        102,
                        90000,
                        "packetization-mode=1;profile-level-id=42e01f"))
                },
                MediaStreamStatusEnum.SendOnly);
            peer.addTrack(track);

            var session = new WebRtcSession(
                Guid.NewGuid(),
                peer,
                deviceName,
                fps,
                quality,
                iceConfiguration.TurnAvailable ? "turn-ready" : "direct-stun");
            peer.onconnectionstatechange += state => OnConnectionStateChanged(session, state);
            peer.oniceconnectionstatechange += state =>
            {
                Console.Error.WriteLine($"[WebRTC] {session.Id} ice={state} device={session.DeviceName}");
                session.LastIceState = state.ToString();
                RecordSessionEvent(session, "ice-state", state.ToString(), state == RTCIceConnectionState.failed ? "warning" : "information");
                if (state == RTCIceConnectionState.failed || state == RTCIceConnectionState.closed)
                {
                    CloseSession(session, "ICE connection closed");
                }
            };

            var result = peer.setRemoteDescription(new RTCSessionDescriptionInit
            {
                type = RTCSdpType.offer,
                sdp = offerSdp
            });
            if (result != SetDescriptionResultEnum.OK)
            {
                peer.Close("Invalid WebRTC offer.");
                throw new InvalidOperationException($"WebRTC offer rejected: {result}.");
            }

            var answer = peer.createAnswer(null);
            await peer.setLocalDescription(answer);
            // SIPSorcery gathers host ICE candidates asynchronously.  Returning
            // the SDP created above can therefore omit the server candidate and
            // leaves Safari stuck in "checking" before it ever receives H.264.
            // Wait briefly, then return the actual local description populated
            // by setLocalDescription.
            await WaitForIceGatheringAsync(peer, 1500);
            sessions[session.Id] = session;
            RecordSessionEvent(session, "created", "ready");

            return new WebRtcOfferAnswer
            {
                Type = "answer",
                Sdp = peer.localDescription != null
                    ? peer.localDescription.sdp.ToString()
                    : answer.sdp
            };
        }

        private static Task WaitForIceGatheringAsync(RTCPeerConnection peer, int timeoutMs)
        {
            if (peer.iceGatheringState == RTCIceGatheringState.complete)
            {
                return Task.CompletedTask;
            }

            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            Action<RTCIceGatheringState> handler = state =>
            {
                if (state == RTCIceGatheringState.complete)
                {
                    completion.TrySetResult(true);
                }
            };
            peer.onicegatheringstatechange += handler;

            var timer = Task.Delay(timeoutMs).ContinueWith(_ => completion.TrySetResult(true));
            return completion.Task.ContinueWith(_ =>
            {
                peer.onicegatheringstatechange -= handler;
                timer.Dispose();
            }, TaskScheduler.Default);
        }

        private void OnConnectionStateChanged(WebRtcSession session, RTCPeerConnectionState state)
        {
            Console.Error.WriteLine($"[WebRTC] {session.Id} connection={state} device={session.DeviceName}");
            session.LastConnectionState = state.ToString();
            RecordSessionEvent(session, "connection-state", state.ToString(),
                state == RTCPeerConnectionState.failed ? "warning" : "information");
            if (state == RTCPeerConnectionState.connected)
            {
                if (Interlocked.Exchange(ref session.StreamStarted, 1) == 0)
                {
                    _ = Task.Run(() => StreamSessionAsync(session));
                }
                return;
            }

            // A disconnected state is often transient on mobile Wi-Fi.  Keep
            // the peer alive long enough for ICE to nominate a replacement
            // pair; the browser-side grace period mirrors this behaviour.
            if (state == RTCPeerConnectionState.failed ||
                state == RTCPeerConnectionState.closed)
            {
                CloseSession(session, $"WebRTC {state}");
            }
        }

        public WebRtcDiagnosticsSnapshot GetDiagnostics()
        {
            var active = sessions.Values
                .OrderByDescending(session => session.CreatedAt)
                .Select(session => new WebRtcSessionDiagnostics
                {
                    SessionId = session.Id.ToString("N"),
                    DeviceName = session.DeviceName,
                    CreatedAt = session.CreatedAt.ToString("O"),
                    ConnectionState = session.LastConnectionState,
                    IceState = session.LastIceState,
                    TransportPlan = session.TransportPlan
                })
                .ToArray();
            return new WebRtcDiagnosticsSnapshot
            {
                GeneratedAt = DateTimeOffset.UtcNow.ToString("O"),
                ActiveSessions = active
            };
        }

        private async Task StreamSessionAsync(WebRtcSession session)
        {
            try
            {
                await h264.StreamToWebRtcAsync(
                    session.Peer,
                    session.DeviceName,
                    session.Fps,
                    session.Quality,
                    session.Cancellation.Token);
            }
            catch (OperationCanceledException) when (session.Cancellation.IsCancellationRequested)
            {
            }
            catch (Exception error)
            {
                Console.Error.WriteLine($"[WebRTC] {session.Id} stream error: {error}");
                CloseSession(session, "H.264 stream failed");
            }
        }

        private void CloseSession(WebRtcSession session, string reason)
        {
            if (Interlocked.Exchange(ref session.Closed, 1) != 0)
            {
                return;
            }

            sessions.TryRemove(session.Id, out _);
            Console.Error.WriteLine($"[WebRTC] {session.Id} closing: {reason}");
            RecordSessionEvent(session, "closed", reason, "information");
            session.Cancellation.Cancel();
            try
            {
                session.Peer.Close(reason);
            }
            catch
            {
            }
            session.Cancellation.Dispose();
        }

        private void RecordSessionEvent(WebRtcSession session, string action, string outcome, string severity = "information")
        {
            audit.Record(
                severity,
                "stream",
                "webrtc-" + action,
                outcome,
                subject: session.DeviceName,
                details: new Dictionary<string, string>
                {
                    ["session"] = session.Id.ToString("N"),
                    ["transportPlan"] = session.TransportPlan,
                    ["ice"] = session.LastIceState,
                    ["connection"] = session.LastConnectionState
                });
        }

        private static List<RTCIceServer> ToSipsIceServers(IEnumerable<WebRtcIceServer> source)
        {
            var result = new List<RTCIceServer>();
            foreach (var server in source ?? Enumerable.Empty<WebRtcIceServer>())
            {
                foreach (var url in server.Urls ?? Enumerable.Empty<string>())
                {
                    if (string.IsNullOrWhiteSpace(url)) continue;
                    result.Add(new RTCIceServer
                    {
                        urls = url,
                        username = server.Username ?? string.Empty,
                        credential = server.Credential ?? string.Empty,
                        credentialType = RTCIceCredentialType.password
                    });
                }
            }
            return result;
        }

        private sealed class WebRtcSession
        {
            public Guid Id { get; }
            public RTCPeerConnection Peer { get; }
            public string DeviceName { get; }
            public int Fps { get; }
            public int Quality { get; }
            public string TransportPlan { get; }
            public DateTimeOffset CreatedAt { get; } = DateTimeOffset.UtcNow;
            public CancellationTokenSource Cancellation { get; } = new CancellationTokenSource();
            public string LastConnectionState = "new";
            public string LastIceState = "new";
            public int StreamStarted;
            public int Closed;

            public WebRtcSession(Guid id, RTCPeerConnection peer, string deviceName, int fps, int quality, string transportPlan)
            {
                Id = id;
                Peer = peer;
                DeviceName = deviceName;
                Fps = Math.Max(1, Math.Min(60, fps));
                Quality = Math.Max(25, Math.Min(85, quality));
                TransportPlan = transportPlan ?? "direct-stun";
            }
        }
    }

    public sealed class WebRtcOfferAnswer
    {
        public string Type { get; set; }
        public string Sdp { get; set; }
    }

    public sealed class WebRtcDiagnosticsSnapshot
    {
        public string GeneratedAt { get; set; }
        public WebRtcSessionDiagnostics[] ActiveSessions { get; set; } = Array.Empty<WebRtcSessionDiagnostics>();
    }

    public sealed class WebRtcSessionDiagnostics
    {
        public string SessionId { get; set; }
        public string DeviceName { get; set; }
        public string CreatedAt { get; set; }
        public string ConnectionState { get; set; }
        public string IceState { get; set; }
        public string TransportPlan { get; set; }
    }
}
