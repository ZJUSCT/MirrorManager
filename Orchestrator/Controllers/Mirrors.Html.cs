using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Orchestrator.Utils;

namespace Orchestrator.Controllers;

using static MiniHtmlGenerator;

public partial class Mirrors
{
    /// <summary>
    /// Mirror details in human-readable format.
    /// </summary>
    [HttpGet("html")]
    [Produces("text/html")]
    public string GetMirrorsHtml()
    {
        var mirrors = jobQueue.GetMirrorItems();
        return PlainPage(
            "Mirrors overview", [
                Table([
                    THead([
                        Th("ID"),
                        Th("URL"),
                        Th("Name"),
                        Th("Upstream"),
                        Th("Size"),
                        Th("Status"),
                        Th("LastSyncAt"),
                        Th("LastSuccessAt"),
                        Th("NextSyncAt"),
                    ]),
                    TBody(mirrors
                        .Select(kv => kv.Value)
                        .OrderBy(x => x.Status)
                        .ThenBy(x => x.Config.Id)
                        .Select(x => Tr([
                            Th(x.Config.Id),
                            Td(x.Config.Info.Url),
                            Td(x.Config.Info.Name.En),
                            Td(x.Config.Info.Upstream),
                            Td(x.Size.ToString()),
                            Td(StatusToString(x.Status)),
                            Td(x.LastSyncAt.ToString(CultureInfo.InvariantCulture)),
                            Td(x.LastSuccessAt.ToString(CultureInfo.InvariantCulture)),
                            Td(x.NextSyncAt().ToString(CultureInfo.InvariantCulture)),
                        ]))
                        .ToList())
                ])
            ],
            css: TableCss
        ).ToString();
    }
}
