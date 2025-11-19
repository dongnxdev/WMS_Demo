using System;
using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
/// <summary>
/// Summary description for Class1
/// </summary>
namespace WMS_Demo.Models
{
    public class ApplicationUser : IdentityUser
    {
        [Required]
        [MaxLength(100)]
        public string FullName { get; set; } = string.Empty;

        [MaxLength(20)]
        public string? StaffCode { get; set; } // Mã nhân viên, có thể null nếu là Admin hệ thống

        public bool IsActive { get; set; } = true; // Mặc định là đang làm, chưa bị đuổi
    }
}