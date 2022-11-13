﻿using Newtonsoft.Json;
using RestSharp;
using RestSharp.Serializers.NewtonsoftJson;
using Starlight.Apis.JoinGame;
using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace Starlight.Apis;

/// <summary>
///     Represents a Roblox web session.
/// </summary>
public class Session : RbxUser, IDisposable
{
    Session()
    {
    }

    /* Clients */
    
    internal readonly RestClient AuthClient = new RestClient("https://auth.roblox.com/").UseNewtonsoftJson();
    internal readonly RestClient GameClient = new RestClient("https://gamejoin.roblox.com/").UseNewtonsoftJson();
    internal readonly RestClient GeneralClient = new RestClient("https://www.roblox.com/").UseNewtonsoftJson();

    /* Tokens */
    
    string _authToken;

    /// <summary>
    ///    The token used to authenticate the session.
    ///    This token is the same thing as a <c>.ROBLOSECURITY</c> cookie.
    /// </summary>
    public string AuthToken
    {
        get => _authToken;
        set
        {
            GeneralClient.AddCookie(".ROBLOSECURITY", value, "/", ".roblox.com");
            AuthClient.AddCookie(".ROBLOSECURITY", value, "/", ".roblox.com");
            GameClient.AddCookie(".ROBLOSECURITY", value, "/", ".roblox.com");
            _authToken = value;
        }
    }

    DateTime _xsrfLastGrabbed;
    string _xsrfToken;

    /* Session Info */
    
    public override string UserId { get; protected set; }
    
    public override string Username { get; protected set; }

    async Task RetrieveInfoAsync()
    {
        try
        {
            var res = await GeneralClient.GetJsonAsync<SessionInfo>("/my/settings/json");
            UserId = res?.UserId;
            Username = res?.Username;

            // Validate that the info retrieval succeeded
            if (string.IsNullOrEmpty(UserId) || string.IsNullOrEmpty(Username))
                throw new NotImplementedException();
        }
        catch (JsonSerializationException)
        {
            // Roblox returns HTML content when the session is invalid, and redirects to the login page.
            // This is a workaround to catch that and throw a more descriptive exception.
            throw new NotImplementedException();
        }
    }

    /* Authentication */

    /// <summary>
    ///     Login to Roblox using an authentication token (<c>.ROBLOSECURITY</c> cookie).
    /// </summary>
    /// <param name="authToken">The authentication token to log in with.</param>
    /// <returns>The <see cref="Session"/> authenticated with the given parameters.</returns>
    public static async Task<Session> LoginAsync(string authToken)
    {
        var session = new Session { AuthToken = authToken };

        await session.RetrieveInfoAsync();
        return session;
    }

    /// <summary>
    ///     Login to Roblox using an authentication ticket.
    /// </summary>
    /// <param name="authTicket">The authentication token to redeem.</param>
    /// <returns>The <see cref="Session"/> authenticated with the given parameters.</returns>
    public static async Task<Session> RedeemAsync(string authTicket)
    {
        var session = new Session();
        var req = new RestRequest("/v1/authentication-ticket/redeem", Method.Post)
            .AddHeader("RBXAuthenticationNegotiation", "1")
            .AddJsonBody(new { authenticationTicket = authTicket });
        await session.AuthClient.ExecuteAsync(req);
        
        await session.RetrieveInfoAsync();
        return session;
    }

    /// <summary>
    ///     Login to Roblox using the given authentication type.
    /// </summary>
    /// <param name="authToken">The authentication token to log in with.</param>
    /// <param name="authType">The method of authentication to use.</param>
    /// <returns>The <see cref="Session"/> authenticated with the given parameters.</returns>
    public static async Task<Session> AuthenticateAsync(string authToken, AuthType authType)
    {
        return authType switch
        {
            AuthType.Token => await LoginAsync(authToken),
            AuthType.Ticket => await RedeemAsync(authToken),
            _ => throw new NotImplementedException()
        };
    }

