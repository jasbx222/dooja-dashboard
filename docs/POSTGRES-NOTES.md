# Postgres provider patch notes

Apply against a copy of `dooja-dashboard` (do not modify the original owned tree in place unless intentional):

```bash
python3 /workspace/dooja-deploy/apply_postgres_patch.py /path/to/dooja-dashboard
# or from that repo after copying postgres.patch:
git apply /workspace/dooja-deploy/postgres.patch
```

Behavior:
- Default remains SQLite (`Database:Provider=Sqlite` or unset).
- Production: `Database:Provider=Postgres` + `ConnectionStrings:Default` (Npgsql).
- `ConnectionStrings:Dooja` remains the SQLite fallback name.
- Project uses `EnsureCreatedAsync` (no `Migrations/` folder). EnsureCreated works on Postgres.
- For evolving production schema later: add EF migrations and stop EnsureCreated on that DB.
- Regenerating migrations for Postgres is recommended once you leave EnsureCreated.

Verified: apply + `dotnet build -c Release` succeeded on a temp copy with .NET SDK 8.0.427 (`/home/box/.dotnet/dotnet`).
