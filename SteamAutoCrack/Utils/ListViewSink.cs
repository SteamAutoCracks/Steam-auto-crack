using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Serilog;
using Serilog.Configuration;
using Serilog.Core;
using Serilog.Events;

namespace SteamAutoCrack.Utils;

public class ListViewSink : ILogEventSink
{
    private readonly ListView _ListView;

    public ListViewSink(ListView ListView)
    {
        _ListView = ListView;
    }

    public void Emit(LogEvent logEvent)
    {
        var level = "";
        var SourceContextStr = "";
        LogEventPropertyValue SourceContext;
        var logColor = Brushes.White;
        switch (logEvent.Level)
        {
            case LogEventLevel.Debug:
                level = "Debug";
                logColor = Brushes.Gray;
                break;
            case LogEventLevel.Warning:
                level = "Warn";
                logColor = Brushes.Yellow;
                break;
            case LogEventLevel.Information:
                level = "Info";
                break;
            case LogEventLevel.Error:
                level = "Error";
                logColor = Brushes.Red;
                break;
        }

        logEvent.Properties.TryGetValue("SourceContext", out SourceContext!);
        SourceContextStr = SourceContext?.ToString();
        SourceContextStr = SourceContextStr?.Substring(SourceContextStr.LastIndexOf('.') + 1).Replace("\"", "")
            .Replace("\\", "");
        Application.Current.Dispatcher.Invoke((Action)(() =>
        {
            if (logEvent.RenderMessage() != string.Empty)
            {
                var item = new { Level = level, Source = SourceContextStr, Message = logEvent.RenderMessage() };
                var listviewitem = new ListViewItem { Content = item, Background = logColor, Foreground = Brushes.Black};
                _ListView.Items.Add(listviewitem);
                _ListView.ScrollIntoView(listviewitem);
            }

            if (logEvent.Exception != null)
            {
                var itemex = new { Level = level, Source = SourceContextStr, logEvent.Exception.Message };
                var listviewitemex = new ListViewItem { Content = itemex, Background = logColor, Foreground = Brushes.Black };
                _ListView.Items.Add(listviewitemex);
                _ListView.ScrollIntoView(listviewitemex);
            }
        }));
    }
}

public static class ListViewSinkExtensions
{
    public static LoggerConfiguration ListViewSink(
        this LoggerSinkConfiguration loggerConfiguration,
        ListView listview)
    {
        return loggerConfiguration.Sink(new ListViewSink(listview));
    }
}