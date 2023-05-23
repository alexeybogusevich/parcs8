namespace Parcs.Portal.Models
{
    public class ArgumentPair
    {
        public ArgumentPair(string key, string value)
        {
            Key = key;
            Value = value;
        }

        public string Key { get; set; }

        public string Value { get; set; }
    }
}