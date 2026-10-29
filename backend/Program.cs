using System.Data;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Services.AddOpenApi();
builder.Services.AddHttpClient("diagnosis", client => client.Timeout = TimeSpan.FromSeconds(5));
builder.Services.AddDbContext<WorkshopDbContext>(options => options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"), sql => sql.CommandTimeout(10).EnableRetryOnFailure(2, TimeSpan.FromSeconds(2), null)).ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning)));
builder.Services.AddIdentityCore<ApplicationUser>(options =>
{
    options.Password.RequiredLength = 10;
    options.Password.RequireDigit = true;
    options.Password.RequireUppercase = true;
    options.User.RequireUniqueEmail = true;
}).AddRoles<IdentityRole<int>>().AddEntityFrameworkStores<WorkshopDbContext>().AddSignInManager();
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = IdentityConstants.ApplicationScheme;
    options.DefaultChallengeScheme = IdentityConstants.ApplicationScheme;
}).AddCookie(IdentityConstants.ApplicationScheme, options =>
{
    options.Cookie.Name = "amcotts.auth";
    options.LoginPath = "/api/auth/login";
    options.Events.OnRedirectToLogin = context => { context.Response.StatusCode = StatusCodes.Status401Unauthorized; return Task.CompletedTask; };
    options.Events.OnRedirectToAccessDenied = context => { context.Response.StatusCode = StatusCodes.Status403Forbidden; return Task.CompletedTask; };
});
builder.Services.AddProblemDetails();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy.WithOrigins(builder.Configuration["FrontendOrigin"] ?? "http://localhost:5173").AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

var app = builder.Build();
// Local startup applies the checked-in EF migrations from the compiled API.
// This keeps client startup independent of the EF command-line tool and NuGet availability.
await using (var migrationScope = app.Services.CreateAsyncScope())
{
    var database = migrationScope.ServiceProvider.GetRequiredService<WorkshopDbContext>();
    await database.Database.MigrateAsync();
    await database.Database.ExecuteSqlRawAsync("IF OBJECT_ID(N'[SystemSettings]', N'U') IS NULL CREATE TABLE [SystemSettings] ([SettingKey] nvarchar(120) NOT NULL CONSTRAINT [PK_SystemSettings] PRIMARY KEY, [SettingValue] nvarchar(4000) NOT NULL, [UpdatedUtc] datetime2 NOT NULL)");
}
await using (var authSeedScope = app.Services.CreateAsyncScope())
{
    var users = authSeedScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var roles = authSeedScope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<int>>>();
    var testUsers = new[]
    {
        (Email: "supervisor@amcotts.com", Password: "Amcotts.Sup2026!", DisplayName: "John Supervisor", Role: "Supervisor/Manager"),
        (Email: "mechanic@amcotts.com", Password: "Amcotts.Mech2026!", DisplayName: "Alex Mechanic", Role: "Mechanic"),
        (Email: "clerk@amcotts.com", Password: "Amcotts.Clerk2026!", DisplayName: "Grace Service Clerk", Role: "Booking Clerk"),
        (Email: "admin@amcotts.com", Password: "Amcotts.Admin2026!", DisplayName: "System Administrator", Role: "Administrator")
    };
    foreach (var seed in testUsers)
    {
        var role = await roles.FindByNameAsync(seed.Role);
        if (role is null)
        {
            var roleResult = await roles.CreateAsync(new IdentityRole<int>(seed.Role));
            if (!roleResult.Succeeded) throw new InvalidOperationException($"Could not create role {seed.Role}.");
        }
        var user = await users.FindByEmailAsync(seed.Email);
        if (user is null)
        {
            user = new ApplicationUser { UserName = seed.Email, Email = seed.Email, DisplayName = seed.DisplayName, EmailConfirmed = true };
            var userResult = await users.CreateAsync(user, seed.Password);
            if (!userResult.Succeeded) throw new InvalidOperationException($"Could not create test account {seed.Email}: {string.Join(", ", userResult.Errors.Select(x => x.Description))}");
        }
        if (!await users.IsInRoleAsync(user, seed.Role)) await users.AddToRoleAsync(user, seed.Role);
    }
}
if (builder.Configuration.GetValue<bool>("DemoData:Enabled"))
{
    await using var demoScope = app.Services.CreateAsyncScope();
    var demoDb = demoScope.ServiceProvider.GetRequiredService<WorkshopDbContext>();
    if (!await demoDb.Bookings.AnyAsync()) await DemoDataSeeder.SeedAsync(demoDb);
}
app.UseExceptionHandler();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
if (app.Environment.IsDevelopment()) app.MapOpenApi();

app.MapPost("/api/auth/bootstrap", async (BootstrapAdminRequest request, UserManager<ApplicationUser> users, RoleManager<IdentityRole<int>> roles, IConfiguration configuration, CancellationToken ct) =>
{
    var expected = configuration["Authentication:BootstrapSecret"];
    if (string.IsNullOrWhiteSpace(expected) || !CryptographicOperations.FixedTimeEquals(System.Text.Encoding.UTF8.GetBytes(expected), System.Text.Encoding.UTF8.GetBytes(request.BootstrapSecret ?? ""))) return Results.StatusCode(StatusCodes.Status403Forbidden);
    if (await users.Users.AnyAsync(ct)) return Results.Conflict(new { message = "Administrator bootstrap has already been completed." });
    var adminRole = new IdentityRole<int>("Administrator");
    var roleResult = await roles.CreateAsync(adminRole);
    if (!roleResult.Succeeded) return Results.BadRequest(roleResult.Errors);
    var user = new ApplicationUser { UserName = request.Email.Trim(), Email = request.Email.Trim(), DisplayName = request.DisplayName.Trim(), EmailConfirmed = true };
    var result = await users.CreateAsync(user, request.Password);
    if (!result.Succeeded) return Results.BadRequest(result.Errors);
    await users.AddToRoleAsync(user, adminRole.Name!);
    return Results.Created("/api/auth/users", new { user.Id, user.Email, role = adminRole.Name });
}).AllowAnonymous();

app.MapPost("/api/auth/login", async (LoginRequest request, UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn, CancellationToken ct) =>
{
    var user = await users.FindByEmailAsync(request.Email.Trim());
    if (user is null || user.IsDisabled || !await users.CheckPasswordAsync(user, request.Password)) return Results.Unauthorized();
    await signIn.SignInAsync(user, isPersistent: false);
    return Results.Ok(new { user.Id, user.DisplayName, roles = await users.GetRolesAsync(user) });
}).AllowAnonymous();
app.MapPost("/api/auth/logout", async (SignInManager<ApplicationUser> signIn) => { await signIn.SignOutAsync(); return Results.NoContent(); }).AllowAnonymous();
app.MapGet("/api/auth/me", async (HttpContext http, UserManager<ApplicationUser> users) =>
{
    if (!(http.User.Identity?.IsAuthenticated ?? false)) return Results.Unauthorized();
    var user = await users.GetUserAsync(http.User);
    return user is null || user.IsDisabled ? Results.Unauthorized() : Results.Ok(new { user.Id, user.DisplayName, roles = await users.GetRolesAsync(user) });
});
app.MapPut("/api/auth/me", async (UpdateProfileRequest request, HttpContext http, UserManager<ApplicationUser> users, CancellationToken ct) =>
{
    if (!(http.User.Identity?.IsAuthenticated ?? false)) return Results.Unauthorized();
    var user = await users.GetUserAsync(http.User);
    if (user is null || user.IsDisabled) return Results.Unauthorized();
    var displayName = request.DisplayName?.Trim();
    if (string.IsNullOrWhiteSpace(displayName) || displayName.Length < 2) return Results.ValidationProblem(new Dictionary<string, string[]> { ["displayName"] = ["Display name must contain at least 2 characters."] });
    user.DisplayName = displayName;
    var result = await users.UpdateAsync(user);
    if (!result.Succeeded) return Results.BadRequest(result.Errors);
    return Results.Ok(new { user.Id, user.DisplayName, roles = await users.GetRolesAsync(user) });
});

app.MapGet("/api/health", () => Results.Ok(new { status = "ok", service = "workshop-api" })).AllowAnonymous();

app.MapGet("/api/admin/users", async (HttpContext http, UserManager<ApplicationUser> users, CancellationToken ct) =>
{
    if (!http.User.IsInRole("Administrator")) return Results.Forbid();
    var result = new List<object>();
    foreach (var user in await users.Users.AsNoTracking().OrderBy(x => x.DisplayName).ToListAsync(ct))
    {
        var roles = await users.GetRolesAsync(user);
        result.Add(new { user.Id, user.Email, user.DisplayName, user.IsDisabled, role = roles.FirstOrDefault() ?? "Unassigned" });
    }
    return Results.Ok(result);
});

