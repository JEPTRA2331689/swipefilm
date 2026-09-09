using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using swipefilm.Auth;

namespace swipefilm.Hubs
{
    /// <summary>
    /// Diffusion temps réel d'une session de groupe. Ne porte aucune logique
    /// métier (adhésion, genre, swipe, matchs — tout passe par SessionController
    /// et AppDbContext) : ce hub gère seulement l'abonnement au groupe SignalR
    /// nommé par le code de session, et les contrôleurs poussent les événements
    /// via IHubContext&lt;SessionHub&gt; après chaque mutation en base.
    /// </summary>
    [Authorize]
    public class SessionHub : Hub
    {
        private readonly SessionService _sessions;

        public SessionHub(SessionService sessions)
        {
            _sessions = sessions;
        }

        public async Task JoinGroup(string code)
        {
            var normalized = code.Trim().ToUpperInvariant();
            await Groups.AddToGroupAsync(Context.ConnectionId, normalized);

            var session = await _sessions.GetActiveSessionByCodeAsync(normalized);
            if (session is null) return;

            var state = await _sessions.GetSessionStateAsync(session.Id);
            if (state != null)
                await Clients.Group(normalized).SendAsync("MemberJoined", state);
        }

        public async Task LeaveGroup(string code)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, code.Trim().ToUpperInvariant());
        }
    }
}
