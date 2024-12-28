namespace Parcs.Portal.Models
{
    public class ArgumentPair(string key, string value)
    {
        public string Key { get; set; } = key;

        public string Value { get; set; } = value;
    }
}