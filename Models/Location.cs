using Microsoft.EntityFrameworkCore; 
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
/// <summary>
/// Summary description for Class1
/// </summary>
namespace WMS_Demo.Models
{
    [Index(nameof(Code), IsUnique = true)]
    public class Location
    {
        [Key]
        public int Id { get; set; }
        
        [Required, MaxLength(20)]
        public string Code { get; set; } = string.Empty; // A-01, B-02...
        
        [MaxLength(200)]
        public string? Description { get; set; }
    }
}