using ClosedXML.Excel;

namespace Auditart.Application.Reports;

public static class ReportsExcelExporter
{
    public static byte[] Build(ReportsSummaryDto summary)
    {
        using var workbook = new XLWorkbook();

        var slaSheet = workbook.Worksheets.Add("Demoras SLA");
        slaSheet.Cell(1, 1).Value = "Métrica";
        slaSheet.Cell(1, 2).Value = "Valor";
        var sla = summary.SlaDelays;
        var slaRows = new (string Label, object? Value)[]
        {
            ("Casos con SLA", sla.CasosConSla),
            ("SLA vencidos", sla.SlaVencidos),
            ("SLA en riesgo", sla.SlaEnRiesgo),
            ("SLA a tiempo", sla.SlaATiempo),
            ("Promedio retraso (h)", sla.PromedioRetrasoHoras),
            ("Máximo retraso (h)", sla.MaxRetrasoHoras),
            ("Vencidos Rojo", sla.VencidosRojo),
            ("Vencidos Amarillo", sla.VencidosAmarillo),
            ("Vencidos Azul", sla.VencidosAzul),
        };
        for (var i = 0; i < slaRows.Length; i++)
        {
            slaSheet.Cell(i + 2, 1).Value = slaRows[i].Label;
            if (slaRows[i].Value is double d)
                slaSheet.Cell(i + 2, 2).Value = d;
            else if (slaRows[i].Value is int n)
                slaSheet.Cell(i + 2, 2).Value = n;
            else
                slaSheet.Cell(i + 2, 2).Value = "—";
        }

        StyleHeader(slaSheet.Range(1, 1, 1, 2));
        slaSheet.Columns().AdjustToContents();

        var operatorsSheet = workbook.Worksheets.Add("Por operador");
        operatorsSheet.Cell(1, 1).Value = "Operador";
        operatorsSheet.Cell(1, 2).Value = "Casos a procesar";
        operatorsSheet.Cell(1, 3).Value = "Vencidos a procesar";
        operatorsSheet.Cell(1, 4).Value = "SLA vencidos";
        operatorsSheet.Cell(1, 5).Value = "SLA en riesgo";
        operatorsSheet.Cell(1, 6).Value = "Prom. retraso SLA (h)";
        operatorsSheet.Cell(1, 7).Value = "Ingresos hoy";
        operatorsSheet.Cell(1, 8).Value = "Coordinar vencidos";
        operatorsSheet.Cell(1, 9).Value = "Crónicos pendientes";
        operatorsSheet.Cell(1, 10).Value = "Celeste (cerradas)";
        operatorsSheet.Cell(1, 11).Value = "Prom. demora gestión (h)";
        operatorsSheet.Cell(1, 12).Value = "Prom. demora procesamiento (h)";

        var row = 2;
        foreach (var item in summary.Operators)
        {
            operatorsSheet.Cell(row, 1).Value = item.OperadorName;
            operatorsSheet.Cell(row, 2).Value = item.CasosAProcesar;
            operatorsSheet.Cell(row, 3).Value = item.VencidosAProcesar;
            operatorsSheet.Cell(row, 4).Value = item.SlaVencidos;
            operatorsSheet.Cell(row, 5).Value = item.SlaEnRiesgo;
            if (item.PromedioRetrasoSlaHoras.HasValue)
                operatorsSheet.Cell(row, 6).Value = item.PromedioRetrasoSlaHoras.Value;
            operatorsSheet.Cell(row, 7).Value = item.IngresosHoy;
            operatorsSheet.Cell(row, 8).Value = item.CoordinarVencidos;
            operatorsSheet.Cell(row, 9).Value = item.CronicosPendientes;
            operatorsSheet.Cell(row, 10).Value = item.Celeste;
            if (item.PromedioDemoraGestionHoras.HasValue)
                operatorsSheet.Cell(row, 11).Value = item.PromedioDemoraGestionHoras.Value;
            if (item.PromedioDemoraProcesamientoHoras.HasValue)
                operatorsSheet.Cell(row, 12).Value = item.PromedioDemoraProcesamientoHoras.Value;
            row++;
        }

        StyleHeader(operatorsSheet.Range(1, 1, 1, 12));
        operatorsSheet.Columns().AdjustToContents();

        var artSheet = workbook.Worksheets.Add("Por ART");
        artSheet.Cell(1, 1).Value = "ART (aseguradora)";
        artSheet.Cell(1, 2).Value = "Siniestros mes actual";
        artSheet.Cell(1, 3).Value = "Siniestros mes anterior";
        artSheet.Cell(1, 4).Value = "Variación";

        row = 2;
        foreach (var item in summary.ByArt)
        {
            artSheet.Cell(row, 1).Value = item.Art;
            artSheet.Cell(row, 2).Value = item.SiniestrosMesActual;
            artSheet.Cell(row, 3).Value = item.SiniestrosMesAnterior;
            artSheet.Cell(row, 4).Value = item.SiniestrosMesActual - item.SiniestrosMesAnterior;
            row++;
        }

        StyleHeader(artSheet.Range(1, 1, 1, 4));
        artSheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void StyleHeader(IXLRange range)
    {
        range.Style.Font.Bold = true;
        range.Style.Fill.BackgroundColor = XLColor.FromHtml("#1e3a5f");
        range.Style.Font.FontColor = XLColor.White;
    }
}
