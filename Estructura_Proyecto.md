# Estructura del Proyecto

A continuación se muestra la estructura de directorios y archivos del proyecto `Login_Administracion_de_Modulos`, excluyendo los directorios de binarios, objetos de compilación, caché de herramientas (`.git`, `node_modules`, `bin`, `obj`, `.vs`), y archivos temporales generados:

```text
+--- .github
|    \--- workflows
|         \--- deploy.yml
+--- .vscode
|    +--- launch.json
|    \--- settings.json
+--- Login_Administracion_de_Modulos
|    +--- Areas
|    |    +--- Modulo04
|    |    |    \--- arias.php
|    |    \--- Modulo05
|    |         +--- Controller
|    |         |    +--- AtencionClinicaController.cs
|    |         |    +--- AveriaDocenteController.cs
|    |         |    +--- EnfermeriaController.cs
|    |         |    +--- PsicologiaController.cs
|    |         |    \--- SoporteController.cs
|    |         +--- Models
|    |         |    +--- ActivoHardwareModel.cs
|    |         |    +--- AtencionMedicaModel.cs
|    |         |    +--- ExpedientePsicoModel.cs
|    |         |    \--- TicketAveriaModel.cs
|    |         \--- Views
|    |              +--- averias_docente
|    |              |    +--- form_reporte.cshtml
|    |              |    \--- mis_reportes.cshtml
|    |              +--- enfermeria
|    |              |    +--- form_atencion.cshtml
|    |              |    +--- historial_paciente.cshtml
|    |              |    \--- panel_enfermeria.cshtml
|    |              +--- psicologia
|    |              |    +--- expediente_alumno.cshtml
|    |              |    +--- form_sesion.cshtml
|    |              |    \--- panel_psicologia.cshtml
|    |              \--- soporte_tecnico
|    |                   +--- consola_principal.cshtml
|    |                   +--- form_atender_ticket.cshtml
|    |                   \--- historial_equipos.cshtml
|    +--- Controlador
|    |    +--- DashboardController.cs
|    |    \--- LoginController.cs
|    +--- Model
|    |    \--- Usuario.cs
|    +--- Properties
|    |    +--- aaaa.html
|    |    \--- launchSettings.json
|    +--- Vista
|    |    +--- Dashboard
|    |    |    +--- Administrador.cshtml
|    |    |    +--- Alumno.cshtml
|    |    |    +--- Docente.cshtml
|    |    |    +--- Practicante.cshtml
|    |    |    +--- Psicologia.cshtml
|    |    |    +--- Secretaria.cshtml
|    |    |    +--- Soporte.cshtml
|    |    |    \--- Topico.cshtml
|    |    +--- images
|    |    |    \--- logo-argentina.png
|    |    +--- Shared
|    |    |    +--- _DashboardLayout.cshtml
|    |    |    +--- _Layout.cshtml
|    |    |    +--- _Layout.cshtml.css
|    |    |    \--- _ValidationScriptsPartial.cshtml
|    |    +--- Error.cshtml
|    |    +--- Error.cshtml.cs
|    |    +--- Index.cshtml
|    |    +--- Index.cshtml.cs
|    |    +--- Privacy.cshtml
|    |    +--- Privacy.cshtml.cs
|    |    +--- _ViewImports.cshtml
|    |    \--- _ViewStart.cshtml
|    +--- wwwroot
|    |    +--- css
|    |    |    +--- dashboard.css
|    |    |    +--- login.css
|    |    |    \--- site.css
|    |    +--- images
|    |    |    \--- logo-argentina.png
|    |    +--- js
|    |    |    \--- site.js
|    |    +--- lib
|    |    |    +--- bootstrap
|    |    |    |    +--- dist
|    |    |    |    |    +--- css
|    |    |    |    |    |    +--- bootstrap-grid.css
|    |    |    |    |    |    +--- bootstrap-grid.css.map
|    |    |    |    |    |    +--- bootstrap-grid.min.css
|    |    |    |    |    |    +--- bootstrap-grid.min.css.map
|    |    |    |    |    |    +--- bootstrap-grid.rtl.css
|    |    |    |    |    |    +--- bootstrap-grid.rtl.css.map
|    |    |    |    |    |    +--- bootstrap-grid.rtl.min.css
|    |    |    |    |    |    +--- bootstrap-grid.rtl.min.css.map
|    |    |    |    |    |    +--- bootstrap-reboot.css
|    |    |    |    |    |    +--- bootstrap-reboot.css.map
|    |    |    |    |    |    +--- bootstrap-reboot.min.css
|    |    |    |    |    |    +--- bootstrap-reboot.min.css.map
|    |    |    |    |    |    +--- bootstrap-reboot.rtl.css
|    |    |    |    |    |    +--- bootstrap-reboot.rtl.css.map
|    |    |    |    |    |    +--- bootstrap-reboot.rtl.min.css
|    |    |    |    |    |    +--- bootstrap-reboot.rtl.min.css.map
|    |    |    |    |    |    +--- bootstrap-utilities.css
|    |    |    |    |    |    +--- bootstrap-utilities.css.map
|    |    |    |    |    |    +--- bootstrap-utilities.min.css
|    |    |    |    |    |    +--- bootstrap-utilities.min.css.map
|    |    |    |    |    |    +--- bootstrap-utilities.rtl.css
|    |    |    |    |    |    +--- bootstrap-utilities.rtl.css.map
|    |    |    |    |    |    +--- bootstrap-utilities.rtl.min.css
|    |    |    |    |    |    +--- bootstrap-utilities.rtl.min.css.map
|    |    |    |    |    |    +--- bootstrap.css
|    |    |    |    |    |    +--- bootstrap.css.map
|    |    |    |    |    |    +--- bootstrap.min.css
|    |    |    |    |    |    +--- bootstrap.min.css.map
|    |    |    |    |    |    +--- bootstrap.rtl.css
|    |    |    |    |    |    +--- bootstrap.rtl.css.map
|    |    |    |    |    |    +--- bootstrap.rtl.min.css
|    |    |    |    |    |    \--- bootstrap.rtl.min.css.map
|    |    |    |    |    \--- js
|    |    |    |    |         +--- bootstrap.bundle.js
|    |    |    |    |         +--- bootstrap.bundle.js.map
|    |    |    |    |         +--- bootstrap.bundle.min.js
|    |    |    |    |         +--- bootstrap.bundle.min.js.map
|    |    |    |    |         +--- bootstrap.esm.js
|    |    |    |    |         +--- bootstrap.esm.js.map
|    |    |    |    |         +--- bootstrap.esm.min.js
|    |    |    |    |         +--- bootstrap.esm.min.js.map
|    |    |    |    |         +--- bootstrap.js
|    |    |    |    |         +--- bootstrap.js.map
|    |    |    |    |         +--- bootstrap.min.js
|    |    |    |    |         \--- bootstrap.min.js.map
|    |    |    |    \--- LICENSE
|    |    |    +--- jquery
|    |    |    |    +--- dist
|    |    |    |    |    +--- jquery.js
|    |    |    |    |    +--- jquery.min.js
|    |    |    |    |    +--- jquery.min.map
|    |    |    |    |    +--- jquery.slim.js
|    |    |    |    |    +--- jquery.slim.min.js
|    |    |    |    |    \--- jquery.slim.min.map
|    |    |    |    \--- LICENSE.txt
|    |    |    +--- jquery-validation
|    |    |    |    +--- dist
|    |    |    |    |    +--- additional-methods.js
|    |    |    |    |    +--- additional-methods.min.js
|    |    |    |    |    +--- jquery.validate.js
|    |    |    |    |    \--- jquery.validate.min.js
|    |    |    |    \--- LICENSE.md
|    |    |    \--- jquery-validation-unobtrusive
|    |    |         +--- dist
|    |    |         |    +--- jquery.validate.unobtrusive.js
|    |    |         |    \--- jquery.validate.unobtrusive.min.js
|    |    |         \--- LICENSE.txt
|    |    \--- favicon.ico
|    +--- appsettings.Development.json
|    +--- appsettings.json
|    +--- bd.sql
|    +--- Documentacion.md
|    +--- Login_Administracion_de_Modulos.csproj
|    \--- Program.cs
+--- test_db_conn
|    +--- Program.cs
|    \--- test_db_conn.csproj
+--- .gitattributes
+--- .gitignore
+--- COMANDOS_GIT.md
+--- Login_Administracion_de_Modulos.slnx
\--- README.md
```
