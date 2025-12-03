using System;
using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace WMS_Demo.Models
{
    /// <summary>
    /// Đại diện cho một người dùng trong hệ thống, kế thừa từ IdentityUser để có các thuộc tính mặc định.
    /// </summary>
    public class ApplicationUser : IdentityUser
    {
       
        [Required]
        [MaxLength(100)]
        public string FullName { get; set; } = string.Empty;

        
        [MaxLength(20)]
        public string? StaffCode { get; set; }

        public bool IsActive { get; set; } = true;
    }
}