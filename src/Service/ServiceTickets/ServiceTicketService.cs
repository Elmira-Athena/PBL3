using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PBL3.Core.Entities;
using PBL3.Core.Constants;
using PBL3.Core.Exceptions;
using PBL3.Infrastructure.Concurrency;
using PBL3.Core.Interfaces;
using PBL3.Infrastructure.Data;
using PBL3.Shared.DTOs.ServiceTickets;
using PBL3.Shared.Enums;

namespace PBL3.Service.ServiceTickets
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

        private readonly IDocumentCodeGenerator _codeGenerator;
        private readonly ILogger<ServiceTicketService> _logger;


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
            IInventorySyncService inventorySyncService,
            IDocumentCodeGenerator codeGenerator,
            ILogger<ServiceTicketService> logger)
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
            _codeGenerator = codeGenerator;
            _logger = logger;
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
                throw new BusinessRuleException("Không tìm thấy Serial trong hệ thống.");

            if (serial.Status != (byte)SerialStatus.Sold)
                throw new BusinessRuleException("Sản phẩm không ở trạng thái Sold.");

            if (await _ticketRepository.HasOpenTicketForSerialAsync(serial.Id))
                throw new BusinessRuleException("Sản phẩm này đã có phiếu sửa chữa chưa đóng.");

            var variant = serial.Variant;
            var order = await _orderRepository.GetByIdAsync(serial.OrderId!.Value);
            if (order == null)
                throw new BusinessRuleException("Không tìm thấy đơn hàng gốc.");

            // Đánh giá bảo hành thời gian thực để chốt snapshot ngay lúc tiếp nhận
            var warranty = await WarrantyEvaluator.EvaluateAsync(serial, variant, _warrantyRepository);

            // Thực thi Transaction để đảm bảo tính toàn vẹn khi ghi nhận phiếu và lịch sử trạng thái ban đầu
            //
            // retrySafe: call-site này thoả cả ba điều kiện của hợp đồng retry
            // (xem IUnitOfWork.ExecuteInTransactionAsync):
            //   1. Không sửa entity nào nạp sẵn ở ngoài — `serial` và `order` đều là
            //      AsNoTracking, chỉ đọc Id/UserId ra để dựng phiếu mới.
            //   2. Mã phiếu và mốc thời gian sinh BÊN TRONG delegate (xem ngay dưới).
            //   3. Không có tác dụng phụ không-idempotent nào chạy trước delegate.
            try
            {
                var ticket = await _unitOfWork.ExecuteInTransactionAsync(async () =>
                {
                    // Sinh mã phiếu dịch vụ ST-yyyyMMdd-NNNNNN.
                    // PHẢI nằm trong delegate: nếu sinh ở ngoài thì lần thử lại dùng lại
                    // đúng mã cũ, và mã đó có thể đã bị request khác lấy mất trong lúc đó.
                    var now = DateTime.UtcNow;
                    var ticketCode = await _codeGenerator.NextAsync(DocumentCodeKind.ServiceTicket);

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

                    return ticket;
                }, retrySafe: true);

                return await GetTicketByIdAsync(ticket.Id, userId, false);
            }
            // 🔴 Kẻ THUA cuộc đua tiếp nhận. Chốt thật là filtered unique index
            // UQ_ServiceTickets_SerialId_Open, không phải câu HasOpenTicketForSerialAsync ở
            // trên — LoadProbe S04 đo được 2 phiếu chưa đóng trên cùng một serial, và nó chỉ
            // vỡ khi có HAI instance: cửa sổ check-then-act đủ hẹp để một tiến trình che được.
            //
            // ⚠️ Vì sao BẮT ở đây chứ không để ConflictExceptionHandler lo. Handler chỉ biết
            // "trùng khoá" nên nó buộc phải nói một câu chung ("Dữ liệu này vừa được người khác
            // tạo hoặc thay đổi…"). Chỗ này biết CHÍNH XÁC chuyện gì xảy ra và đã có sẵn đúng
            // câu đó ở nhánh kiểm tra sớm — dùng lại nguyên văn để người dùng thấy CÙNG một
            // thông báo dù họ thua cuộc đua hay bị chặn từ đầu. Đó là lý do kế hoạch đợt 3 ghi
            // "để service call-site cung cấp thông báo theo ngữ cảnh".
            //
            // 🚨 Nhận diện bằng TÊN CONSTRAINT, không dò ex.Message (chuỗi tiếng Anh, đổi theo
            // phiên bản — đúng bẫy #7) và cũng không chỉ dò "vi phạm unique nào đó".
            //
            // ✅ Đợt 7: trước đây khối này bắt `SqlException { Number: 2601 or 2627 }` — tức MỌI
            // vi phạm unique trong hàm — rồi dựa vào một lập luận CỤC BỘ ("ở đây chỉ có một
            // nguồn") để khẳng định đó là serial trùng. Lập luận đúng, nhưng không gì bắt lỗi khi
            // nó hết đúng. PostgreSQL cho biết TÊN constraint, nên nay khớp đích danh — sai
            // constraint thì rơi xuống 409 chung, không bịa ra câu nghiệp vụ sai sự thật.
            //
            // ✅ Và nó thoát luôn bẫy RetryLimitExceededException: khuôn cũ so khớp MỘT tầng
            // InnerException, nên khi execution strategy bọc lại thì không khớp gì cả.
            // IsUniqueViolation đi hết chuỗi.
            catch (Exception ex) when (
                ConflictClassifier.IsUniqueViolation(ex, DbConstraints.ServiceTicketSerialOpen))
            {
                _logger.LogWarning(ex,
                    "Tiếp nhận trùng cho serial {SerialNumber} — unique index đã chặn.",
                    request.SerialNumber);
                throw new BusinessRuleException("Sản phẩm này đã có phiếu sửa chữa chưa đóng.");
            }
        }

        public async Task<bool> AssignTechnicianAsync(int ticketId, Guid employeeId, Guid userId)
        {
            var ticket = await _ticketRepository.GetByIdWithTrackingAsync(ticketId);
            if (ticket == null)
                throw new BusinessRuleException("Phiếu không tồn tại.");

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
                throw new BusinessRuleException("Phiếu không tồn tại.");

            CheckAssignment(ticket, userId, isAdmin);

            if (ticket.Status != (byte)1)
                throw new BusinessRuleException("Phiếu phải ở trạng thái Đang chẩn đoán.");

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
                throw new BusinessRuleException("Phiếu không tồn tại.");

            CheckAssignment(ticket, userId, isAdmin);

            if (ticket.Status != (byte)1)
                throw new BusinessRuleException("Phiếu phải ở trạng thái Đang chẩn đoán.");

            var resolutionType = request.ResolutionType;

            // RÀNG BUỘC PHÂN NHÁNH NGHIỆM NGẶT DỰA TRÊN SNAPSHOT BẢO HÀNH ĐẦU VÀO:
            // - Nếu tại thời điểm tiếp nhận thiết bị đã HẾT bảo hành (WasInWarrantyAtIntake = false):
            //   Bắt buộc đi nhánh Sửa chữa tính phí dịch vụ (ResolutionType = 4 - PaidRepair), cấm đi các luồng bảo hành.
            // - Nếu tại thời điểm tiếp nhận thiết bị CÒN bảo hành (WasInWarrantyAtIntake = true):
            //   Cấm chọn nhánh Sửa chữa tính phí dịch vụ, bắt buộc đi theo các nhánh bảo hành miễn phí.
            if (!ticket.WasInWarrantyAtIntake && resolutionType != (byte)4)
                throw new BusinessRuleException("Sản phẩm đã hết bảo hành, chỉ có thể chọn sửa tính phí.");

            if (ticket.WasInWarrantyAtIntake && resolutionType == (byte)4)
                throw new BusinessRuleException("Sản phẩm còn bảo hành, không thể chọn sửa tính phí.");

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
            // Kiểm tra nghiệp vụ NGOÀI transaction: GetByIdAsync là bản AsNoTracking, chỉ để
            // trả lỗi sớm. Phiếu được nạp LẠI có tracking bên trong delegate mới ghi —
            // điều kiện 1 của hợp đồng retry ở IUnitOfWork.
            var ticket = await _ticketRepository.GetByIdAsync(ticketId);
            if (ticket == null)
                throw new BusinessRuleException("Phiếu không tồn tại.");

            CheckAssignment(ticket, userId, isAdmin);

            // RÀNG BUỘC QUY TRÌNH: Báo giá chỉ được lập khi phiếu đang ở trạng thái chẩn đoán lỗi (Diagnosing - 1)
            if (ticket.Status != (byte)1)
                throw new BusinessRuleException("Phiếu phải ở trạng thái Đang chẩn đoán.");

            ValidateTransition(ticket.Status, (byte)2);

            // RÀNG BUỘC NGHIỆP VỤ: Chỉ các phiếu được định tuyến theo nhánh Sửa chữa tính phí (PaidRepair - 4) mới cần báo giá
            if (ticket.ResolutionType != (byte)4)
                throw new BusinessRuleException("Chỉ phiếu sửa tính phí mới có báo giá.");

            // Sử dụng Transaction để bảo vệ quy trình lưu trữ báo giá & chi tiết báo giá đồng thời cập nhật phiếu dịch vụ
            try
            {
                // ĐÃ RÀ RETRY — thoả cả ba điều kiện của hợp đồng ở IUnitOfWork:
                // (1) `trackedTicket`, `Quotation` và `QuotationItem` đều nạp/dựng BÊN TRONG;
                // (2) IssuedDate và ChangedAt tính bên trong;
                // (3) không có tác dụng phụ không-idempotent nào chạy trước delegate.
                var quotation = await _unitOfWork.ExecuteInTransactionAsync(async () =>
                {
                    // Nạp LẠI có tracking bên trong delegate. Giữ instance của lần thử trước là
                    // đúng bẫy mất dữ liệu: EF đã đánh dấu nó Unchanged với snapshot = Status 2,
                    // nên lần thử này gán lại 2 sẽ KHÔNG sinh UPDATE, trong khi hàng dữ liệu vừa
                    // bị rollback về 1.
                    var trackedTicket = await _ticketRepository.GetByIdWithTrackingAsync(ticketId);

                    // Chốt chống race. NÉM chứ không return, để transaction rollback.
                    if (trackedTicket is null || trackedTicket.Status != (byte)1 || trackedTicket.ResolutionType != (byte)4)
                        throw new BusinessRuleException(
                            "Phiếu vừa được thay đổi ở nơi khác. Vui lòng tải lại trang.");

                    // NGHIỆP VỤ HỦY BÁO GIÁ CŨ: đánh dấu mọi báo giá "Chờ duyệt" (0) của phiếu này
                    // thành "Bị thay thế" (3), để bảo đảm chỉ tồn tại duy nhất một bản báo giá
                    // có hiệu lực.
                    //
                    // Cách cũ đọc qua GetByTicketIdAsync (có AsNoTracking) rồi gán q.Status = 3.
                    // Entity trả về KHÔNG nằm trong Change Tracker nên SaveChangesAsync không
                    // sinh câu UPDATE nào — bất biến mà comment trên mô tả CHƯA BAO GIỜ TỒN TẠI.
                    //
                    // Một câu UPDATE set-based vừa sửa lỗi đó, vừa đóng luôn khe check-then-act.
                    await _quotationRepository.MarkPendingAsSupersededAsync(ticketId);

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
                    trackedTicket.Status = (byte)2;
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

                    return quotation;
                }, retrySafe: true);

                var reloadedQuotation = await _quotationRepository.GetByIdWithItemsAsync(quotation.Id);
                return MapQuotationToDto(reloadedQuotation);
            }
            catch
            {
                throw;
            }
        }

        public async Task<bool> AcceptQuotationAsync(int ticketId, int quotationId, QuotationAcceptDto request, Guid userId, bool isAdmin = false)
        {
            // Kiểm tra nghiệp vụ NGOÀI transaction: GetByIdAsync là bản AsNoTracking, chỉ để
            // trả lỗi sớm. Phiếu được nạp LẠI có tracking bên trong delegate mới ghi.
            var ticket = await _ticketRepository.GetByIdAsync(ticketId);
            if (ticket == null)
                throw new BusinessRuleException("Phiếu không tồn tại.");

            CheckAssignmentOrCustomer(ticket, userId, isAdmin);

            if (ticket.Status != (byte)2)
                throw new BusinessRuleException("Phiếu phải ở trạng thái Chờ duyệt báo giá.");

            // Báo giá đọc bằng projection AsNoTracking: ở luồng này nó CHỈ được kiểm tra, còn
            // việc ghi do TryDecideAsync (một câu UPDATE set-based) đảm nhiệm. Nạp tracked ở
            // đây chỉ tạo ra một entity lỗi thời ngay sau câu UPDATE đó.
            var quotation = await _dbContext.Quotations
                .AsNoTracking()
                .Where(q => q.Id == quotationId)
                .Select(q => new { q.TicketId, q.Status })
                .FirstOrDefaultAsync();
            if (quotation == null || quotation.TicketId != ticketId)
                throw new BusinessRuleException("Báo giá không tồn tại.");

            // CHỐT CHẶN BẢO MẬT: Báo giá được chọn bắt buộc phải có trạng thái là Chờ duyệt (0 - Pending)
            // Ngăn chặn duyệt trùng, duyệt đè hoặc thao tác lại trên báo giá đã được xử lý từ trước.
            if (quotation.Status != (byte)0)
                throw new BusinessRuleException("Chỉ được duyệt báo giá chưa được xử lý.");

            var nextStatus = request.NextStatus;
            if (nextStatus != (byte)4 && nextStatus != (byte)5)
                throw new BusinessRuleException("Trạng thái tiếp theo không hợp lệ.");

            ValidateTransition(ticket.Status, nextStatus);

            try
            {
                // ĐÃ RÀ RETRY — thoả cả ba điều kiện của hợp đồng ở IUnitOfWork:
                // (1) `trackedTicket` và bản ghi lịch sử đều nạp/dựng BÊN TRONG delegate;
                // (2) ModifiedDate / ChangedAt / DecidedAt tính bên trong;
                // (3) không có tác dụng phụ không-idempotent nào chạy trước delegate —
                //     TryDecideAsync nằm TRONG transaction nên rollback hoàn tác luôn nó.
                await _unitOfWork.ExecuteInTransactionAsync(async () =>
                {
                    // Nạp LẠI có tracking bên trong delegate — xem giải thích ở CreateQuotationAsync.
                    var trackedTicket = await _ticketRepository.GetByIdWithTrackingAsync(ticketId);
                    if (trackedTicket is null || trackedTicket.Status != (byte)2)
                        throw new BusinessRuleException(
                            "Phiếu vừa được thay đổi ở nơi khác. Vui lòng tải lại trang.");

                    // CỔNG NGUYÊN TỬ: chốt kiểm ở trên chỉ fail-fast cho UX — nó là check-then-act
                    // và không chặn được hai request đồng thời. Câu UPDATE ... WHERE Status = 0
                    // dưới đây mới là thứ bảo đảm đúng một request duyệt được.
                    if (!await _quotationRepository.TryDecideAsync(
                            quotationId, fromStatus: (byte)0, toStatus: (byte)1,
                            decidedAt: DateTime.UtcNow, note: null))
                        throw new BusinessRuleException(
                            "Báo giá này vừa được xử lý ở nơi khác. Vui lòng tải lại trang.");

                    // Chuyển dịch trạng thái phiếu dịch vụ sang Đang sửa (5) hoặc Chờ phụ tùng (4) tùy thuộc quyết định
                    trackedTicket.Status = nextStatus;
                    trackedTicket.ModifiedDate = DateTime.UtcNow;

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
                }, retrySafe: true);

                return true;
            }
            catch
            {
                throw;
            }
        }

        public async Task<bool> RejectQuotationAsync(int ticketId, int quotationId, QuotationRejectDto request, Guid userId, bool isAdmin = false)
        {
            // Kiểm tra nghiệp vụ NGOÀI transaction: GetByIdAsync là bản AsNoTracking, chỉ để
            // trả lỗi sớm. Phiếu được nạp LẠI có tracking bên trong delegate mới ghi.
            var ticket = await _ticketRepository.GetByIdAsync(ticketId);
            if (ticket == null)
                throw new BusinessRuleException("Phiếu không tồn tại.");

            CheckAssignmentOrCustomer(ticket, userId, isAdmin);

            if (ticket.Status != (byte)2)
                throw new BusinessRuleException("Phiếu phải ở trạng thái Chờ duyệt báo giá.");

            // Báo giá đọc bằng projection AsNoTracking — việc ghi do TryDecideAsync đảm nhiệm.
            var quotation = await _dbContext.Quotations
                .AsNoTracking()
                .Where(q => q.Id == quotationId)
                .Select(q => new { q.TicketId, q.Status })
                .FirstOrDefaultAsync();
            if (quotation == null || quotation.TicketId != ticketId)
                throw new BusinessRuleException("Báo giá không tồn tại.");

            // CHỐT CHẶN còn thiếu: trước đây nhánh từ chối KHÔNG kiểm Status gì cả,
            // khác hẳn nhánh duyệt. Hệ quả: từ chối được cả báo giá ĐÃ ĐƯỢC DUYỆT,
            // chỉ cần bấm hai lần — không cần đồng thời mới lộ.
            if (quotation.Status != (byte)0)
                throw new BusinessRuleException("Chỉ được từ chối báo giá chưa được xử lý.");

            ValidateTransition(ticket.Status, (byte)3);

            try
            {
                // ĐÃ RÀ RETRY — thoả cả ba điều kiện của hợp đồng (xem nhánh duyệt phía trên).
                await _unitOfWork.ExecuteInTransactionAsync(async () =>
                {
                    // Nạp LẠI có tracking bên trong delegate — xem giải thích ở CreateQuotationAsync.
                    var trackedTicket = await _ticketRepository.GetByIdWithTrackingAsync(ticketId);
                    if (trackedTicket is null || trackedTicket.Status != (byte)2)
                        throw new BusinessRuleException(
                            "Phiếu vừa được thay đổi ở nơi khác. Vui lòng tải lại trang.");

                    // CỔNG NGUYÊN TỬ — xem giải thích ở nhánh duyệt.
                    if (!await _quotationRepository.TryDecideAsync(
                            quotationId, fromStatus: (byte)0, toStatus: (byte)2,
                            decidedAt: DateTime.UtcNow, note: request.Reason))
                        throw new BusinessRuleException(
                            "Báo giá này vừa được xử lý ở nơi khác. Vui lòng tải lại trang.");

                    trackedTicket.Status = (byte)3;
                    trackedTicket.ModifiedDate = DateTime.UtcNow;

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
                }, retrySafe: true);

                return true;
            }
            catch
            {
                throw;
            }
        }

        public async Task<RmaShipmentDetailDto?> CreateRmaShipmentAsync(int ticketId, RmaShipmentCreateDto request, Guid userId, bool isAdmin = false)
        {
            // Kiểm tra nghiệp vụ NGOÀI transaction: GetByIdAsync là bản AsNoTracking, chỉ để
            // trả lỗi sớm. Phiếu được nạp LẠI có tracking bên trong delegate mới ghi.
            var ticket = await _ticketRepository.GetByIdAsync(ticketId);
            if (ticket == null)
                throw new BusinessRuleException("Phiếu không tồn tại.");

            CheckAssignment(ticket, userId, isAdmin);

            if (ticket.ResolutionType != (byte)2)
                throw new BusinessRuleException("Phiếu phải chọn nhánh RMA.");

            if (!ticket.WasInWarrantyAtIntake)
                throw new BusinessRuleException("Sản phẩm đã hết bảo hành, không thể gửi RMA.");

            // Issue #8: Check duplicate RMA
            var existingRma = await _rmaRepository.GetByTicketIdReadOnlyAsync(ticketId);
            if (existingRma != null)
                throw new BusinessRuleException("Phiếu này đã được gửi hãng rồi.");

            ValidateTransition(ticket.Status, (byte)6);

            // Issue #13: Use UoW transaction pattern instead of individual SaveChanges
            try
            {
                // ĐÃ RÀ RETRY — thoả cả ba điều kiện của hợp đồng ở IUnitOfWork:
                // (1) `trackedTicket` nạp lại và `RmaShipment` dựng mới BÊN TRONG delegate;
                // (2) ShippedDate / ModifiedDate / ChangedAt tính bên trong;
                // (3) không có tác dụng phụ không-idempotent nào chạy trước delegate.
                await _unitOfWork.ExecuteInTransactionAsync(async () =>
                {
                    // Nạp LẠI có tracking bên trong delegate — xem giải thích ở CreateQuotationAsync.
                    var trackedTicket = await _ticketRepository.GetByIdWithTrackingAsync(ticketId);
                    if (trackedTicket is null || trackedTicket.ResolutionType != (byte)2)
                        throw new BusinessRuleException(
                            "Phiếu vừa được thay đổi ở nơi khác. Vui lòng tải lại trang.");

                    // Chốt chống RMA trùng phải nằm TRONG transaction: bản kiểm ở ngoài chỉ
                    // fail-fast cho UX, và ở lần thử lại nó đã đọc từ trước khi rollback.
                    if (await _rmaRepository.GetByTicketIdReadOnlyAsync(ticketId) != null)
                        throw new BusinessRuleException("Phiếu này đã được gửi hãng rồi.");

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

                    trackedTicket.Status = (byte)6;
                    trackedTicket.ModifiedDate = DateTime.UtcNow;

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
                }, retrySafe: true);

                var reloadedRma = await _rmaRepository.GetByTicketIdReadOnlyAsync(ticketId);
                return MapRmaShipmentToDto(reloadedRma!);
            }
            catch
            {
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
            // Kiểm tra nghiệp vụ NGOÀI transaction: cả hai đều đọc CHỈ-ĐỌC, chỉ để trả lỗi sớm.
            // `rma` và `ticket` được nạp LẠI có tracking bên trong delegate mới ghi — bản tracked
            // nạp ở ngoài chính là bẫy mất dữ liệu khi chạy lại (xem hợp đồng ở IUnitOfWork).
            if (await _rmaRepository.GetByTicketIdReadOnlyAsync(ticketId) == null)
                throw new BusinessRuleException("Không có phiếu RMA cho ticket này.");

            var ticket = await _ticketRepository.GetByIdAsync(ticketId);
            if (ticket == null)
                throw new BusinessRuleException("Phiếu không tồn tại.");

            CheckAssignment(ticket, userId, isAdmin);

            // Xác định trạng thái đích tiếp theo của phiếu dựa trên phản hồi của Hãng sản xuất
            byte toStatus = request.ManufacturerResolution switch
            {
                2 => (byte)8, // Manufacturer Replaced -> Đã đổi 1-1
                3 => (byte)1, // Manufacturer Rejected -> Quay lại trạng thái Đang chẩn đoán
                _ => (byte)7  // Manufacturer Repaired -> Đã nhận lại từ hãng
            };
            ValidateTransition(ticket.Status, toStatus);

            try
            {
                // ĐÃ RÀ RETRY — thoả cả ba điều kiện của hợp đồng ở IUnitOfWork:
                // (1) `rma`, `trackedTicket`, `newSerial`, `oldOrderSerial`, `oldWarranties` và
                //     các bản ghi mới đều nạp/dựng BÊN TRONG delegate;
                // (2) `now` tính bên trong; (3) không có tác dụng phụ nào chạy trước delegate.
                //
                // Trả về VariantId cần đồng bộ tồn kho thay vì đọc `ticket.Serial` sau transaction:
                // `ticket` ở ngoài là bản AsNoTracking dùng cho tiền kiểm, không phải bản đã ghi.
                var syncVariantId = await _unitOfWork.ExecuteInTransactionAsync<int?>(async () =>
                {
                    var now = DateTime.UtcNow;

                    // Nạp LẠI có tracking bên trong delegate — xem giải thích ở CreateQuotationAsync.
                    var rma = await _rmaRepository.GetByTicketIdTrackedAsync(ticketId);
                    var trackedTicket = await _ticketRepository.GetByIdWithTrackingAsync(ticketId);
                    if (rma is null || trackedTicket is null)
                        throw new BusinessRuleException(
                            "Phiếu vừa được thay đổi ở nơi khác. Vui lòng tải lại trang.");

                    byte previousStatus = trackedTicket.Status;

                    rma.ManufacturerResolution = request.ManufacturerResolution;
                    rma.ManufacturerNotes = request.ManufacturerNotes;
                    rma.ReceivedBackDate = now;
                    rma.ReceivedByEmployeeId = userId;

                    // NGHIỆP VỤ PHỨC TẠP: Xử lý đổi mới thiết bị khi Hãng đồng ý bảo hành đổi 1-1 (ManufacturerResolution = 2)
                    if (request.ManufacturerResolution == (byte)2)
                    {
                        if (!request.ReplacementSerialId.HasValue)
                            throw new BusinessRuleException("Phải cung cấp Serial thay thế khi hãng đã thay thế.");

                        var newSerialId = request.ReplacementSerialId.Value;
                        var oldSerial = trackedTicket.Serial;
                        var newSerial = await _serialRepository.GetByIdWithTrackingAsync(newSerialId);

                        // Kiểm định độ khả dụng của Serial thay thế mới nhận từ hãng
                        if (newSerial == null || newSerial.Status != (byte)0)
                            throw new BusinessRuleException("Serial thay thế không sẵn trong kho hoặc đã được giữ chỗ.");

                        if (newSerial.VariantId != oldSerial.VariantId)
                            throw new BusinessRuleException("Serial thay thế phải cùng biến thể với serial hỏng.");

                        // Truy vấn liên kết xuất kho trong lịch sử để trỏ lại sang Serial mới (Invoice Preservation)
                        var oldOrderSerial = await _dbContext.OrderSerials
                            .FirstOrDefaultAsync(os => os.SerialId == oldSerial.Id);
                        if (oldOrderSerial == null)
                            throw new BusinessRuleException("Không tìm thấy bản ghi xuất kho gốc của serial này.");

                        // Xác định thời hạn bảo hành gốc để phục vụ kế thừa
                        var oldWarranties = await _warrantyRepository.GetActiveBySerialIdTrackedAsync(oldSerial.Id);  // TRACKED: bên dưới ghi oldWarranties[0].Status = 2
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

                        trackedTicket.Status = (byte)8;
                        trackedTicket.ReplacementSerialId = newSerialId;

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
                        trackedTicket.Status = request.ManufacturerResolution == (byte)3 ? (byte)1 : (byte)7;
                    }

                    await _ticketRepository.AddStatusHistoryAsync(new ServiceTicketStatusHistory
                    {
                        TicketId = ticketId,
                        FromStatus = previousStatus,
                        ToStatus = trackedTicket.Status,
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

                    // Chỉ cần đồng bộ tồn kho khi có hoán đổi serial vật lý (hãng thay thế).
                    return request.ManufacturerResolution == (byte)2
                        ? trackedTicket.Serial.VariantId
                        : (int?)null;
                }, retrySafe: true);

                // NGHIỆP VỤ KHO: Tự động kích hoạt đồng bộ hóa số lượng tồn kho của biến thể do có sự biến đổi Serial vật lý thực tế
                if (syncVariantId.HasValue)
                {
                    await _inventorySyncService.SyncStockBatchAsync(new[] { syncVariantId.Value });
                }

                return true;
            }
            catch
            {
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
            // Kiểm tra QUYỀN chạy ngoài transaction để fail-fast: đọc AsNoTracking
            // (GetByIdWithDetailsAsync), chỉ dùng để chặn sớm, không ghi gì.
            //
            // Toàn bộ kiểm tra NGHIỆP VỤ còn lại đã được chuyển VÀO trong delegate cùng với
            // phần ghi. Ở call-site này việc tách "kiểm ngoài / ghi trong" không đáng: năm cụm
            // entity đằng nào cũng phải nạp lại bên trong, nên kiểm ở ngoài sẽ là bản sao thứ
            // hai của cùng một logic — và mọi kiểm tra đều đã ném InvalidOperationException,
            // vốn được `catch { throw; }` trả nguyên vẹn cho tầng gọi.
            var precheckTicket = await _ticketRepository.GetByIdWithDetailsAsync(ticketId);
            if (precheckTicket == null)
                throw new BusinessRuleException("Phiếu không tồn tại.");

            CheckAssignment(precheckTicket, userId, isAdmin);

            try
            {
                // ĐÃ RÀ RETRY — thoả cả ba điều kiện của hợp đồng ở IUnitOfWork.
                // Trước khi rà, đây là call-site nặng nhất: NĂM cụm entity — `ticket`,
                // `oldSerial`, `newSerial`, `oldOrderSerial`, `oldWarranties` — đều nạp TRACKED
                // ở ngoài rồi sửa bên trong, tức năm lần dính đúng bẫy mất dữ liệu âm thầm.
                // Nay cả năm đều nạp LẠI bên trong delegate ở mỗi lần thử.
                //
                // Trả về VariantId cần đồng bộ tồn kho thay vì đọc `oldSerial` sau transaction.
                var syncVariantId = await _unitOfWork.ExecuteInTransactionAsync(async () =>
                {
                    var now = DateTime.UtcNow;

                    // ── Nạp LẠI toàn bộ entity sẽ ghi, BÊN TRONG delegate ──
                    var ticket = await _ticketRepository.GetByIdWithTrackingAsync(ticketId);
                    if (ticket == null)
                        throw new BusinessRuleException("Phiếu không tồn tại.");

                    if (ticket.Status != (byte)1 && ticket.Status != (byte)7)
                        throw new BusinessRuleException("Trạng thái phiếu không cho phép đổi 1-1.");

                    if (ticket.ResolutionType != (byte)3 && ticket.ResolutionType != (byte)2)
                        throw new BusinessRuleException("Loại giải pháp không phải đổi 1-1.");

                    // NGHIỆP VỤ BẢO VỆ CHỐT CHẶN: Đánh giá bảo hành thời gian thực (Live Warranty Re-evaluation) tại thời điểm đổi máy.
                    // Điều này cực kỳ quan trọng để phòng chống rủi ro thiết bị thực tế đã trôi qua thời hạn bảo hành tối đa trong quãng thời gian dài chẩn đoán hoặc chờ linh kiện.
                    var liveWarranty = await WarrantyEvaluator.EvaluateAsync(
                        ticket.Serial,
                        ticket.Serial.Variant,
                        _warrantyRepository);

                    if (!liveWarranty.IsInWarranty)
                        throw new BusinessRuleException("Bảo hành đã hết hạn, không thể đổi 1-1.");

                    if (ticket.ReplacementSerialId.HasValue)
                        throw new BusinessRuleException("Phiếu này đã được đổi 1-1 trước đó.");

                    ValidateTransition(ticket.Status, (byte)8);

                    var oldSerial = await _serialRepository.GetByIdWithTrackingAsync(ticket.SerialId);
                    if (oldSerial == null)
                        throw new BusinessRuleException("Serial cũ không tồn tại.");

                    var newSerial = await _serialRepository.GetByIdWithTrackingAsync(newSerialId);
                    if (newSerial == null || newSerial.Status != (byte)0)
                        throw new BusinessRuleException("Serial thay thế không sẵn trong kho hoặc đã được giữ chỗ.");

                    if (newSerial.VariantId != oldSerial.VariantId)
                        throw new BusinessRuleException("Serial thay thế phải cùng biến thể với serial hỏng.");

                    // Hóa đơn lịch sử: Lấy dòng ánh xạ hóa đơn vật lý của máy cũ
                    var oldOrderSerial = await _dbContext.OrderSerials
                        .FirstOrDefaultAsync(os => os.SerialId == oldSerial.Id);
                    if (oldOrderSerial == null)
                        throw new BusinessRuleException("Không tìm thấy bản ghi xuất kho gốc của serial này.");

                    // Lấy thời hạn kết thúc bảo hành hiện hữu để chuyển tiếp bảo hành kế thừa
                    var oldWarranties = await _warrantyRepository.GetActiveBySerialIdTrackedAsync(oldSerial.Id);  // TRACKED: bên dưới ghi oldWarranties[0].Status = 2
                    var oldEndDate = oldWarranties.FirstOrDefault()?.EndDate
                        ?? oldSerial.SoldDate?.AddMonths(oldSerial.Variant.WarrantyMonth)
                        ?? now;

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

                    return oldSerial.VariantId;
                }, retrySafe: true);

                // NGHIỆP VỤ KHO: Tự động kích hoạt đồng bộ hóa số lượng tồn kho khả dụng của biến thể trong RAM
                await _inventorySyncService.SyncStockBatchAsync(new[] { syncVariantId });

                return true;
            }
            catch
            {
                throw;
            }
        }

        public async Task<bool> MarkInternalRepairCompletedAsync(int ticketId, ServiceTicketCompleteDto request, Guid userId, bool isAdmin = false)
        {
            // Kiểm tra nghiệp vụ NGOÀI transaction: GetByIdAsync là bản AsNoTracking, chỉ để
            // trả lỗi sớm. Phiếu được nạp LẠI có tracking bên trong delegate mới ghi.
            var ticket = await _ticketRepository.GetByIdAsync(ticketId);
            if (ticket == null)
                throw new BusinessRuleException("Phiếu không tồn tại.");

            CheckAssignment(ticket, userId, isAdmin);

            if (!new[] { (byte)5, (byte)7, (byte)8 }.Contains(ticket.Status))
                throw new BusinessRuleException("Phiếu phải ở trạng thái sửa chữa hoặc đã nhận từ hãng.");

            ValidateTransition(ticket.Status, (byte)9);

            try
            {
                // ĐÃ RÀ RETRY — thoả cả ba điều kiện của hợp đồng ở IUnitOfWork:
                // (1) `trackedTicket` nạp lại và các bản ghi lịch sử/log dựng BÊN TRONG delegate;
                // (2) CompletedDate / ModifiedDate / ChangedAt / LoggedAt tính bên trong;
                // (3) không có tác dụng phụ không-idempotent nào chạy trước delegate.
                await _unitOfWork.ExecuteInTransactionAsync(async () =>
                {
                    var now = DateTime.UtcNow;

                    // Nạp LẠI có tracking bên trong delegate — xem giải thích ở CreateQuotationAsync.
                    var trackedTicket = await _ticketRepository.GetByIdWithTrackingAsync(ticketId);
                    if (trackedTicket is null || !new[] { (byte)5, (byte)7, (byte)8 }.Contains(trackedTicket.Status))
                        throw new BusinessRuleException(
                            "Phiếu vừa được thay đổi ở nơi khác. Vui lòng tải lại trang.");

                    // Issue #4: Capture FromStatus BEFORE changing status
                    byte previousStatus = trackedTicket.Status;

                    trackedTicket.Status = (byte)9;
                    trackedTicket.CompletedDate = now;
                    trackedTicket.ModifiedDate = now;

                    await _ticketRepository.AddStatusHistoryAsync(new ServiceTicketStatusHistory
                    {
                        TicketId = ticketId,
                        FromStatus = previousStatus,
                        ToStatus = (byte)9,
                        ChangedByEmployeeId = userId,
                        ChangedAt = now,
                        Note = "Hoàn tất sửa chữa"
                    });

                    // Issue #12: Only add log if not already logged during swap operation
                    if (previousStatus != (byte)8)
                    {
                        await _logRepository.AddAsync(new SerialRepairLog
                        {
                            SerialId = trackedTicket.SerialId,
                            TicketId = ticketId,
                            ResolutionType = trackedTicket.ResolutionType,
                            LoggedAt = now,
                            LoggedByEmployeeId = userId,
                            Summary = request.Note ?? "Sửa chữa xong"
                        });
                    }

                    await _unitOfWork.SaveChangesAsync();
                }, retrySafe: true);

                return true;
            }
            catch
            {
                throw;
            }
        }

        public async Task<bool> MarkWaitingPartsAsync(int ticketId, Guid userId, bool isAdmin = false)
        {
            var ticket = await _ticketRepository.GetByIdWithTrackingAsync(ticketId);
            if (ticket == null)
                throw new BusinessRuleException("Phiếu không tồn tại.");

            CheckAssignment(ticket, userId, isAdmin);

            if (ticket.Status != (byte)5)
                throw new BusinessRuleException("Phiếu phải ở trạng thái Đang sửa.");

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
                throw new BusinessRuleException("Phiếu không tồn tại.");

            CheckAssignment(ticket, userId, isAdmin);

            if (ticket.Status != (byte)4)
                throw new BusinessRuleException("Phiếu phải ở trạng thái Chờ phụ tùng.");

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
                throw new BusinessRuleException("Phiếu không tồn tại.");

            CheckAssignment(ticket, userId, isAdmin);

            if (ticket.ResolutionType != (byte)1)
                throw new BusinessRuleException("Chỉ áp dụng cho phiếu sửa chữa bảo hành nội bộ.");

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
                throw new BusinessRuleException("Phiếu không tồn tại.");

            CheckAssignment(ticket, userId, isAdmin);

            // RÀNG BUỘC PHÁP LÝ: Hóa đơn chỉ được phép xuất khi Phiếu dịch vụ đã hoàn tất sửa chữa (Completed - 9)
            if (ticket.Status != (byte)9)
                throw new BusinessRuleException("Phiếu phải ở trạng thái Hoàn tất.");

            // RÀNG BUỘC NGHIỆP VỤ: Chỉ nhánh Sửa chữa tính phí (PaidRepair - 4) mới sinh hóa đơn tài chính dịch vụ
            if (ticket.ResolutionType != (byte)4)
                throw new BusinessRuleException("Chỉ sửa tính phí mới tạo hóa đơn.");

            // CHỐT CHẶN TRÁNH TRÙNG LẶP: Mỗi phiếu sửa chữa tính phí chỉ được phép có duy nhất 1 hóa đơn dịch vụ
            if (await _invoiceRepository.InvoiceExistsForTicketAsync(ticketId))
                throw new BusinessRuleException("Hóa đơn cho phiếu này đã tồn tại.");

            // Lấy báo giá đã được khách hàng duyệt làm căn cứ áp giá thanh toán
            var quotations = await _quotationRepository.GetByTicketIdReadOnlyAsync(ticketId);
            var acceptedQuote = quotations.FirstOrDefault(q => q.Status == (byte)1);
            if (acceptedQuote == null)
                throw new BusinessRuleException("Không tìm thấy báo giá được duyệt.");

            // Khởi chạy Transaction để bảo đảm việc sinh hóa đơn và sao chép danh mục phụ tùng diễn ra an toàn
            //
            // retrySafe: call-site này thoả cả ba điều kiện của hợp đồng retry
            // (xem IUnitOfWork.ExecuteInTransactionAsync):
            //   1. Không sửa entity nào nạp sẵn ở ngoài — `ticket` và `quotations` đều đọc
            //      qua đường AsNoTracking, và delegate chỉ TẠO MỚI (ServiceInvoice +
            //      ServiceInvoiceItem), không cập nhật bản ghi có sẵn nào.
            //   2. Mã hóa đơn và mốc thời gian sinh BÊN TRONG delegate.
            //   3. Không có tác dụng phụ không-idempotent nào chạy trước delegate.
            try
            {
                var invoice = await _unitOfWork.ExecuteInTransactionAsync(async () =>
                {
                    // Sinh mã hóa đơn dịch vụ SRV-yyyyMMdd-NNNNNN
                    var now = DateTime.UtcNow;
                    var invoiceCode = await _codeGenerator.NextAsync(DocumentCodeKind.ServiceInvoice);

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

                    return invoice;
                }, retrySafe: true);

                var reloadedInvoice = await _invoiceRepository.GetByIdWithDetailsAsync(invoice.Id);
                return MapServiceInvoiceToDto(reloadedInvoice);
            }
            catch
            {
                throw;
            }
        }

        public async Task<bool> CancelTicketAsync(int ticketId, string reason, Guid userId)
        {
            var ticket = await _ticketRepository.GetByIdWithTrackingAsync(ticketId);
            if (ticket == null)
                throw new BusinessRuleException("Phiếu không tồn tại.");

            if (new[] { (byte)3, (byte)9, (byte)10 }.Contains(ticket.Status))
                throw new BusinessRuleException("Không thể thay đổi trạng thái phiếu đã đóng.");

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
                throw new BusinessRuleException($"Không thể chuyển trạng thái từ '{GetStatusLabel(currentStatus)}' sang '{GetStatusLabel(targetStatus)}'.");
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
