using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PBL3.Core.Entities;
using PBL3.Core.Interfaces;
using PBL3.Infrastructure.Data;
using PBL3.Shared.DTOs.ServiceTickets;
using PBL3.Shared.Enums;

namespace PBL3.Application.ServiceTickets
{
    public class ServiceTicketService : IServiceTicketService
    {
        private readonly IServiceTicketRepository _ticketRepository;
        private readonly IQuotationRepository _quotationRepository;
        private readonly IRmaShipmentRepository _rmaRepository;
        private readonly IServiceInvoiceRepository _invoiceRepository;
        private readonly ISerialRepairLogRepository _logRepository;
        private readonly IWarrantyRepository _warrantyRepository;
        private readonly IProductSerialRepository _serialRepository;
        private readonly IOrderRepository _orderRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly HushStoreDbContext _dbContext;
        private readonly IInventorySyncService _inventorySyncService;

        public ServiceTicketService(
            IServiceTicketRepository ticketRepository,
            IQuotationRepository quotationRepository,
            IRmaShipmentRepository rmaRepository,
            IServiceInvoiceRepository invoiceRepository,
            ISerialRepairLogRepository logRepository,
            IWarrantyRepository warrantyRepository,
            IProductSerialRepository serialRepository,
            IOrderRepository orderRepository,
            IUnitOfWork unitOfWork,
            HushStoreDbContext dbContext,
            IInventorySyncService inventorySyncService)
        {
            _ticketRepository = ticketRepository;
            _quotationRepository = quotationRepository;
            _rmaRepository = rmaRepository;
            _invoiceRepository = invoiceRepository;
            _logRepository = logRepository;
            _warrantyRepository = warrantyRepository;
            _serialRepository = serialRepository;
            _orderRepository = orderRepository;
            _unitOfWork = unitOfWork;
            _dbContext = dbContext;
            _inventorySyncService = inventorySyncService;
        }

        /// <summary>
        /// NGHIỆP VỤ: Đánh giá điều kiện bảo hành của thiết bị dựa trên mã Serial Number trước khi làm thủ tục tiếp nhận.
        /// Xác thực trạng thái thiết bị đã bán (Sold), tìm hóa đơn gốc, kiểm tra xem có phiếu sửa chữa nào đang mở không,
        /// và xác định các nhánh định tuyến khả thi (ví dụ: Bảo hành hãng RMA, Sửa chữa nội bộ, Đổi mới 1-1, hoặc Sửa chữa tính phí).
        /// </summary>
        public async Task<ServiceTicketIntakeEvaluationDto> GetWarrantyEvaluationAsync(string serialNumber)
        {
            // NGHIỆP VỤ: Tra cứu thông tin chi tiết của mã Serial Number trong cơ sở dữ liệu
            var serial = await _serialRepository.GetBySerialNumberAsync(serialNumber);

            if (serial == null)
                return new ServiceTicketIntakeEvaluationDto
                {
                    BlockingReason = "Không tìm thấy Serial trong hệ thống."
                };

            // RÀNG BUỘC: Thiết bị được yêu cầu bảo hành/sửa chữa bắt buộc phải có trạng thái vật lý là đã bán (Sold)
            if (serial.Status != (byte)SerialStatus.Sold)
                return new ServiceTicketIntakeEvaluationDto
                {
                    BlockingReason = "Sản phẩm này chưa được bán hoặc không ở trạng thái hợp lệ."
                };

            // RÀNG BUỘC: Phải xác định được liên kết đơn hàng gốc để đối chiếu ngày xuất bán thực tế
            if (!serial.OrderId.HasValue)
                return new ServiceTicketIntakeEvaluationDto
                {
                    BlockingReason = "Không thể tìm thấy thông tin đơn hàng gốc của sản phẩm này."
                };

            var order = await _orderRepository.GetByIdAsync(serial.OrderId.Value);
            if (order == null)
                return new ServiceTicketIntakeEvaluationDto
                {
                    BlockingReason = "Đơn hàng gốc không tồn tại."
                };

            // CHỐT CHẶN AN TOÀN: Ngăn chặn tiếp nhận trùng. Mỗi serial tại một thời điểm chỉ được phép có tối đa 1 phiếu sửa chữa chưa đóng.
            if (await _ticketRepository.HasOpenTicketForSerialAsync(serial.Id))
                return new ServiceTicketIntakeEvaluationDto
                {
                    BlockingReason = "Sản phẩm này đã có phiếu sửa chữa chưa đóng."
                };

            var variant = serial.Variant;
            // Thực hiện tính toán và đánh giá thời hạn bảo hành thực tế dựa trên ngày bán và số tháng bảo hành cấu hình
            var warranty = await WarrantyEvaluator.EvaluateAsync(serial, variant, _warrantyRepository);

            var allowedBranches = new List<string>();
            // NGHIỆP VỤ PHÂN NHÁNH ĐỊNH TUYẾN:
            // - Nếu thiết bị CÒN bảo hành: Được phép chọn Sửa chữa nội bộ (InternalRepair), Bảo hành gửi hãng (Rma), hoặc Đổi trả thiết bị mới (Swap)
            // - Nếu thiết bị HẾT bảo hành: Bắt buộc đi theo luồng Sửa chữa tính phí dịch vụ (PaidRepair)
            if (warranty.IsInWarranty)
            {
                allowedBranches.Add("InternalRepair");
                allowedBranches.Add("Rma");
                allowedBranches.Add("Swap");
            }
            else
            {
                allowedBranches.Add("PaidRepair");
            }

            return new ServiceTicketIntakeEvaluationDto
            {
                SerialNumber = serial.SerialNumber,
                ProductName = variant.Product.Name,
                VariantName = variant.VariantName,
                IsInWarranty = warranty.IsInWarranty,
                WarrantyExpiresOn = warranty.ExpiresOn,
                WarrantySource = GetWarrantySourceLabel(warranty.Source),
                CustomerId = order.UserId,
                CustomerName = order.User?.Profile?.FullName ?? order.ShipName,
                CustomerEmail = order.User?.Email,
                BlockingReason = null,
                AllowedBranches = allowedBranches
            };
        }

