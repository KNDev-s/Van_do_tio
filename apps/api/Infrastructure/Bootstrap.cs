using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using VanEscolar.Api.Data;
using VanEscolar.Api.Domain;
namespace VanEscolar.Api.Infrastructure;
public static class Bootstrap {
  public static async Task CreateOwner(IServiceProvider services) {
    using var scope = services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<VanDb>();
    var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
    string email = Environment.GetEnvironmentVariable("BOOTSTRAP_ADMIN_EMAIL")?.Trim() ?? "";
    string password = Environment.GetEnvironmentVariable("BOOTSTRAP_ADMIN_PASSWORD") ?? "";
    string operation = Environment.GetEnvironmentVariable("BOOTSTRAP_OPERATION_NAME")?.Trim() ?? "Van Escolar Piloto";
    if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password) || password.Length < 12)
      throw new InvalidOperationException("Configure BOOTSTRAP_ADMIN_EMAIL, BOOTSTRAP_ADMIN_PASSWORD (mínimo 12 caracteres) e BOOTSTRAP_OPERATION_NAME.");
    if (await db.Tenants.AnyAsync()) throw new InvalidOperationException("A operação já existe. Bootstrap é permitido uma única vez.");
    await using var tx = await db.Database.BeginTransactionAsync();
    var owner = new AppUser { UserName = email, Email = email, EmailConfirmed = true }; // Operator provisioned the pilot owner out of band.
    var result = await users.CreateAsync(owner, password);
    if (!result.Succeeded) throw new InvalidOperationException(string.Join("; ", result.Errors.Select(x => x.Description)));
    var tenant = new Tenant { DisplayName = operation };
    db.Tenants.Add(tenant);
    db.Memberships.Add(new Membership { TenantId = tenant.Id, UserId = owner.Id });
    await db.SaveChangesAsync(); await tx.CommitAsync();
    Console.WriteLine("Proprietário do piloto provisionado. Remova as variáveis BOOTSTRAP_* do ambiente após executar.");
  }
}
