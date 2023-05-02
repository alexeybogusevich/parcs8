using Parcs.Net;

namespace Parcs.Core.Services
{
    public class ArgumentsProvider : IArgumentsProvider
    {
        private readonly int _pointsNumber;
        private readonly IDictionary<string, string> _argumentsDictionary;

        public ArgumentsProvider(int pointsNumber, IDictionary<string, string> argumentsDictionary)
        {
            _pointsNumber = pointsNumber;
            _argumentsDictionary = argumentsDictionary;
        }

        public int GetPointsNumber() => _pointsNumber;

        public IDictionary<string, string> GetArguments() => _argumentsDictionary;
    }
}