        /// <summary>
        /// NGHIỆP VỤ: Tiếp nhận thiết bị lỗi từ khách hàng và khởi tạo Phiếu sửa chữa (Service Ticket).
        /// Thực hiện chốt snapshot tình trạng ngoại quan lúc nhận máy (móp méo, trầy xước, thiếu phụ kiện, cháy nổ)
        /// làm cơ sở pháp lý chống tranh chấp, sinh mã phiếu chuẩn ST-yyyyMMdd-NNN, và lưu lịch sử trạng thái đầu tiên.
        /// </summary>
        public async Task<ServiceTicketDetailDto?> CreateTicketFromSerialScanAsync(ServiceTicketIntakeRequestDto request, Guid userId)
        {
            var serial = await _serialRepository.GetBySerialNumberAsync(request.SerialNumber);
            if (serial == null)
                throw new InvalidOperationException("Không tìm thấy Serial trong hệ thống.");

            if (serial.Status != (byte)SerialStatus.Sold)
                throw new InvalidOperationException("Sản phẩm không ở trạng thái Sold.");

            if (await _ticketRepository.HasOpenTicketForSerialAsync(serial.Id))
                throw new InvalidOperationException("Sản phẩm này đã có phiếu sửa chữa chưa đóng.");

            var variant = serial.Variant;
            var order = await _orderRepository.GetByIdAsync(serial.OrderId!.Value);
            if (order == null)
                throw new InvalidOperationException("Không tìm thấy đơn hàng gốc.");

            // Đánh giá bảo hành thời gian thực để chốt snapshot ngay lúc tiếp nhận
            var warranty = await WarrantyEvaluator.EvaluateAsync(serial, variant, _warrantyRepository);

            // Sinh mã phiếu dịch vụ tăng dần tự động ST-yyyyMMdd-NNN theo ngày
            var now = DateTime.UtcNow;
            var datePrefix = "ST-" + now.ToString("yyyyMMdd");
            var lastCode = await _ticketRepository.GetLastTicketCodeByDateAsync(datePrefix);
            int nextIndex = 1;
            if (!string.IsNullOrEmpty(lastCode))
            {
                var suffix = lastCode.Substring(lastCode.LastIndexOf('-') + 1);
                if (int.TryParse(suffix, out int lastIdx))
                    nextIndex = lastIdx + 1;
            }
            var ticketCode = $"{datePrefix}-{nextIndex:D3}";

            // Thực thi Transaction để đảm bảo tính toàn vẹn khi ghi nhận phiếu và lịch sử trạng thái ban đầu
            await _unitOfWork.BeginTransactionAsync();
            try
            {
                var ticket = new ServiceTicket
                {
                    TicketCode = ticketCode,
                    SerialId = serial.Id,
                    OriginalOrderId = order.Id,
                    CustomerId = order.UserId,
                    IntakeDate = now,
                    IntakeEmployeeId = userId,
                    // NGHIỆP VỤ: Ghi vết Snapshot Ngoại quan lúc nhận máy (Phòng chống tranh chấp pháp lý về móp méo, trầy xước, thiếu linh kiện sau sửa)
                    HasScratches = request.HasScratches,
                    HasDents = request.HasDents,
                    HasBurnMarks = request.HasBurnMarks,
                    HasMissingAccessories = request.HasMissingAccessories,
                    CosmeticNotes = request.CosmeticNotes,
                    CustomerReportedIssue = request.CustomerReportedIssue,
                    WalkInCustomerName = request.WalkInCustomerName,
                    WalkInCustomerPhone = request.WalkInCustomerPhone,
                    // NGHIỆP VỤ CHỐT CHẶN: Lưu cứng thông tin bảo hành ngay tại thời điểm tiếp nhận (WasInWarrantyAtIntake)
                    // Làm căn cứ xuyên suốt cho việc phê duyệt các nhánh bảo hành miễn phí hoặc sửa tính phí về sau
                    WasInWarrantyAtIntake = warranty.IsInWarranty,
                    WarrantyEndDateAtIntake = warranty.ExpiresOn,
                    WarrantyEvalSource = warranty.Source,
                    Status = (byte)0, // Trạng thái mặc định ban đầu: Received (Đã tiếp nhận)
                    ResolutionType = (byte)0, // Trạng thái giải quyết mặc định: Pending (Chờ xác định nhánh)
                    CreatedDate = now,
                    ModifiedDate = now
                };

                await _ticketRepository.AddAsync(ticket);
                await _unitOfWork.SaveChangesAsync();

                // KHỞI TẠO LỊCH SỬ TRẠNG THÁI: Lưu vết lịch sử chuyển dịch trạng thái đầu tiên của phiếu dịch vụ
                await _ticketRepository.AddStatusHistoryAsync(new ServiceTicketStatusHistory
                {
                    TicketId = ticket.Id,
                    FromStatus = (byte)0, // Từ Received
                    ToStatus = (byte)0, // Đến Received
                    ChangedByEmployeeId = userId,
                    ChangedAt = now,
                    Note = "Tiếp nhận sản phẩm"
                });

                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitAsync();

                return await GetTicketByIdAsync(ticket.Id, userId, false);
            }
            catch
            {
                await _unitOfWork.RollbackAsync();
                throw;
            }
        }

        public async Task<bool> AssignTechnicianAsync(int ticketId, Guid employeeId, Guid userId)
        {
            var ticket = await _ticketRepository.GetByIdWithTrackingAsync(ticketId);
            if (ticket == null)
                throw new InvalidOperationException("Phiếu không tồn tại.");

            ValidateTransition(ticket.Status, (byte)1);

            ticket.AssignedEmployeeId = employeeId;
            ticket.Status = (byte)1;
            ticket.ModifiedDate = DateTime.UtcNow;

            await _ticketRepository.AddStatusHistoryAsync(new ServiceTicketStatusHistory
            {
                TicketId = ticketId,
                FromStatus = (byte)0,
                ToStatus = (byte)1,
                ChangedByEmployeeId = userId,
                ChangedAt = DateTime.UtcNow,
                Note = "Giao phó cho kỹ thuật viên"
            });

            await _ticketRepository.SaveChangesAsync();
            return true;
        }

        public async Task<bool> RecordDiagnosisAsync(int ticketId, ServiceTicketDiagnosisDto request, Guid userId, bool isAdmin = false)
        {
            var ticket = await _ticketRepository.GetByIdWithTrackingAsync(ticketId);
            if (ticket == null)
                throw new InvalidOperationException("Phiếu không tồn tại.");

            CheckAssignment(ticket, userId, isAdmin);

            if (ticket.Status != (byte)1)
                throw new InvalidOperationException("Phiếu phải ở trạng thái Đang chẩn đoán.");

            ticket.DiagnosisFindings = request.DiagnosisFindings;
            ticket.DiagnosedAt = DateTime.UtcNow;
            ticket.DiagnosedByEmployeeId = userId;
            ticket.ModifiedDate = DateTime.UtcNow;

            await _ticketRepository.SaveChangesAsync();
            return true;
        }

        /// <summary>
        /// NGHIỆP VỤ: Định tuyến nhánh xử lý (Sửa bảo hành nội bộ, RMA gửi hãng, Đổi trả 1-1, hoặc Sửa tính phí).
        /// Ràng buộc chặt chẽ: Thiết bị còn hạn bảo hành tại thời điểm tiếp nhận (snapshot WasInWarrantyAtIntake)
        /// không được chọn nhánh sửa tính phí, và ngược lại thiết bị hết bảo hành bắt buộc phải đi nhánh sửa tính phí.
        /// </summary>
        public async Task<bool> ChooseBranchAsync(int ticketId, ServiceTicketBranchDto request, Guid userId, bool isAdmin = false)
        {
            var ticket = await _ticketRepository.GetByIdWithTrackingAsync(ticketId);
            if (ticket == null)
                throw new InvalidOperationException("Phiếu không tồn tại.");

            CheckAssignment(ticket, userId, isAdmin);

            if (ticket.Status != (byte)1)
                throw new InvalidOperationException("Phiếu phải ở trạng thái Đang chẩn đoán.");

            var resolutionType = request.ResolutionType;

            // RÀNG BUỘC PHÂN NHÁNH NGHIỆM NGẶT DỰA TRÊN SNAPSHOT BẢO HÀNH ĐẦU VÀO:
            // - Nếu tại thời điểm tiếp nhận thiết bị đã HẾT bảo hành (WasInWarrantyAtIntake = false):
            //   Bắt buộc đi nhánh Sửa chữa tính phí dịch vụ (ResolutionType = 4 - PaidRepair), cấm đi các luồng bảo hành.
            // - Nếu tại thời điểm tiếp nhận thiết bị CÒN bảo hành (WasInWarrantyAtIntake = true):
            //   Cấm chọn nhánh Sửa chữa tính phí dịch vụ, bắt buộc đi theo các nhánh bảo hành miễn phí.
            if (!ticket.WasInWarrantyAtIntake && resolutionType != (byte)4)
                throw new InvalidOperationException("Sản phẩm đã hết bảo hành, chỉ có thể chọn sửa tính phí.");

            if (ticket.WasInWarrantyAtIntake && resolutionType == (byte)4)
                throw new InvalidOperationException("Sản phẩm còn bảo hành, không thể chọn sửa tính phí.");

            ticket.ResolutionType = resolutionType;
            ticket.ModifiedDate = DateTime.UtcNow;

            await _ticketRepository.SaveChangesAsync();
            return true;
        }

