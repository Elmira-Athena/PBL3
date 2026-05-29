namespace PBL3.Application.Common.Exceptions;

public class ForbiddenException : Exception
{
    public ForbiddenException(string message = "Bạn không có quyền thực hiện hành động này.")
        : base(message) { }
}