app.MapPost("/api/admin/users", async (AdminCreateUserRequest request, HttpContext http, UserManager<ApplicationUser> users, RoleManager<IdentityRole<int>> roles, CancellationToken ct) =>
{
    if (!http.User.IsInRole("Administrator")) return Results.Forbid();
    var allowed = new[] { "Booking Clerk", "Mechanic", "Supervisor/Manager", "Administrator" };
    if (!allowed.Contains(request.Role) || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.DisplayName) || string.IsNullOrWhiteSpace(request.Password)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["account"] = ["A valid name, email, password and role are required."] });
    if (await users.FindByEmailAsync(request.Email.Trim()) is not null) return Results.Conflict(new { message = "An account with that email already exists." });
    if (await roles.FindByNameAsync(request.Role) is null) await roles.CreateAsync(new IdentityRole<int>(request.Role));
    var user = new ApplicationUser { UserName = request.Email.Trim(), Email = request.Email.Trim(), DisplayName = request.DisplayName.Trim(), EmailConfirmed = true };
    var created = await users.CreateAsync(user, request.Password);
    if (!created.Succeeded) return Results.BadRequest(new { message = string.Join(" ", created.Errors.Select(x => x.Description)) });
    await users.AddToRoleAsync(user, request.Role);
    return Results.Created($"/api/admin/users/{user.Id}", new { user.Id, user.Email, user.DisplayName, role = request.Role, isDisabled = false });
});

app.MapPut("/api/admin/users/{id:int}", async (int id, AdminUpdateUserRequest request, HttpContext http, UserManager<ApplicationUser> users, RoleManager<IdentityRole<int>> roles, CancellationToken ct) =>
{
    if (!http.User.IsInRole("Administrator")) return Results.Forbid();
    var user = await users.FindByIdAsync(id.ToString());
    if (user is null) return Results.NotFound();
    if (user.Id == int.Parse(users.GetUserId(http.User) ?? "0") && request.IsDisabled == true) return Results.BadRequest(new { message = "You cannot disable the current administrator account." });
    if (request.DisplayName is not null && request.DisplayName.Trim().Length >= 2) user.DisplayName = request.DisplayName.Trim();
    if (request.IsDisabled.HasValue) user.IsDisabled = request.IsDisabled.Value;
    var update = await users.UpdateAsync(user);
    if (!update.Succeeded) return Results.BadRequest(new { message = string.Join(" ", update.Errors.Select(x => x.Description)) });
    if (!string.IsNullOrWhiteSpace(request.Role))
    {
        var allowed = new[] { "Booking Clerk", "Mechanic", "Supervisor/Manager", "Administrator" };
        if (!allowed.Contains(request.Role)) return Results.BadRequest(new { message = "The selected role is not valid." });
        var current = await users.GetRolesAsync(user);
        await users.RemoveFromRolesAsync(user, current);
        await users.AddToRoleAsync(user, request.Role);
    }
    return Results.Ok(new { user.Id, user.Email, user.DisplayName, user.IsDisabled, role = (await users.GetRolesAsync(user)).FirstOrDefault() ?? "Unassigned" });
});

app.MapPut("/api/admin/mechanics/{id:int}", async (int id, AdminUpdateMechanicRequest request, HttpContext http, WorkshopDbContext db, CancellationToken ct) =>
{
    if (!http.User.IsInRole("Administrator")) return Results.Forbid();
    var mechanic = await db.Mechanics.Include(x => x.Skills).SingleOrDefaultAsync(x => x.Id == id, ct);
    if (mechanic is null) return Results.NotFound();
    if (!string.IsNullOrWhiteSpace(request.DisplayName)) mechanic.DisplayName = request.DisplayName.Trim();
    mechanic.JobTitle = request.JobTitle?.Trim();
    mechanic.IsActive = request.IsActive;
    mechanic.IsAvailable = request.IsAvailable;
    mechanic.WorkloadCapacity = Math.Clamp(request.WorkloadCapacity, 1, 20);
    db.MechanicSkills.RemoveRange(mechanic.Skills);
    mechanic.Skills = (request.Skills ?? []).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => new MechanicSkill { MechanicId = mechanic.Id, FaultCategory = x.Trim() }).ToList();
    await db.SaveChangesAsync(ct);
    return Results.Ok(new { mechanic.Id, mechanic.DisplayName, mechanic.JobTitle, mechanic.IsActive, mechanic.IsAvailable, mechanic.WorkloadCapacity, skills = mechanic.Skills.Select(x => x.FaultCategory) });
});

app.MapGet("/api/admin/config", async (HttpContext http, WorkshopDbContext db, IConfiguration configuration, CancellationToken ct) =>
{
    if (!http.User.IsInRole("Administrator")) return Results.Forbid();
    var result = new Dictionary<string, string>
    {
        ["workshopName"] = "Amcotts Workshop Control Center",
        ["defaultServiceDurationMinutes"] = "120",
        ["maximumQueueSize"] = "20",
        ["requireInspectionBeforeAllocation"] = "true",
        ["diagnosticServiceConfigured"] = (!string.IsNullOrWhiteSpace(configuration["DiagnosticService:BaseUrl"])).ToString().ToLowerInvariant(),
        ["authenticationProvider"] = configuration["Authentication:Provider"] ?? "Local"
    };
    foreach (var row in await db.SystemSettings.AsNoTracking().ToListAsync(ct)) result[row.SettingKey] = row.SettingValue;
    return Results.Ok(result);
});

app.MapPut("/api/admin/config", async (Dictionary<string, string> settings, HttpContext http, WorkshopDbContext db, CancellationToken ct) =>
{
    if (!http.User.IsInRole("Administrator")) return Results.Forbid();
    var allowed = new[] { "workshopName", "defaultServiceDurationMinutes", "maximumQueueSize", "requireInspectionBeforeAllocation" };
    foreach (var pair in settings.Where(x => allowed.Contains(x.Key)))
    {
        var value = pair.Value?.Trim() ?? "";
        var row = await db.SystemSettings.SingleOrDefaultAsync(x => x.SettingKey == pair.Key, ct);
        if (row is null) db.SystemSettings.Add(new SystemSetting { SettingKey = pair.Key, SettingValue = value, UpdatedUtc = DateTime.UtcNow });
        else { row.SettingValue = value; row.UpdatedUtc = DateTime.UtcNow; }
    }
    await db.SaveChangesAsync(ct);
    return Results.Ok(settings);
});

app.MapGet("/api/admin/audit", async (HttpContext http, WorkshopDbContext db, CancellationToken ct) =>
{
    if (!http.User.IsInRole("Administrator")) return Results.Forbid();
    var result = new List<object>();
    var statuses = await db.StatusEvents.AsNoTracking().OrderByDescending(x => x.OccurredUtc).Take(100).ToListAsync(ct);
    result.AddRange(statuses.Select(x => new { type = "Booking status", occurredUtc = x.OccurredUtc, reference = $"BK-{x.BookingId:0000}", details = $"{x.PreviousStatus ?? "Created"} → {x.NewStatus}: {x.Reason ?? "No reason recorded"}" }));
    var reviews = await db.DiagnosticSuggestions.AsNoTracking().Include(x => x.Session).Where(x => x.ReviewedUtc != null).OrderByDescending(x => x.ReviewedUtc).Take(100).ToListAsync(ct);
    result.AddRange(reviews.Select(x => new { type = "Diagnostic review", occurredUtc = x.ReviewedUtc!.Value, reference = $"Booking #{x.Session.BookingId}", details = $"{x.ReviewStatus}: {x.Label}" }));
    return Results.Ok(result.OrderByDescending(x => x.GetType().GetProperty("occurredUtc")?.GetValue(x)).Take(200));
});
app.MapGet("/api/config", (IConfiguration configuration) => Results.Ok(new { diagnosticServiceConfigured = !string.IsNullOrWhiteSpace(configuration["DiagnosticService:BaseUrl"]), authentication = configuration["Authentication:Provider"] ?? "Local" })).AllowAnonymous();

app.MapGet("/api/customers", async (WorkshopDbContext db, CancellationToken ct) => Results.Ok(await db.Customers.AsNoTracking().OrderBy(x => x.Name).ToListAsync(ct)));
app.MapPost("/api/customers", async (CreateCustomerRequest request, HttpContext http, WorkshopDbContext db, CancellationToken ct) =>
{
    if (!CanCreateBookings(http.User)) return Results.Forbid();
    if (string.IsNullOrWhiteSpace(request.Name)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["name"] = ["Customer or fleet name is required."] });
    var name = request.Name.Trim();
    if (await db.Customers.AnyAsync(x => x.Name.ToLower() == name.ToLower(), ct)) return Results.Conflict(new { code = "duplicate_customer", message = "A customer or fleet with this name already exists." });
    var customer = new Customer { Name = name, ContactDetails = request.ContactDetails?.Trim(), Notes = request.Notes?.Trim(), CreatedUtc = DateTime.UtcNow };
    db.Customers.Add(customer);
    await db.SaveChangesAsync(ct);
    return Results.Created($"/api/customers/{customer.Id}", customer);
});

app.MapGet("/api/trucks", async (WorkshopDbContext db, CancellationToken ct) =>
    Results.Ok(await db.Trucks.AsNoTracking().OrderBy(x => x.RegistrationNumber).Select(x => new
    {
        x.Id,
        x.CustomerId,
        x.RegistrationNumber,
        x.Make,
        x.Model,
        x.Vin,
        x.CreatedUtc,
        customer = x.Customer == null ? null : x.Customer.Name
    }).ToListAsync(ct)));
