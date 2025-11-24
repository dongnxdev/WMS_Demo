using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WMS_Demo.Models
{
    /// <summary>
    /// Ghi lại lịch sử thay đổi tồn kho của vật tư.
    /// </summary>
    public class InventoryLog
    {
        [Key]
        public long Id { get; set; } // ID của log.

        public int ItemId { get; set; }
        [ForeignKey(nameof(ItemId))]
        public Item? Item { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        [Required, MaxLength(50)]
        public string ActionType { get; set; } = string.Empty; // Loại hành động: "INBOUND", "OUTBOUND", "ADJUSTMENT".

        public int ReferenceId { get; set; } // ID tham chiếu đến phiếu nhập/xuất.

        public double ChangeQuantity { get; set; } // Số lượng thay đổi (dương hoặc âm).

        public double NewStock { get; set; } // Số lượng tồn kho mới sau khi thay đổi.
    }
}