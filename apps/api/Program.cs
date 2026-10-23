using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using VanEscolar.Api.Data;
using VanEscolar.Api.Domain;
using VanEscolar.Api.Features;
using VanEscolar.Api.Infrastructure;
var bootstrapOwner = args.Contains("--bootstrap-owner", StringComparer.Ordinal);
var builder = WebApplication.CreateBuilder(args.Where(x => x != "--bootstrap-owner").ToArray());
var connection = builder.Configuration.GetConnectionString("MainDb") ?? throw new InvalidOperationException("ConnectionStrings__MainDb obrigatório.");
builder.Services.AddDbContext<VanDb>(o => o.UseNpgsql(connection));
builder.Services.AddIdentity<AppUser, IdentityRole<Guid>>(options => {
  options.User.RequireUniqueEmail = true;
  options.Password.RequiredLength = 12;
  options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
  options.Lockout.MaxFailedAccessAttempts = 5;
  options.SignIn.RequireConfirmedEmail = true;
}).AddEntityFrameworkStores<VanDb>().AddDefaultTokenProviders();
builder.Services.ConfigureApplicationCookie(cookie => {
  cookie.Cookie.Name = builder.Environment.IsProduction() ? "__Host-VanSession" : "VanSessionDev";
  cookie.Cookie.HttpOnly = true; cookie.Cookie.SameSite = SameSiteMode.Lax;
  cookie.Cookie.SecurePolicy = builder.Environment.IsProduction() ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
  cookie.Cookie.Path = "/";
  cookie.SlidingExpiration = true;
  cookie.ExpireTimeSpan = TimeSpan.FromHours(10);
  // Preserve Identity's own security-stamp validation callbacks.
  cookie.Events.OnRedirectToLogin = c => { c.Response.StatusCode = 401; return Task.CompletedTask; };
  cookie.Events.OnRedirectToAccessDenied = c => { c.Response.StatusCode = 403; return Task.CompletedTask; };
});
builder.Services.AddAuthorization();
builder.Services.AddAntiforgery(o => {
  o.HeaderName = "X-CSRF-TOKEN";
  o.Cookie.Name = builder.Environment.IsProduction() ? "__Host-VanCSRF" : "VanCSRFDev";
  o.Cookie.SecurePolicy = builder.Environment.IsProduction() ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
  o.Cookie.HttpOnly = true; o.Cookie.SameSite = SameSiteMode.Lax;
});
builder.Services.AddProblemDetails();
builder.Services.AddRateLimiter(o => {
  o.RejectionStatusCode = 429;
  o.AddPolicy("login", ctx => RateLimitPartition.GetFixedWindowLimiter(ctx.Connection.RemoteIpAddress?.ToString() ?? "anonymous", _ => new FixedWindowRateLimiterOptions { PermitLimit = 8, Window = TimeSpan.FromMinutes(10), QueueLimit = 0 }));
});
var app = builder.Build();
if (bootstrapOwner) { await Bootstrap.CreateOwner(app.Services); return; }
if (!app.Environment.IsDevelopment()) { app.UseHsts(); app.UseHttpsRedirection(); }
app.UseExceptionHandler();
app.Use(async (ctx, next) => {
  if (ctx.Request.Path.StartsWithSegments("/api")) ctx.Response.Headers.CacheControl = "no-store";
  await next();
});
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
// SPA JSON mutations are CSRF-protected, including login and logout.
app.Use(async (ctx, next) => {
  if (ctx.Request.Path.StartsWithSegments("/api") && !HttpMethods.IsGet(ctx.Request.Method) && !HttpMethods.IsHead(ctx.Request.Method) && !HttpMethods.IsOptions(ctx.Request.Method)) {
    var anti = ctx.RequestServices.GetRequiredService<IAntiforgery>();
    if (!await anti.IsRequestValidAsync(ctx)) {
      ctx.Response.StatusCode = 400;
      await ctx.Response.WriteAsJsonAsync(new { title = "Requisição bloqueada", detail = "Token de proteção inválido. Atualize a página e tente novamente." });
      return;
    }
  }
  await next();
});
app.MapGet("/health/live", () => Results.Ok(new { status = "live" }));
app.MapGet("/health/ready", async (VanDb db) => await db.Database.CanConnectAsync() ? Results.Ok(new { status = "ready" }) : Results.StatusCode(503));
app.MapGet("/api/v1/auth/antiforgery", (IAntiforgery anti, HttpContext ctx) => {
  ctx.Response.Headers.CacheControl = "no-store";
  return Results.Ok(new { token = anti.GetAndStoreTokens(ctx).RequestToken });
}).AllowAnonymous();
app.MapPost("/api/v1/auth/login", async (LoginInput input, SignInManager<AppUser> signIn, UserManager<AppUser> users, VanDb db, HttpContext ctx) => {
  if (string.IsNullOrWhiteSpace(input.Email) || string.IsNullOrWhiteSpace(input.Password)) return Access.Invalid("Informe e-mail e senha.");
  var user = await users.FindByEmailAsync(input.Email.Trim());
  if (user is null) return Results.Problem(title: "Falha no login", detail: "E-mail ou senha inválidos.", statusCode: 401);
  var result = await signIn.PasswordSignInAsync(user, input.Password, isPersistent: false, lockoutOnFailure: true);
  if (result.IsLockedOut) return Results.Problem(title: "Acesso temporariamente bloqueado", detail: "Tente novamente mais tarde.", statusCode: 429);
  if (!result.Succeeded) return Results.Problem(title: "Falha no login", detail: "E-mail ou senha inválidos.", statusCode: 401);
  // Read access from database, not request payload.
  var membership = await db.Memberships.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == user.Id && x.Role == "TenantOwner" && x.Active);
  var tenant = membership is null ? null : await db.Tenants.AsNoTracking().SingleOrDefaultAsync(x => x.Id == membership.TenantId);
  if (tenant is null) { await signIn.SignOutAsync(); return Access.Denied(); }
  ctx.Response.Headers.CacheControl = "no-store";
  return Results.Ok(new { email = user.Email, operationName = tenant.DisplayName });
}).AllowAnonymous().RequireRateLimiting("login");
app.MapGet("/api/v1/auth/me", async (HttpContext ctx, VanDb db) => {
  var access = await Access.Resolve(ctx, db); if (access is null) return Access.Denied();
  ctx.Response.Headers.CacheControl = "no-store";
  return Results.Ok(new { email = ctx.User.Identity?.Name, operationName = access.OperationName });
}).RequireAuthorization();
app.MapPost("/api/v1/auth/logout", async (SignInManager<AppUser> signIn) => { await signIn.SignOutAsync(); return Results.NoContent(); }).RequireAuthorization();
app.MapPilotFeatures();
app.Run();
public sealed record LoginInput(string Email, string Password);
