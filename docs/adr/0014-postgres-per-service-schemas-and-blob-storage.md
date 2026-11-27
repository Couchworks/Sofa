# Postgres with a schema per service, and files behind a storage interface

Every service keeps its data in PostgreSQL through EF Core, in a schema of its own: one database on one box, which can become separate databases per service later without changing code. Files such as screenshots and large transcripts go through a storage interface, kept on local disk on one box and in S3-compatible storage in the cloud, so the one-box install needs no storage server.

## Considered Options

- **SQL Server**: as capable, but licensed beyond its free edition and heavier to run on one box.
- **A database server per service from the start**: the strictest separation, but several database servers on one box for no gain yet.
- **MinIO for files everywhere**: one S3 API locally too, but a server to run, and its community edition has been scaled back.
