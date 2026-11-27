# Every User of a Workspace has Write in its Projects, until Projects have access of their own

Milestone 1 has no access per Project, so every User of a Workspace has Write in each of its Projects: they read its Flow and Agents, and change an Agent's configuration and a Phase's cast, Gate and Sandbox. What reaches beyond one Project stays with the Workspace's Admins: creating a Project and binding it to Providers, adding Users and changing who is in the Team, and making a team Runner, which spends the Team's API key. A User makes and changes only their own personal Runners, and sees those and their Teams' Runners; an Admin sees every team Runner of the Workspace.

The App Gateway asks Identity, as for Admins (ADR 0028), whether the signed-in User belongs to the Workspace in the route: 401 without a token, 403 for anyone outside the Workspace, or not an Admin where an Admin is needed.

## Considered Options

- **Write for Admins only**: nothing to undo later, but a Workspace with one Admin would leave everyone else unable to tune the Agents they delegate to.
- **Access per Project now**: where Sofa is going, but screens, rules and records for a single Team that milestone 1 doesn't need.

## Consequences

- When Projects get access of their own, Users of a Workspace start with Write in its existing Projects, so nobody loses what they have today.
