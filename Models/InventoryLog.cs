using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WMS_Demo.Models
{
    public class InventoryLog
    {
        [Key]
        public long Id { get; set; } // Dùng long chuẩn bài rồi

        public int ItemId { get; set; }
        [ForeignKey(nameof(ItemId))]
        public Item? Item { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        [Required, MaxLength(50)]
        public string ActionType { get; set; } = string.Empty; // "INBOUND", "OUTBOUND", "ADJUSTMENT"

        public int ReferenceId { get; set; } // ID phiếu nhập/xuất

        public double ChangeQuantity { get; set; } // + hoặc -

        public double NewStock { get; set; } // Tồn kho SAU khi thay đổi
    }
}