app.MapPost("/api/trucks", async (CreateTruckRequest request, HttpContext http, WorkshopDbContext db, CancellationToken ct) =>
{
    if (!CanCreateBookings(http.User)) return Results.Forbid();
    var registration = NormalizeRegistration(request.RegistrationNumber);
    if (string.IsNullOrWhiteSpace(registration)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["registrationNumber"] = ["Registration number is required."] });
    if (!await db.Customers.AnyAsync(x => x.Id == request.CustomerId, ct)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["customerId"] = ["Customer was not found."] });
    if (await db.Trucks.AnyAsync(x => x.NormalizedRegistration == registration, ct)) return Results.Conflict(new { code = "duplicate_truck", message = "A truck with this registration already exists." });
    var truck = new Truck { CustomerId = request.CustomerId, RegistrationNumber = request.RegistrationNumber.Trim(), NormalizedRegistration = registration, Make = request.Make?.Trim(), Model = request.Model?.Trim(), Vin = request.Vin?.Trim(), CreatedUtc = DateTime.UtcNow };
    db.Trucks.Add(truck);
    await db.SaveChangesAsync(ct);
    return Results.Created($"/api/trucks/{truck.Id}", truck);
});

app.MapGet("/api/bookings", async (HttpContext http, WorkshopDbContext db, string? search, string? status, int page, int pageSize, CancellationToken ct) =>
{
    page = Math.Max(1, page); pageSize = Math.Clamp(pageSize == 0 ? 25 : pageSize, 1, 100);
    var query = db.Bookings.AsNoTracking().AsSplitQuery().AsQueryable();
    if (http.User.IsInRole("Mechanic"))
    {
        var mechanic = await ResolveCurrentMechanic(http, db, ct);
        if (mechanic is null) return Results.Ok(new { page, pageSize, total = 0, items = Array.Empty<object>() });
        query = query.Where(x => x.Status != "Completed" && x.Status != "Cancelled" && x.Assignments.Any(a => a.MechanicId == mechanic.Id && a.ClosedUtc == null));
    }
    if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
    if (!string.IsNullOrWhiteSpace(search)) { var value = search.Trim(); query = query.Where(x => x.Truck!.RegistrationNumber.Contains(value) || x.Customer!.Name.Contains(value) || x.Complaint.Contains(value)); }
    var total = await query.CountAsync(ct);
    var bookings = await query.OrderByDescending(x => x.ArrivalUtc).Skip((page - 1) * pageSize).Take(pageSize).Select(x => new
    {
        id = x.Id,
        queueNumber = x.QueueNumber,
        serviceDate = x.ServiceDate,
        arrivalUtc = x.ArrivalUtc,
        complaint = x.Complaint,
        priority = x.Priority,
        status = x.Status,
        confirmedFaultCategory = x.ConfirmedFaultCategory ?? x.RepairRecord!.ConfirmedPrimaryFault,
        mechanic = x.Assignments.Where(a => a.ClosedUtc == null).OrderByDescending(a => a.CreatedUtc).Select(a => a.Mechanic!.DisplayName).FirstOrDefault(),
        customer = x.Customer!.Name,
        customerId = x.CustomerId,
        truckId = x.TruckId,
        registration = x.Truck!.RegistrationNumber,
        make = x.Truck.Make,
        model = x.Truck.Model,
        vin = x.Truck.Vin,
        symptoms = x.Symptoms.OrderBy(s => s.Id).Select(s => s.Name).ToList(),
        statusEvents = x.StatusEvents.OrderBy(e => e.OccurredUtc).Select(e => new { e.PreviousStatus, e.NewStatus, e.Reason, e.OccurredUtc }).ToList()
    }).ToListAsync(ct);
    return Results.Ok(new { page, pageSize, total, items = bookings });
});

app.MapGet("/api/mechanic/workbench", async (HttpContext http, WorkshopDbContext db, CancellationToken ct) =>
{
    if (!http.User.IsInRole("Mechanic")) return Results.Forbid();
    var mechanic = await ResolveCurrentMechanic(http, db, ct);
    if (mechanic is null) return Results.NotFound(new { message = "No mechanic profile is linked to this account." });
    var jobs = await db.Bookings.AsNoTracking()
        .Include(x => x.Customer)
        .Include(x => x.Truck)
        .Include(x => x.Symptoms)
        .Include(x => x.StatusEvents.OrderBy(e => e.OccurredUtc))
        .Include(x => x.Assignments).ThenInclude(x => x.Mechanic)
        .Where(x => x.Status != "Completed" && x.Status != "Cancelled" && x.Assignments.Any(a => a.MechanicId == mechanic.Id && a.ClosedUtc == null))
        .OrderByDescending(x => x.Status == "InRepair")
        .ThenByDescending(x => x.ArrivalUtc)
        .ToListAsync(ct);
    return Results.Ok(new
    {
        mechanic = new { mechanic.Id, mechanic.DisplayName, mechanic.JobTitle, mechanic.WorkloadCapacity, skills = mechanic.Skills.Select(x => x.FaultCategory) },
        jobs = jobs.Select(ToBookingResponse)
    });
});

app.MapGet("/api/bookings/{id:int}", async (int id, HttpContext http, WorkshopDbContext db, CancellationToken ct) =>
{
    if (http.User.IsInRole("Mechanic") && !await MechanicOwnsBooking(http, db, id, ct)) return Results.Forbid();
    var booking = await db.Bookings.AsNoTracking().AsSplitQuery().Include(x => x.Customer).Include(x => x.Truck).Include(x => x.Symptoms).Include(x => x.Assignments).ThenInclude(x => x.Mechanic).Include(x => x.StatusEvents.OrderBy(e => e.OccurredUtc)).Include(x => x.RepairRecord).SingleOrDefaultAsync(x => x.Id == id, ct);
    return booking is null ? Results.NotFound() : Results.Ok(ToBookingResponse(booking));
});

app.MapPost("/api/bookings", async (CreateBookingRequest request, HttpContext http, WorkshopDbContext db, CancellationToken ct) =>
{
    if (!CanCreateBookings(http.User)) return Results.Forbid();
    if (string.IsNullOrWhiteSpace(request.RequestKey)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["requestKey"] = ["An idempotency request key is required."] });
    if (string.IsNullOrWhiteSpace(request.Complaint)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["complaint"] = ["A reported complaint is required."] });
    var existing = await db.Bookings.AsNoTracking().Include(x => x.Customer).Include(x => x.Truck).Include(x => x.Symptoms).SingleOrDefaultAsync(x => x.RequestKey == request.RequestKey, ct);
    if (existing is not null) return Results.Ok(ToBookingResponse(existing));
    var truck = await db.Trucks.Include(x => x.Customer).SingleOrDefaultAsync(x => x.Id == request.TruckId, ct);
    if (truck is null) return Results.ValidationProblem(new Dictionary<string, string[]> { ["truckId"] = ["Truck was not found."] });
    if (truck.CustomerId != request.CustomerId) return Results.ValidationProblem(new Dictionary<string, string[]> { ["customerId"] = ["The truck does not belong to the selected customer."] });

    await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
    try
    {
        var serviceDate = request.ServiceDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        if (await db.Bookings.AnyAsync(x => x.TruckId == request.TruckId && x.ServiceDate == serviceDate && x.Status != "Completed" && x.Status != "Cancelled", ct)) return Results.Conflict(new { code = "duplicate_active_booking", message = "This truck already has an active booking for the selected service date." });
        var nextQueue = (await db.Bookings.Where(x => x.ServiceDate == serviceDate).Select(x => (int?)x.QueueNumber).MaxAsync(ct) ?? 0) + 1;
        var booking = new Booking { CustomerId = request.CustomerId, TruckId = request.TruckId, ServiceDate = serviceDate, QueueNumber = nextQueue, RequestKey = request.RequestKey.Trim(), ArrivalUtc = DateTime.UtcNow, Complaint = request.Complaint.Trim(), Priority = string.IsNullOrWhiteSpace(request.Priority) ? "MEDIUM" : request.Priority.Trim().ToUpperInvariant(), Status = "Waiting", CreatedUtc = DateTime.UtcNow, Symptoms = (request.Symptoms ?? []).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).Select(x => new BookingSymptom { Name = x.Trim() }).ToList() };
        booking.StatusEvents.Add(new StatusEvent { NewStatus = "Waiting", Reason = "Booking created", OccurredUtc = DateTime.UtcNow });
        db.Bookings.Add(booking);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        await db.Entry(booking).Reference(x => x.Customer).LoadAsync(ct);
        await db.Entry(booking).Reference(x => x.Truck).LoadAsync(ct);
        return Results.Created($"/api/bookings/{booking.Id}", ToBookingResponse(booking));
    }
    catch (DbUpdateException)
    {
        await transaction.RollbackAsync(ct);
        var retry = await db.Bookings.AsNoTracking().Include(x => x.Customer).Include(x => x.Truck).Include(x => x.Symptoms).SingleOrDefaultAsync(x => x.RequestKey == request.RequestKey, ct);
        return retry is null ? Results.Conflict(new { code = "queue_conflict", message = "The booking could not be created safely. Please retry with the same request key." }) : Results.Ok(ToBookingResponse(retry));
    }
});

app.MapGet("/api/trucks/{id:int}/history", async (int id, HttpContext http, WorkshopDbContext db, CancellationToken ct) =>
{
    if (http.User.IsInRole("Mechanic") && !await MechanicCanViewTruckHistory(http, db, id, ct)) return Results.Forbid();
    var truck = await db.Trucks.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
    if (truck is null) return Results.NotFound();
    var history = await db.Bookings.AsNoTracking().Where(x => x.TruckId == id).Include(x => x.StatusEvents).OrderByDescending(x => x.ArrivalUtc).Select(x => new { x.Id, x.QueueNumber, x.ServiceDate, x.Complaint, x.Status, x.ArrivalUtc, events = x.StatusEvents.OrderBy(e => e.OccurredUtc) }).ToListAsync(ct);
    return Results.Ok(new { truck, bookings = history });
});