        /// <summary>
        /// NGHIỆP VỤ: Lập Báo giá sửa chữa (Quotation) cho khách hàng đối với các phiếu sửa chữa tính phí.
        /// Tính toán tổng chi phí sửa chữa = Phí nhân công (LaborCost) + Tổng chi phí linh kiện thay thế (PartsTotal).
        /// Tự động vô hiệu hóa các báo giá nháp cũ trước đó và cập nhật trạng thái phiếu sang Chờ duyệt báo giá (SentQuotation).
        /// </summary>
        public async Task<QuotationDetailDto?> CreateQuotationAsync(int ticketId, QuotationCreateDto request, Guid userId, bool isAdmin = false)
        {
            var ticket = await _ticketRepository.GetByIdWithTrackingAsync(ticketId);
            if (ticket == null)
                throw new InvalidOperationException("Phiếu không tồn tại.");

            CheckAssignment(ticket, userId, isAdmin);

            // RÀNG BUỘC QUY TRÌNH: Báo giá chỉ được lập khi phiếu đang ở trạng thái chẩn đoán lỗi (Diagnosing - 1)
            if (ticket.Status != (byte)1)
                throw new InvalidOperationException("Phiếu phải ở trạng thái Đang chẩn đoán.");

            ValidateTransition(ticket.Status, (byte)2);

            // RÀNG BUỘC NGHIỆP VỤ: Chỉ các phiếu được định tuyến theo nhánh Sửa chữa tính phí (PaidRepair - 4) mới cần báo giá
            if (ticket.ResolutionType != (byte)4)
                throw new InvalidOperationException("Chỉ phiếu sửa tính phí mới có báo giá.");

            // Sử dụng Transaction để bảo vệ quy trình lưu trữ báo giá & chi tiết báo giá đồng thời cập nhật phiếu dịch vụ
            await _unitOfWork.BeginTransactionAsync();
            try
            {
                // NGHIỆP VỤ HỦY BÁO GIÁ CŨ: Lấy tất cả báo giá nháp cũ của phiếu này đang ở trạng thái "Chờ duyệt" (0)
                // và đánh dấu chúng thành "Bị thay thế" (3) để bảo đảm chỉ tồn tại duy nhất một bản báo giá có hiệu lực.
                var oldQuotations = await _quotationRepository.GetByTicketIdAsync(ticketId);
                foreach (var q in oldQuotations.Where(q => q.Status == (byte)0))
                {
                    q.Status = (byte)3;
                }

                // CÔNG THỨC TÍNH CHI PHÍ: Tổng tiền báo giá = Chi phí nhân công (LaborCost) + Tổng tiền linh kiện (PartsTotal)
                var partsTotal = request.Items.Sum(i => i.Quantity * i.UnitPrice);
                var grandTotal = request.LaborCost + partsTotal;

                var quotation = new Quotation
                {
                    TicketId = ticketId,
                    IssuedDate = DateTime.UtcNow,
                    IssuedByEmployeeId = userId,
                    LaborCost = request.LaborCost,
                    PartsTotal = partsTotal,
                    GrandTotal = grandTotal,
                    Status = (byte)0, // Mặc định báo giá mới tạo ở trạng thái: Chờ duyệt (Pending)
                };

                await _quotationRepository.AddAsync(quotation);
                await _unitOfWork.SaveChangesAsync();

                // Ghi nhận chi tiết từng dòng linh kiện/dịch vụ phụ tùng trong báo giá
                foreach (var item in request.Items)
                {
                    await _dbContext.QuotationItems.AddAsync(new QuotationItem
                    {
                        QuotationId = quotation.Id,
                        VariantId = item.VariantId,
                        Description = item.Description,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice,
                        LineTotal = item.Quantity * item.UnitPrice
                    });
                }

                // Cập nhật trạng thái phiếu dịch vụ sang: Chờ duyệt báo giá (SentQuotation - 2)
                ticket.Status = (byte)2;
                await _ticketRepository.AddStatusHistoryAsync(new ServiceTicketStatusHistory
                {
                    TicketId = ticketId,
                    FromStatus = (byte)1,
                    ToStatus = (byte)2,
                    ChangedByEmployeeId = userId,
                    ChangedAt = DateTime.UtcNow,
                    Note = "Gửi báo giá cho khách"
                });

                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitAsync();

                var reloadedQuotation = await _quotationRepository.GetByIdWithItemsAsync(quotation.Id);
                return MapQuotationToDto(reloadedQuotation);
            }
            catch
            {
                await _unitOfWork.RollbackAsync();
                throw;
            }
        }

        public async Task<bool> AcceptQuotationAsync(int ticketId, int quotationId, QuotationAcceptDto request, Guid userId, bool isAdmin = false)
        {
            var ticket = await _ticketRepository.GetByIdWithTrackingAsync(ticketId);
            if (ticket == null)
                throw new InvalidOperationException("Phiếu không tồn tại.");

            CheckAssignmentOrCustomer(ticket, userId, isAdmin);

            if (ticket.Status != (byte)2)
                throw new InvalidOperationException("Phiếu phải ở trạng thái Chờ duyệt báo giá.");

            var quotation = await _quotationRepository.GetByIdWithTrackingAsync(quotationId);
            if (quotation == null || quotation.TicketId != ticketId)
                throw new InvalidOperationException("Báo giá không tồn tại.");

            // CHỐT CHẶN BẢO MẬT: Báo giá được chọn bắt buộc phải có trạng thái là Chờ duyệt (0 - Pending)
            // Ngăn chặn duyệt trùng, duyệt đè hoặc thao tác lại trên báo giá đã được xử lý từ trước.
            if (quotation.Status != (byte)0)
                throw new InvalidOperationException("Chỉ được duyệt báo giá chưa được xử lý.");

            var nextStatus = request.NextStatus;
            if (nextStatus != (byte)4 && nextStatus != (byte)5)
                throw new InvalidOperationException("Trạng thái tiếp theo không hợp lệ.");

            ValidateTransition(ticket.Status, nextStatus);

            await _unitOfWork.BeginTransactionAsync();
            try
            {
                // Cập nhật trạng thái báo giá sang: Đã duyệt (1 - Approved)
                quotation.Status = (byte)1;
                quotation.CustomerDecidedAt = DateTime.UtcNow;

                // Chuyển dịch trạng thái phiếu dịch vụ sang Đang sửa (5) hoặc Chờ phụ tùng (4) tùy thuộc quyết định
                ticket.Status = nextStatus;
                ticket.ModifiedDate = DateTime.UtcNow;

                await _ticketRepository.AddStatusHistoryAsync(new ServiceTicketStatusHistory
                {
                    TicketId = ticketId,
                    FromStatus = (byte)2,
                    ToStatus = nextStatus,
                    ChangedByEmployeeId = userId,
                    ChangedAt = DateTime.UtcNow,
                    Note = "Khách chấp nhận báo giá"
                });

                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitAsync();
                return true;
            }
            catch
            {
                await _unitOfWork.RollbackAsync();
                throw;
            }
        }

        public async Task<bool> RejectQuotationAsync(int ticketId, int quotationId, QuotationRejectDto request, Guid userId, bool isAdmin = false)
        {
            var ticket = await _ticketRepository.GetByIdWithTrackingAsync(ticketId);
            if (ticket == null)
                throw new InvalidOperationException("Phiếu không tồn tại.");

            CheckAssignmentOrCustomer(ticket, userId, isAdmin);

            if (ticket.Status != (byte)2)
                throw new InvalidOperationException("Phiếu phải ở trạng thái Chờ duyệt báo giá.");

            var quotation = await _quotationRepository.GetByIdWithTrackingAsync(quotationId);
            if (quotation == null || quotation.TicketId != ticketId)
                throw new InvalidOperationException("Báo giá không tồn tại.");

            ValidateTransition(ticket.Status, (byte)3);

            await _unitOfWork.BeginTransactionAsync();
            try
            {
                quotation.Status = (byte)2;
                quotation.CustomerDecisionNote = request.Reason;
                quotation.CustomerDecidedAt = DateTime.UtcNow;

                ticket.Status = (byte)3;
                ticket.ModifiedDate = DateTime.UtcNow;

                await _ticketRepository.AddStatusHistoryAsync(new ServiceTicketStatusHistory
                {
                    TicketId = ticketId,
                    FromStatus = (byte)2,
                    ToStatus = (byte)3,
                    ChangedByEmployeeId = userId,
                    ChangedAt = DateTime.UtcNow,
                    Note = "Khách từ chối báo giá"
                });

                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitAsync();
                return true;
            }
            catch
            {
                await _unitOfWork.RollbackAsync();
                throw;
            }
        }

