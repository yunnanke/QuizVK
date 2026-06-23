using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

namespace QuizMvp.Hubs
{
    public class QuizHub : Hub
    {
        private static readonly ConcurrentDictionary<string, HashSet<string>> _rooms = new();
        private static readonly ConcurrentDictionary<string, string> _userRooms = new();

        public async Task JoinRoom(string roomCode, string userName)
        {
            if (!_rooms.ContainsKey(roomCode))
                _rooms[roomCode] = new HashSet<string>();

            _rooms[roomCode].Add(Context.ConnectionId);
            _userRooms[Context.ConnectionId] = roomCode;

            await Groups.AddToGroupAsync(Context.ConnectionId, roomCode);

            await Clients.Group(roomCode).SendAsync("UserJoined", userName);

            await Clients.Group(roomCode).SendAsync("UpdateParticipants", GetParticipants(roomCode));
        }

        public async Task LeaveRoom(string roomCode)
        {
            if (_userRooms.TryRemove(Context.ConnectionId, out var room))
            {
                if (_rooms.TryGetValue(room, out var participants))
                {
                    participants.Remove(Context.ConnectionId);
                    await Groups.RemoveFromGroupAsync(Context.ConnectionId, room);
                    await Clients.Group(room).SendAsync("UpdateParticipants", GetParticipants(room));
                }
            }
        }

        public async Task StartQuizSession(string roomCode, int sessionId)
        {
            await Clients.Group(roomCode).SendAsync("QuizStarted", sessionId);
        }

        public async Task NextQuestion(string roomCode, int questionIndex, int totalQuestions)
        {
            await Clients.Group(roomCode).SendAsync("ShowQuestion", questionIndex, totalQuestions);
        }

        public async Task EndQuiz(string roomCode)
        {
            await Clients.Group(roomCode).SendAsync("QuizEnded");
        }

        private List<string> GetParticipants(string roomCode)
        {
            if (_rooms.TryGetValue(roomCode, out var participants))
            {
                return participants.ToList();
            }
            return new List<string>();
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            if (_userRooms.TryRemove(Context.ConnectionId, out var roomCode))
            {
                if (_rooms.TryGetValue(roomCode, out var participants))
                {
                    participants.Remove(Context.ConnectionId);
                    await Clients.Group(roomCode).SendAsync("UpdateParticipants", GetParticipants(roomCode));
                }
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomCode);
            }
            await base.OnDisconnectedAsync(exception);
        }
    }
}