app.MapGet("/api/bookings/{id:int}/inspection", async (int id, HttpContext http, WorkshopDbContext db, CancellationToken ct) =>
{
    if (http.User.IsInRole("Mechanic") && !await MechanicOwnsBooking(http, db, id, ct)) return Results.Forbid();
    var record = await db.InspectionRecords.AsNoTracking().SingleOrDefaultAsync(x => x.BookingId == id, ct);
    return record is null ? Results.NotFound() : Results.Ok(new { record.Id, record.BookingId, record.ChecklistJson, checklistItems = JsonSerializer.Deserialize<string[]>(record.ChecklistJson) ?? [], record.Findings, record.Severity, record.EstimatedRepairMinutes, record.UpdatedUtc });
});

app.MapPut("/api/bookings/{id:int}/inspection", async (int id, SaveInspectionRequest request, HttpContext http, WorkshopDbContext db, CancellationToken ct) =>
{
    if (!CanPerformTechnicalWork(http.User)) return Results.Forbid();
    if (http.User.IsInRole("Mechanic") && !await MechanicOwnsBooking(http, db, id, ct)) return Results.Forbid();
    var booking = await db.Bookings.SingleOrDefaultAsync(x => x.Id == id, ct);
    if (booking is null) return Results.NotFound();
    if (booking.Status is "Completed" or "Cancelled") return Results.Conflict(new { code = "terminal_booking", message = "Terminal bookings cannot receive inspection changes." });
    var record = await db.InspectionRecords.SingleOrDefaultAsync(x => x.BookingId == id, ct);
    if (record is null) { record = new InspectionRecord { BookingId = id }; db.InspectionRecords.Add(record); }
    record.ChecklistJson = JsonSerializer.Serialize((request.ChecklistItems ?? []).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase));
    record.Findings = request.Findings?.Trim();
    record.Severity = request.Severity?.Trim();
    record.EstimatedRepairMinutes = request.EstimatedRepairMinutes;
    record.UpdatedUtc = DateTime.UtcNow;
    if (booking.Status is "Waiting" or "Assigned")
    {
        var previousStatus = booking.Status;
        booking.Status = "UnderInspection";
        booking.StatusEvents.Add(new StatusEvent { PreviousStatus = previousStatus, NewStatus = "UnderInspection", Reason = "Inspection progress saved", OccurredUtc = DateTime.UtcNow });
    }
    await db.SaveChangesAsync(ct);
    return Results.Ok(new { record.Id, record.BookingId, checklistItems = JsonSerializer.Deserialize<string[]>(record.ChecklistJson) ?? [], record.Findings, record.Severity, record.EstimatedRepairMinutes, record.UpdatedUtc, bookingStatus = booking.Status });
});

app.MapPost("/api/bookings/{id:int}/status", async (int id, ChangeBookingStatusRequest request, HttpContext http, WorkshopDbContext db, CancellationToken ct) =>
{
    var booking = await db.Bookings.SingleOrDefaultAsync(x => x.Id == id, ct);
    if (booking is null) return Results.NotFound();
    var actorRole = OperationalRole(http.User);
    if (actorRole is null) return Results.Forbid();
    var nextStatus = request.NewStatus?.Trim() ?? "";
    if (booking.Status == nextStatus) return Results.Ok(new { booking.Id, previousStatus = booking.Status, newStatus = booking.Status, idempotent = true });
    if (booking.Status is "Completed" or "Cancelled") return Results.Conflict(new { code = "terminal_booking", message = "Completed and cancelled bookings cannot be changed." });
    if (actorRole == "Mechanic" && booking.Status == "UnderInspection" && nextStatus == "ReadyForAllocation" && await db.Assignments.AnyAsync(x => x.BookingId == id && x.ClosedUtc == null, ct))
    {
        nextStatus = "InRepair";
    }
    var inspectionSaveShortcut = booking.Status == "Waiting" && nextStatus == "ReadyForAllocation";
    if (!inspectionSaveShortcut && !IsAllowedTransition(booking.Status, nextStatus)) return Results.Conflict(new { code = "illegal_transition", message = $"A booking cannot move from {booking.Status} to {nextStatus}." });
    if (!RoleCanTransition(actorRole, booking.Status, nextStatus)) return Results.StatusCode(StatusCodes.Status403Forbidden);
    if (inspectionSaveShortcut && await InspectionRequiredForAllocation(db, ct) && !await db.InspectionRecords.AnyAsync(x => x.BookingId == id, ct)) return Results.Conflict(new { code = "inspection_required", message = "An inspection must be saved before this booking can be sent for allocation." });
    if (nextStatus is "Cancelled" or "Completed" && string.IsNullOrWhiteSpace(request.Reason)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["reason"] = [$"A reason is required when moving a booking to {nextStatus}."] });
    if (nextStatus == "Completed")
    {
        var repair = await db.RepairRecords.SingleOrDefaultAsync(x => x.BookingId == id, ct);
        if (repair is null || string.IsNullOrWhiteSpace(repair.Findings) || string.IsNullOrWhiteSpace(repair.WorkPerformed)) return Results.Conflict(new { code = "repair_record_required", message = "A complete repair record with findings and work performed is required before completion." });
    }

    var previous = booking.Status;
    booking.Status = nextStatus;
    if (nextStatus == "Completed")
    {
        var repair = await db.RepairRecords.SingleOrDefaultAsync(x => x.BookingId == id, ct);
        if (repair is not null) repair.RepairCompletedUtc = DateTime.UtcNow;
    }
    booking.StatusEvents.Add(new StatusEvent { PreviousStatus = previous, NewStatus = nextStatus, Reason = request.Reason?.Trim(), OccurredUtc = DateTime.UtcNow });
    await db.SaveChangesAsync(ct);
    return Results.Ok(new { booking.Id, previousStatus = previous, newStatus = nextStatus });
});

app.MapGet("/api/mechanics", async (HttpContext http, WorkshopDbContext db, CancellationToken ct) =>
{
    if (!CanAllocate(http.User)) return Results.Forbid();
    var mechanics = await db.Mechanics.AsNoTracking().Include(x => x.Skills).OrderBy(x => x.Id).ToListAsync(ct);
    var result = new List<object>();
    foreach (var mechanic in mechanics)
    {
        var workload = await db.Assignments.CountAsync(x => x.MechanicId == mechanic.Id && x.ClosedUtc == null, ct);
        result.Add(new { mechanic.Id, mechanic.DisplayName, mechanic.JobTitle, mechanic.IsActive, mechanic.IsAvailable, mechanic.WorkloadCapacity, activeWorkload = workload, skills = mechanic.Skills.Select(x => x.FaultCategory) });
    }
    return Results.Ok(result);
});

app.MapGet("/api/bookings/{id:int}/allocation-recommendation", async (int id, HttpContext http, WorkshopDbContext db, CancellationToken ct) =>
{
    if (!CanAllocate(http.User)) return Results.Forbid();
    var booking = await db.Bookings.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
    if (booking is null) return Results.NotFound();
    var mechanics = await db.Mechanics.AsNoTracking().Include(x => x.Skills).Where(x => x.IsActive && x.IsAvailable).OrderBy(x => x.Id).ToListAsync(ct);
    var candidates = new List<object>();
    foreach (var mechanic in mechanics)
    {
        var workload = await db.Assignments.CountAsync(x => x.MechanicId == mechanic.Id && x.ClosedUtc == null, ct);
        var hasSkill = string.IsNullOrWhiteSpace(booking.ConfirmedFaultCategory) || mechanic.Skills.Any(x => FaultCategoriesMatch(x.FaultCategory, booking.ConfirmedFaultCategory));
        if (hasSkill && workload < mechanic.WorkloadCapacity) candidates.Add(new { mechanic.Id, mechanic.DisplayName, mechanic.JobTitle, activeWorkload = workload, mechanic.WorkloadCapacity, explanation = $"Available, qualified and carrying {workload} of {mechanic.WorkloadCapacity} active jobs." });
    }
    return candidates.Count == 0 ? Results.Ok(new { eligible = false, reason = "No available mechanic with a matching skill and remaining capacity was found." }) : Results.Ok(new { eligible = true, recommended = candidates[0], candidates });
});

