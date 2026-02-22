using Microsoft.EntityFrameworkCore;
using HiveOrders.Api.Shared.Data;
using HiveOrders.Api.Shared.Infrastructure;

namespace HiveOrders.Api.Features.RecurringOrders;

public class RecurringOrderHandler : IRecurringOrderHandler
{
    private readonly ApplicationDbContext _db;
    private readonly ITenantContext _tenantContext;

    public RecurringOrderHandler(ApplicationDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<IReadOnlyList<RecurringOrderTemplateResponse>> GetMyTemplatesAsync(string userId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.TenantId;
        if (tenantId == null) return [];
        var templates = await _db.RecurringOrderTemplates
            .Where(t => t.TenantId == tenantId.Value && t.CreatedByUserId == userId)
            .OrderBy(t => t.RestaurantName)
            .ToListAsync(cancellationToken);

        return templates.Select(Map).ToList();
    }

    public async Task<RecurringOrderTemplateResponse?> GetByIdAsync(int id, string userId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.TenantId;
        if (tenantId == null) return null;
        var template = await _db.RecurringOrderTemplates
            .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tenantId.Value && t.CreatedByUserId == userId, cancellationToken);

        return template == null ? null : Map(template);
    }

    public async Task<RecurringOrderTemplateResponse?> CreateAsync(CreateRecurringOrderTemplateRequest request, string userId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.TenantId ?? throw new UnauthorizedAccessException("Tenant context required.");
        var template = new RecurringOrderTemplate
        {
            TenantId = tenantId,
            RestaurantName = request.RestaurantName,
            RestaurantUrl = request.RestaurantUrl,
            CronExpression = request.CronExpression,
            CreatedByUserId = userId,
            IsActive = true
        };

        _db.RecurringOrderTemplates.Add(template);
        await _db.SaveChangesAsync(cancellationToken);

        return Map(template);
    }

    public async Task<RecurringOrderTemplateResponse?> UpdateAsync(int id, UpdateRecurringOrderTemplateRequest request, string userId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.TenantId;
        if (tenantId == null) return null;
        var template = await _db.RecurringOrderTemplates
            .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tenantId.Value && t.CreatedByUserId == userId, cancellationToken);

        if (template == null)
            return null;

        if (request.RestaurantName != null) template.RestaurantName = request.RestaurantName;
        if (request.RestaurantUrl != null) template.RestaurantUrl = request.RestaurantUrl;
        if (request.CronExpression != null) template.CronExpression = request.CronExpression;
        if (request.IsActive.HasValue) template.IsActive = request.IsActive.Value;

        await _db.SaveChangesAsync(cancellationToken);
        return Map(template);
    }

    public async Task<bool> DeleteAsync(int id, string userId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.TenantId;
        if (tenantId == null) return false;
        var template = await _db.RecurringOrderTemplates
            .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tenantId.Value && t.CreatedByUserId == userId, cancellationToken);

        if (template == null)
            return false;

        _db.RecurringOrderTemplates.Remove(template);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static RecurringOrderTemplateResponse Map(RecurringOrderTemplate t) =>
        new(t.Id, t.RestaurantName, t.RestaurantUrl, t.CronExpression, t.CreatedByUserId, t.IsActive, t.NextRunAt);
}