        public async Task<RmaShipmentDetailDto?> CreateRmaShipmentAsync(int ticketId, RmaShipmentCreateDto request, Guid userId, bool isAdmin = false)
        {
            var ticket = await _ticketRepository.GetByIdWithTrackingAsync(ticketId);
            if (ticket == null)
                throw new InvalidOperationException("Phiếu không tồn tại.");

            CheckAssignment(ticket, userId, isAdmin);

            if (ticket.ResolutionType != (byte)2)
                throw new InvalidOperationException("Phiếu phải chọn nhánh RMA.");

            if (!ticket.WasInWarrantyAtIntake)
                throw new InvalidOperationException("Sản phẩm đã hết bảo hành, không thể gửi RMA.");

            // Issue #8: Check duplicate RMA
            var existingRma = await _rmaRepository.GetByTicketIdAsync(ticketId);
            if (existingRma != null)
                throw new InvalidOperationException("Phiếu này đã được gửi hãng rồi.");

            ValidateTransition(ticket.Status, (byte)6);

            // Issue #13: Use UoW transaction pattern instead of individual SaveChanges
            await _unitOfWork.BeginTransactionAsync();
            try
            {
                var rma = new RmaShipment
                {
                    TicketId = ticketId,
                    CarrierName = request.CarrierName,
                    TrackingCode = request.TrackingCode,
                    ShippedDate = DateTime.UtcNow,
                    ShippedByEmployeeId = userId,
                    ManufacturerResolution = (byte)0
                };

                await _rmaRepository.AddAsync(rma);

                ticket.Status = (byte)6;
                ticket.ModifiedDate = DateTime.UtcNow;

                await _ticketRepository.AddStatusHistoryAsync(new ServiceTicketStatusHistory
                {
                    TicketId = ticketId,
                    FromStatus = (byte)1,
                    ToStatus = (byte)6,
                    ChangedByEmployeeId = userId,
                    ChangedAt = DateTime.UtcNow,
                    Note = $"Gửi hãng qua {request.CarrierName}"
                });

                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitAsync();

                var reloadedRma = await _rmaRepository.GetByTicketIdAsync(ticketId);
                return MapRmaShipmentToDto(reloadedRma!);
            }
            catch
            {
                await _unitOfWork.RollbackAsync();
                throw;
            }
        }

        /// <summary>
        /// NGHIỆP VỤ: Ghi nhận kết quả xử lý bảo hành từ hãng (RMA Resolution) trả về cửa hàng.
        /// Tùy theo quyết định của hãng:
        /// - Đã sửa xong: Nhận thiết bị cũ về kho và sẵn sàng trả khách.
        /// - Từ chối bảo hành: Chuyển lại trạng thái chẩn đoán lỗi.
        /// - Đã đổi mới thiết bị (Manufacturer Replaced): Tiến hành swap serial hỏng thành serial mới hãng cấp,
        ///   cập nhật trạng thái serial cũ thành hư hỏng (Defective) để thu hồi, serial mới thành đã bán (Sold) liên kết hóa đơn gốc,
        ///   và tạo bản ghi bảo hành mới kế thừa hạn bảo hành gốc.
        /// </summary>
        public async Task<bool> RecordRmaResolutionAsync(int ticketId, RmaResolutionUpdateDto request, Guid userId, bool isAdmin = false)
        {
            var rma = await _rmaRepository.GetByTicketIdAsync(ticketId);
            if (rma == null)
                throw new InvalidOperationException("Không có phiếu RMA cho ticket này.");

            var ticket = await _ticketRepository.GetByIdWithTrackingAsync(ticketId);
            if (ticket == null)
                throw new InvalidOperationException("Phiếu không tồn tại.");

            CheckAssignment(ticket, userId, isAdmin);

            // Xác định trạng thái đích tiếp theo của phiếu dựa trên phản hồi của Hãng sản xuất
            byte toStatus = request.ManufacturerResolution switch
            {
                2 => (byte)8, // Manufacturer Replaced -> Đã đổi 1-1
                3 => (byte)1, // Manufacturer Rejected -> Quay lại trạng thái Đang chẩn đoán
                _ => (byte)7  // Manufacturer Repaired -> Đã nhận lại từ hãng
            };
            ValidateTransition(ticket.Status, toStatus);

            await _unitOfWork.BeginTransactionAsync();
            try
            {
                var now = DateTime.UtcNow;
                byte previousStatus = ticket.Status;

                rma.ManufacturerResolution = request.ManufacturerResolution;
                rma.ManufacturerNotes = request.ManufacturerNotes;
                rma.ReceivedBackDate = now;
                rma.ReceivedByEmployeeId = userId;

                // NGHIỆP VỤ PHỨC TẠP: Xử lý đổi mới thiết bị khi Hãng đồng ý bảo hành đổi 1-1 (ManufacturerResolution = 2)
                if (request.ManufacturerResolution == (byte)2)
                {
                    if (!request.ReplacementSerialId.HasValue)
                        throw new InvalidOperationException("Phải cung cấp Serial thay thế khi hãng đã thay thế.");

                    var newSerialId = request.ReplacementSerialId.Value;
                    var oldSerial = ticket.Serial;
                    var newSerial = await _serialRepository.GetByIdWithTrackingAsync(newSerialId);

                    // Kiểm định độ khả dụng của Serial thay thế mới nhận từ hãng
                    if (newSerial == null || newSerial.Status != (byte)0)
                        throw new InvalidOperationException("Serial thay thế không sẵn trong kho hoặc đã được giữ chỗ.");

                    if (newSerial.VariantId != oldSerial.VariantId)
                        throw new InvalidOperationException("Serial thay thế phải cùng biến thể với serial hỏng.");

                    // Truy vấn liên kết xuất kho trong lịch sử để trỏ lại sang Serial mới (Invoice Preservation)
                    var oldOrderSerial = await _dbContext.OrderSerials
                        .FirstOrDefaultAsync(os => os.SerialId == oldSerial.Id);
                    if (oldOrderSerial == null)
                        throw new InvalidOperationException("Không tìm thấy bản ghi xuất kho gốc của serial này.");

                    // Xác định thời hạn bảo hành gốc để phục vụ kế thừa
                    var oldWarranties = await _warrantyRepository.GetActiveBySerialIdAsync(oldSerial.Id);
                    var oldEndDate = oldWarranties.FirstOrDefault()?.EndDate
                        ?? oldSerial.SoldDate?.AddMonths(oldSerial.Variant.WarrantyMonth)
                        ?? now;

                    // 1. Thu hồi Serial cũ bằng cách đổi trạng thái sang Hỏng (Defective - 4)
                    oldSerial.Status = (byte)4;

                    // 2. Kích hoạt Serial mới bằng cách đổi trạng thái sang Đã bán (Sold - 2) và liên kết đơn hàng cũ
                    newSerial.Status = (byte)2;
                    newSerial.SoldDate = now;
                    newSerial.OrderId = oldSerial.OrderId;

                    // 3. Bảo toàn hóa đơn: Sửa lại tham chiếu Serial cũ thành Serial mới trong bảng OrderSerials
                    oldOrderSerial.SerialId = newSerialId;

                    // 4. Hủy hiệu lực bảo hành cũ (Status = 2 - Cancelled/Deactivated)
                    if (oldWarranties.Count > 0)
                    {
                        oldWarranties[0].Status = (byte)2;
                    }

                    // 5. Khởi tạo bảo hành mới cho thiết bị thay thế nhưng kế thừa ngày hết hạn gốc (oldEndDate)
                    var newWarranty = new Warranty
                    {
                        SerialId = newSerialId,
                        CustomerId = oldWarranties.FirstOrDefault()?.CustomerId,
                        OrderId = oldSerial.OrderId!.Value,
                        StartDate = now,
                        EndDate = oldEndDate,
                        Status = (byte)0 // Active (Hoạt động)
                    };
                    await _warrantyRepository.AddAsync(newWarranty);

                    ticket.Status = (byte)8;
                    ticket.ReplacementSerialId = newSerialId;

                    // Ghi chép lịch sử sửa chữa thiết bị (Serial Repair Log) chi tiết
                    await _logRepository.AddAsync(new SerialRepairLog
                    {
                        SerialId = oldSerial.Id,
                        TicketId = ticketId,
                        ResolutionType = (byte)2,
                        LoggedAt = now,
                        LoggedByEmployeeId = userId,
                        Summary = $"Hãng thay thế sang serial {newSerial.SerialNumber}. Bảo hành kế thừa đến {oldEndDate:dd/MM/yyyy}.",
                        ReplacedBySerialId = newSerialId
                    });
                }
                else
                {
                    ticket.Status = request.ManufacturerResolution == (byte)3 ? (byte)1 : (byte)7;
                }

                await _ticketRepository.AddStatusHistoryAsync(new ServiceTicketStatusHistory
                {
                    TicketId = ticketId,
                    FromStatus = previousStatus,
                    ToStatus = ticket.Status,
                    ChangedByEmployeeId = userId,
                    ChangedAt = now,
                    Note = request.ManufacturerResolution switch
                    {
                        2 => "Hãng thay thế, chuyển sang Đã đổi 1-1",
                        3 => "Hãng từ chối, quay lại chẩn đoán",
                        _ => "Nhận lại từ hãng"
                    }
                });

                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitAsync();

                // NGHIỆP VỤ KHO: Tự động kích hoạt đồng bộ hóa số lượng tồn kho của biến thể do có sự biến đổi Serial vật lý thực tế
                if (request.ManufacturerResolution == (byte)2)
                {
                    await _inventorySyncService.SyncStockBatchAsync(new[] { ticket.Serial.VariantId });
                }

                return true;
            }
            catch
            {
                await _unitOfWork.RollbackAsync();
                throw;
            }
        }

