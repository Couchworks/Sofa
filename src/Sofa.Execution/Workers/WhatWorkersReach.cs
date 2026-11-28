using Sofa.Contracts.Flow;

namespace Sofa.Execution.Workers;

public static class WhatWorkersReach
{
    public static readonly IReadOnlyList<string> GitHub =
    [
        "github.com",
        "api.github.com",
        "codeload.github.com",
        "uploads.github.com",
        "objects.githubusercontent.com",
        "raw.githubusercontent.com",
    ];

    public static IReadOnlyList<string> ApiOf(Vendor vendor) => vendor switch
    {
        Vendor.Anthropic => ["api.anthropic.com", "platform.claude.com"],
        _ => throw new ArgumentOutOfRangeException(nameof(vendor), vendor, "Sofa knows no such vendor."),
    };

    public static IReadOnlyList<string> Hosts =>
        [.. Enum.GetValues<Vendor>().SelectMany(ApiOf), .. GitHub];
}
