using Microsoft.AspNetCore.Hosting.Server;

namespace swipefilm.Models
{
    public class ServerMovie
    {
        public Guid Id { get; set; }
        public Guid ServerId { get; set; }
        public Guid MovieId { get; set; }

        public UserServer Server { get; set; }
        public Movie Movie { get; set; }
    }
}
