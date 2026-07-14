using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MilOps.Presentation.Common;

namespace MilOps.Presentation.Views;

public partial class JalaliDatePicker : UserControl
{
    private static readonly PersianCalendar Cal = new();

    private int _viewYear;
    private int _viewMonth;

    // ── Dependency Property ──────────────────────────────────────────────────

    public static readonly DependencyProperty SelectedDateProperty =
        DependencyProperty.Register(
            nameof(SelectedDate),
            typeof(DateTime?),
            typeof(JalaliDatePicker),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnSelectedDateChanged));

    public DateTime? SelectedDate
    {
        get => (DateTime?)GetValue(SelectedDateProperty);
        set => SetValue(SelectedDateProperty, value);
    }

    private static void OnSelectedDateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var picker = (JalaliDatePicker)d;
        picker.SyncTextFromDate();
    }

    // ── Constructor ──────────────────────────────────────────────────────────

    public JalaliDatePicker()
    {
        InitializeComponent();

        // Default view = today's Jalali month
        var today = DateTime.Today;
        _viewYear  = Cal.GetYear(today);
        _viewMonth = Cal.GetMonth(today);
    }

    // ── Calendar open/close ──────────────────────────────────────────────────

    private void CalBtn_Click(object sender, RoutedEventArgs e)
    {
        // With StaysOpen=False the outside-click that lands on this button has
        // already closed the popup just before Click fires; without this guard
        // the button could only ever open the calendar, never toggle it shut.
        if ((DateTime.UtcNow - _popupLastClosedUtc).TotalMilliseconds < 250) return;

        // Align view to current SelectedDate if set
        if (SelectedDate.HasValue)
        {
            _viewYear  = Cal.GetYear(SelectedDate.Value);
            _viewMonth = Cal.GetMonth(SelectedDate.Value);
        }
        BuildDays();
        CalendarPopup.IsOpen = true;
    }

    private DateTime _popupLastClosedUtc = DateTime.MinValue;
    private void CalendarPopup_Closed(object sender, EventArgs e) =>
        _popupLastClosedUtc = DateTime.UtcNow;

    private void PrevMonth_Click(object sender, RoutedEventArgs e)
    {
        _viewMonth--;
        if (_viewMonth < 1) { _viewMonth = 12; _viewYear--; }
        BuildDays();
    }

    private void NextMonth_Click(object sender, RoutedEventArgs e)
    {
        _viewMonth++;
        if (_viewMonth > 12) { _viewMonth = 1; _viewYear++; }
        BuildDays();
    }

    private void Today_Click(object sender, RoutedEventArgs e)
    {
        SelectDate(DateTime.Today);
    }

    // ── Build day grid ───────────────────────────────────────────────────────

    private static readonly SolidColorBrush FridayBrush =
        new(Color.FromRgb(0xC0, 0x39, 0x2B)); // #C0392B

    private void BuildDays()
    {
        MonthYearLabel.Text = PersianDate.ToPersianDigits(
            $"{PersianDate.MonthNames[_viewMonth - 1]} {_viewYear}");

        DaysGrid.Children.Clear();

        // Day-1 of this Jalali month in Gregorian
        var firstGreg = Cal.ToDateTime(_viewYear, _viewMonth, 1, 0, 0, 0, 0);
        // Columns: Sat=0, Sun=1, Mon=2, Tue=3, Wed=4, Thu=5, Fri=6
        int startCol = ((int)firstGreg.DayOfWeek + 1) % 7;

        // Empty placeholders before day 1
        for (int i = 0; i < startCol; i++)
            DaysGrid.Children.Add(new UIElement());

        int daysInMonth = Cal.GetDaysInMonth(_viewYear, _viewMonth);
        var today = DateTime.Today;
        int? todayDay    = IsThisMonth(today)                        ? Cal.GetDayOfMonth(today)               : null;
        int? selectedDay = SelectedDate.HasValue && IsThisMonth(SelectedDate.Value)
                           ? Cal.GetDayOfMonth(SelectedDate.Value) : null;

        for (int day = 1; day <= daysInMonth; day++)
        {
            int capturedDay = day;
            var dayGreg = Cal.ToDateTime(_viewYear, _viewMonth, day, 0, 0, 0, 0);
            bool isFriday = dayGreg.DayOfWeek == DayOfWeek.Friday;

            // Tag drives all visual states — ControlTemplate DataTriggers handle rendering
            string? tag = (day == selectedDay && day == todayDay) ? "selected-today"
                        : (day == selectedDay)                    ? "selected"
                        : (day == todayDay)                       ? "today"
                        : null;

            var btn = new Button
            {
                Content = PersianDate.ToPersianDigits(day.ToString()),
                Style   = (Style)Resources["CalDayBtn"],
                Tag     = tag,
            };

            // Friday gets a red foreground; template DataTriggers override it when today/selected
            if (isFriday)
                btn.Foreground = FridayBrush;

            btn.Click += (_, _) =>
            {
                var greg = Cal.ToDateTime(_viewYear, _viewMonth, capturedDay, 0, 0, 0, 0);
                SelectDate(greg);
            };

            DaysGrid.Children.Add(btn);
        }
    }

    private bool IsThisMonth(DateTime date)
        => Cal.GetYear(date) == _viewYear && Cal.GetMonth(date) == _viewMonth;

    private void SelectDate(DateTime date)
    {
        SelectedDate = date;
        CalendarPopup.IsOpen = false;
    }

    // ── Text box sync ────────────────────────────────────────────────────────

    private void SyncTextFromDate()
    {
        DateTextBox.Text = SelectedDate.HasValue
            ? PersianDate.ToJalali(SelectedDate.Value)
            : string.Empty;

        // Clear any previous invalid-input error border
        if (TryFindResource("BorderBrush") is Brush normalBorder)
            OuterBorder.BorderBrush = normalBorder;
    }

    private void DateTextBox_LostFocus(object sender, RoutedEventArgs e)
        => CommitTextInput();

    private void DateTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter || e.Key == Key.Return) CommitTextInput();
    }

    private void CommitTextInput()
    {
        var text = DateTextBox.Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            SelectedDate = null;
            return;
        }
        if (PersianDate.TryParse(text, out var date))
        {
            SelectedDate = date.ToDateTime(TimeOnly.MinValue);
            OuterBorder.BorderBrush = (Brush)FindResource("BorderBrush");
        }
        else
        {
            // Invalid input – restore from current SelectedDate
            SyncTextFromDate();
            OuterBorder.BorderBrush = Brushes.Red;
        }
    }
}
