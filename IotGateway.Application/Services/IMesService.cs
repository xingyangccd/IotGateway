using IotGateway.Application.DTOs;

namespace IotGateway.Application.Services;

public interface IMesService
{
    Task<IReadOnlyList<MesProductDto>> GetProductsAsync();
    Task<MesProductDto> SaveProductAsync(long? id, MesProductRequest request);
    Task DeleteProductAsync(long id);
    Task<IReadOnlyList<MesLineDto>> GetLinesAsync();
    Task<MesLineDto> SaveLineAsync(long? id, MesLineRequest request);
    Task DeleteLineAsync(long id);
    Task<MesPageDto<MesOrderDto>> GetOrdersAsync(int page, int pageSize, string? keyword, string? status);
    Task<MesOrderDto> GetOrderAsync(long id);
    Task<MesOrderDto> SaveOrderAsync(long? id, MesOrderRequest request, string operatorName);
    Task DeleteOrderAsync(long id, string operatorName);
    Task<MesOrderDto> TransitionAsync(long id, MesTransitionRequest request, string operatorName);
    Task<MesReportDto> ReportAsync(long id, MesReportRequest request, string operatorName);
    Task<IReadOnlyList<MesReportDto>> GetReportsAsync(long orderId);
    Task<IReadOnlyList<MesInspectionDto>> GetInspectionsAsync(long? orderId);
    Task<MesInspectionDto> InspectAsync(MesInspectionRequest request, string operatorName);
    Task<MesTraceDto> TraceAsync(string? batchNo);
    Task<MesDashboardDto> GetDashboardAsync();
}