        /// <summary>
        /// NGHIỆP VỤ: Thực hiện Đổi trả thiết bị mới 1-1 trực tiếp từ kho của cửa hàng cho khách (còn bảo hành).
        /// Cập nhật thiết bị cũ thành hàng lỗi thu hồi (Status = 4), thiết bị mới xuất kho thay thế liên kết đơn hàng cũ (Status = 2),
        /// thay đổi mã serial gốc trong bảng OrderSerials thành serial mới, vô hiệu hóa bảo hành cũ và 
        /// tự động sinh bản ghi bảo hành mới cho thiết bị thay thế kế thừa ngày hết hạn gốc.
        /// </summary>
        public async Task<bool> Perform1For1SwapAsync(int ticketId, int newSerialId, Guid userId, bool isAdmin = false)
        {
            var ticket = await _ticketRepository.GetByIdWithTrackingAsync(ticketId);
            if (ticket == null)
                throw new InvalidOperationException("Phiếu không tồn tại.");

            CheckAssignment(ticket, userId, isAdmin);

            if (ticket.Status != (byte)1 && ticket.Status != (byte)7)
                throw new InvalidOperationException("Trạng thái phiếu không cho phép đổi 1-1.");

            if (ticket.ResolutionType != (byte)3 && ticket.ResolutionType != (byte)2)
                throw new InvalidOperationException("Loại giải pháp không phải đổi 1-1.");

            // NGHIỆP VỤ BẢO VỆ CHỐT CHẶN: Đánh giá bảo hành thời gian thực (Live Warranty Re-evaluation) tại thời điểm đổi máy.
            // Điều này cực kỳ quan trọng để phòng chống rủi ro thiết bị thực tế đã trôi qua thời hạn bảo hành tối đa trong quãng thời gian dài chẩn đoán hoặc chờ linh kiện.
            var liveWarranty = await WarrantyEvaluator.EvaluateAsync(
                ticket.Serial,
                ticket.Serial.Variant,
                _warrantyRepository);

            if (!liveWarranty.IsInWarranty)
                throw new InvalidOperationException("Bảo hành đã hết hạn, không thể đổi 1-1.");

            if (ticket.ReplacementSerialId.HasValue)
                throw new InvalidOperationException("Phiếu này đã được đổi 1-1 trước đó.");

            ValidateTransition(ticket.Status, (byte)8);

            var oldSerial = await _serialRepository.GetByIdWithTrackingAsync(ticket.SerialId);
            if (oldSerial == null)
                throw new InvalidOperationException("Serial cũ không tồn tại.");

            var newSerial = await _serialRepository.GetByIdWithTrackingAsync(newSerialId);
            if (newSerial == null || newSerial.Status != (byte)0)
                throw new InvalidOperationException("Serial thay thế không sẵn trong kho hoặc đã được giữ chỗ.");

            if (newSerial.VariantId != oldSerial.VariantId)
                throw new InvalidOperationException("Serial thay thế phải cùng biến thể với serial hỏng.");

            // Hóa đơn lịch sử: Lấy dòng ánh xạ hóa đơn vật lý của máy cũ
            var oldOrderSerial = await _dbContext.OrderSerials
                .FirstOrDefaultAsync(os => os.SerialId == oldSerial.Id);
            if (oldOrderSerial == null)
                throw new InvalidOperationException("Không tìm thấy bản ghi xuất kho gốc của serial này.");

            // Lấy thời hạn kết thúc bảo hành hiện hữu để chuyển tiếp bảo hành kế thừa
            var oldWarranties = await _warrantyRepository.GetActiveBySerialIdAsync(oldSerial.Id);
            var oldEndDate = oldWarranties.FirstOrDefault()?.EndDate
                ?? oldSerial.SoldDate?.AddMonths(oldSerial.Variant.WarrantyMonth)
                ?? DateTime.UtcNow;

            await _unitOfWork.BeginTransactionAsync();
            try
            {
                var now = DateTime.UtcNow;
                byte previousStatus = ticket.Status;

                // 1. Phế thải Serial cũ: Đổi trạng thái sang Hỏng (Defective - 4)
                oldSerial.Status = (byte)4;

                // 2. Xuất kho Serial thay thế: Đổi trạng thái sang Đã bán (Sold - 2) và liên kết đơn hàng cũ
                newSerial.Status = (byte)2;
                newSerial.SoldDate = now;
                newSerial.OrderId = oldSerial.OrderId;

                // 3. Hoán đổi liên kết: Ghi nhận Serial mới thay thế Serial cũ trong bảng chi tiết xuất kho OrderSerials
                oldOrderSerial.SerialId = newSerialId;

                // 4. Vô hiệu hóa bảo hành cũ
                if (oldWarranties.Count > 0)
                {
                    oldWarranties[0].Status = (byte)2;
                }

                // 5. Tạo bảo hành mới kế thừa thời hạn bảo hành cũ của khách
                var newWarranty = new Warranty
                {
                    SerialId = newSerialId,
                    CustomerId = oldWarranties.FirstOrDefault()?.CustomerId,
                    OrderId = oldSerial.OrderId!.Value,
                    StartDate = now,
                    EndDate = oldEndDate,
                    Status = (byte)0
                };
                await _warrantyRepository.AddAsync(newWarranty);

                ticket.Status = (byte)8;
                ticket.ReplacementSerialId = newSerialId;
                ticket.ResolutionType = (byte)3;
                ticket.ModifiedDate = now;

                await _ticketRepository.AddStatusHistoryAsync(new ServiceTicketStatusHistory
                {
                    TicketId = ticketId,
                    FromStatus = previousStatus,
                    ToStatus = (byte)8,
                    ChangedByEmployeeId = userId,
                    ChangedAt = now,
                    Note = $"Đổi 1-1 sang serial {newSerial.SerialNumber}"
                });

                await _logRepository.AddAsync(new SerialRepairLog
                {
                    SerialId = oldSerial.Id,
                    TicketId = ticketId,
                    ResolutionType = (byte)3,
                    LoggedAt = now,
                    LoggedByEmployeeId = userId,
                    Summary = $"Đổi 1-1 sang serial {newSerial.SerialNumber}. Bảo hành kế thừa đến {oldEndDate:dd/MM/yyyy}.",
                    ReplacedBySerialId = newSerialId
                });

                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitAsync();

                // NGHIỆP VỤ KHO: Tự động kích hoạt đồng bộ hóa số lượng tồn kho khả dụng của biến thể trong RAM
                await _inventorySyncService.SyncStockBatchAsync(new[] { oldSerial.VariantId });

                return true;
            }
            catch
            {
                await _unitOfWork.RollbackAsync();
                throw;
            }
        }

