namespace Fatewake.Web.Presentation;
/// <summary>Places remembered moments into repeatable pages without changing their reading order.</summary>
public sealed record NovelPage(int Number, int SurvivorDay, NovelPanel[] Panels)
{
    /// <summary>Builds four-panel pages, reserving a separate page for each disclosed cutscene.</summary>
    public static NovelPage[] Arrange(IEnumerable<NovelPanel> panels)
    {
        var pages = new List<NovelPage>();
        var pending = new List<NovelPanel>();
        int? day = null;
        void Flush()
        {
            if (pending.Count == 0)
                return;
            pages.Add(new(pages.Count + 1, day!.Value, pending.ToArray()));
            pending.Clear();
        }
        foreach (var panel in panels.OrderBy(x => x.Sequence))
        {
            if (day != panel.SurvivorDay)
            {
                Flush();
                day = panel.SurvivorDay;
            }
            if (panel.Kind == "cutscene")
            {
                Flush();
                pages.Add(new(pages.Count + 1, panel.SurvivorDay, [panel]));
                continue;
            }
            pending.Add(panel);
            if (pending.Count == 4)
                Flush();
        }
        Flush();
        return pages.ToArray();
    }
}
