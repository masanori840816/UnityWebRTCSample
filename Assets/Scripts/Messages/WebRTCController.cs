using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Unity.WebRTC;

public class WebRTCController
{
    private RTCPeerConnection _peerConnection = null;
    private AudioSource _remoteAudioSource;
    public event Action<ClientMessage> OnLocalDescriptionCreated;
    public event Action<ClientMessage> OnIceCandidateCreated;
    private RTCDataChannel _dataChannel1;
    private RTCDataChannel _dataChannel2;
    private RTCDataChannel _remoteDataChannel;
    private DelegateOnMessage onDataChannelMessage;
    private DelegateOnDataChannel onDataChannel;

    public void Init(AudioSource remoteAudio)
    {
        _remoteAudioSource = remoteAudio;

        onDataChannel = channel =>
        {
            _remoteDataChannel = channel;
            _remoteDataChannel.OnMessage = onDataChannelMessage;
        };
        onDataChannelMessage = bytes => { Debug.Log("onDataChannelMessage:" + System.Text.Encoding.UTF8.GetString(bytes)); };
    }
    public void Connect()
    {
        if (_peerConnection != null)
        {
            _peerConnection.Dispose();
        }
        RTCConfiguration config = GetConfig();
        _peerConnection = new RTCPeerConnection(ref config);
        _peerConnection.OnIceCandidate = HandleIceCandidate;
        _peerConnection.OnConnectionStateChange = HandleConnectionStateChange;
        _peerConnection.OnTrack = HandleTrackEvent;
        _peerConnection.OnDataChannel = onDataChannel;
        RTCDataChannelInit conf1 = new RTCDataChannelInit();
        conf1.id = 20;
        _dataChannel1 = _peerConnection.CreateDataChannel("sample", conf1);
        _dataChannel1.OnOpen = () =>
        {
            Debug.Log("DataChannel Open");
        };
        RTCDataChannelInit conf2 = new RTCDataChannelInit();
        conf2.id = 21;
        _dataChannel2 = _peerConnection.CreateDataChannel("sample2", conf2);

        _dataChannel2.OnOpen = () =>
        {
            Debug.Log("DataChannel2 Open");
        };
    }
    public void Close()
    {
        if (_peerConnection != null)
        {
            _dataChannel1?.Close();
            _dataChannel2?.Close();
            _remoteDataChannel?.Close();
            _peerConnection.Close();
            _peerConnection.Dispose();
            _peerConnection = null;
            Debug.Log("RTCPeerConnection closed and disposed.");
        }
    }
    public void AddLocalTrack(MediaStreamTrack track, MediaStream stream)
    {
        if (_peerConnection != null)
        {
            Debug.Log("RTCPeerConnection ad track." + track.Kind);
            _peerConnection.AddTrack(track, stream);
        }
    }
    public async void OnOfferReceived(RTCSessionDescription offer)
    {
        if (_peerConnection == null)
        {
            return;
        }
        
        RTCSetSessionDescriptionAsyncOperation setRemoteOp = _peerConnection.SetRemoteDescription(ref offer);
        while (!setRemoteOp.IsDone)
        {
            await Task.Delay(10);
        }
        if (setRemoteOp.IsError)
        {
            Debug.LogError($"Failed to set remote Offer: {setRemoteOp.Error.message}");
            return;
        }
        RTCSessionDescriptionAsyncOperation createAnswerOp = _peerConnection.CreateAnswer();
        while (!createAnswerOp.IsDone)
        {
            await Task.Delay(10);
        }

        if (createAnswerOp.IsError)
        {
            Debug.LogError($"Failed to create Answer: {createAnswerOp.Error.message}");
            return;
        }
        // 3. ローカルのAnswerを設定し、シグナリングサーバーへ送信
        await SetAndSignalLocalDescription(createAnswerOp.Desc);
    }
    private RTCConfiguration GetConfig()
    {
        RTCConfiguration result = default;
        result.iceServers = new[] { new RTCIceServer {
            urls = new[] { "stun:stun.l.google.com:19302" } 
        } };

        return result;
    }
    public void OnCandidateReceived(RTCIceCandidate candidate)
    {
        if (_peerConnection == null) return;
        _peerConnection.AddIceCandidate(candidate);
    }
    private async Task SetAndSignalLocalDescription(RTCSessionDescription desc)
    {
        RTCSetSessionDescriptionAsyncOperation setLocalOp = _peerConnection.SetLocalDescription(ref desc);
        while (!setLocalOp.IsDone)
        {
            await Task.Delay(10);
        }

        if (setLocalOp.IsError)
        {
            Debug.LogError($"Failed to set local description: {setLocalOp.Error.message}");
            return;
        }
        OnLocalDescriptionCreated?.Invoke(GenerateAnswerMessage(desc));
        Debug.Log($"Set local {desc.type} and signaled to server.");
    }
    private void HandleIceCandidate(RTCIceCandidate candidate)
    {
        OnIceCandidateCreated?.Invoke(GenerateCandidateMessage(candidate));
        Debug.Log($"Generated local ICE Candidate and signaled: {candidate.Candidate}");
    }

    private void HandleConnectionStateChange(RTCPeerConnectionState state)
    {
        Debug.Log($"Connection State Changed: {state}");
        if (state == RTCPeerConnectionState.Connected)
        {
            Debug.Log("WebRTC Connection Established!");
        }
    }

    private void HandleTrackEvent(RTCTrackEvent e)
    {
        MediaStream remoteStream = e.Streams.FirstOrDefault();

        if (remoteStream != null)
        {
            Debug.Log($"Remote Track Received! Kind: {e.Track.Kind}, Stream ID: {remoteStream.Id}");
            if (e.Track.Kind == TrackKind.Audio && _remoteAudioSource != null)
            {
                // RTCMediaStreamTrackをAudioStreamTrackにキャスト
                AudioStreamTrack audioTrack = e.Track as AudioStreamTrack;

                if (audioTrack != null)
                {
                    _remoteAudioSource.SetTrack(audioTrack);

                    // 再生を開始するためにPlay()を呼び出す必要がある場合があります
                    if (!_remoteAudioSource.isPlaying)
                    {
                        _remoteAudioSource.Play();
                    }
                }
            }
        }
        else
        {
            Debug.Log($"Remote Track Received! Kind: {e.Track.Kind}, No Stream ID found.");
        }
    }
    private ClientMessage GenerateAnswerMessage(RTCSessionDescription answerDescription)
    {
        SessionDescription desc = new SessionDescription
        {
            sdp = answerDescription.sdp,
        };
        switch(answerDescription.type)
        {
            case RTCSdpType.Answer:
                desc.type = "answer";
                break;
            default:
                // Answer以外は無いはず
                Debug.Log($"Generate other type {answerDescription.type}");
                break;
        }
        return new ClientMessage
        {
            @event = "answer",
            userName = "example",
            data = JsonUtility.ToJson(desc),
        };
    }
    private ClientMessage GenerateCandidateMessage(RTCIceCandidate newCandidate)
    {
        IceCandidate cnd = new IceCandidate
        {
            candidate = newCandidate.Candidate,
            sdpMid = newCandidate.SdpMid,
            sdpMLineIndex = newCandidate.SdpMLineIndex,
            usernameFragment = newCandidate.UserNameFragment,
        };
        return new ClientMessage
        {
            @event = "candidate",
            userName = "example",
            data = JsonUtility.ToJson(cnd),
        };
    }
}