        public async Task<bool> MarkInternalRepairCompletedAsync(int ticketId, ServiceTicketCompleteDto request, Guid userId, bool isAdmin = false)
        {
            var ticket = await _ticketRepository.GetByIdWithTrackingAsync(ticketId);
            if (ticket == null)
                throw new InvalidOperationException("Phiếu không tồn tại.");

            CheckAssignment(ticket, userId, isAdmin);

            if (!new[] { (byte)5, (byte)7, (byte)8 }.Contains(ticket.Status))
                throw new InvalidOperationException("Phiếu phải ở trạng thái sửa chữa hoặc đã nhận từ hãng.");

            ValidateTransition(ticket.Status, (byte)9);

            await _unitOfWork.BeginTransactionAsync();
            try
            {
                // Issue #4: Capture FromStatus BEFORE changing status
                byte previousStatus = ticket.Status;

                ticket.Status = (byte)9;
                ticket.CompletedDate = DateTime.UtcNow;
                ticket.ModifiedDate = DateTime.UtcNow;

                await _ticketRepository.AddStatusHistoryAsync(new ServiceTicketStatusHistory
                {
                    TicketId = ticketId,
                    FromStatus = previousStatus,
                    ToStatus = (byte)9,
                    ChangedByEmployeeId = userId,
                    ChangedAt = DateTime.UtcNow,
                    Note = "Hoàn tất sửa chữa"
                });

                // Issue #12: Only add log if not already logged during swap operation
                if (previousStatus != (byte)8)
                {
                    await _logRepository.AddAsync(new SerialRepairLog
                    {
                        SerialId = ticket.SerialId,
                        TicketId = ticketId,
                        ResolutionType = ticket.ResolutionType,
                        LoggedAt = DateTime.UtcNow,
                        LoggedByEmployeeId = userId,
                        Summary = request.Note ?? "Sửa chữa xong"
                    });
                }

                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitAsync();
                return true;
            }
            catch
            {
                await _unitOfWork.RollbackAsync();
                throw;
            }
        }

