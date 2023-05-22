using Microsoft.AspNetCore.Components.Forms;
using System.ComponentModel.DataAnnotations;

namespace Parcs.Portal.Models
{
    public class CreateModuleViewModel
    {
        [Required(ErrorMessage = "Name is required")]
        [MaxLength(50, ErrorMessage = "Module name should not exceed 50 characters")]
        [MinLength(3, ErrorMessage = "Module name should be at least 3 characters long")]
        public string Name { get; set; }

        [Required(ErrorMessage = "Binaries are required")]
        [MinLength(1, ErrorMessage = "At least one file is required")]
        public IEnumerable<IBrowserFile> BinaryFiles { get; set; }
    }
}