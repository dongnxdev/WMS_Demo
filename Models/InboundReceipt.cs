using Microsoft.EntityFrameworkCore; 
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
/// <summary>
/// Summary description for Class1
/// </summary>
namespace WMS_Demo.Models
{
    public class InboundReceipt
    {
        [Key]
        public int Id { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow; // Mặc định lấy giờ UTC, hiển thị thì convert sau

        public int SupplierId { get; set; }
        [ForeignKey(nameof(SupplierId))]
        public Supplier? Supplier { get; set; } // Navigation Property

        [Required]
        public string UserId { get; set; } = string.Empty;
        [ForeignKey(nameof(UserId))]
        public ApplicationUser? CreatedBy { get; set; } 

        public string? Notes { get; set; }

        // Quan hệ 1-N: Một phiếu có nhiều dòng chi tiết
        public ICollection<InboundReceiptDetail> Details { get; set; } = new List<InboundReceiptDetail>();
    }
}