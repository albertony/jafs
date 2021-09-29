namespace Jottacloud
{
    public sealed class HttpMethod // Minimalistic variant of System.Net.Http.HttpMethod, avoiding dependency to the System.Net.Http assembly.
    {
        private string name;
        public override string ToString() { return name; }
        public static HttpMethod Head { get { return new HttpMethod() { name = "HEAD" }; } }
        public static HttpMethod Get { get { return new HttpMethod() { name = "GET" }; } }
        public static HttpMethod Post { get { return new HttpMethod() { name = "POST" }; } }
    }
}
