using Microsoft.AspNetCore.Components.Forms;
using System.ComponentModel.DataAnnotations;

namespace Parcs.Portal.Models
{
    public class CreateJobViewModel
    {
        [Required(ErrorMessage = "Assembly name is required")]
        [MaxLength(50, ErrorMessage = "Assembly name should not exceed 50 characters")]
        [MinLength(1, ErrorMessage = "Assembly name should be at least 1 characters long")]
        public string AssemblyName { get; set; }

        [Required(ErrorMessage = "Class name is required")]
        [MaxLength(50, ErrorMessage = "Class name should not exceed 50 characters")]
        [MinLength(1, ErrorMessage = "Class name should be at least 1 characters long")]
        public string ClassName { get; set; }

        public IEnumerable<IBrowserFile> InputFiles { get; set; } = Enumerable.Empty<IBrowserFile>();
    }
}