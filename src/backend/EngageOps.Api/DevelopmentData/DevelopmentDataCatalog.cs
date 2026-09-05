namespace EngageOps.Api.DevelopmentData;

internal static class DevelopmentDataCatalog
{
    public static IReadOnlyList<DevelopmentOrganisationData> Organisations { get; } =
    [
        new("Northstar Demo Workforce",
        [
            .. new[]
                {
                    "Alderbrook",
                    "Beacon",
                    "Cedar",
                    "Delta",
                    "Elmbridge",
                    "Frontier",
                    "Granite",
                    "Harbour",
                    "Meridian",
                }
                .SelectMany(prefix => new[]
                {
                    $"{prefix} Advisory",
                    $"{prefix} Facilities",
                    $"{prefix} Logistics",
                    $"{prefix} Operations",
                    $"{prefix} Services",
                }),
        ]),
        new("Cedar Demo Workforce",
        [
            "Bramble Consulting",
            "Cobalt Engineering",
            "Willow Healthcare",
        ]),
        new("Newhaven Demo Workforce", []),
    ];
}

internal sealed record DevelopmentOrganisationData(
    string Name,
    IReadOnlyList<string> ClientNames);
