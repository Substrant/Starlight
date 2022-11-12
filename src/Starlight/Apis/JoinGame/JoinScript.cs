﻿using Newtonsoft.Json;
using System.Linq;
using System.Net;

namespace Starlight.Apis.JoinGame;

/// <summary>
///     Represents a join script.<br/>
///     A join script is esentially another term for server information.
/// </summary>
public class JoinScript
{
    [JsonProperty("MachineAddress")] internal string Address;
    [JsonProperty("ServerPort")] internal int? Port;
    [JsonProperty("UdmuxEndpoints")] internal UdmuxEndpoint[] UdmuxEndpoints;

    /// <summary>
    ///     Get the address and port of the server to join.
    /// </summary>
    /// <returns>An <see cref="IPEndPoint"/> representing the endpoint of the server.</returns>
    public IPEndPoint GetEndpoint()
    {
        IPAddress ipAddr;
        int port;

        if (UdmuxEndpoints is not null)
        {
            var endpoint = UdmuxEndpoints.FirstOrDefault();
            if (endpoint is null)
                return null;

            IPAddress.TryParse(endpoint.Address, out ipAddr);
            port = endpoint.Port.GetValueOrDefault();
        }
        else
        {
            IPAddress.TryParse(Address, out ipAddr);
            port = Port.GetValueOrDefault();
        }

        return new IPEndPoint(ipAddr, port);
    }
}