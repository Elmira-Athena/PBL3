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

        public async Task<ServiceTicketIntakeEvaluationDto> GetWarrantyEvaluationAsync(string serialNumber)
        {
            var serial = await _serialRepository.GetBySerialNumberAsync(serialNumber);

            if (serial == null)
                return new ServiceTicketIntakeEvaluationDto
                {
                    BlockingReason = "Không tìm thấy Serial trong hệ thống."
                };

            if (serial.Status != (byte)SerialStatus.Sold)
                return new ServiceTicketIntakeEvaluationDto
                {
                    BlockingReason = "Sản phẩm này chưa được bán hoặc không ở trạng thái hợp lệ."
                };

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

            if (await _ticketRepository.HasOpenTicketForSerialAsync(serial.Id))
                return new ServiceTicketIntakeEvaluationDto
                {
                    BlockingReason = "Sản phẩm này đã có phiếu sửa chữa chưa đóng."
                };

            var variant = serial.Variant;
            var warranty = await WarrantyEvaluator.EvaluateAsync(serial, variant, _warrantyRepository);

            var allowedBranches = new List<string>();
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

            var warranty = await WarrantyEvaluator.EvaluateAsync(serial, variant, _warrantyRepository);

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
                    HasScratches = request.HasScratches,
                    HasDents = request.HasDents,
                    HasBurnMarks = request.HasBurnMarks,
                    HasMissingAccessories = request.HasMissingAccessories,
                    CosmeticNotes = request.CosmeticNotes,
                    CustomerReportedIssue = request.CustomerReportedIssue,
                    WalkInCustomerName = request.WalkInCustomerName,
                    WalkInCustomerPhone = request.WalkInCustomerPhone,
                    WasInWarrantyAtIntake = warranty.IsInWarranty,
                    WarrantyEndDateAtIntake = warranty.ExpiresOn,
                    WarrantyEvalSource = warranty.Source,
                    Status = (byte)0, // Received
                    ResolutionType = (byte)0, // Pending
                    CreatedDate = now,
                    ModifiedDate = now
                };

                await _ticketRepository.AddAsync(ticket);
                await _unitOfWork.SaveChangesAsync();

                // Create initial status history
                await _ticketRepository.AddStatusHistoryAsync(new ServiceTicketStatusHistory
                {
                    TicketId = ticket.Id,
                    FromStatus = (byte)0, // From Received
                    ToStatus = (byte)0, // To Received
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

        public async Task<bool> RecordDiagnosisAsync(int ticketId, ServiceTicketDiagnosisDto request, Guid userId)
        {
            var ticket = await _ticketRepository.GetByIdAsync(ticketId);
            if (ticket == null)
                throw new InvalidOperationException("Phiếu không tồn tại.");

            if (ticket.Status != (byte)1)
                throw new InvalidOperationException("Phiếu phải ở trạng thái Đang chẩn đoán.");

            ticket.DiagnosisFindings = request.DiagnosisFindings;
            ticket.DiagnosedAt = DateTime.UtcNow;
            ticket.DiagnosedByEmployeeId = userId;
            ticket.ModifiedDate = DateTime.UtcNow;

            await _ticketRepository.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ChooseBranchAsync(int ticketId, ServiceTicketBranchDto request, Guid userId)
        {
            var ticket = await _ticketRepository.GetByIdWithDetailsAsync(ticketId);
            if (ticket == null)
                throw new InvalidOperationException("Phiếu không tồn tại.");

            if (ticket.Status != (byte)1)
                throw new InvalidOperationException("Phiếu phải ở trạng thái Đang chẩn đoán.");

            var resolutionType = request.ResolutionType;

            // Issue #5: Use intake snapshot warranty, not live evaluation
            // Warranty gate is determined at intake time and does not change
            if (!ticket.WasInWarrantyAtIntake && resolutionType != (byte)4)
                throw new InvalidOperationException("Sản phẩm đã hết bảo hành, chỉ có thể chọn sửa tính phí.");

            if (ticket.WasInWarrantyAtIntake && resolutionType == (byte)4)
                throw new InvalidOperationException("Sản phẩm còn bảo hành, không thể chọn sửa tính phí.");

            ticket.ResolutionType = resolutionType;
            ticket.ModifiedDate = DateTime.UtcNow;

            await _ticketRepository.SaveChangesAsync();
            return true;
        }

        public async Task<QuotationDetailDto?> CreateQuotationAsync(int ticketId, QuotationCreateDto request, Guid userId)
        {
            var ticket = await _ticketRepository.GetByIdAsync(ticketId);
            if (ticket == null)
                throw new InvalidOperationException("Phiếu không tồn tại.");

            // Issue #10: Check ticket status is Diagnosing
            if (ticket.Status != (byte)1)
                throw new InvalidOperationException("Phiếu phải ở trạng thái Đang chẩn đoán.");

            ValidateTransition(ticket.Status, (byte)2);

            if (ticket.ResolutionType != (byte)4)
                throw new InvalidOperationException("Chỉ phiếu sửa tính phí mới có báo giá.");

            await _unitOfWork.BeginTransactionAsync();
            try
            {
                var oldQuotations = await _quotationRepository.GetByTicketIdAsync(ticketId);
                foreach (var q in oldQuotations.Where(q => q.Status == (byte)0))
                {
                    q.Status = (byte)3;
                }

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
                    Status = (byte)0,
                };

                await _quotationRepository.AddAsync(quotation);
                await _unitOfWork.SaveChangesAsync();

                foreach (var item in request.Items)
                {
                    await _dbContext.QuotationItems.AddAsync(new QuotationItem
                    {
                        QuotationId = quotation.Id,
                        VariantId = item.VariantId,
                        Description = item.Description,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice
                    });
                }

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

        public async Task<bool> AcceptQuotationAsync(int ticketId, int quotationId, QuotationAcceptDto request, Guid userId)
        {
            var ticket = await _ticketRepository.GetByIdAsync(ticketId);
            if (ticket == null)
                throw new InvalidOperationException("Phiếu không tồn tại.");

            if (ticket.Status != (byte)2)
                throw new InvalidOperationException("Phiếu phải ở trạng thái Chờ duyệt báo giá.");

            var quotation = await _quotationRepository.GetByIdWithTrackingAsync(quotationId);
            if (quotation == null || quotation.TicketId != ticketId)
                throw new InvalidOperationException("Báo giá không tồn tại.");

            // Issue #6: Check quotation is in Pending status before accepting
            if (quotation.Status != (byte)0)
                throw new InvalidOperationException("Chỉ được duyệt báo giá chưa được xử lý.");

            var nextStatus = request.NextStatus;
            if (nextStatus != (byte)4 && nextStatus != (byte)5)
                throw new InvalidOperationException("Trạng thái tiếp theo không hợp lệ.");

            ValidateTransition(ticket.Status, nextStatus);

            await _unitOfWork.BeginTransactionAsync();
            try
            {
                quotation.Status = (byte)1;
                quotation.CustomerDecidedAt = DateTime.UtcNow;

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

        public async Task<bool> RejectQuotationAsync(int ticketId, int quotationId, QuotationRejectDto request, Guid userId)
        {
            var ticket = await _ticketRepository.GetByIdAsync(ticketId);
            if (ticket == null)
                throw new InvalidOperationException("Phiếu không tồn tại.");

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

        public async Task<RmaShipmentDetailDto?> CreateRmaShipmentAsync(int ticketId, RmaShipmentCreateDto request, Guid userId)
        {
            var ticket = await _ticketRepository.GetByIdWithTrackingAsync(ticketId);
            if (ticket == null)
                throw new InvalidOperationException("Phiếu không tồn tại.");

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

        public async Task<bool> RecordRmaResolutionAsync(int ticketId, RmaResolutionUpdateDto request, Guid userId)
        {
            var rma = await _rmaRepository.GetByTicketIdAsync(ticketId);
            if (rma == null)
                throw new InvalidOperationException("Không có phiếu RMA cho ticket này.");

            var ticket = await _ticketRepository.GetByIdWithTrackingAsync(ticketId);
            if (ticket == null)
                throw new InvalidOperationException("Phiếu không tồn tại.");

            byte toStatus = request.ManufacturerResolution == (byte)2 ? (byte)8 : (byte)7;
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

                // Issue #7: Handle manufacturer Replaced (2) - perform 1-for-1 swap
                if (request.ManufacturerResolution == (byte)2)
                {
                    if (!request.ReplacementSerialId.HasValue)
                        throw new InvalidOperationException("Phải cung cấp Serial thay thế khi hãng đã thay thế.");

                    var newSerialId = request.ReplacementSerialId.Value;
                    var oldSerial = ticket.Serial;
                    var newSerial = await _serialRepository.GetByIdWithTrackingAsync(newSerialId);

                    if (newSerial == null || newSerial.Status != (byte)0)
                        throw new InvalidOperationException("Serial thay thế không sẵn trong kho hoặc đã được giữ chỗ.");

                    if (newSerial.VariantId != oldSerial.VariantId)
                        throw new InvalidOperationException("Serial thay thế phải cùng biến thể với serial hỏng.");

                    var oldOrderSerial = await _dbContext.OrderSerials
                        .FirstOrDefaultAsync(os => os.SerialId == oldSerial.Id);
                    if (oldOrderSerial == null)
                        throw new InvalidOperationException("Không tìm thấy bản ghi xuất kho gốc của serial này.");

                    var oldWarranties = await _warrantyRepository.GetActiveBySerialIdAsync(oldSerial.Id);
                    var oldEndDate = oldWarranties.FirstOrDefault()?.EndDate
                        ?? oldSerial.SoldDate?.AddMonths(oldSerial.Variant.WarrantyMonth)
                        ?? now;

                    // Perform swap
                    oldSerial.Status = (byte)4;
                    newSerial.Status = (byte)2;
                    newSerial.SoldDate = now;
                    newSerial.OrderId = oldSerial.OrderId;

                    oldOrderSerial.SerialId = newSerialId;

                    if (oldWarranties.Count > 0)
                    {
                        oldWarranties[0].Status = (byte)2;
                    }

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
                    ticket.Status = (byte)7;
                }

                await _ticketRepository.AddStatusHistoryAsync(new ServiceTicketStatusHistory
                {
                    TicketId = ticketId,
                    FromStatus = previousStatus,
                    ToStatus = ticket.Status,
                    ChangedByEmployeeId = userId,
                    ChangedAt = now,
                    Note = request.ManufacturerResolution == (byte)2
                        ? "Hãng thay thế, chuyển sang Đã đổi 1-1"
                        : "Nhận lại từ hãng"
                });

                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitAsync();

                // Sync inventory if swap occurred
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

        public async Task<bool> Perform1For1SwapAsync(int ticketId, int newSerialId, Guid userId)
        {
            var ticket = await _ticketRepository.GetByIdWithTrackingAsync(ticketId);
            if (ticket == null)
                throw new InvalidOperationException("Phiếu không tồn tại.");

            if (ticket.Status != (byte)1 && ticket.Status != (byte)7)
                throw new InvalidOperationException("Trạng thái phiếu không cho phép đổi 1-1.");

            if (ticket.ResolutionType != (byte)3 && ticket.ResolutionType != (byte)2)
                throw new InvalidOperationException("Loại giải pháp không phải đổi 1-1.");

            // Issue #11: Re-evaluate warranty at swap time in case warranty expired
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

            var oldOrderSerial = await _dbContext.OrderSerials
                .FirstOrDefaultAsync(os => os.SerialId == oldSerial.Id);
            if (oldOrderSerial == null)
                throw new InvalidOperationException("Không tìm thấy bản ghi xuất kho gốc của serial này.");

            var oldWarranties = await _warrantyRepository.GetActiveBySerialIdAsync(oldSerial.Id);
            var oldEndDate = oldWarranties.FirstOrDefault()?.EndDate
                ?? oldSerial.SoldDate?.AddMonths(oldSerial.Variant.WarrantyMonth)
                ?? DateTime.UtcNow;

            await _unitOfWork.BeginTransactionAsync();
            try
            {
                var now = DateTime.UtcNow;

                // Issue #2: Capture FromStatus BEFORE changing status
                byte previousStatus = ticket.Status;

                oldSerial.Status = (byte)4;
                newSerial.Status = (byte)2;
                newSerial.SoldDate = now;
                newSerial.OrderId = oldSerial.OrderId;

                oldOrderSerial.SerialId = newSerialId;

                if (oldWarranties.Count > 0)
                {
                    oldWarranties[0].Status = (byte)2;
                }

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

                await _inventorySyncService.SyncStockBatchAsync(new[] { oldSerial.VariantId });

                return true;
            }
            catch
            {
                await _unitOfWork.RollbackAsync();
                throw;
            }
        }

        public async Task<bool> MarkInternalRepairCompletedAsync(int ticketId, ServiceTicketCompleteDto request, Guid userId)
        {
            var ticket = await _ticketRepository.GetByIdWithTrackingAsync(ticketId);
            if (ticket == null)
                throw new InvalidOperationException("Phiếu không tồn tại.");

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

        public async Task<bool> MarkWaitingPartsAsync(int ticketId, Guid userId)
        {
            var ticket = await _ticketRepository.GetByIdAsync(ticketId);
            if (ticket == null)
                throw new InvalidOperationException("Phiếu không tồn tại.");

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

        public async Task<bool> ResumeRepairAsync(int ticketId, Guid userId)
        {
            var ticket = await _ticketRepository.GetByIdAsync(ticketId);
            if (ticket == null)
                throw new InvalidOperationException("Phiếu không tồn tại.");

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

        public async Task<ServiceInvoiceDetailDto?> IssueServiceInvoiceAsync(int ticketId, ServiceInvoiceCreateDto request, Guid userId)
        {
            var ticket = await _ticketRepository.GetByIdWithDetailsAsync(ticketId);
            if (ticket == null)
                throw new InvalidOperationException("Phiếu không tồn tại.");

            if (ticket.Status != (byte)9)
                throw new InvalidOperationException("Phiếu phải ở trạng thái Hoàn tất.");

            if (ticket.ResolutionType != (byte)4)
                throw new InvalidOperationException("Chỉ sửa tính phí mới tạo hóa đơn.");

            if (await _invoiceRepository.InvoiceExistsForTicketAsync(ticketId))
                throw new InvalidOperationException("Hóa đơn cho phiếu này đã tồn tại.");

            var quotations = await _quotationRepository.GetByTicketIdAsync(ticketId);
            var acceptedQuote = quotations.FirstOrDefault(q => q.Status == (byte)1);
            if (acceptedQuote == null)
                throw new InvalidOperationException("Không tìm thấy báo giá được duyệt.");

            await _unitOfWork.BeginTransactionAsync();
            try
            {
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
                    PaymentMethod = (byte)0,
                    PaymentStatus = (byte)0
                };

                await _invoiceRepository.AddAsync(invoice);
                await _unitOfWork.SaveChangesAsync();

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
                        UnitPrice = qItem.UnitPrice
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
            var ticket = await _ticketRepository.GetByIdAsync(ticketId);
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

        private void ValidateTransition(byte currentStatus, byte targetStatus)
        {
            var validTransitions = new[]
            {
                (0, 1), (0, 10),
                (1, 2), (1, 4), (1, 5), (1, 6), (1, 8), (1, 10),
                (2, 4), (2, 5), (2, 3), (2, 10),
                (4, 5), (4, 10),
                (5, 4), (5, 9), (5, 10),
                (6, 7), (6, 10),
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
                    }).ToList() ?? new List<ServiceTicketStatusHistoryDto>()
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
