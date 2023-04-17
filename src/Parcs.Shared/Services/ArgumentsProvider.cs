using Parcs.Net;

namespace Parcs.Shared.Services
{
    public class ArgumentsProvider : IArgumentsProvider
    {
        private readonly ArgumentsBase _argumentsBase;
        private readonly IDictionary<string, string> _argumentsDictionary;

        public ArgumentsProvider(int pointsNumber, IDictionary<string, string> argumentsDictionary)
        {
            _argumentsBase = new ArgumentsBase { PointsNumber = pointsNumber };
            _argumentsDictionary = argumentsDictionary;
        }

        public ArgumentsBase GetBase() => _argumentsBase;

        public IDictionary<string, string> GetRaw() => _argumentsDictionary;
    }
}