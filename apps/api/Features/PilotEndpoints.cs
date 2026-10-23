using System.Data;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using VanEscolar.Api.Data;
using VanEscolar.Api.Domain;
using VanEscolar.Api.Infrastructure;
namespace VanEscolar.Api.Features;
public static class PilotEndpoints {
  public static void MapPilotFeatures(this WebApplication app) {
    var g = app.MapGroup("/api/v1").RequireAuthorization();
    g.MapGet("/schools", async (HttpContext h, VanDb db) => {
      var a = await Access.Resolve(h, db); if (a is null) return Access.Denied();
      return Results.Ok(await db.Schools.AsNoTracking().Where(x => x.TenantId == a.TenantId).OrderBy(x => x.Name)
        .Select(x => new { x.Id, x.Name, x.Address, x.Active }).ToListAsync());
    });
    g.MapPost("/schools", async (SchoolInput input, HttpContext h, VanDb db) => {
      var a = await Access.Resolve(h, db); if (a is null) return Access.Denied();
      var name = input.Name?.Trim() ?? ""; var address = input.Address?.Trim() ?? "";
      if (name.Length is < 2 or > 120 || address.Length is < 4 or > 240) return Access.Invalid("Informe nome e endereço válidos da escola.");
      if (await db.Schools.AnyAsync(x => x.TenantId == a.TenantId && x.Name.ToLower() == name.ToLower())) return Access.Conflict("Esta escola já está cadastrada.");
      var item = new School { TenantId = a.TenantId, Name = name, Address = address };
      db.Schools.Add(item); Access.Audit(db, a, "Create", "School", item.Id); await db.SaveChangesAsync();
      return Results.Created($"/api/v1/schools/{item.Id}", new { item.Id, item.Name, item.Address, item.Active });
    });
    g.MapGet("/students", async (HttpContext h, VanDb db) => {
      var a = await Access.Resolve(h, db); if (a is null) return Access.Denied();
      var records = await (from s in db.Students.AsNoTracking() join school in db.Schools.AsNoTracking() on s.SchoolId equals school.Id
        where s.TenantId == a.TenantId && school.TenantId == a.TenantId
        orderby s.FullName select new { s.Id, s.FullName, s.SchoolId, SchoolName = school.Name, s.Shift, s.PickupAddress, s.DropoffAddress, s.Active }).ToListAsync();
      var guardLinks = await (from sg in db.StudentGuardians.AsNoTracking() join gd in db.Guardians.AsNoTracking() on sg.GuardianId equals gd.Id
        where sg.TenantId == a.TenantId && gd.TenantId == a.TenantId
        select new { sg.StudentId, gd.FullName, gd.Phone }).ToListAsync();
      var guardianByStudent = guardLinks.GroupBy(x => x.StudentId).ToDictionary(x => x.Key, x => x.First());
      return Results.Ok(records.Select(s => new { s.Id, s.FullName, s.SchoolId, s.SchoolName, s.Shift, s.PickupAddress, s.DropoffAddress, s.Active,
        GuardianName = guardianByStudent.GetValueOrDefault(s.Id)?.FullName ?? "", GuardianPhone = guardianByStudent.GetValueOrDefault(s.Id)?.Phone ?? "" }));
    });
    g.MapPost("/students", async (StudentInput input, HttpContext h, VanDb db) => {
      var a = await Access.Resolve(h, db); if (a is null) return Access.Denied();
      if (input.FullName?.Trim().Length is not >= 3 or > 150 || input.GuardianName?.Trim().Length is not >= 3 or > 150 || input.GuardianPhone?.Trim().Length is not >= 8 or > 30 ||
          input.PickupAddress?.Trim().Length is not >= 4 or > 240 || input.DropoffAddress?.Trim().Length is not >= 4 or > 240 ||
          input.Shift is not ("Manhã" or "Tarde" or "Integral" or "Noite")) return Access.Invalid("Verifique os dados do aluno e responsável.");
      var school = await db.Schools.AsNoTracking().SingleOrDefaultAsync(x => x.Id == input.SchoolId && x.TenantId == a.TenantId && x.Active);
      if (school is null) return Access.Invalid("Escola inválida.");
      await using var tx = await db.Database.BeginTransactionAsync();
      var s = new Student { TenantId = a.TenantId, SchoolId = school.Id, FullName = input.FullName.Trim(), Shift = input.Shift,
        PickupAddress = input.PickupAddress.Trim(), DropoffAddress = input.DropoffAddress.Trim() };
      var guardian = new Guardian { TenantId = a.TenantId, FullName = input.GuardianName.Trim(), Phone = input.GuardianPhone.Trim() };
      db.Students.Add(s); db.Guardians.Add(guardian);
      db.StudentGuardians.Add(new StudentGuardian { TenantId = a.TenantId, StudentId = s.Id, GuardianId = guardian.Id });
      Access.Audit(db, a, "Create", "Student", s.Id); await db.SaveChangesAsync(); await tx.CommitAsync();
      return Results.Created($"/api/v1/students/{s.Id}", new { s.Id, s.FullName, s.SchoolId, SchoolName = school.Name, s.Shift, s.PickupAddress, s.DropoffAddress, GuardianName = guardian.FullName, GuardianPhone = guardian.Phone, s.Active });
    });
    g.MapGet("/contracts", async (HttpContext h, VanDb db) => {
      var a = await Access.Resolve(h, db); if (a is null) return Access.Denied();
      return Results.Ok(await (from c in db.Contracts.AsNoTracking() join s in db.Students.AsNoTracking() on c.StudentId equals s.Id
        where c.TenantId == a.TenantId && s.TenantId == a.TenantId orderby s.FullName
        select new { c.Id, c.StudentId, StudentName = s.FullName, c.MonthlyAmount, c.DueDay, c.StartsOn, c.Installments, c.Status }).ToListAsync());
    });
    g.MapPost("/contracts", async (ContractInput input, HttpContext h, VanDb db) => {
      var a = await Access.Resolve(h, db); if (a is null) return Access.Denied();
      if (input.MonthlyAmount <= 0 || input.MonthlyAmount > 999999 || decimal.Round(input.MonthlyAmount, 2) != input.MonthlyAmount || input.DueDay is < 1 or > 28 || input.Installments is < 1 or > 36 ||
          input.StartsOn < new DateOnly(2020,1,1) || input.StartsOn > DateOnly.FromDateTime(DateTime.UtcNow.AddYears(2))) return Access.Invalid("Verifique valor, vencimento, data de início e parcelas.");
      if (!await db.Students.AnyAsync(x => x.Id == input.StudentId && x.TenantId == a.TenantId && x.Active)) return Access.Invalid("Aluno não encontrado.");
      if (await db.Contracts.AnyAsync(x => x.StudentId == input.StudentId && x.TenantId == a.TenantId && x.Status == "Active")) return Access.Conflict("Aluno já possui contrato ativo.");
      var c = new TransportContract { TenantId = a.TenantId, StudentId = input.StudentId, MonthlyAmount = input.MonthlyAmount,
        DueDay = input.DueDay, StartsOn = input.StartsOn, Installments = input.Installments };
      db.Contracts.Add(c); Access.Audit(db, a, "Create", "Contract", c.Id); await db.SaveChangesAsync();
      return Results.Created($"/api/v1/contracts/{c.Id}", new { c.Id, c.StudentId, c.MonthlyAmount, c.DueDay, c.StartsOn, c.Installments, c.Status });
    });
    g.MapPost("/billing/generate", async (MonthInput input, HttpContext h, VanDb db) => {
      var a = await Access.Resolve(h, db); if (a is null) return Access.Denied();
      if (!TryMonth(input.Month, out var month)) return Access.Invalid("Competência inválida. Use AAAA-MM.");
      if (month < new DateOnly(2020,1,1) || month > new DateOnly(DateTime.UtcNow.Year + 2,12,1)) return Access.Invalid("Competência fora da janela permitida.");
      var contracts = await db.Contracts.AsNoTracking().Where(x => x.TenantId == a.TenantId && x.Status == "Active").ToListAsync();
      int created = 0;
      await using var tx = await db.Database.BeginTransactionAsync();
      foreach (var c in contracts) {
        var begin = new DateOnly(c.StartsOn.Year, c.StartsOn.Month, 1);
        if (month < begin || month >= begin.AddMonths(c.Installments)) continue;
        if (await db.Charges.AnyAsync(x => x.TenantId == a.TenantId && x.ContractId == c.Id && x.Competence == month)) continue;
        var due = new DateOnly(month.Year, month.Month, c.DueDay);
        var ch = new Charge { TenantId = a.TenantId, ContractId = c.Id, StudentId = c.StudentId, Competence = month, DueDate = due,
          Amount = c.MonthlyAmount, Status = due < LocalDates.Today() ? "Overdue" : "Open" };
        db.Charges.Add(ch); Access.Audit(db, a, "Generate", "Charge", ch.Id); created++;
      }
      try { await db.SaveChangesAsync(); await tx.CommitAsync(); }
      catch (DbUpdateException e) when (e.InnerException is PostgresException p && p.SqlState == PostgresErrorCodes.UniqueViolation) { return Access.Conflict("Outra geração simultânea foi detectada. Atualize a página."); }
      return Results.Ok(new { created });
    });
    g.MapGet("/charges", async (string month, HttpContext h, VanDb db) => {
      var a = await Access.Resolve(h, db); if (a is null) return Access.Denied();
      if (!TryMonth(month, out var m)) return Access.Invalid("Mês inválido.");
      var charges = await (from c in db.Charges.AsNoTracking() join s in db.Students.AsNoTracking() on c.StudentId equals s.Id
        where c.TenantId == a.TenantId && s.TenantId == a.TenantId && c.Competence == m
        orderby c.DueDate, s.FullName select new { c.Id, c.StudentId, StudentName = s.FullName, c.Competence, c.DueDate, c.Amount, c.Status }).ToListAsync();
      var ids = charges.Select(c => c.Id).ToArray();
      var totals = await db.Payments.AsNoTracking().Where(p => p.TenantId == a.TenantId && ids.Contains(p.ChargeId) && !p.Reversed)
        .GroupBy(p => p.ChargeId).Select(gr => new { Id = gr.Key, Total = gr.Sum(x => x.Amount) }).ToDictionaryAsync(x => x.Id, x => x.Total);
      return Results.Ok(charges.Select(c => new { c.Id, c.StudentId, c.StudentName, c.Competence, c.DueDate, c.Amount,
        PaidAmount = totals.GetValueOrDefault(c.Id), Status = c.Status == "Open" && c.DueDate < LocalDates.Today() ? "Overdue" : c.Status }));
    });
    g.MapPost("/charges/{id:guid}/payments", async (Guid id, PaymentInput input, HttpContext h, VanDb db) => {
      var a = await Access.Resolve(h, db); if (a is null) return Access.Denied();
      if (input.Amount <= 0 || input.Amount > 999999 || decimal.Round(input.Amount, 2) != input.Amount || input.IdempotencyKey?.Length is < 8 or > 100 ||
          input.Method is not ("PIX" or "Dinheiro" or "Transferência" or "Cartão" or "Outro")) return Access.Invalid("Pagamento inválido.");
      // Identical retries return the existing payment; distinct concurrent attempts serialize on the charge row.
      var existing = await db.Payments.AsNoTracking().SingleOrDefaultAsync(p => p.TenantId == a.TenantId && p.IdempotencyKey == input.IdempotencyKey);
      if (existing is not null) return existing.ChargeId == id && existing.Amount == input.Amount ? Results.Ok(new { existing.Id }) : Access.Conflict("Chave de pagamento reutilizada com outros dados.");
      await using var tx = await db.Database.BeginTransactionAsync();
      // Lock the charge row to keep two simultaneous payments from overpaying the same charge.
      var charge = await db.Charges.FromSqlInterpolated($"SELECT * FROM \"Charges\" WHERE \"Id\" = {id} AND \"TenantId\" = {a.TenantId} FOR UPDATE")
        .SingleOrDefaultAsync();
      if (charge is null) return Access.Missing();
      if (charge.Status is "Cancelled" or "Waived") return Access.Conflict("Cobrança cancelada ou dispensada.");
      decimal already = await db.Payments.Where(x => x.TenantId == a.TenantId && x.ChargeId == id && !x.Reversed).SumAsync(x => (decimal?)x.Amount) ?? 0;
      if (already >= charge.Amount || input.Amount > charge.Amount - already) return Access.Conflict("Valor recebido excede o saldo em aberto.");
      var p = new Payment { TenantId = a.TenantId, ChargeId = id, Amount = input.Amount, Method = input.Method,
        IdempotencyKey = input.IdempotencyKey, CreatedByUserId = a.UserId };
      db.Payments.Add(p);
      if (already + input.Amount == charge.Amount) charge.Status = "Paid";
      Access.Audit(db, a, "Receive", "Payment", p.Id);
      try { await db.SaveChangesAsync(); await tx.CommitAsync(); }
      catch (DbUpdateException e) when (e.InnerException is PostgresException pg && pg.SqlState == PostgresErrorCodes.UniqueViolation) { return Access.Conflict("Pagamento já enviado. Atualize a página."); }
      return Results.Ok(new { p.Id });
    });
    g.MapGet("/expenses", async (string month, HttpContext h, VanDb db) => {
      var a = await Access.Resolve(h, db); if (a is null) return Access.Denied();
      if (!TryMonth(month, out var m)) return Access.Invalid("Mês inválido.");
      var end = m.AddMonths(1);
      return Results.Ok(await db.Expenses.AsNoTracking().Where(x => x.TenantId == a.TenantId && x.Date >= m && x.Date < end && x.Status == "Active")
        .OrderByDescending(x => x.Date).Select(x => new { x.Id, x.Category, x.Description, x.Amount, x.Date, x.Status }).ToListAsync());
    });
    g.MapPost("/expenses", async (ExpenseInput input, HttpContext h, VanDb db) => {
      var a = await Access.Resolve(h, db); if (a is null) return Access.Denied();
      string[] categories = ["Combustível", "Manutenção", "Seguro", "Impostos e taxas", "Pedágio/estacionamento", "Limpeza", "Administrativo", "Outros"];
      if (!categories.Contains(input.Category) || input.Amount <= 0 || input.Amount > 999999 || decimal.Round(input.Amount, 2) != input.Amount || input.Description?.Trim().Length is not >= 3 or > 240 || input.Date < new DateOnly(2020,1,1)) return Access.Invalid("Despesa inválida.");
      var e = new Expense { TenantId = a.TenantId, Category = input.Category, Description = input.Description.Trim(), Amount = input.Amount, Date = input.Date };
      db.Expenses.Add(e); Access.Audit(db, a, "Create", "Expense", e.Id); await db.SaveChangesAsync();
      return Results.Created($"/api/v1/expenses/{e.Id}", new { e.Id, e.Category, e.Description, e.Amount, e.Date, e.Status });
    });
    g.MapGet("/absences", async (DateOnly date, HttpContext h, VanDb db) => {
      var a = await Access.Resolve(h, db); if (a is null) return Access.Denied();
      return Results.Ok(await (from ab in db.Absences.AsNoTracking() join s in db.Students.AsNoTracking() on ab.StudentId equals s.Id
        where ab.TenantId == a.TenantId && s.TenantId == a.TenantId && ab.Date == date orderby s.FullName
        select new { ab.Id, ab.StudentId, StudentName = s.FullName, ab.Date, ab.Period, ab.Reason, Acknowledged = ab.AcknowledgedAtUtc != null }).ToListAsync());
    });
    g.MapPost("/absences", async (AbsenceInput input, HttpContext h, VanDb db) => {
      var a = await Access.Resolve(h, db); if (a is null) return Access.Denied();
      if (input.Period is not ("Dia inteiro" or "Ida" or "Volta") || input.Reason?.Length > 250 || input.Date < new DateOnly(2020,1,1)) return Access.Invalid("Ausência inválida.");
      if (!await db.Students.AnyAsync(x => x.TenantId == a.TenantId && x.Id == input.StudentId && x.Active)) return Access.Invalid("Aluno inválido.");
      var notice = new AbsenceNotice { TenantId = a.TenantId, StudentId = input.StudentId, Date = input.Date, Period = input.Period, Reason = input.Reason?.Trim() ?? "", CreatedByUserId = a.UserId };
      db.Absences.Add(notice); Access.Audit(db, a, "Create", "Absence", notice.Id); await db.SaveChangesAsync();
      return Results.Created($"/api/v1/absences/{notice.Id}", new { notice.Id, notice.StudentId, notice.Date, notice.Period, notice.Reason, Acknowledged = false });
    });
    g.MapPost("/absences/{id:guid}/acknowledge", async (Guid id, HttpContext h, VanDb db) => {
      var a = await Access.Resolve(h, db); if (a is null) return Access.Denied();
      var notice = await db.Absences.SingleOrDefaultAsync(x => x.TenantId == a.TenantId && x.Id == id);
      if (notice is null) return Access.Missing();
      notice.AcknowledgedAtUtc ??= DateTime.UtcNow;
      Access.Audit(db, a, "Acknowledge", "Absence", id); await db.SaveChangesAsync();
      return Results.NoContent();
    });
    g.MapGet("/dashboard/summary", async (string month, HttpContext h, VanDb db) => {
      var a = await Access.Resolve(h, db); if (a is null) return Access.Denied();
      if (!TryMonth(month, out var m)) return Access.Invalid("Mês inválido.");
      var end = m.AddMonths(1); var startUtc = new DateTime(m.Year, m.Month, 1, 0, 0, 0, DateTimeKind.Utc);
      var endUtc = startUtc.AddMonths(1); var today = LocalDates.Today();
      var issued = await db.Charges.AsNoTracking().Where(c => c.TenantId == a.TenantId && c.Competence == m && c.Status != "Cancelled" && c.Status != "Waived").ToListAsync();
      var ids = issued.Select(c => c.Id).ToArray();
      var sums = await db.Payments.AsNoTracking().Where(p => p.TenantId == a.TenantId && !p.Reversed && ids.Contains(p.ChargeId))
        .GroupBy(p => p.ChargeId).Select(g => new { g.Key, Total = g.Sum(p => p.Amount) }).ToDictionaryAsync(x => x.Key, x => x.Total);
      decimal expected = issued.Sum(c => c.Amount);
      decimal remaining = issued.Sum(c => Math.Max(0, c.Amount - sums.GetValueOrDefault(c.Id)));
      decimal overdue = issued.Where(c => c.DueDate < today).Sum(c => Math.Max(0, c.Amount - sums.GetValueOrDefault(c.Id)));
      decimal received = await db.Payments.AsNoTracking().Where(p => p.TenantId == a.TenantId && !p.Reversed && p.PaidAtUtc >= startUtc && p.PaidAtUtc < endUtc).SumAsync(p => (decimal?)p.Amount) ?? 0;
      decimal expenses = await db.Expenses.AsNoTracking().Where(e => e.TenantId == a.TenantId && e.Date >= m && e.Date < end && e.Status == "Active").SumAsync(e => (decimal?)e.Amount) ?? 0;
      int activeStudents = await db.Students.CountAsync(x => x.TenantId == a.TenantId && x.Active);
      int activeSchools = await db.Schools.CountAsync(x => x.TenantId == a.TenantId && x.Active);
      int absencesToday = await db.Absences.CountAsync(x => x.TenantId == a.TenantId && x.Date == today);
      return Results.Ok(new { activeStudents, activeSchools, expected, received, outstanding = remaining, overdue, expenses, balance = received - expenses, absencesToday });
    });
  }
  private static bool TryMonth(string raw, out DateOnly result) => DateOnly.TryParseExact(raw + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out result);
}
public sealed record SchoolInput(string Name, string Address);
public sealed record StudentInput(string FullName, Guid SchoolId, string Shift, string PickupAddress, string DropoffAddress, string GuardianName, string GuardianPhone);
public sealed record ContractInput(Guid StudentId, decimal MonthlyAmount, int DueDay, DateOnly StartsOn, int Installments);
public sealed record MonthInput(string Month);
public sealed record PaymentInput(decimal Amount, string Method, string IdempotencyKey);
public sealed record ExpenseInput(string Category, string Description, decimal Amount, DateOnly Date);
public sealed record AbsenceInput(Guid StudentId, DateOnly Date, string Period, string? Reason);
