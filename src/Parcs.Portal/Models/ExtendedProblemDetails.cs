using Microsoft.AspNetCore.Mvc;

namespace Parcs.Portal.Models
{
    public class ExtendedProblemDetails : ProblemDetails
    {
        public Dictionary<string, List<string>> Errors { get; set; }
    }
}