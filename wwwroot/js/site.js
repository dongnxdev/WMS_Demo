// wwwroot/js/site.js

$(document).ready(function () {
    // Xử lý sự kiện click vào nút toggle sidebar
    $("#sidebarToggle").on("click", function (e) {
        e.preventDefault(); // Chặn hành vi mặc định
        $("body").toggleClass("sb-sidenav-toggled");
    });
});