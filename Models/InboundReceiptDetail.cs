using Microsoft.EntityFrameworkCore; 
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WMS_Demo.Models
{
    /// <summary>
    /// Đại diện cho một dòng chi tiết trên phiếu nhập kho.
    /// </summary>
    public class InboundReceiptDetail
    {
        
        [Key]
        public int Id { get; set; }

        public int InboundReceiptId { get; set; }
        
        [ForeignKey(nameof(InboundReceiptId))]
        public InboundReceipt? InboundReceipt { get; set; }

      
        public int ItemId { get; set; }
       
        [ForeignKey(nameof(ItemId))]
        public Item? Item { get; set; }

       
        [Column(TypeName = "decimal(18, 2)")]
        public decimal Quantity { get; set; }

      
        public int LocationId { get; set; }
      
        [ForeignKey(nameof(LocationId))]
        public Location? Location { get; set; }
        
       
        [Column(TypeName = "decimal(18, 2)")]
        public decimal UnitPrice { get; set; }
    }
}