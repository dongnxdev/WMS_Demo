using Microsoft.EntityFrameworkCore; 
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
/// <summary>
/// Đại diện cho một phiếu nhập kho.
/// </summary>
namespace WMS_Demo.Models
{
    public class InboundReceipt
    {
        [Key]
        public int Id { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow; // Ngày tạo phiếu.

        public int SupplierId { get; set; }
        [ForeignKey(nameof(SupplierId))]
        public Supplier? Supplier { get; set; } // Tham chiếu đến nhà cung cấp.

        [Required]
        public string UserId { get; set; } = string.Empty;
        [ForeignKey(nameof(UserId))]
        public ApplicationUser? CreatedBy { get; set; } 

        public string? Notes { get; set; }

        // Danh sách các chi tiết của phiếu nhập.
        public ICollection<InboundReceiptDetail> Details { get; set; } = new List<InboundReceiptDetail>();
    }
}