using Microsoft.EntityFrameworkCore; 
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
/// <summary>
/// Đại diện cho một mặt hàng trong kho.
/// </summary>
namespace WMS_Demo.Models
{
    [Index(nameof(Code), IsUnique = true)] // Đảm bảo mã vật tư là duy nhất.
    public class Item
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string Code { get; set; } = string.Empty; // Mã vật tư (SKU).

        [MaxLength(20)]
        public string Unit { get; set; } = "Cái"; // Đơn vị tính.
        
        [Required]
        public double SafetyStock { get; set; } = 0; // Mức tồn kho an toàn.

        public double CurrentStock { get; set; } = 0; 
    }
}