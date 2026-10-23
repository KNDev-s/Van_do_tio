using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using VanEscolar.Api.Data;
using VanEscolar.Api.Domain;
namespace VanEscolar.Api.Infrastructure;
public record TenantAccess(Guid TenantId, Guid UserId, string OperationName);
public static class Access {
  public static async Task<TenantAccess?> Resolve(HttpContext http, VanDb db) {
    if (!Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var uid)) return null;
    // Never accept tenant identifiers from the browser as authorization evidence.
    var membership = await db.Memberships.AsNoTracking().Where(x => x.UserId == uid && x.Active && x.Role == "TenantOwner")
      .OrderBy(x => x.Id).FirstOrDefaultAsync();
    if (membership is null) return null;
    var tenant = await db.Tenants.AsNoTracking().SingleOrDefaultAsync(x => x.Id == membership.TenantId);
    return tenant is null ? null : new TenantAccess(tenant.Id, uid, tenant.DisplayName);
  }
  public static IResult Denied() => Results.Problem(title: "Acesso negado", detail: "Você não possui acesso a esta operação.", statusCode: 403);
  public static IResult Invalid(string message) => Results.Problem(title: "Dados inválidos", detail: message, statusCode: 400);
  public static IResult Missing() => Results.Problem(title: "Não encontrado", detail: "Registro não encontrado nesta operação.", statusCode: 404);
  public static IResult Conflict(string message) => Results.Problem(title: "Conflito", detail: message, statusCode: 409);
  public static void Audit(VanDb db, TenantAccess access, string action, string type, Guid id) => db.Audits.Add(new AuditEvent { TenantId = access.TenantId, UserId = access.UserId, Action = action, EntityType = type, EntityId = id });
}
