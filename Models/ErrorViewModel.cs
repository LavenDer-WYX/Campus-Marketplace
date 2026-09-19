namespace WebApplication1.Models
{
    // 错误页只显示请求追踪编号，不向用户展示内部异常细节。
    public class ErrorViewModel
    {
        // 问号表示在极少数情况下编号可以为空。
        public string? RequestId { get; set; }

        // => 是只读计算属性：有编号时页面才显示。
        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
    }
}
