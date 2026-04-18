using System;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;

namespace RandomVision.Services;

internal enum PreviewCoverage
{
    Complete,
    PartialNeedsInput,
    AlreadyVisible
}

internal sealed class EventPreviewResult
{
    public EventPreviewResult(string eventTitle, IReadOnlyList<EventOptionPreview> options)
    {
        EventTitle = eventTitle;
        Options = options;
    }

    public string EventTitle { get; }

    public IReadOnlyList<EventOptionPreview> Options { get; }
}

internal sealed class EventOptionPreview
{
    public EventOptionPreview(EventOption sourceOption, string title, PreviewCoverage coverage, IEnumerable<string>? lines = null)
    {
        SourceOption = sourceOption;
        Title = title;
        Coverage = coverage;
        Lines = NormalizeLines(lines);
    }

    public EventOption SourceOption { get; }

    public string Title { get; set; }

    public PreviewCoverage Coverage { get; set; }

    public List<string> Lines { get; }

    public List<EventPreviewEntity> Entities { get; } = new();

    private static List<string> NormalizeLines(IEnumerable<string>? lines)
    {
        if (lines is null)
        {
            return new List<string>();
        }

        var normalized = new List<string>();
        string? previous = null;
        foreach (var rawLine in lines)
        {
            var line = rawLine?.Trim();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            // Collapse adjacent duplicate hints to reduce visual crowding in dense events.
            if (string.Equals(previous, line, StringComparison.Ordinal))
            {
                continue;
            }

            normalized.Add(line);
            previous = line;
        }

        return normalized;
    }
}

internal sealed class EventPreviewEntity
{
    public EventPreviewEntity(string key, string label, IEnumerable<IHoverTip> hoverTips)
    {
        Key = key;
        Label = label;
        HoverTips = new List<IHoverTip>(hoverTips);
    }

    public string Key { get; }

    public string Label { get; }

    public List<IHoverTip> HoverTips { get; }
}
