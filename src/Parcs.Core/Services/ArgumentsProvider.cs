using Parcs.Net;

namespace Parcs.Core.Services
{
    public class ArgumentsProvider(int pointsNumber, IDictionary<string, string> argumentsDictionary) : IArgumentsProvider
    {
        private readonly int _pointsNumber = pointsNumber;
        private readonly IDictionary<string, string> _argumentsDictionary = argumentsDictionary;

        public int GetPointsNumber() => _pointsNumber;

        public IDictionary<string, string> GetArguments() => _argumentsDictionary;
    }
}