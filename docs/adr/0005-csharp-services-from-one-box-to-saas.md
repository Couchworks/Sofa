# C# services that run on one box, in Kubernetes and as SaaS

The backend is a set of C# services, designed by SOLID principles, that runs on a single machine, then in a Kubernetes cluster, and possibly later as a hosted product. So every record carries its Workspace from the start, services talk through contracts that don't assume they share a process or a database, and nothing depends on one cloud's services. Frontends (web, desktop, possibly mobile) are separate clients of the same documented API.

## Consequences

- Running locally needs the services, a database and a message broker started together; which broker and which orchestration tooling are decided separately.
