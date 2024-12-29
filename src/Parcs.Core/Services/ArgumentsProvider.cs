using Parcs.Net;

namespace Parcs.Core.Services
{
    public class ArgumentsProvider(IDictionary<string, string> argumentsDictionary) : IArgumentsProvider
    {
        private readonly IDictionary<string, string> _argumentsDictionary = argumentsDictionary;

        public IDictionary<string, string> GetArguments() => _argumentsDictionary;
    }
}