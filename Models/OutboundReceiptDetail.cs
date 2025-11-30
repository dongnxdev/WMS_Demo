using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WMS_Demo.Models
{
    /// <summary>
    /// Đại diện cho chi tiết của một phiếu xuất kho.
    /// </summary>
    public class OutboundReceiptDetail
    {
        [Key]
        public int Id { get; set; }

        public int OutboundReceiptId { get; set; }
        [ForeignKey(nameof(OutboundReceiptId))]
        public OutboundReceipt? OutboundReceipt { get; set; } // Tham chiếu đến phiếu xuất kho chính.

        public int ItemId { get; set; }
        [ForeignKey(nameof(ItemId))]
        public Item? Item { get; set; } // Tham chiếu đến vật tư.
        [Column(TypeName = "decimal(18, 2)")]

        public decimal Quantity { get; set; }

        public int LocationId { get; set; }
        [ForeignKey(nameof(LocationId))]
        public Location? Location { get; set; } // Tham chiếu đến vị trí.
        [Column(TypeName = "decimal(18, 2)")]

        public decimal SalesPrice { get; set; } // Giá bán.
        [Column(TypeName = "decimal(18, 2)")]

        public decimal CostPrice { get; set; } // Giá vốn.
    }
}
