using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class IceCandidate
{
    public string candidate;
    public string sdpMid;
    public int? sdpMLineIndex;
    public string usernameFragment;
}
