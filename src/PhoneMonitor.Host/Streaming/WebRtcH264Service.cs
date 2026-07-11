using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SIPSorcery.Net;
using SIPSorceryMedia.Abstractions;

namespace PhoneMonitor.Host.Streaming
{
    /// <summary>
    /// Owns browser WebRTC sessions for the virtual display. Signalling is a
    /// single HTTPS offer/answer request; media then travels directly over the
    /// DTLS/SRTP WebRTC connection on the local network.
    /// </summary>
    public sealed class WebRtcH264Service
    {
        private readonly H264AnnexBStreamer h264;
        private readonly ConcurrentDictionary<Guid, WebRtcSession> sessions = new ConcurrentDictionary<Guid, WebRtcSession>();

        public WebRtcH264Service(H264AnnexBStreamer h264)
        {
            this.h264 = h264;
        }

        public bool IsAvailable => h264.IsAvailable;

        public async Task<WebRtcOfferAnswer> CreateAnswerAsync(string offerSdp, string deviceName, int fps, int quality)
        {
            if (string.IsNullOrWhiteSpace(offerSdp))
            {
                throw new ArgumentException("WebRTC offer SDP is required.", nameof(offerSdp));
            }

            var configuration = new RTCConfiguration
            {
                // A phone connects to this Host over the same LAN. Include its
                // actual LAN address in the SDP rather than only loopback.
                X_ICEIncludeAllInterfaceAddresses = true
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

            var session = new WebRtcSession(Guid.NewGuid(), peer, deviceName, fps, quality);
            peer.onconnectionstatechange += state => OnConnectionStateChanged(session, state);
            peer.oniceconnectionstatechange += state =>
            {
                Console.Error.WriteLine($"[WebRTC] {session.Id} ice={state} device={session.DeviceName}");
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

        private sealed class WebRtcSession
        {
            public Guid Id { get; }
            public RTCPeerConnection Peer { get; }
            public string DeviceName { get; }
            public int Fps { get; }
            public int Quality { get; }
            public CancellationTokenSource Cancellation { get; } = new CancellationTokenSource();
            public int StreamStarted;
            public int Closed;

            public WebRtcSession(Guid id, RTCPeerConnection peer, string deviceName, int fps, int quality)
            {
                Id = id;
                Peer = peer;
                DeviceName = deviceName;
                Fps = Math.Max(1, Math.Min(60, fps));
                Quality = Math.Max(25, Math.Min(85, quality));
            }
        }
    }

    public sealed class WebRtcOfferAnswer
    {
        public string Type { get; set; }
        public string Sdp { get; set; }
    }
}
