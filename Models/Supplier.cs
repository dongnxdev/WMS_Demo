using Microsoft.EntityFrameworkCore; 
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
/// <summary>
/// Summary description for Class1
/// </summary>
namespace WMS_Demo.Models
{
    public class Supplier
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        
        [MaxLength(500)]
        public string? Address { get; set; } = string.Empty; 

        [MaxLength(20)]
        public string? PhoneNumber { get; set; } = string.Empty; 
    }
}