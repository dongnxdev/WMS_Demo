using Microsoft.EntityFrameworkCore; 
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WMS_Demo.Models
{
    public class OutboundReceipt
    {
        [Key]
        public int Id { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        public string? Reason { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;
        [ForeignKey(nameof(UserId))]
        public ApplicationUser? CreatedBy { get; set; }

        public string? Notes { get; set; }

        public ICollection<OutboundReceiptDetail> Details { get; set; } = new List<OutboundReceiptDetail>();
    }
}
