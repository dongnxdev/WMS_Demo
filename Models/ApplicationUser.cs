using System;
using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
/// <summary>
/// Đại diện cho một người dùng trong hệ thống, mở rộng từ IdentityUser.
/// </summary>
namespace WMS_Demo.Models
{
    public class ApplicationUser : IdentityUser
    {
        [Required]
        [MaxLength(100)]
        public string FullName { get; set; } = string.Empty;

        [MaxLength(20)]
        public string? StaffCode { get; set; } // Mã nhân viên, có thể rỗng.

        public bool IsActive { get; set; } = true; // Trạng thái hoạt động của tài khoản.
    }
}