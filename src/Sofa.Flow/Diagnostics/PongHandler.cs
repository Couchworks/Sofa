using Sofa.Contracts.Diagnostics;

namespace Sofa.Flow.Diagnostics;

public static class PongHandler
{
    public static void Handle(Pong pong, RoundTrips roundTrips, ILogger<Pong> logger)
    {
        if (!roundTrips.Answer(pong.RoundTripId))
        {
            logger.LogWarning("Pong for unknown round trip {RoundTripId}", pong.RoundTripId);
        }
    }
}
