// Helpers/PaginatedList.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace WMS_Demo.Helpers
{
    /// <summary>
    /// Lớp generic để hỗ trợ phân trang cho các danh sách dữ liệu.
    /// </summary>
    /// <typeparam name="T">Kiểu dữ liệu của các phần tử trong danh sách.</typeparam>
    public class PaginatedList<T> : List<T>
    {
     
        public int PageIndex { get; private set; }

    
        public int TotalPages { get; private set; }

        public PaginatedList(List<T> items, int count, int pageIndex, int pageSize)
        {
            PageIndex = pageIndex;
            TotalPages = (int)Math.Ceiling(count / (double)pageSize);

            this.AddRange(items);
        }

    
        public bool HasPreviousPage => PageIndex > 1;

        public bool HasNextPage => PageIndex < TotalPages;

        /// <summary>
        /// Tạo một danh sách đã phân trang một cách bất đồng bộ từ một IQueryable.
        /// </summary>
        /// <param name="source">Nguồn dữ liệu IQueryable.</param>
        /// <param name="pageIndex">Chỉ số trang cần lấy.</param>
        /// <param name="pageSize">Kích thước của một trang.</param>
        /// <returns>Một đối tượng PaginatedList.</returns>
        public static async Task<PaginatedList<T>> CreateAsync(IQueryable<T> source, int pageIndex, int pageSize)
        {
            var count = await source.CountAsync();
            var items = await source.Skip((pageIndex - 1) * pageSize).Take(pageSize).ToListAsync();
            return new PaginatedList<T>(items, count, pageIndex, pageSize);
        }
    }
}