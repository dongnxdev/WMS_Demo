using Microsoft.EntityFrameworkCore; 
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WMS_Demo.Models
{
    /// <summary>
    /// Đại diện cho thông tin một khách hàng.
    /// </summary>
    public class Customer
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