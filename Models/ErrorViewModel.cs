namespace WMS_Demo.Models
{
    /// <summary>
    /// Model dùng để hiển thị thông tin lỗi.
    /// </summary>
    public class ErrorViewModel
    {
       
        public string? RequestId { get; set; }

        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
    }
}
