# Phases pick from a few built-in Sandboxes

What a Run may reach (the network, the Repositories and what it may push, tools allowed without asking) is set by a Sandbox its Phase picks from a few built in, such as read-only for Review, pushing to its own branch for Implement and to the Integration branch for Integrate. Each Phase can then hold no more than it needs, without anyone setting up access per Phase: the built-in Sandboxes come filled in, repository tokens are scoped by Sofa from the Project's Repositories, and a Project adjusts one only where its defaults don't fit.

## Considered Options

- **One Sandbox for every Run**: no setup at all, but a review agent could push and every agent holds the widest access any Phase needs.
- **Access set per Phase from scratch**: least privilege everywhere, but setup for every Phase of every Project, which nobody keeps up.
