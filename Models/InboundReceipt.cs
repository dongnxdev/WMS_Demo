using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WMS_Demo.Models
{
    /// <summary>
    /// Đại diện cho thông tin một phiếu nhập kho (Inbound Receipt).
    /// </summary>
    public class InboundReceipt
    {

        [Key]
        public int Id { get; set; }


        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        public int SupplierId { get; set; }

        [ForeignKey(nameof(SupplierId))]
        public Supplier? Supplier { get; set; }


        [Required]
        public string UserId { get; set; } = string.Empty;

        [ForeignKey(nameof(UserId))]
        public ApplicationUser? CreatedBy { get; set; }


        public string? Notes { get; set; }


        public ICollection<InboundReceiptDetail> Details { get; set; } = new List<InboundReceiptDetail>();
    }
}