app.MapPost("/api/bookings/{id:int}/allocate", async (int id, AllocateBookingRequest request, HttpContext http, WorkshopDbContext db, CancellationToken ct) =>
{
    if (!CanAllocate(http.User)) return Results.Forbid();
    await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
    var booking = await db.Bookings.SingleOrDefaultAsync(x => x.Id == id, ct);
    if (booking is null) return Results.NotFound();
    if (booking.Status != "ReadyForAllocation") return Results.Conflict(new { code = "not_ready_for_allocation", message = "The booking must be ready for allocation." });
    if (await InspectionRequiredForAllocation(db, ct) && !await db.InspectionRecords.AnyAsync(x => x.BookingId == id, ct)) return Results.Conflict(new { code = "inspection_required", message = "An inspection must be saved before this booking can be allocated." });
    if (await db.Assignments.AnyAsync(x => x.BookingId == id && x.ClosedUtc == null, ct)) return Results.Conflict(new { code = "active_assignment_exists", message = "This booking already has an active assignment." });
    var mechanics = await db.Mechanics.Include(x => x.Skills).Where(x => x.IsActive && x.IsAvailable).OrderBy(x => x.Id).ToListAsync(ct);
    var eligible = new List<(Mechanic mechanic, int workload)>();
    foreach (var mechanic in mechanics)
    {
        var workload = await db.Assignments.CountAsync(x => x.MechanicId == mechanic.Id && x.ClosedUtc == null, ct);
        var hasSkill = string.IsNullOrWhiteSpace(booking.ConfirmedFaultCategory) || mechanic.Skills.Any(x => FaultCategoriesMatch(x.FaultCategory, booking.ConfirmedFaultCategory));
        if (hasSkill && workload < mechanic.WorkloadCapacity) eligible.Add((mechanic, workload));
    }
    var chosen = request.MechanicId is null ? eligible.OrderBy(x => x.workload).ThenBy(x => x.mechanic.Id).FirstOrDefault() : eligible.FirstOrDefault(x => x.mechanic.Id == request.MechanicId.Value);
    if (chosen.mechanic is null) return Results.Conflict(new { code = "no_eligible_mechanic", message = "No eligible mechanic is available for this booking." });
    var assignment = new Assignment { BookingId = id, MechanicId = chosen.mechanic.Id, CreatedUtc = DateTime.UtcNow, Reason = request.Reason?.Trim() ?? "Least-loaded eligible mechanic recommendation" };
    booking.Status = "Assigned";
    booking.StatusEvents.Add(new StatusEvent { PreviousStatus = "ReadyForAllocation", NewStatus = "Assigned", Reason = assignment.Reason, OccurredUtc = DateTime.UtcNow });
    db.Assignments.Add(assignment);
    await db.SaveChangesAsync(ct);
    await transaction.CommitAsync(ct);
    return Results.Ok(new { assignment.Id, bookingId = id, mechanicId = chosen.mechanic.Id, mechanic = chosen.mechanic.DisplayName, explanation = $"Assigned using skill, availability and workload. Active workload before assignment: {chosen.workload}/{chosen.mechanic.WorkloadCapacity}." });
});

app.MapPost("/api/bookings/{id:int}/diagnose", async (int id, DiagnoseBookingRequest request, HttpContext http, WorkshopDbContext db, IHttpClientFactory clients, IConfiguration configuration, CancellationToken ct) =>
{
    if (!CanReviewDiagnostic(http.User)) return Results.Forbid();
    if (http.User.IsInRole("Mechanic") && !await MechanicOwnsBooking(http, db, id, ct)) return Results.Forbid();
    var booking = await db.Bookings.Include(x => x.Truck).Include(x => x.Symptoms).SingleOrDefaultAsync(x => x.Id == id, ct);
    if (booking is null) return Results.NotFound();
    if (booking.Status is "Completed" or "Cancelled") return Results.Conflict(new { code = "terminal_booking", message = "Terminal bookings cannot receive new diagnostic sessions." });
    var baseUrl = configuration["DiagnosticService:BaseUrl"];
    if (string.IsNullOrWhiteSpace(baseUrl)) return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    var payload = new { symptoms = booking.Symptoms.Select(x => x.Name).Concat(request.Observations ?? []).Distinct().Take(12), observations = request.Observations ?? [], complaint = booking.Complaint, vehicle_make = booking.Truck?.Make };
    DiagnosticServiceResponse? result;
    try
    {
        var client = clients.CreateClient("diagnosis");
        client.BaseAddress = new Uri(baseUrl);
        using var response = await client.PostAsJsonAsync("/diagnose", payload, ct);
        if (!response.IsSuccessStatusCode) return Results.Problem("Diagnostic service returned an unavailable response.", statusCode: StatusCodes.Status503ServiceUnavailable);
        result = await response.Content.ReadFromJsonAsync<DiagnosticServiceResponse>(cancellationToken: ct);
    }
    catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return Results.Problem("Diagnostic service timed out.", statusCode: StatusCodes.Status504GatewayTimeout); }
    catch (HttpRequestException) { return Results.Problem("Diagnostic service is unavailable.", statusCode: StatusCodes.Status503ServiceUnavailable); }
    if (result is null) return Results.Problem("Diagnostic service returned an empty response.", statusCode: StatusCodes.Status503ServiceUnavailable);
    var session = new DiagnosticSession { BookingId = booking.Id, RequestedUtc = DateTime.UtcNow, Result = result.Result, Method = result.Method, ModelVersion = result.ModelVersion, Message = result.Message };
    session.Suggestions = (result.Suggestions ?? []).Where(x => x.Rank is >= 1 and <= 3).Select(x => new DiagnosticSuggestion { Rank = x.Rank, FaultCategoryId = x.FaultCategoryId, Label = x.Label, Action = x.Action, Score = x.Score, ScoreType = x.ScoreType, EvidenceJson = JsonSerializer.Serialize(x.Evidence) }).ToList();
    db.DiagnosticSessions.Add(session);
    await db.SaveChangesAsync(ct);
    return Results.Ok(new { sessionId = session.Id, session.Result, session.Method, session.ModelVersion, session.Message, suggestions = session.Suggestions.Select(x => new { x.Id, x.Rank, x.FaultCategoryId, x.Label, x.Action, x.Score, x.ScoreType, evidence = JsonSerializer.Deserialize<string[]>(x.EvidenceJson) ?? [] }) });
});

app.MapPost("/api/diagnostic-suggestions/{id:int}/review", async (int id, ReviewDiagnosticSuggestionRequest request, HttpContext http, WorkshopDbContext db, CancellationToken ct) =>
{
    if (!CanReviewDiagnostic(http.User)) return Results.Forbid();
    var suggestion = await db.DiagnosticSuggestions.Include(x => x.Session).ThenInclude(x => x.Booking).SingleOrDefaultAsync(x => x.Id == id, ct);
    if (suggestion is null) return Results.NotFound();
    if (http.User.IsInRole("Mechanic") && !await MechanicOwnsBooking(http, db, suggestion.Session.BookingId, ct)) return Results.Forbid();
    if (request.ReviewStatus is not ("Accepted" or "Rejected" or "Revised")) return Results.ValidationProblem(new Dictionary<string, string[]> { ["reviewStatus"] = ["Review status must be Accepted, Rejected or Revised."] });
    suggestion.ReviewStatus = request.ReviewStatus;
    suggestion.ReviewNote = request.Note?.Trim();
    suggestion.ReviewedUtc = DateTime.UtcNow;
    suggestion.ReviewedByRole = OperationalRole(http.User);
    if (request.ReviewStatus is "Accepted" or "Revised") suggestion.Session.Booking.ConfirmedFaultCategory = request.ConfirmedFaultCategory?.Trim() ?? suggestion.FaultCategoryId;
    if (request.ReviewStatus is "Accepted" or "Revised" && suggestion.Session.Booking.Status is "Waiting" or "UnderInspection")
    {
        var previousStatus = suggestion.Session.Booking.Status;
        suggestion.Session.Booking.Status = "ReadyForAllocation";
        db.StatusEvents.Add(new StatusEvent { BookingId = suggestion.Session.BookingId, PreviousStatus = previousStatus, NewStatus = "ReadyForAllocation", Reason = "Diagnostic review accepted; ready for allocation", OccurredUtc = DateTime.UtcNow });
    }
    await db.SaveChangesAsync(ct);
    return Results.Ok(new { suggestion.Id, suggestion.ReviewStatus, suggestion.Session.Booking.ConfirmedFaultCategory });
});

app.MapPost("/api/bookings/{id:int}/repair", async (int id, SaveRepairRecordRequest request, HttpContext http, WorkshopDbContext db, CancellationToken ct) =>
{
    if (!CanPerformTechnicalWork(http.User)) return Results.Forbid();
    var booking = await db.Bookings.SingleOrDefaultAsync(x => x.Id == id, ct);
    if (booking is null) return Results.NotFound();
    if (http.User.IsInRole("Mechanic") && !await MechanicOwnsBooking(http, db, id, ct)) return Results.Forbid();
    if (booking.Status is "Completed" or "Cancelled") return Results.Conflict(new { code = "terminal_booking", message = "Terminal bookings cannot receive repair changes." });
    if (string.IsNullOrWhiteSpace(request.Findings) || string.IsNullOrWhiteSpace(request.WorkPerformed)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["repair"] = ["Findings and work performed are required."] });
    var record = await db.RepairRecords.SingleOrDefaultAsync(x => x.BookingId == id, ct);
    if (record is null) { record = new RepairRecord { BookingId = id, RepairStartedUtc = DateTime.UtcNow }; db.RepairRecords.Add(record); }
    record.ConfirmedPrimaryFault = request.ConfirmedPrimaryFault?.Trim() ?? booking.ConfirmedFaultCategory;
    record.Findings = request.Findings.Trim(); record.WorkPerformed = request.WorkPerformed.Trim(); record.PartsUsed = request.PartsUsed?.Trim(); record.Notes = request.Notes?.Trim(); record.MechanicId = request.MechanicId;
    if (booking.Status == "Assigned") { booking.Status = "InRepair"; booking.StatusEvents.Add(new StatusEvent { PreviousStatus = "Assigned", NewStatus = "InRepair", Reason = "Repair record started", OccurredUtc = DateTime.UtcNow }); }
    await db.SaveChangesAsync(ct);
    return Results.Ok(record);
});

