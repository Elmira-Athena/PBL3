using PBL3.Core.Interfaces;
using PBL3.Infrastructure.Data;
using PBL3.Infrastructure.Repositories;

namespace PBL3.API.Extensions;

public static class RepositoryExtensions
{
    public static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IManufacturerRepository, ManufacturerRepository>();
        services.AddScoped<ISupplierRepository, SupplierRepository>();
        services.AddScoped<IImportReceiptRepository, ImportReceiptRepository>();
        services.AddScoped<IProductSerialRepository, ProductSerialRepository>();
        services.AddScoped<IInventoryCheckRepository, InventoryCheckRepository>();
        services.AddScoped<IWarrantyRepository, WarrantyRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IVoucherRepository, VoucherRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IEmployeeRepository, EmployeeRepository>();
        services.AddScoped<ICartRepository, CartRepository>();
        services.AddScoped<IUserAddressRepository, UserAddressRepository>();
        services.AddScoped<IProductReviewRepository, ProductReviewRepository>();
        services.AddScoped<IServiceTicketRepository, ServiceTicketRepository>();
        services.AddScoped<IQuotationRepository, QuotationRepository>();
        services.AddScoped<IRmaShipmentRepository, RmaShipmentRepository>();
        services.AddScoped<IServiceInvoiceRepository, ServiceInvoiceRepository>();
        services.AddScoped<ISerialRepairLogRepository, SerialRepairLogRepository>();
        services.AddScoped<IBannerRepository, BannerRepository>();

        // Unit of Work
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        return services;
    }
}
