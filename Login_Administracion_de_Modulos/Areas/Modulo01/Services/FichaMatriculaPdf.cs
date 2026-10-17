using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Login_Administracion_de_Modulos.Areas.Modulo01.Models;

namespace Login_Administracion_de_Modulos.Areas.Modulo01.Services;

/// <summary>
/// Ficha de matrícula PDF (QuestPDF Community). Se genera al cerrar la
/// matrícula desde el puesto de Secretaría y se envía al correo del
/// estudiante. Contiene los datos del alumno, su matrícula y las unidades
/// didácticas inscritas del nuevo ciclo.
/// </summary>
public static class FichaMatriculaPdf
{
    static FichaMatriculaPdf() => QuestPDF.Settings.License = LicenseType.Community;

    /// <summary>Genera la ficha en bytes lista para descargar o adjuntar.</summary>
    public static byte[] Generar(ModeloFichaMatricula ficha)
    {
        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(28);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Helvetica"));

                page.Header().PaddingBottom(14).Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text(t =>
                        {
                            t.Span("IESTP \"Argentina\"\n").FontSize(14).Bold();
                            t.Span("Ficha de Matrícula " + ficha.Periodo).FontSize(10);
                        });
                        row.RelativeItem().AlignRight().Text(t =>
                        {
                            t.Span("Código: ").FontSize(9);
                            t.Span(ficha.CodigoMatricula).FontSize(11).Bold();
                        });
                    });
                    col.Item().PaddingTop(6).Height(2).Background("#0f4c81");
                });

                page.Content().PaddingVertical(10).Column(col =>
                {
                    col.Spacing(10);

                    // Datos del estudiante
                    col.Item().Background("#f1f5f9").Border(1).BorderColor("#cbd5e1")
                       .Padding(10).Column(secc =>
                    {
                        secc.Item().Text("Datos del estudiante").FontSize(11).Bold().FontColor("#0f4c81");
                        secc.Spacing(4);
                        secc.Item().Text($"Nombre: {ficha.Estudiante}");
                        secc.Item().Text($"Código: {ficha.CodigoEstudiante}    DNI: {ficha.Dni}");
                        secc.Item().Text($"Carrera: {ficha.Carrera} ({ficha.CarreraCodigo})");
                        secc.Item().Text($"Condición: {ficha.Condicion}");
                        if (!string.IsNullOrWhiteSpace(ficha.CursosDesaprobadosNombres))
                            secc.Item().Text($"Cursos a cargo: {ficha.CursosDesaprobadosNombres}");
                        secc.Item().Text($"Ciclo culminado: {ficha.CicloCulminado} → Nuevo ciclo: {ficha.CicloProximo}");
                    });

                    // Matrícula
                    col.Item().Background("#f1f5f9").Border(1).BorderColor("#cbd5e1")
                       .Padding(10).Column(secc =>
                    {
                        secc.Item().Text("Matrícula").FontSize(11).Bold().FontColor("#0f4c81");
                        secc.Spacing(4);
                        secc.Item().Text($"Código: {ficha.CodigoMatricula}");
                        secc.Item().Text($"Turno: {ficha.Turno}    Tipo: {ficha.TipoMatricula}");
                        secc.Item().Text($"Fecha de matrícula: {ficha.FechaMatricula:dd/MM/yyyy}");
                        secc.Item().Text($"Estado: {ficha.Estado}");
                    });

                    // Unidades didácticas inscritas
                    col.Item().Column(secc =>
                    {
                        secc.Item().PaddingBottom(4).Text("Unidades didácticas inscritas").FontSize(11).Bold().FontColor("#0f4c81");
                        secc.Item().Table(tab =>
                        {
                            tab.ColumnsDefinition(c =>
                            {
                                c.ConstantColumn(28);
                                c.RelativeColumn();
                                c.ConstantColumn(58);
                                c.ConstantColumn(74);
                                c.RelativeColumn();
                            });
                            static IContainer Cell(IContainer c) => c
                                .Border(1).BorderColor("#e2e8f0").PaddingVertical(4).PaddingHorizontal(6);
                            tab.Header(h =>
                            {
                                h.Cell().Background("#0f4c81").Element(Cell).Text("#").FontColor("#ffffff").FontSize(9);
                                h.Cell().Background("#0f4c81").Element(Cell).Text("Código").FontColor("#ffffff").FontSize(9);
                                h.Cell().Background("#0f4c81").Element(Cell).Text("Ciclo").FontColor("#ffffff").FontSize(9);
                                h.Cell().Background("#0f4c81").Element(Cell).Text("Créditos").FontColor("#ffffff").FontSize(9);
                                h.Cell().Background("#0f4c81").Element(Cell).Text("Curso a cargo").FontColor("#ffffff").FontSize(9);
                            });
                            var n = 1;
                            foreach (var ud in ficha.Unidades)
                            {
                                var esCargo = ud.EsCursoACargo ? "Sí" : "—";
                                tab.Cell().Background(n % 2 == 0 ? "#f8fafc" : "#ffffff").Element(Cell)
                                   .Text(n.ToString()).FontSize(9);
                                tab.Cell().Background(n % 2 == 0 ? "#f8fafc" : "#ffffff").Element(Cell)
                                   .Text($"{ud.Codigo} — {ud.Nombre}").FontSize(9);
                                tab.Cell().Background(n % 2 == 0 ? "#f8fafc" : "#ffffff").Element(Cell)
                                   .Text(ud.Ciclo).FontSize(9);
                                tab.Cell().Background(n % 2 == 0 ? "#f8fafc" : "#ffffff").Element(Cell)
                                   .Text(ud.Creditos.ToString()).FontSize(9);
                                tab.Cell().Background(n % 2 == 0 ? "#f8fafc" : "#ffffff").Element(Cell)
                                   .Text(esCargo).FontSize(9);
                                n++;
                            }
                        });
                    });

                    if (ficha.Unidades.Count == 0)
                        col.Item().Text("Sin unidades didácticas inscritas.").FontColor("#64748b");
                });

                page.Footer().PaddingTop(10).BorderTop(1).BorderColor("#cbd5e1").Row(row =>
                {
                    row.RelativeItem().Text($"Generado por Secretaría Académica — {DateTime.Now:dd/MM/yyyy HH:mm}")
                       .FontSize(8).FontColor("#64748b");
                    row.RelativeItem().AlignRight().Text("IESTP \"Argentina\" — Sistema de Matrícula Cero Filas")
                       .FontSize(8).FontColor("#64748b");
                });
            });
        });
        return doc.GeneratePdf();
    }
}
