namespace PBL3.Shared.DTOs.ServiceTickets
{
    // Request DTOs - aliases/extensions for request operations
    public class CreateServiceTicketRequest : ServiceTicketCreateDto
    {
    }

    public class AssignTechnicianRequest : ServiceTicketAssignDto
    {
    }

    public class RecordDiagnosisRequest : ServiceTicketDiagnosisDto
    {
    }

    public class ChooseBranchRequest : ServiceTicketBranchDto
    {
    }

    public class CreateQuotationRequest : QuotationCreateDto
    {
    }

    public class AcceptQuotationRequest : QuotationAcceptDto
    {
    }

    public class RejectQuotationRequest : QuotationRejectDto
    {
    }

    public class CreateRmaShipmentRequest : RmaShipmentCreateDto
    {
    }

    public class UpdateRmaResolutionRequest : RmaResolutionUpdateDto
    {
    }

    public class Perform1For1SwapRequest : Perform1For1SwapDto
    {
    }

    public class CompleteRepairRequest : ServiceTicketCompleteDto
    {
    }

    public class IssueServiceInvoiceRequest : ServiceInvoiceCreateDto
    {
    }

    public class CancelTicketRequest : ServiceTicketCancelDto
    {
    }

    // Response/Detail DTO aliases
    public class QuotationDto : QuotationDetailDto
    {
    }

    public class RmaShipmentDto : RmaShipmentDetailDto
    {
    }

    public class SerialRepairLogDto : SerialRepairHistoryDto
    {
    }
}
