using System.ComponentModel.DataAnnotations;

namespace Parcs.Portal.Models
{
    public class RunJobViewModel
    {
        [Required]
        [Range(0, int.MaxValue, ErrorMessage = "The number of points must be a positive integer")]
        public int PointsNumber { get; set; } = 1;

        public List<ArgumentPair> Arguments { get; set; } = [];
    }
}