using Microsoft.EntityFrameworkCore; 
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WMS_Demo.Models
{
    public class OutboundReceiptDetail
    {
        [Key]
        public int Id { get; set; }

        public int OutboundReceiptId { get; set; }
        [ForeignKey(nameof(OutboundReceiptId))]
        public OutboundReceipt? OutboundReceipt { get; set; }

        public int ItemId { get; set; }
        [ForeignKey(nameof(ItemId))]
        public Item? Item { get; set; }

        public double Quantity { get; set; }

        public int LocationId { get; set; }
        [ForeignKey(nameof(LocationId))]
        public Location? Location { get; set; }
    }
}
