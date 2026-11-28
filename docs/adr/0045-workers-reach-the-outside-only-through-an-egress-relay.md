# Workers reach the outside only through an egress relay

A Worker container runs on an internal Docker network of its own, which has no route out of Docker and no way to the box: `host.docker.internal` doesn't even resolve there. One relay container, nginx's stream module, sits on that network and on an ordinary one, and is the only way out. It passes on TLS by the name the Worker asks for (SNI), without opening it, and only for its vendor's API (`api.anthropic.com`, and `platform.claude.com` where a subscription's sign-in is refreshed) and GitHub (`github.com`, its API, and the hosts git and the API fetch from); any other name is cut off. On one port of its own it passes TCP on to the Agent Gateway, and to no other port of the box. The Worker finds each allowed name at the relay through `--add-host`, so Claude Code, git and the MCP client need no proxy settings. Execution starts the networks and the relay as it needs them, named after the Agent Gateway's port so two copies of Sofa on one box keep apart, starts the relay again when its configuration changes, and removes them when it stops.

## Considered Options

- **`host.docker.internal` and the default network**, as slice 6 had: no relay, but Docker Desktop's host address reaches every port of the box, and the default network reaches anything.
- **An HTTP proxy with an allow-list** (`HTTPS_PROXY`): the same allow-list, but every program in the Worker must honour the variables, and one that ignores them still has a route out.
- **Firewall rules on the box**: no container, but different on Docker Desktop, Linux and later Kubernetes, and outside what Execution can set up itself.

## Consequences

- The relay allows names, not Repositories: a Worker can reach any of GitHub, limited by its token (ADR 0044), and its vendor's API with any key.
- This is tested on Docker Desktop, where an internal network reaches nothing. On Linux, depending on Docker's version, the box's address on the network's bridge may still answer, and needs checking there. Kubernetes will need a NetworkPolicy instead.
- Execution needs the relay's image (`Workers:RelayImage`, nginx), pulled once like the Worker's.
- `sofa runner login` runs outside this network, as signing in needs claude.ai.