    /// <summary>
    ///     Create an authentication ticket for a one-time login.
    /// </summary>
    /// <returns>The authentication ticket that was created.</returns>
    public async Task<string> GetTicketAsync()
    {
        // Get a new authentication ticket
        var req = new RestRequest("/v1/authentication-ticket", Method.Post)
            .AddHeader("X-CSRF-TOKEN", await GetXsrfTokenAsync())
            .AddHeader("Referer", "https://www.roblox.com/");
        var res = await AuthClient.ExecuteAsync(req);

        // Get the ticket header
        var ticketHeader = res.Headers?.FirstOrDefault(x => x.Name == "rbx-authentication-ticket");
        if (ticketHeader is null)
            throw new NotImplementedException();

        return ticketHeader.Value?.ToString();
    }

    /// <summary>
    ///     <para>Get a cross-site request forgery token for use in the Roblox web API.</para>
    ///     More information on XSRF and why there's tokens for it:<br/>
    ///     <see href="https://en.wikipedia.org/wiki/Cross-site_request_forgery" />.
    /// </summary>
    /// <param name="bypassCache">Bypass the cache.</param>
    /// <returns>The cross-site request forgery token.</returns>
    public async Task<string> GetXsrfTokenAsync(bool bypassCache = false)
    {
        // Return previous token if xsrf token is still alive
        if (!bypassCache && (string.IsNullOrEmpty(_xsrfToken) || (DateTime.Now - _xsrfLastGrabbed).TotalMinutes > 2))
            return _xsrfToken;

        // This won't log you out without a xsrf token
        var req = new RestRequest("/v2/logout", Method.Post);
        var res = await AuthClient.ExecuteAsync(req);

        // Get xsrf token header
        var xsrfHeader = res.Headers?.FirstOrDefault(x => x.Name?.ToLowerInvariant() == "x-csrf-token");
        if (xsrfHeader is null)
            throw new NotImplementedException();

        // Set token
        _xsrfToken = xsrfHeader.Value?.ToString();
        _xsrfLastGrabbed = DateTime.Now;

        return _xsrfToken;
    }

    /* Game joining */

    /// <summary>
    ///     Attempt to get a <see cref="JoinResponse"/> from a <see cref="JoinRequest"/>.
    /// </summary>
    /// <param name="joinReq">The join request to send to the server.</param>
    /// <param name="maxTries">The maximum amount of retries. Using zero here means infinite tries.</param>
    /// <returns>The response from the game join API.</returns>
    public async Task<JoinResponse> RequestJoinAsync(JoinRequest joinReq, int maxTries = 0)
    {
        var tries = -1;
        for (; ; )
        {
            var reqBody = JsonConvert.SerializeObject(this, Formatting.None,
                new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            var req = new RestRequest(joinReq.Endpoint, Method.Post)
                .AddHeader("User-Agent", "Roblox/WinInet")
                .AddJsonBody(reqBody);
            var res = await GameClient.ExecuteAsync<JoinResponse>(req);

            if (res.Data is null)
                return new JoinResponse { Success = false };

            if (res.StatusCode != HttpStatusCode.OK || res.Data.Status == JoinStatus.Fail)
            {
                res.Data.Success = false;
                return res.Data;
            }

            if (res.Data.Status == JoinStatus.Retry && tries != maxTries)
            {
                await Task.Delay(2000); // Wait a bit.
                tries++;
                continue;
            }

            res.Data.Success = true;
            return res.Data;
        }
    }

    /* IDisposable implementation */

    bool _disposed;

    protected virtual void Dispose(bool disposing)
    {
        if (!disposing || _disposed)
            return;
        
        _disposed = true;
        GeneralClient.Dispose();
        AuthClient.Dispose();
        GameClient.Dispose();
    }

    /// <summary>
    ///     Clean up and release all clients that have been allocated.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    class SessionInfo
    {
        [JsonProperty("UserId")] public string UserId;
        [JsonProperty("Name")] public string Username;
    }
}