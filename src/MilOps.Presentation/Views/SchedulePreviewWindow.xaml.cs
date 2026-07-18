using System;
using MilOps.Application.Schedules;
using MilOps.Presentation.Services;

namespace MilOps.Presentation.Views;

/// <summary>
/// Read-only modal showing a schedule's board (لوح) with print/PDF actions.
/// Receives a fully-loaded DTO, so it has no query or DI-scope lifetime of its own.
/// </summary>
public partial class SchedulePreviewWindow : Window
{
    private readonly GuardScheduleDto _dto;

    public SchedulePreviewWindow(GuardScheduleDto dto)
    {
        InitializeComponent();
        _dto = dto;
        DataContext = dto;
    }

    // Resolved per click, not in the constructor, so the window stays
    // constructible in design/render harnesses without the app's DI container.
    private static IPrintService Print => App.Services.GetRequiredService<IPrintService>();

    private void Pdf_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var print = Print;
            print.ExportToPdf(ScheduleReport.Build(print, _dto), "لوح پستی.pdf");
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "PDF export failed in SchedulePreviewWindow.");
            MessageBox.Show(this, "ساخت فایل PDF انجام نشد.", "خطا",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Print_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var print = Print;
            print.Print(ScheduleReport.Build(print, _dto), "لوح پستی");
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Print failed in SchedulePreviewWindow.");
            MessageBox.Show(this, "چاپ انجام نشد. از اتصال و روشن بودن چاپگر اطمینان حاصل کنید.", "خطا",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
