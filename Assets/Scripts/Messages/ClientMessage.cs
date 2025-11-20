using System;
using UnityEngine;

[Serializable]
public class ClientMessage
{
    /* offer, answer, candidate, clientName */
    public string @event = "";
    public string userName = "";
    /* JSON value */
    public string data = "";
}
