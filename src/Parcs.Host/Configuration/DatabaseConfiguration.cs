namespace Parcs.Host.Configuration
{
    public class DatabaseConfiguration
    {
        public const string SectionName = "Database";

        public string HostName { get; set; }

        public int Port { get; set; }

        public string DatabaseName { get; set; }

        public string Username { get; set; }

        public string Password { get; set; }
    }
}