app.MapGet("/api/reports/turnaround", async (HttpContext http, WorkshopDbContext db, DateOnly? from, DateOnly? to, CancellationToken ct) =>
{
    if (!CanAllocate(http.User)) return Results.Forbid();
    var start = from ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30)); var end = to ?? DateOnly.FromDateTime(DateTime.UtcNow);
    var rows = await db.Bookings.AsNoTracking().Include(x => x.RepairRecord).Where(x => x.ServiceDate >= start && x.ServiceDate <= end && x.Status == "Completed" && x.RepairRecord != null && x.RepairRecord.RepairCompletedUtc != null).ToListAsync(ct);
    var result = rows.GroupBy(x => x.RepairRecord!.ConfirmedPrimaryFault ?? x.ConfirmedFaultCategory ?? "Uncategorised").Select(g => new { faultCategory = g.Key, count = g.Count(), averageHours = Math.Round(g.Average(x => (x.RepairRecord!.RepairCompletedUtc!.Value - x.ArrivalUtc).TotalHours), 2), fastestHours = Math.Round(g.Min(x => (x.RepairRecord!.RepairCompletedUtc!.Value - x.ArrivalUtc).TotalHours), 2), slowestHours = Math.Round(g.Max(x => (x.RepairRecord!.RepairCompletedUtc!.Value - x.ArrivalUtc).TotalHours), 2) }).OrderByDescending(x => x.count);
    return Results.Ok(new { from = start, to = end, completedJobs = rows.Count, incompleteJobs = await db.Bookings.CountAsync(x => x.ServiceDate >= start && x.ServiceDate <= end && x.Status != "Completed" && x.Status != "Cancelled", ct), rows = result });
});

app.MapGet("/api/reports/mechanic-completion", async (HttpContext http, WorkshopDbContext db, DateOnly? from, DateOnly? to, CancellationToken ct) =>
{
    if (!CanAllocate(http.User)) return Results.Forbid();
    var startUtc = (from ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30))).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc); var endUtc = (to ?? DateOnly.FromDateTime(DateTime.UtcNow)).AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
    var assignments = await db.Assignments.AsNoTracking().Include(x => x.Mechanic).Include(x => x.Booking).Where(x => x.CreatedUtc >= startUtc && x.CreatedUtc < endUtc).ToListAsync(ct);
    var rows = assignments.GroupBy(x => new { x.MechanicId, Name = x.Mechanic!.DisplayName }).Select(g => new { mechanicId = g.Key.MechanicId, mechanic = g.Key.Name, numerator = g.Count(x => x.Booking!.Status == "Completed"), denominator = g.Count(x => x.Booking!.Status != "Cancelled"), openAssignments = g.Count(x => x.Booking!.Status is not "Completed" and not "Cancelled"), completionRate = g.Count(x => x.Booking!.Status == "Completed") * 100.0 / Math.Max(1, g.Count(x => x.Booking!.Status != "Cancelled")) });
    return Results.Ok(new { from = startUtc, to = endUtc, definition = "Completed eligible assignments divided by all eligible non-cancelled assignments in the cohort.", rows });
});

app.MapGet("/api/reports/recurring-faults", async (HttpContext http, WorkshopDbContext db, DateOnly? from, DateOnly? to, CancellationToken ct) =>
{
    if (!CanAllocate(http.User)) return Results.Forbid();
    var start = from ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30)); var end = to ?? DateOnly.FromDateTime(DateTime.UtcNow);
    var records = await db.Bookings.AsNoTracking().Include(x => x.Truck).Where(x => x.ServiceDate >= start && x.ServiceDate <= end && x.ConfirmedFaultCategory != null && x.Status != "Cancelled").Select(x => new { x.ConfirmedFaultCategory, x.TruckId, x.Truck!.RegistrationNumber }).ToListAsync(ct);
    var rows = records.GroupBy(x => x.ConfirmedFaultCategory!).Select(g => new { faultCategory = g.Key, occurrences = g.Count(), trucksAffected = g.Select(x => x.TruckId).Distinct().Count(), repeatTruckOccurrences = g.GroupBy(x => x.TruckId).Sum(t => Math.Max(0, t.Count() - 1)) }).OrderByDescending(x => x.occurrences);
    return Results.Ok(new { from = start, to = end, rows });
});

app.Run();

