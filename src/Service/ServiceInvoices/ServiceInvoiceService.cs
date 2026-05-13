using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PBL3.Core.Interfaces;
using PBL3.Shared.DTOs.ServiceTickets;

namespace PBL3.Service.ServiceInvoices
{
    public class ServiceInvoiceService : IServiceInvoiceService
    {
        private readonly IServiceInvoiceRepository _repository;

        public ServiceInvoiceService(IServiceInvoiceRepository repository)
        {
            _repository = repository;
        }

        public async Task<ServiceInvoiceDetailDto?> GetByIdAsync(int id)
        {
            var invoice = await _repository.GetByIdWithDetailsAsync(id);
            return invoice == null ? null : MapToDetailDto(invoice);
        }

        public async Task<ServiceInvoiceDetailDto?> GetByTicketIdAsync(int ticketId)
        {
            var invoice = await _repository.GetByTicketIdAsync(ticketId);
            return invoice == null ? null : MapToDetailDto(invoice);
        }

        public async Task<(List<ServiceInvoiceListDto> Items, int TotalCount)> GetPagedListAsync(
            string? keyword, int pageNumber, int pageSize, string? sortBy, bool sortDescending)
        {
            var (items, totalCount) = await _repository.GetPagedListAsync(keyword, pageNumber, pageSize, sortBy, sortDescending);
            var dtos = items.Select(MapToListDto).ToList();
            return (dtos, totalCount);
        }

        private ServiceInvoiceDetailDto MapToDetailDto(dynamic invoice)
        {
            var items = new List<ServiceInvoiceItemDto>();
            if (invoice.Items != null)
            {
                foreach (var i in invoice.Items)
                {
                    items.Add(new ServiceInvoiceItemDto
                    {
                        Id = i.Id,
                        Description = i.Description,
                        Quantity = i.Quantity,
                        UnitPrice = i.UnitPrice,
                        LineTotal = i.LineTotal
                    });
                }
            }

            return new ServiceInvoiceDetailDto
            {
                Id = invoice.Id,
                InvoiceCode = invoice.InvoiceCode,
                TicketId = invoice.TicketId,
                QuotationId = invoice.QuotationId,
                IssuedDate = invoice.IssuedDate,
                IssuedByEmployeeId = invoice.IssuedByEmployeeId,
                LaborCost = invoice.LaborCost,
                PartsTotal = invoice.PartsTotal,
                GrandTotal = invoice.GrandTotal,
                PaymentMethod = invoice.PaymentMethod,
                PaymentStatus = invoice.PaymentStatus,
                Note = invoice.Note,
                Items = items
            };
        }

        private ServiceInvoiceListDto MapToListDto(dynamic invoice)
        {
            return new ServiceInvoiceListDto
            {
                Id = invoice.Id,
                InvoiceCode = invoice.InvoiceCode,
                TicketId = invoice.TicketId,
                IssuedDate = invoice.IssuedDate,
                GrandTotal = invoice.GrandTotal,
                PaymentStatus = invoice.PaymentStatus
            };
        }
    }
}
