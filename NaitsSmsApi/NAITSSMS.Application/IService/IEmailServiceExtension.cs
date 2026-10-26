using NAITSSMS.Application.DTOs;

namespace NAITSSMS.Application.IService;

public interface IEmailServiceExtension
{
    Task<object> PostSystemInformation(string userIdOrSchoolId, CancellationToken ct);
    Task<ContactEnquiryResult> SendContactEnquiry(ContactUsDto model, CancellationToken ct = default);
}