static string NormalizeRegistration(string value) => new(value.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
static bool FaultCategoriesMatch(string left, string? right)
{
    if (string.IsNullOrWhiteSpace(right)) return true;
    static string Normalize(string value)
    {
        var normalized = new string(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
        return normalized.EndsWith("fault", StringComparison.Ordinal) ? normalized[..^5] : normalized;
    }
    return Normalize(left) == Normalize(right);
}
static bool IsAllowedTransition(string current, string next) => current switch
{
    "Waiting" => next is "UnderInspection" or "Cancelled",
    "UnderInspection" => next is "ReadyForAllocation" or "InRepair" or "Cancelled",
    "ReadyForAllocation" => next is "Assigned" or "Cancelled",
    "Assigned" => next is "InRepair" or "Cancelled",
    "InRepair" => next is "Completed" or "Cancelled",
    _ => false
};
static string? OperationalRole(System.Security.Claims.ClaimsPrincipal user) => user.IsInRole("Administrator") ? "Administrator" : user.IsInRole("Supervisor/Manager") ? "Supervisor/Manager" : user.IsInRole("Mechanic") ? "Mechanic" : user.IsInRole("Booking Clerk") ? "Booking Clerk" : null;
static bool CanCreateBookings(System.Security.Claims.ClaimsPrincipal user) => user.IsInRole("Booking Clerk") || user.IsInRole("Supervisor/Manager");
static bool CanPerformTechnicalWork(System.Security.Claims.ClaimsPrincipal user) => user.IsInRole("Mechanic") || user.IsInRole("Administrator");
static bool CanReviewDiagnostic(System.Security.Claims.ClaimsPrincipal user) => user.IsInRole("Mechanic") || user.IsInRole("Supervisor/Manager") || user.IsInRole("Administrator");
static bool CanAllocate(System.Security.Claims.ClaimsPrincipal user) => user.IsInRole("Supervisor/Manager") || user.IsInRole("Administrator");
static async Task<bool> InspectionRequiredForAllocation(WorkshopDbContext db, CancellationToken ct)
{
    var setting = await db.SystemSettings.AsNoTracking().SingleOrDefaultAsync(x => x.SettingKey == "requireInspectionBeforeAllocation", ct);
    return setting is null || !bool.TryParse(setting.SettingValue, out var required) || required;
}
static async Task<Mechanic?> ResolveCurrentMechanic(HttpContext http, WorkshopDbContext db, CancellationToken ct)
{
    var email = http.User.Identity?.Name;
    // The seeded demo mechanic account is intentionally linked to the seeded Marcus Chen profile.
    // A user-to-mechanic relation can replace this configured link when account administration is added.
    var mechanicId = string.Equals(email, "mechanic@amcotts.com", StringComparison.OrdinalIgnoreCase) ? 1 : (int?)null;
    var query = db.Mechanics.Include(x => x.Skills).AsQueryable();
    return mechanicId is int id
        ? await query.SingleOrDefaultAsync(x => x.Id == id, ct)
        : await query.SingleOrDefaultAsync(x => x.DisplayName == email, ct);
}
static async Task<bool> MechanicOwnsBooking(HttpContext http, WorkshopDbContext db, int bookingId, CancellationToken ct)
{
    var mechanic = await ResolveCurrentMechanic(http, db, ct);
    return mechanic is not null && await db.Assignments.AnyAsync(x => x.BookingId == bookingId && x.MechanicId == mechanic.Id && x.ClosedUtc == null, ct);
}
static async Task<bool> MechanicCanViewTruckHistory(HttpContext http, WorkshopDbContext db, int truckId, CancellationToken ct)
{
    var mechanic = await ResolveCurrentMechanic(http, db, ct);
    return mechanic is not null && await db.Assignments.AnyAsync(x => x.MechanicId == mechanic.Id && x.Booking!.TruckId == truckId, ct);
}
static bool RoleCanTransition(string role, string current, string next) => next == "Cancelled"
    ? role is "Supervisor/Manager" or "Administrator"
    : next switch
    {
        "UnderInspection" => role is "Mechanic" or "Supervisor/Manager" or "Administrator",
        "ReadyForAllocation" => role is "Mechanic" or "Supervisor/Manager" or "Administrator",
        "Assigned" => role is "Supervisor/Manager" or "Administrator",
        "InRepair" => role is "Mechanic" or "Supervisor/Manager" or "Administrator",
        "Completed" => role is "Mechanic" or "Supervisor/Manager" or "Administrator",
        _ => false
    };
static object ToBookingResponse(Booking booking) => new { id = booking.Id, queueNumber = booking.QueueNumber, serviceDate = booking.ServiceDate, arrivalUtc = booking.ArrivalUtc, complaint = booking.Complaint, priority = booking.Priority, status = booking.Status, confirmedFaultCategory = booking.ConfirmedFaultCategory ?? booking.RepairRecord?.ConfirmedPrimaryFault, mechanic = booking.Assignments.Where(x => x.ClosedUtc == null).OrderByDescending(x => x.CreatedUtc).Select(x => x.Mechanic!.DisplayName).FirstOrDefault(), customer = booking.Customer?.Name, customerId = booking.CustomerId, truckId = booking.TruckId, registration = booking.Truck?.RegistrationNumber, make = booking.Truck?.Make, model = booking.Truck?.Model, vin = booking.Truck?.Vin, symptoms = booking.Symptoms.Select(x => x.Name), statusEvents = booking.StatusEvents.OrderBy(x => x.OccurredUtc).Select(x => new { x.PreviousStatus, x.NewStatus, x.Reason, x.OccurredUtc }) };

public sealed record CreateCustomerRequest(string Name, string? ContactDetails, string? Notes);
public sealed record CreateTruckRequest(int CustomerId, string RegistrationNumber, string? Make, string? Model, string? Vin);
public sealed record CreateBookingRequest(int CustomerId, int TruckId, string RequestKey, DateOnly? ServiceDate, string Complaint, string? Priority, string[]? Symptoms);
public sealed record ChangeBookingStatusRequest(string NewStatus, string? ActorRole, string? Reason);
public sealed record AllocateBookingRequest(int? MechanicId, string ActorRole, string? Reason);
public sealed record DiagnoseBookingRequest(string[]? Observations);
public sealed record ReviewDiagnosticSuggestionRequest(string ReviewStatus, string? ConfirmedFaultCategory, string? Note, string? ReviewerRole);
public sealed record AdminCreateUserRequest(string DisplayName, string Email, string Password, string Role);
public sealed record AdminUpdateUserRequest(string? DisplayName, string? Role, bool? IsDisabled);
public sealed record AdminUpdateMechanicRequest(string DisplayName, string? JobTitle, bool IsActive, bool IsAvailable, int WorkloadCapacity, string[]? Skills);
public sealed record SaveRepairRecordRequest(string? ConfirmedPrimaryFault, string Findings, string WorkPerformed, string? PartsUsed, string? Notes, int? MechanicId);
public sealed record BootstrapAdminRequest(string Email, string Password, string DisplayName, string? BootstrapSecret);
public sealed record LoginRequest(string Email, string Password);
public sealed record UpdateProfileRequest(string DisplayName);
public sealed record SaveInspectionRequest(string[]? ChecklistItems, string? Findings, string? Severity, int? EstimatedRepairMinutes);
public sealed record DiagnosticServiceSuggestion(
    [property: JsonPropertyName("rank")] int Rank,
    [property: JsonPropertyName("fault_category_id")] string FaultCategoryId,
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("action")] string Action,
    [property: JsonPropertyName("score")] double Score,
    [property: JsonPropertyName("score_type")] string ScoreType,
    [property: JsonPropertyName("evidence")] string[] Evidence);
public sealed record DiagnosticServiceResponse(
    [property: JsonPropertyName("result")] string Result,
    [property: JsonPropertyName("method")] string Method,
    [property: JsonPropertyName("model_version")] string ModelVersion,
    [property: JsonPropertyName("suggestions")] DiagnosticServiceSuggestion[]? Suggestions,
    [property: JsonPropertyName("message")] string? Message);

public sealed class WorkshopDbContext(DbContextOptions<WorkshopDbContext> options) : Microsoft.AspNetCore.Identity.EntityFrameworkCore.IdentityDbContext<ApplicationUser, IdentityRole<int>, int>(options)
{
    public DbSet<Customer> Customers => Set<Customer>(); public DbSet<Truck> Trucks => Set<Truck>(); public DbSet<Booking> Bookings => Set<Booking>(); public DbSet<BookingSymptom> BookingSymptoms => Set<BookingSymptom>(); public DbSet<StatusEvent> StatusEvents => Set<StatusEvent>(); public DbSet<Mechanic> Mechanics => Set<Mechanic>(); public DbSet<MechanicSkill> MechanicSkills => Set<MechanicSkill>(); public DbSet<Assignment> Assignments => Set<Assignment>(); public DbSet<DiagnosticSession> DiagnosticSessions => Set<DiagnosticSession>(); public DbSet<DiagnosticSuggestion> DiagnosticSuggestions => Set<DiagnosticSuggestion>(); public DbSet<RepairRecord> RepairRecords => Set<RepairRecord>(); public DbSet<InspectionRecord> InspectionRecords => Set<InspectionRecord>(); public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<SystemSetting>().HasKey(x => x.SettingKey);
        modelBuilder.Entity<ApplicationUser>().HasIndex(x => x.FirebaseUid).IsUnique().HasFilter("[FirebaseUid] IS NOT NULL");
        modelBuilder.Entity<ApplicationUser>().Property(x => x.DisplayName).HasMaxLength(160).IsRequired();
        modelBuilder.Entity<ApplicationUser>().Property(x => x.FirebaseUid).HasMaxLength(180);
        modelBuilder.Entity<Customer>().Property(x => x.Name).HasMaxLength(180).IsRequired();
        modelBuilder.Entity<Truck>().Property(x => x.RegistrationNumber).HasMaxLength(30).IsRequired(); modelBuilder.Entity<Truck>().Property(x => x.NormalizedRegistration).HasMaxLength(30).IsRequired(); modelBuilder.Entity<Truck>().HasIndex(x => x.NormalizedRegistration).IsUnique();
        modelBuilder.Entity<Booking>().Property(x => x.RequestKey).HasMaxLength(120).IsRequired(); modelBuilder.Entity<Booking>().HasIndex(x => x.RequestKey).IsUnique(); modelBuilder.Entity<Booking>().HasIndex(x => new { x.ServiceDate, x.QueueNumber }).IsUnique(); modelBuilder.Entity<Booking>().HasIndex(x => new { x.Status, x.ServiceDate }); modelBuilder.Entity<Booking>().Property(x => x.RowVersion).IsRowVersion();
        modelBuilder.Entity<Booking>().HasOne(x => x.Customer).WithMany(x => x.Bookings).HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict); modelBuilder.Entity<Booking>().HasOne(x => x.Truck).WithMany(x => x.Bookings).HasForeignKey(x => x.TruckId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<BookingSymptom>().Property(x => x.Name).HasMaxLength(100).IsRequired(); modelBuilder.Entity<StatusEvent>().Property(x => x.NewStatus).HasMaxLength(40).IsRequired();
        modelBuilder.Entity<Booking>().Property(x => x.ConfirmedFaultCategory).HasMaxLength(120); modelBuilder.Entity<Mechanic>().Property(x => x.DisplayName).HasMaxLength(160).IsRequired(); modelBuilder.Entity<MechanicSkill>().Property(x => x.FaultCategory).HasMaxLength(120).IsRequired(); modelBuilder.Entity<Assignment>().HasIndex(x => new { x.BookingId, x.ClosedUtc }).IsUnique().HasFilter("[ClosedUtc] IS NULL"); modelBuilder.Entity<Assignment>().HasIndex(x => new { x.MechanicId, x.ClosedUtc });
        modelBuilder.Entity<Mechanic>().HasData(new Mechanic { Id = 1, DisplayName = "Marcus Chen", JobTitle = "Senior Technician", IsActive = true, IsAvailable = true, WorkloadCapacity = 4 }, new Mechanic { Id = 2, DisplayName = "Sarah Jenkins", JobTitle = "Diagnostic Specialist", IsActive = true, IsAvailable = true, WorkloadCapacity = 4 }, new Mechanic { Id = 3, DisplayName = "David Smith", JobTitle = "General Mechanic", IsActive = true, IsAvailable = true, WorkloadCapacity = 5 });
        modelBuilder.Entity<MechanicSkill>().HasData(new MechanicSkill { Id = 1, MechanicId = 1, FaultCategory = "Brake system pressure fault" }, new MechanicSkill { Id = 2, MechanicId = 1, FaultCategory = "Electrical fault" }, new MechanicSkill { Id = 3, MechanicId = 2, FaultCategory = "Electrical fault" }, new MechanicSkill { Id = 4, MechanicId = 2, FaultCategory = "Engine performance fault" }, new MechanicSkill { Id = 5, MechanicId = 3, FaultCategory = "Brake system pressure fault" });
        modelBuilder.Entity<DiagnosticSession>().HasIndex(x => new { x.BookingId, x.RequestedUtc }); modelBuilder.Entity<DiagnosticSuggestion>().HasIndex(x => new { x.SessionId, x.Rank }).IsUnique(); modelBuilder.Entity<DiagnosticSuggestion>().Property(x => x.FaultCategoryId).HasMaxLength(120).IsRequired(); modelBuilder.Entity<DiagnosticSuggestion>().Property(x => x.ScoreType).HasMaxLength(80).IsRequired(); modelBuilder.Entity<RepairRecord>().HasIndex(x => x.BookingId).IsUnique(); modelBuilder.Entity<RepairRecord>().Property(x => x.Findings).IsRequired(); modelBuilder.Entity<RepairRecord>().Property(x => x.WorkPerformed).IsRequired();
        modelBuilder.Entity<InspectionRecord>().HasIndex(x => x.BookingId).IsUnique(); modelBuilder.Entity<InspectionRecord>().HasOne(x => x.Booking).WithOne().HasForeignKey<InspectionRecord>(x => x.BookingId).OnDelete(DeleteBehavior.Cascade);
    }
}
public sealed class Customer { public int Id { get; set; } public string Name { get; set; } = ""; public string? ContactDetails { get; set; } public string? Notes { get; set; } public DateTime CreatedUtc { get; set; } public List<Truck> Trucks { get; set; } = []; public List<Booking> Bookings { get; set; } = []; }
public sealed class Truck { public int Id { get; set; } public int CustomerId { get; set; } public Customer? Customer { get; set; } public string RegistrationNumber { get; set; } = ""; public string NormalizedRegistration { get; set; } = ""; public string? Make { get; set; } public string? Model { get; set; } public string? Vin { get; set; } public DateTime CreatedUtc { get; set; } public List<Booking> Bookings { get; set; } = []; }
public sealed class Booking { public int Id { get; set; } public int CustomerId { get; set; } public Customer? Customer { get; set; } public int TruckId { get; set; } public Truck? Truck { get; set; } public DateOnly ServiceDate { get; set; } public int QueueNumber { get; set; } public string RequestKey { get; set; } = ""; public DateTime ArrivalUtc { get; set; } public DateTime CreatedUtc { get; set; } public string Complaint { get; set; } = ""; public string Priority { get; set; } = "MEDIUM"; public string Status { get; set; } = "Waiting"; public string? ConfirmedFaultCategory { get; set; } public byte[] RowVersion { get; set; } = []; public List<BookingSymptom> Symptoms { get; set; } = []; public List<StatusEvent> StatusEvents { get; set; } = []; public List<Assignment> Assignments { get; set; } = []; public RepairRecord? RepairRecord { get; set; } }
public sealed class BookingSymptom { public int Id { get; set; } public int BookingId { get; set; } public string Name { get; set; } = ""; }
public sealed class StatusEvent { public int Id { get; set; } public int BookingId { get; set; } public string? PreviousStatus { get; set; } public string NewStatus { get; set; } = ""; public string? Reason { get; set; } public DateTime OccurredUtc { get; set; } }
public sealed class Mechanic { public int Id { get; set; } public string DisplayName { get; set; } = ""; public string? JobTitle { get; set; } public bool IsActive { get; set; } = true; public bool IsAvailable { get; set; } = true; public int WorkloadCapacity { get; set; } = 4; public List<MechanicSkill> Skills { get; set; } = []; public List<Assignment> Assignments { get; set; } = []; }
public sealed class MechanicSkill { public int Id { get; set; } public int MechanicId { get; set; } public Mechanic? Mechanic { get; set; } public string FaultCategory { get; set; } = ""; }
public sealed class Assignment { public int Id { get; set; } public int BookingId { get; set; } public Booking? Booking { get; set; } public int MechanicId { get; set; } public Mechanic? Mechanic { get; set; } public DateTime CreatedUtc { get; set; } public DateTime? ClosedUtc { get; set; } public string? Reason { get; set; } }
public sealed class DiagnosticSession { public int Id { get; set; } public int BookingId { get; set; } public Booking Booking { get; set; } = null!; public DateTime RequestedUtc { get; set; } public string Result { get; set; } = ""; public string Method { get; set; } = ""; public string ModelVersion { get; set; } = ""; public string? Message { get; set; } public List<DiagnosticSuggestion> Suggestions { get; set; } = []; }
public sealed class DiagnosticSuggestion { public int Id { get; set; } public int SessionId { get; set; } public DiagnosticSession Session { get; set; } = null!; public int Rank { get; set; } public string FaultCategoryId { get; set; } = ""; public string Label { get; set; } = ""; public string Action { get; set; } = ""; public double Score { get; set; } public string ScoreType { get; set; } = ""; public string EvidenceJson { get; set; } = "[]"; public string? ReviewStatus { get; set; } public string? ReviewNote { get; set; } public string? ReviewedByRole { get; set; } public DateTime? ReviewedUtc { get; set; } }
public sealed class RepairRecord { public int Id { get; set; } public int BookingId { get; set; } public Booking Booking { get; set; } = null!; public int? MechanicId { get; set; } public string? ConfirmedPrimaryFault { get; set; } public string Findings { get; set; } = ""; public string WorkPerformed { get; set; } = ""; public string? PartsUsed { get; set; } public string? Notes { get; set; } public DateTime RepairStartedUtc { get; set; } public DateTime? RepairCompletedUtc { get; set; } }
public sealed class InspectionRecord { public int Id { get; set; } public int BookingId { get; set; } public Booking Booking { get; set; } = null!; public string ChecklistJson { get; set; } = "[]"; public string? Findings { get; set; } public string? Severity { get; set; } public int? EstimatedRepairMinutes { get; set; } public DateTime UpdatedUtc { get; set; } }
public sealed class SystemSetting { public string SettingKey { get; set; } = ""; public string SettingValue { get; set; } = ""; public DateTime UpdatedUtc { get; set; } }

