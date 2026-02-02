namespace CustomResoManager.runner;

public class AppLifeCycler
{
    /* File này đảm bảo chạy tất cả các modules và giao diện khi bắt đầu chạy chương trình. */
    /* File này cũng sẽ gọi đến UI và khởi tạo UI, gắn kết UI với logic backend. */
    /* File này cũng sẽ gọi đến update để kiểm tra cập nhật và cài đặt cập nhật. */
    /* Nên tạo các subclasses để gọi đến các modules và gọi đến UI, class này sẽ gọi đến các subclasses. */
}