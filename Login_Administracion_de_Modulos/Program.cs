using Login_Administracion_de_Modulos.Seguridad;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

// 0. Cargar el archivo .env (raíz del proyecto) y las variables de entorno del
//    sistema. Los secretos (BD, Google) NUNCA se commitean al repositorio.
ConfiguracionSegura.CargarVariablesEntorno(builder);

// 1. MVC con validación antiforgery automática en TODOS los formularios POST.
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute());
});
builder.Services.AddDistributedMemoryCache();

// 2. (Reservado) Sesión ligera para datos de presentación de módulos futuros
//    (p. ej. recordar filtros de pantalla). La AUTENTICACIÓN real va por cookies
//    de claims — HttpContext.Session NO valida a nadie y hoy nadie la usa.
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(60);
    options.Cookie.Name = ".IESTP.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});

// 3. Autenticación por cookies (claims). Es el ÚNICO mecanismo de sesión
//    válido del sistema: el login escribe claims (UsuarioId, PersonaId,
//    RolActivo, nombre) y [ProtegerPanel] los lee.
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Login";
        options.LogoutPath = "/logout";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    });

// 4. SSO con Google: SOLO se registra si las credenciales están presentes en
//    .env / variables de entorno. Sin ellas, la ruta /login-google-oauth
//    simplemente no existe. NUNCA hardcodear ClientId/ClientSecret aquí.
var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
var googleConfigurado =
    !string.IsNullOrWhiteSpace(googleClientId) && !googleClientId.Contains("CONFIGURAR") &&
    !string.IsNullOrWhiteSpace(googleClientSecret) && !googleClientSecret.Contains("CONFIGURAR");

if (googleConfigurado)
{
    builder.Services.AddAuthentication(options =>
        {
            options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = Microsoft.AspNetCore.Authentication.Google.GoogleDefaults.AuthenticationScheme;
        })
        .AddGoogle(options =>
        {
            options.ClientId = googleClientId!;
            options.ClientSecret = googleClientSecret!;
        });
    Console.WriteLine("[Startup] Google SSO: HABILITADO");
}
else
{
    Console.WriteLine("[Startup] Google SSO: deshabilitado (falta GOOGLE_CLIENT_ID/GOOGLE_CLIENT_SECRET en .env)");
}

// 4b. Factory de conexiones por módulo (SearchPath=modXX,core,public) usada por los
//     servicios portados de los módulos 01 y 09 (patrón Dapper del proyecto original).
builder.Services.AddScoped<Login_Administracion_de_Modulos.Areas.Modulo00.IModuleDbConnectionFactory, Login_Administracion_de_Modulos.Areas.Modulo00.ModuleDbConnectionFactory>();
// Servicios del Módulo 01 (Matrícula Académica)
builder.Services.AddScoped<Login_Administracion_de_Modulos.Areas.Modulo01.Services.IMatriculaServicio, Login_Administracion_de_Modulos.Areas.Modulo01.Services.MatriculaServicio>();
builder.Services.AddScoped<Login_Administracion_de_Modulos.Areas.Modulo01.Services.IMatriculaturaServicio, Login_Administracion_de_Modulos.Areas.Modulo01.Services.MatriculaturaServicio>();
builder.Services.AddScoped<Login_Administracion_de_Modulos.Areas.Modulo01.Services.IServicioCorreoFicha, Login_Administracion_de_Modulos.Areas.Modulo01.Services.ServicioCorreoFicha>();
builder.Services.AddScoped<Login_Administracion_de_Modulos.Areas.Modulo01.Services.AcademicoServicio>();
// Servicios del Módulo 09 (Trámites TUPA y Pagos)
builder.Services.AddScoped<Login_Administracion_de_Modulos.Areas.Modulo09.Services.ITramiteServicio, Login_Administracion_de_Modulos.Areas.Modulo09.Services.TramiteServicio>();
builder.Services.AddScoped<Login_Administracion_de_Modulos.Areas.Modulo09.Services.IPagoServicio, Login_Administracion_de_Modulos.Areas.Modulo09.Services.PagoServicio>();

var app = builder.Build();

// 5. Cabeceras de seguridad HTTP
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "SAMEORIGIN");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    await next();
});

// 6. Pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Login/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Login}/{action=Index}/{id?}");

app.Run();