public static class DemoDataSeeder
{
    public static async Task SeedAsync(WorkshopDbContext db)
    {
        var now = DateTime.UtcNow;
        var completedCustomer = new Customer { Name = "Demo Haulage Fleet", ContactDetails = "Demo record" };
        var cancelledCustomer = new Customer { Name = "Demo Logistics", ContactDetails = "Demo record" };
        var completedTruck = new Truck { Customer = completedCustomer, RegistrationNumber = "DEMO 001", NormalizedRegistration = "DEMO001", Make = "Scania", Model = "R500", Vin = "DEMO-VIN-001" };
        var cancelledTruck = new Truck { Customer = cancelledCustomer, RegistrationNumber = "DEMO 002", NormalizedRegistration = "DEMO002", Make = "Volvo", Model = "FH16", Vin = "DEMO-VIN-002" };
        db.Customers.AddRange(completedCustomer, cancelledCustomer);
        db.Trucks.AddRange(completedTruck, cancelledTruck);
        await db.SaveChangesAsync();

        var completed = new Booking
        {
            CustomerId = completedCustomer.Id,
            TruckId = completedTruck.Id,
            ServiceDate = DateOnly.FromDateTime(now.AddDays(-14)),
            QueueNumber = 1,
            RequestKey = "client-demo-completed-001",
            ArrivalUtc = now.AddDays(-14),
            CreatedUtc = now.AddDays(-14),
            Complaint = "Routine brake and safety inspection",
            Priority = "MEDIUM",
            Status = "Completed",
            ConfirmedFaultCategory = "Brake system pressure fault",
            Symptoms = [new BookingSymptom { Name = "Brake Squeal" }, new BookingSymptom { Name = "ABS Light" }]
        };
        var cancelled = new Booking
        {
            CustomerId = cancelledCustomer.Id,
            TruckId = cancelledTruck.Id,
            ServiceDate = DateOnly.FromDateTime(now.AddDays(-7)),
            QueueNumber = 1,
            RequestKey = "client-demo-cancelled-001",
            ArrivalUtc = now.AddDays(-7),
            CreatedUtc = now.AddDays(-7),
            Complaint = "Electrical fault diagnosis requested",
            Priority = "LOW",
            Status = "Cancelled",
            Symptoms = [new BookingSymptom { Name = "Electrical Fault" }]
        };
        db.Bookings.AddRange(completed, cancelled);
        await db.SaveChangesAsync();

        db.StatusEvents.AddRange(
            new StatusEvent { BookingId = completed.Id, NewStatus = "Waiting", Reason = "Demo booking created", OccurredUtc = now.AddDays(-14) },
            new StatusEvent { BookingId = completed.Id, PreviousStatus = "Waiting", NewStatus = "UnderInspection", Reason = "Inspection completed", OccurredUtc = now.AddDays(-14).AddHours(1) },
            new StatusEvent { BookingId = completed.Id, PreviousStatus = "UnderInspection", NewStatus = "ReadyForAllocation", Reason = "Inspection approved for allocation", OccurredUtc = now.AddDays(-14).AddHours(2) },
            new StatusEvent { BookingId = completed.Id, PreviousStatus = "ReadyForAllocation", NewStatus = "Assigned", Reason = "Assigned to Marcus Chen", OccurredUtc = now.AddDays(-14).AddHours(3) },
            new StatusEvent { BookingId = completed.Id, PreviousStatus = "Assigned", NewStatus = "InRepair", Reason = "Repair started", OccurredUtc = now.AddDays(-14).AddHours(4) },
            new StatusEvent { BookingId = completed.Id, PreviousStatus = "InRepair", NewStatus = "Completed", Reason = "Demo repair completed", OccurredUtc = now.AddDays(-13) },
            new StatusEvent { BookingId = cancelled.Id, NewStatus = "Waiting", Reason = "Demo booking created", OccurredUtc = now.AddDays(-7) },
            new StatusEvent { BookingId = cancelled.Id, PreviousStatus = "Waiting", NewStatus = "Cancelled", Reason = "Customer cancelled before workshop entry", OccurredUtc = now.AddDays(-6) }
        );
        db.Assignments.Add(new Assignment { BookingId = completed.Id, MechanicId = 1, CreatedUtc = now.AddDays(-14).AddHours(3), ClosedUtc = now.AddDays(-13), Reason = "Demo completed assignment" });
        db.InspectionRecords.Add(new InspectionRecord { BookingId = completed.Id, ChecklistJson = JsonSerializer.Serialize(new[] { "Braking System", "Fluid Levels & Leaks" }), Findings = "Brake pressure was below the expected range.", Severity = "Medium", EstimatedRepairMinutes = 120, UpdatedUtc = now.AddDays(-14).AddHours(2) });
        db.RepairRecords.Add(new RepairRecord { BookingId = completed.Id, MechanicId = 1, ConfirmedPrimaryFault = "Brake system pressure fault", Findings = "Demo inspection confirmed reduced brake pressure.", WorkPerformed = "Adjusted the brake system and completed safety testing.", PartsUsed = "Seals and hydraulic fluid", Notes = "Demo history record.", RepairStartedUtc = now.AddDays(-14).AddHours(4), RepairCompletedUtc = now.AddDays(-13) });
        await db.SaveChangesAsync();
    }
}
