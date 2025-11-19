using Microsoft.EntityFrameworkCore; 
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
/// <summary>
/// Summary description for Class1
/// </summary>
namespace WMS_Demo.Models
{
    public class InboundReceiptDetail
    {
        [Key]
        public int Id { get; set; }

        public int InboundReceiptId { get; set; }
        [ForeignKey(nameof(InboundReceiptId))]
        public InboundReceipt? InboundReceipt { get; set; } // Để EF Core hiểu quan hệ cha-con

        public int ItemId { get; set; }
        [ForeignKey(nameof(ItemId))]
        public Item? Item { get; set; }

        public double Quantity { get; set; }

        public int LocationId { get; set; }
        [ForeignKey(nameof(LocationId))]
        public Location? Location { get; set; }
    }
}