        public async Task<bool> MarkWaitingPartsAsync(int ticketId, Guid userId, bool isAdmin = false)
        {
            var ticket = await _ticketRepository.GetByIdWithTrackingAsync(ticketId);
            if (ticket == null)
                throw new InvalidOperationException("Phiếu không tồn tại.");

            CheckAssignment(ticket, userId, isAdmin);

            if (ticket.Status != (byte)5)
                throw new InvalidOperationException("Phiếu phải ở trạng thái Đang sửa.");

            ValidateTransition(ticket.Status, (byte)4);

            ticket.Status = (byte)4;
            ticket.ModifiedDate = DateTime.UtcNow;

            await _ticketRepository.AddStatusHistoryAsync(new ServiceTicketStatusHistory
            {
                TicketId = ticketId,
                FromStatus = (byte)5,
                ToStatus = (byte)4,
                ChangedByEmployeeId = userId,
                ChangedAt = DateTime.UtcNow,
                Note = "Chờ phụ tùng"
            });

            await _ticketRepository.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ResumeRepairAsync(int ticketId, Guid userId, bool isAdmin = false)
        {
            var ticket = await _ticketRepository.GetByIdWithTrackingAsync(ticketId);
            if (ticket == null)
                throw new InvalidOperationException("Phiếu không tồn tại.");

            CheckAssignment(ticket, userId, isAdmin);

            if (ticket.Status != (byte)4)
                throw new InvalidOperationException("Phiếu phải ở trạng thái Chờ phụ tùng.");

            ValidateTransition(ticket.Status, (byte)5);

            ticket.Status = (byte)5;
            ticket.ModifiedDate = DateTime.UtcNow;

            await _ticketRepository.AddStatusHistoryAsync(new ServiceTicketStatusHistory
            {
                TicketId = ticketId,
                FromStatus = (byte)4,
                ToStatus = (byte)5,
                ChangedByEmployeeId = userId,
                ChangedAt = DateTime.UtcNow,
                Note = "Tiếp tục sửa chữa"
            });

            await _ticketRepository.SaveChangesAsync();
            return true;
        }

        public async Task<bool> StartRepairAsync(int ticketId, Guid userId, bool isAdmin = false)
        {
            var ticket = await _ticketRepository.GetByIdWithTrackingAsync(ticketId);
            if (ticket == null)
                throw new InvalidOperationException("Phiếu không tồn tại.");

            CheckAssignment(ticket, userId, isAdmin);

            if (ticket.ResolutionType != (byte)1)
                throw new InvalidOperationException("Chỉ áp dụng cho phiếu sửa chữa bảo hành nội bộ.");

            ValidateTransition(ticket.Status, (byte)5);
            byte prev = ticket.Status;
            ticket.Status = (byte)5;
            ticket.ModifiedDate = DateTime.UtcNow;

            await _ticketRepository.AddStatusHistoryAsync(new ServiceTicketStatusHistory
            {
                TicketId = ticketId,
                FromStatus = prev,
                ToStatus = (byte)5,
                ChangedByEmployeeId = userId,
                ChangedAt = DateTime.UtcNow,
                Note = "Bắt đầu sửa chữa bảo hành"
            });

            await _ticketRepository.SaveChangesAsync();
            return true;
        }

        /// <summary>
        /// NGHIỆP VỤ: Lập hóa đơn thanh toán dịch vụ (Service Invoice) cho phiếu sửa chữa tính phí đã hoàn tất.
        /// Đảm bảo tính toán chính xác số tiền dựa trên báo giá đã được khách hàng duyệt trước đó,
        /// sinh mã hóa đơn SRV-yyyyMMdd-NNN, kết xuất các mặt hàng linh kiện thực tế thay thế và ghi nhận phương thức thanh toán.
        /// </summary>
        public async Task<ServiceInvoiceDetailDto?> IssueServiceInvoiceAsync(int ticketId, ServiceInvoiceCreateDto request, Guid userId, bool isAdmin = false)
        {
            var ticket = await _ticketRepository.GetByIdWithDetailsAsync(ticketId);
            if (ticket == null)
                throw new InvalidOperationException("Phiếu không tồn tại.");

            CheckAssignment(ticket, userId, isAdmin);

            // RÀNG BUỘC PHÁP LÝ: Hóa đơn chỉ được phép xuất khi Phiếu dịch vụ đã hoàn tất sửa chữa (Completed - 9)
            if (ticket.Status != (byte)9)
                throw new InvalidOperationException("Phiếu phải ở trạng thái Hoàn tất.");

            // RÀNG BUỘC NGHIỆP VỤ: Chỉ nhánh Sửa chữa tính phí (PaidRepair - 4) mới sinh hóa đơn tài chính dịch vụ
            if (ticket.ResolutionType != (byte)4)
                throw new InvalidOperationException("Chỉ sửa tính phí mới tạo hóa đơn.");

            // CHỐT CHẶN TRÁNH TRÙNG LẶP: Mỗi phiếu sửa chữa tính phí chỉ được phép có duy nhất 1 hóa đơn dịch vụ
            if (await _invoiceRepository.InvoiceExistsForTicketAsync(ticketId))
                throw new InvalidOperationException("Hóa đơn cho phiếu này đã tồn tại.");

            // Lấy báo giá đã được khách hàng duyệt làm căn cứ áp giá thanh toán
            var quotations = await _quotationRepository.GetByTicketIdAsync(ticketId);
            var acceptedQuote = quotations.FirstOrDefault(q => q.Status == (byte)1);
            if (acceptedQuote == null)
                throw new InvalidOperationException("Không tìm thấy báo giá được duyệt.");

            // Khởi chạy Transaction để bảo đảm việc sinh hóa đơn và sao chép danh mục phụ tùng diễn ra an toàn
            await _unitOfWork.BeginTransactionAsync();
            try
            {
                // Sinh mã hóa đơn dịch vụ tự động SRV-yyyyMMdd-NNN tăng dần theo ngày
                var now = DateTime.UtcNow;
                var datePrefix = "SRV-" + now.ToString("yyyyMMdd");
                var lastCode = await _invoiceRepository.GetLastInvoiceCodeByDateAsync(datePrefix);
                int nextIndex = 1;
                if (!string.IsNullOrEmpty(lastCode))
                {
                    var suffix = lastCode.Substring(lastCode.LastIndexOf('-') + 1);
                    if (int.TryParse(suffix, out int lastIdx))
                        nextIndex = lastIdx + 1;
                }
                var invoiceCode = $"{datePrefix}-{nextIndex:D3}";

                // Tạo mới hóa đơn dịch vụ (ServiceInvoice) với đầy đủ thông số nhân công và phụ tùng kế thừa từ báo giá đã duyệt
                var invoice = new ServiceInvoice
                {
                    InvoiceCode = invoiceCode,
                    TicketId = ticketId,
                    QuotationId = acceptedQuote.Id,
                    IssuedDate = now,
                    IssuedByEmployeeId = userId,
                    LaborCost = acceptedQuote.LaborCost,
                    PartsTotal = acceptedQuote.PartsTotal,
                    GrandTotal = acceptedQuote.GrandTotal,
                    PaymentMethod = request.PaymentMethod,
                    PaymentStatus = (byte)0, // Mặc định hóa đơn mới sinh ở trạng thái: Chưa thanh toán (Unpaid)
                    Note = request.Note
                };

                await _invoiceRepository.AddAsync(invoice);
                await _unitOfWork.SaveChangesAsync();

                // Sao chép chính xác danh sách linh kiện thay thế từ Báo giá sang bảng hóa đơn thực tế (ServiceInvoiceItems)
                var quotationItems = await _dbContext.QuotationItems
                    .Where(qi => qi.QuotationId == acceptedQuote.Id)
                    .ToListAsync();

                foreach (var qItem in quotationItems)
                {
                    await _dbContext.ServiceInvoiceItems.AddAsync(new ServiceInvoiceItem
                    {
                        InvoiceId = invoice.Id,
                        VariantId = qItem.VariantId,
                        Description = qItem.Description,
                        Quantity = qItem.Quantity,
                        UnitPrice = qItem.UnitPrice,
                        LineTotal = qItem.Quantity * qItem.UnitPrice
                    });
                }

                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitAsync();

                var reloadedInvoice = await _invoiceRepository.GetByIdWithDetailsAsync(invoice.Id);
                return MapServiceInvoiceToDto(reloadedInvoice);
            }
            catch
            {
                await _unitOfWork.RollbackAsync();
                throw;
            }
        }

        public async Task<bool> CancelTicketAsync(int ticketId, string reason, Guid userId)
        {
            var ticket = await _ticketRepository.GetByIdWithTrackingAsync(ticketId);
            if (ticket == null)
                throw new InvalidOperationException("Phiếu không tồn tại.");

            if (new[] { (byte)3, (byte)9, (byte)10 }.Contains(ticket.Status))
                throw new InvalidOperationException("Không thể thay đổi trạng thái phiếu đã đóng.");

            ValidateTransition(ticket.Status, (byte)10);

            // Issue #3: Capture FromStatus BEFORE changing status
            byte previousStatus = ticket.Status;

            ticket.Status = (byte)10;
            ticket.CancelReason = reason;
            ticket.CancelledAt = DateTime.UtcNow;
            ticket.ModifiedDate = DateTime.UtcNow;

            await _ticketRepository.AddStatusHistoryAsync(new ServiceTicketStatusHistory
            {
                TicketId = ticketId,
                FromStatus = previousStatus,
                ToStatus = (byte)10,
                ChangedByEmployeeId = userId,
                ChangedAt = DateTime.UtcNow,
                Note = reason
            });

            await _ticketRepository.SaveChangesAsync();
            return true;
        }

        public async Task<ServiceTicketDetailDto?> GetTicketByIdAsync(int id, Guid? currentUserId = null, bool isCustomer = false)
        {
            var ticket = await _ticketRepository.GetByIdWithDetailsAsync(id);
            if (ticket == null)
                return null;

            // Issue #9: Fix IDOR vulnerability - walk-in tickets should not be visible to customers
            if (isCustomer)
            {
                if (!ticket.CustomerId.HasValue || ticket.CustomerId != currentUserId)
                    throw new UnauthorizedAccessException("Phiếu này không thuộc về bạn.");
            }

            return MapToDetailDto(ticket);
        }

        public async Task<(List<ServiceTicketListDto> Items, int TotalCount)> GetPagedTicketsAsync(
            string? keyword, byte? status, byte? resolutionType, Guid? assignedEmployeeId, Guid? customerId,
            DateTime? fromDate, DateTime? toDate, int pageNumber, int pageSize, string? sortBy, bool sortDescending)
        {
            var (items, totalCount) = await _ticketRepository.GetPagedListAsync(
                keyword, status, resolutionType, assignedEmployeeId, customerId,
                fromDate, toDate, pageNumber, pageSize, sortBy, sortDescending);

            var dtos = items.Select(t => new ServiceTicketListDto
            {
                Id = t.Id,
                TicketCode = t.TicketCode,
                SerialNumber = t.Serial.SerialNumber,
                ProductName = t.Serial.Variant.Product.Name,
                IntakeDate = t.IntakeDate,
                Status = t.Status,
                StatusLabel = GetStatusLabel(t.Status)
            }).ToList();

            return (dtos, totalCount);
        }

        public async Task<(List<ServiceTicketListDto> Items, int TotalCount)> GetMyTicketsAsync(
            Guid userId, string? keyword, byte? status, int pageNumber, int pageSize, string? sortBy, bool sortDescending)
        {
            var (items, totalCount) = await _ticketRepository.GetTicketsByOrderUserIdAsync(
                userId, keyword, status, pageNumber, pageSize, sortBy, sortDescending);

            var dtos = items.Select(t => new ServiceTicketListDto
            {
                Id = t.Id,
                TicketCode = t.TicketCode,
                SerialNumber = t.Serial.SerialNumber,
                ProductName = t.Serial.Variant.Product.Name,
                IntakeDate = t.IntakeDate,
                Status = t.Status,
                StatusLabel = GetStatusLabel(t.Status)
            }).ToList();

            return (dtos, totalCount);
        }

        public async Task<List<ServiceTicketStatusHistoryDto>> GetTicketHistoryAsync(int ticketId)
        {
            var ticket = await _ticketRepository.GetByIdWithDetailsAsync(ticketId);
            if (ticket == null)
                return new List<ServiceTicketStatusHistoryDto>();

            return ticket.StatusHistory
                .OrderByDescending(h => h.ChangedAt)
                .Select(h => new ServiceTicketStatusHistoryDto
                {
                    Id = h.Id,
                    FromStatus = h.FromStatus,
                    FromStatusLabel = GetStatusLabel(h.FromStatus),
                    ToStatus = h.ToStatus,
                    ToStatusLabel = GetStatusLabel(h.ToStatus),
                    ChangedAt = h.ChangedAt,
                    Note = h.Note
                })
                .ToList();
        }

        public async Task<List<SerialRepairHistoryDto>> GetSerialRepairHistoryAsync(string serialNumber)
        {
            var serial = await _serialRepository.GetBySerialNumberAsync(serialNumber);
            if (serial == null)
                return new List<SerialRepairHistoryDto>();

            var logs = await _logRepository.GetBySerialIdAsync(serial.Id);
            return logs.Select(l => new SerialRepairHistoryDto
            {
                Id = l.Id,
                SerialId = l.SerialId,
                TicketId = l.TicketId,
                TicketCode = l.Ticket?.TicketCode,
                ResolutionType = l.ResolutionType,
                ResolutionTypeLabel = GetResolutionLabel(l.ResolutionType),
                LoggedAt = l.LoggedAt,
                Summary = l.Summary,
                ReplacedBySerialId = l.ReplacedBySerialId,
                ReplacedBySerialNumber = l.ReplacedBySerial?.SerialNumber
            }).ToList();
        }

        private static void CheckAssignment(ServiceTicket ticket, Guid userId, bool isAdmin)
        {
            if (!isAdmin && ticket.AssignedEmployeeId.HasValue && ticket.AssignedEmployeeId != userId)
                throw new UnauthorizedAccessException("Bạn không phải kỹ thuật viên được giao phó cho phiếu này.");
        }

        private static void CheckAssignmentOrCustomer(ServiceTicket ticket, Guid userId, bool isAdmin)
        {
            if (isAdmin) return;
            if (ticket.CustomerId.HasValue && ticket.CustomerId == userId) return;
            if (ticket.AssignedEmployeeId.HasValue && ticket.AssignedEmployeeId != userId)
                throw new UnauthorizedAccessException("Bạn không phải kỹ thuật viên được giao phó cho phiếu này.");
        }

        private void ValidateTransition(byte currentStatus, byte targetStatus)
        {
            var validTransitions = new[]
            {
                (0, 1), (0, 10),
                (1, 2), (1, 4), (1, 5), (1, 6), (1, 8), (1, 10),
                (2, 4), (2, 5), (2, 3), (2, 10),
                (4, 5), (4, 10),
                (5, 4), (5, 9), (5, 10),
                (6, 1), (6, 7), (6, 10),
                (7, 9), (7, 8), (7, 10),
                (8, 9), (8, 10)
            };

            if (!validTransitions.Contains((currentStatus, targetStatus)))
                throw new InvalidOperationException($"Không thể chuyển trạng thái từ '{GetStatusLabel(currentStatus)}' sang '{GetStatusLabel(targetStatus)}'.");
        }

        private ServiceTicketDetailDto MapToDetailDto(ServiceTicket ticket)
        {
            return new ServiceTicketDetailDto
            {
                Id = ticket.Id,
                TicketCode = ticket.TicketCode,
                SerialNumber = ticket.Serial.SerialNumber,
                ProductName = ticket.Serial.Variant.Product.Name,
                SerialVariantId = ticket.Serial.VariantId,
                IntakeDate = ticket.IntakeDate,
                Status = ticket.Status,
                StatusLabel = GetStatusLabel(ticket.Status),
                ResolutionType = ticket.ResolutionType,
                CustomerName = ticket.Customer?.Profile?.FullName ?? ticket.WalkInCustomerName,
                HasScratches = ticket.HasScratches,
                HasDents = ticket.HasDents,
                HasBurnMarks = ticket.HasBurnMarks,
                HasMissingAccessories = ticket.HasMissingAccessories,
                CosmeticNotes = ticket.CosmeticNotes,
                WasInWarrantyAtIntake = ticket.WasInWarrantyAtIntake,
                WarrantyEndDateAtIntake = ticket.WarrantyEndDateAtIntake,
                CustomerReportedIssue = ticket.CustomerReportedIssue,
                DiagnosisFindings = ticket.DiagnosisFindings,
                DiagnosedAt = ticket.DiagnosedAt,
                AssignedEmployeeId = ticket.AssignedEmployeeId,
                StatusHistory = ticket.StatusHistory?.OrderByDescending(h => h.ChangedAt)
                    .Select(h => new ServiceTicketStatusHistoryDto
                    {
                        Id = h.Id,
                        FromStatus = h.FromStatus,
                        FromStatusLabel = GetStatusLabel(h.FromStatus),
                        ToStatus = h.ToStatus,
                        ToStatusLabel = GetStatusLabel(h.ToStatus),
                        ChangedAt = h.ChangedAt,
                        Note = h.Note
                    }).ToList() ?? new List<ServiceTicketStatusHistoryDto>(),
                Quotations = ticket.Quotations?
                    .OrderByDescending(q => q.IssuedDate)
                    .Select(q => MapQuotationToDto(q))
                    .ToList() ?? new(),
                RmaShipment = ticket.RmaShipment != null ? MapRmaShipmentToDto(ticket.RmaShipment) : null,
                Invoice = ticket.Invoice != null ? MapServiceInvoiceToDto(ticket.Invoice) : null
            };
        }

        private string GetStatusLabel(byte status) => status switch
        {
            0 => "Đã tiếp nhận",
            1 => "Đang chẩn đoán",
            2 => "Đã gửi báo giá",
            3 => "Khách từ chối báo giá",
            4 => "Chờ phụ tùng",
            5 => "Đang sửa chữa",
            6 => "Đã gửi hãng (RMA)",
            7 => "Đã nhận lại từ hãng",
            8 => "Đã đổi 1-1",
            9 => "Hoàn tất",
            10 => "Đã hủy",
            _ => "Không xác định"
        };

        private string GetResolutionLabel(byte type) => type switch
        {
            1 => "Sửa nội bộ",
            2 => "RMA",
            3 => "Đổi 1-1",
            4 => "Sửa tính phí",
            _ => "Chờ xác định"
        };

        private string GetWarrantySourceLabel(byte source) => source switch
        {
            0 => "WarrantyRow",
            1 => "ComputedFromSoldDate",
            2 => "NoWarranty",
            _ => "Unknown"
        };

        private QuotationDetailDto MapQuotationToDto(Quotation quotation) => new()
        {
            Id = quotation.Id,
            TicketId = quotation.TicketId,
            IssuedDate = quotation.IssuedDate,
            LaborCost = quotation.LaborCost,
            PartsTotal = quotation.PartsTotal,
            GrandTotal = quotation.GrandTotal,
            Status = quotation.Status,
            StatusLabel = GetQuotationStatusLabel(quotation.Status),
            CustomerDecidedAt = quotation.CustomerDecidedAt,
            CustomerDecisionNote = quotation.CustomerDecisionNote,
            Items = quotation.Items?.Select(i => new QuotationItemDto
            {
                Id = i.Id,
                Description = i.Description,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                LineTotal = i.LineTotal
            }).ToList() ?? new()
        };

        private RmaShipmentDetailDto MapRmaShipmentToDto(RmaShipment rma) => new()
        {
            Id = rma.Id,
            TicketId = rma.TicketId,
            CarrierName = rma.CarrierName,
            TrackingCode = rma.TrackingCode,
            ShippedDate = rma.ShippedDate,
            ReceivedBackDate = rma.ReceivedBackDate,
            ManufacturerResolution = rma.ManufacturerResolution,
            ManufacturerResolutionLabel = GetManufacturerResolutionLabel(rma.ManufacturerResolution),
            ManufacturerNotes = rma.ManufacturerNotes
        };

        private ServiceInvoiceDetailDto MapServiceInvoiceToDto(ServiceInvoice invoice) => new()
        {
            Id = invoice.Id,
            InvoiceCode = invoice.InvoiceCode,
            TicketId = invoice.TicketId,
            QuotationId = invoice.QuotationId,
            IssuedByEmployeeId = invoice.IssuedByEmployeeId,
            IssuedDate = invoice.IssuedDate,
            LaborCost = invoice.LaborCost,
            PartsTotal = invoice.PartsTotal,
            GrandTotal = invoice.GrandTotal,
            PaymentMethod = invoice.PaymentMethod,
            PaymentMethodLabel = GetPaymentMethodLabel(invoice.PaymentMethod),
            PaymentStatus = invoice.PaymentStatus,
            PaymentStatusLabel = GetPaymentStatusLabel(invoice.PaymentStatus),
            Note = invoice.Note,
            Items = invoice.Items?.Select(i => new ServiceInvoiceItemDto
            {
                Id = i.Id,
                Description = i.Description,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                LineTotal = i.LineTotal
            }).ToList() ?? new()
        };

        private string GetQuotationStatusLabel(byte status) => status switch
        {
            0 => "Chờ duyệt",
            1 => "Đã duyệt",
            2 => "Từ chối",
            3 => "Thay thế",
            _ => "Không xác định"
        };

        private string GetManufacturerResolutionLabel(byte resolution) => resolution switch
        {
            0 => "Chưa xác định",
            1 => "Đã sửa",
            2 => "Đã thay thế",
            3 => "Từ chối",
            _ => "Không xác định"
        };

        private string GetPaymentMethodLabel(byte method) => method switch
        {
            0 => "Tiền mặt",
            1 => "Chuyển khoản",
            2 => "VNPay",
            _ => "Không xác định"
        };

        private string GetPaymentStatusLabel(byte status) => status switch
        {
            0 => "Chưa thanh toán",
            1 => "Đã thanh toán",
            _ => "Không xác định"
        };
    }
}
