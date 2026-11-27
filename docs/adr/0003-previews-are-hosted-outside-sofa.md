# Previews are built and hosted outside Sofa

Sofa never builds or runs a Project's software for Validation. The Project's own pipeline publishes a Preview, such as a web build on Cloudflare per pull request (as Kwazzl does), and a Preview Provider tells Sofa where it is, for Users and for Runs that take screenshots. Sofa then needs no build infrastructure, hosting or secrets for the software it works on, and a Project keeps previews the way it already does.

## Consequences

- A Preview Provider learns a Preview's address from a signal made for machines, such as a GitHub Deployment or commit status the pipeline publishes, never by reading a comment meant for people.
- A Project without a preview pipeline gets no Preview; Validation then has the code and the screenshots only.
- Previews behind sign-in (Cloudflare Access, for example) need a credential agents can use to take screenshots, and hosts' request limits (Cloudflare's free plan allows 100,000 a day) make screenshots something Sofa counts as Usage.
